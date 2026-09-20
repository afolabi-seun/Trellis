# Coding Standards

`project_architecture.md` defines where code lives (Domain/Application/Infrastructure/
Presentation, per module) and the dependency rule between those layers. This document defines
how code is written within that structure — the conventions that keep nine-to-eleven modules,
built by however many people end up touching this codebase, looking like one coherent
application rather than a patchwork of individual styles. Nothing here exists in the OMS
reference explicitly either; it's written specifically for the Clean Architecture layering just
adopted, since that structure creates a few failure modes (Domain purity erosion, `IQueryable`
leaking past a repository) that a plain layered architecture doesn't have to guard against.

---

## Language Baseline

- **.NET 8, C# 12**, nullable reference types enabled solution-wide (`<Nullable>enable</Nullable>`
  in `Directory.Build.props`, not per-project) — a `string?` vs `string` distinction is exactly
  the kind of thing that should be a compiler error, not a runtime `NullReferenceException`
  discovered in Staging.
- **File-scoped namespaces** (`namespace Trellis.Modules.PropertyAsset.Domain;`), not the
  brace-block style — less indentation, same meaning.
- **Implicit usings** enabled; explicit `using` statements only for anything not covered by the
  implicit set.
- **`dotnet format --verify-no-changes`** and the .NET SDK's built-in analyzers
  (`EnableNETAnalyzers`, `AnalysisLevel=latest`) run in CI per `devops_environments.md` — a PR
  that fails formatting or analyzer rules doesn't merge, full stop. This is enforcement, not a
  style suggestion, precisely because "the linter will catch it" is what makes a large naming
  or layering convention actually hold across a solo build's inevitable rushed Friday commits.
- **Analyzers are off for `**/Migrations/*.cs`** (`src/backend/.editorconfig`) — `dotnet ef
  migrations add` generates that code, and its own generator patterns (inlining a composite
  index's columns as an array literal, for one) trip rules aimed at hand-written hot paths, not
  one-time schema DDL. Found at Phase 1's first multi-column index. Everything else in a
  module's `Infrastructure/Data/` folder — the `DbContext`, `Configurations/`,
  `Repositories/` — is hand-written and stays held to the full analyzer set; only the
  `Migrations/` subfolder specifically is exempted.

---

## Domain Purity Rules

This is the one category of rule that exists specifically because of the Clean Architecture
adoption in `project_architecture.md` — a plain layered architecture doesn't need to police
this, but a Domain layer that's supposed to have zero infrastructure knowledge will quietly
grow some the first time someone's in a hurry, unless it's a stated rule with an enforced check:

- **No EF Core, ASP.NET Core, or any `Infrastructure`-layer type reference anywhere under a
  module's `Domain/` folder.** No `[Table]`, `[Column]`, `[Key]` attributes on an entity — all
  mapping lives in `Infrastructure/Data/Configurations/`. This is checked by a Roslyn analyzer
  rule (a custom `BannedApiAnalyzer` rule set banning `Microsoft.EntityFrameworkCore.*` and
  `Microsoft.AspNetCore.*` usings inside any `*/Domain/*` path), not just a convention someone
  has to remember.
- **Entities encapsulate their own invariants.** No public setters on anything that represents
  a business rule — construction and mutation go through named methods (`Property.Create(...)`,
  `WorkflowInstance.Approve(approverId)`) that can reject an invalid state, not
  `property.Status = "whatever"` from three layers away. See the `Property.Create` example in
  `project_architecture.md`.
- **Domain exceptions are for invariant violations only** (`InvalidUnitStatusTransitionException`),
  never for expected business outcomes like "not found" — that's a `ServiceResult<T>` failure
  at the Application layer, per `error_handling.md`. A Domain exception thrown from inside an
  entity method should be rare and should always mean "the caller tried to put this object into
  a state its own rules forbid."

---

## Async Conventions

- Every I/O-bound method is suffixed `Async` and returns `Task` or `Task<T>` — no exceptions,
  including internal/private helper methods that await something.
- `CancellationToken` is accepted at the Controller layer (from ASP.NET Core's own request
  binding) and threaded through Application and Infrastructure calls explicitly — not dropped
  at the Service layer because "the request is probably going to finish anyway."
- **Never** `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` on a `Task` outside of `Main` /
  a designated composition-root shim. These deadlock under ASP.NET Core's synchronization
  context in ways that are miserable to debug and entirely avoidable by just awaiting.
- `async void` is reserved for event handlers only — everywhere else it's `async Task`, so
  exceptions propagate to a place that can actually catch them (see `error_handling.md`'s
  exception middleware, which can't catch what an `async void` method swallowed).

---

## Dependency Injection

- **Constructor injection only.** No service locator pattern (`IServiceProvider.GetService<T>()`
  called from inside a Service or Repository) — if a class needs a dependency, it declares it
  in its constructor, full stop. This is what makes a class's dependencies visible from its
  signature and mockable in a unit test without a DI container running.
- **One interface per class that crosses a layer boundary** — `IPropertyRepository` has exactly
  one production implementation, `PropertyRepository`. Multiple implementations exist only where
  the Dual-Mode design in `project_architecture.md` / `deployment_topologies.md` calls for it
  (in-process vs. HTTP, cloud vs. on-prem secret provider) — an interface with only ever one
  implementation and no stated reason for a second one is a sign the abstraction isn't earning
  its keep, not a default to reach for everywhere.
- **Each module registers itself** via a `{Module}ModuleExtensions.Add{Module}Module()`
  extension method, called once from `Program.cs`:

  ```csharp
  // Modules/PropertyAsset/PropertyAssetModuleExtensions.cs
  public static class PropertyAssetModuleExtensions
  {
      public static IServiceCollection AddPropertyAssetModule(this IServiceCollection services, IConfiguration config)
      {
          services.AddDbContext<PropertyDbContext>(opt => opt.UseNpgsql(config.GetConnectionString("Trellis")));
          services.AddScoped<IPropertyRepository, PropertyRepository>();
          services.AddScoped<IPropertyService, PropertyService>();
          return services;
      }
  }

  // Program.cs stays a short, readable list, not a 300-line registration dump
  builder.Services
      .AddAuthModule(builder.Configuration)
      .AddTenantModule(builder.Configuration)
      .AddPropertyAssetModule(builder.Configuration)
      // ...
      .AddBillingModule(builder.Configuration);
  ```

---

## Configuration

**Revised from an earlier version of this section**, which bound Options classes from
`appsettings.json`. `appsettings.json` no longer exists in this codebase — every value, config
and secret alike, is an environment variable (`devops_environments.md`'s Configuration & Secrets
section, `.env` locally via `DotNetEnv`, the hosting platform's own env vars everywhere else).
What doesn't change: every module still binds its own section to exactly one strongly-typed
Options class — no scattered `config["Modules:Vendor:MaxAssignmentsPerDay"]` indexer calls
inside Services or Repositories, anywhere. `IConfiguration` resolves a double-underscore env var
(`Modules__Vendor__MaxAssignmentsPerDay`) to the same `Modules:Vendor:MaxAssignmentsPerDay` path
a JSON file would have produced, so everything below this paragraph is unchanged from the
appsettings-based version except where the value physically comes from.

- **One `{Module}Options.cs` per module**, in `Modules/{Module}/Configuration/` — a plain POCO
  with data-annotation validation attributes, not a class that knows how to read `IConfiguration`
  itself:

  ```csharp
  // Modules/Vendor/Configuration/VendorOptions.cs
  public class VendorOptions
  {
      [Range(1, 100)]
      public int MaxAssignmentsPerDay { get; init; } = 20;

      [Required]
      public string DefaultAssignmentNotificationTemplate { get; init; } = "vendor-assignment-created";
  }
  ```

- **Bound once**, inside that module's own `Add{Module}Module()` extension (the same extension
  method from the Dependency Injection section above) — `IConfiguration` itself is never injected
  into an Application or Infrastructure class:

  ```csharp
  // Modules/Vendor/VendorModuleExtensions.cs
  public static IServiceCollection AddVendorModule(this IServiceCollection services, IConfiguration config)
  {
      services.AddOptions<VendorOptions>()
          .Bind(config.GetSection("Modules:Vendor"))
          .ValidateDataAnnotations()
          .ValidateOnStart();   // fails fast at boot, not on first use

      services.AddScoped<IVendorRepository, VendorRepository>();
      services.AddScoped<IVendorService, VendorService>();
      return services;
  }
  ```

  ```bash
  # .env (local, gitignored) / set as an env var directly in the hosting platform elsewhere —
  # double underscore is ASP.NET Core's built-in nested-key separator for env var configuration
  Modules__Vendor__MaxAssignmentsPerDay=20
  ```

- **Consumed via `IOptions<VendorOptions>`** in a constructor, like any other dependency. Default
  to `IOptions<T>` (values fixed for the process lifetime), not `IOptionsSnapshot<T>` or
  `IOptionsMonitor<T>` — per `devops_environments.md`, a config change means setting a new
  environment variable and a redeploy, not a live reload, so there's nothing for a
  snapshot/monitor to buy here until that deployment model changes.

This is what "no `appsettings.json`" means in practice: there is no file to be flat or nested
about — every place that gives a config value *meaning* (a default, a valid range, a required
field) lives in one typed class next to the module that owns it, not spread across the services
that happen to read it, regardless of whether the raw value came from a local `.env` file or a
platform dashboard.

---

## Optimistic Concurrency (BDR-NFR-006, real as of BRD v0.4)

A handbook gap review found no stated behaviour for two users editing the same record at the
same time — a plain EF Core setup defaults to last-write-wins, silently, which is exactly wrong
for the cases that actually come up: two Department Staff acting on the same workflow step, or
a race between two staff recording a rent payment against the same schedule
(`rent_collection_design.md`). This is **BDR-NFR-006**, folded into
`Requirements/Trellis_BRD_v0.4.docx`: *"The system shall detect a concurrent update to a stale
version of a record and reject it, rather than silently overwriting a change made by another
user."*

**Mechanism: Postgres's built-in `xmin` system column, not a hand-rolled version number.**
Every Postgres row already carries `xmin` (the transaction ID that last wrote it) at zero
schema cost — Npgsql's EF Core provider can use it directly as a concurrency token:

```csharp
// Infrastructure/Data/Configurations/PropertyConfiguration.cs
public class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("property", schema: "trellis_property");
        builder.HasKey(p => p.PropertyId);
        // UseXminAsConcurrencyToken() — this section's original one-liner — is obsolete in the
        // Npgsql.EntityFrameworkCore.PostgreSQL version this codebase is on (found at Phase 1
        // implementation, not before). Npgsql's own current replacement, same zero-schema-cost
        // guarantee, one line either way:
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
```

This keeps the Domain entity exactly as pure as the rest of this document requires —
`xmin` is tracked by EF Core as a shadow property entirely inside Infrastructure; `Property.cs`
in `Domain/Entities/` never knows a version number exists. It's the same reasoning as the rest
of the Domain Purity Rules above, applied to one more persistence detail.

**Surfacing it to a caller.** A Response DTO for any mutable entity includes a `RowVersion`
field (the `xmin` value, opaque to the client — a string it echoes back, never parses):

```csharp
public record PropertyResponse(Guid PropertyId, string Address, string RowVersion);
public record PropertyUpdateRequest(string Address, string RowVersion);
```

The Service layer passes the presented `RowVersion` back to EF Core as the expected token
before saving; a mismatch throws `DbUpdateConcurrencyException`, caught at the Service boundary
and returned as `ServiceResult<T>.ConcurrencyConflict()` — a new outcome, mapped to 409, kept
distinct from the existing duplicate-creation `Conflict()` outcome so the frontend can tell
"someone else already changed this" apart from "this already exists." See `error_handling.md`
for the full mapping and the frontend's handling of it.

**Where this applies**: every entity with an `Update`/`Patch` endpoint, not just the two
motivating cases above — this is a baseline convention adopted in Phase 0 (`roadmap.md`), the
same way `ServiceResult<T>` and the Clean Architecture folder layout are, precisely because
retrofitting a concurrency token onto entities already in production is far more disruptive
than building it in from the first migration.

---

## Repository Query Encapsulation

**`IQueryable<T>` never crosses out of the Infrastructure layer.** A repository method returns
a materialized `List<T>`, a single entity, or a DTO — never an unexecuted `IQueryable` for the
Service layer to keep composing. This is a common EF Core anti-pattern worth naming explicitly:
letting `IQueryable` leak means the Service layer (and sometimes the Controller) ends up quietly
deciding what SQL gets generated, which defeats the purpose of having a Repository abstraction
at all and makes `TenantAwareRepository<T>`'s tenant filter (`multi_tenant_design.md`) trivial
to accidentally bypass by composing a new query on top of a leaked one.

```csharp
// Wrong — leaks query composition past the repository boundary
public IQueryable<Property> GetByTenant(Guid tenantId) => Scoped(_context.Properties);

// Right — the repository decides and executes the query; the Service gets a result, not a query
public async Task<List<Property>> GetByTenantAsync(Guid tenantId, CancellationToken ct) =>
    await Scoped(_context.Properties).ToListAsync(ct);
```

---

## DTOs and Immutability

- Application-layer DTOs (`PropertyCreateRequest`, `PropertyResponse`) are C# `record` types —
  value equality and immutability by default, appropriate for data that's just carrying values
  across a boundary.
- Domain entities are **not** records — they have identity (`PropertyId`) that persists across
  mutation, which is exactly what a `record`'s value-equality semantics would get wrong. Plain
  classes with private setters and behavior methods, per the Domain Purity Rules above.
- Controllers never return a Domain entity directly (see `project_architecture.md`'s Thin
  Controllers section) — always a DTO, mapped explicitly in the Service layer (a small, boring
  `ToResponse()` extension method per entity, not a general-purpose object mapper like
  AutoMapper, which tends to hide exactly the kind of accidental cross-layer leakage this
  document is trying to prevent).

---

## Comments and Documentation

- **XML doc comments are required on every public interface** in `Application/Interfaces/` —
  that interface *is* the module's public contract with the rest of the app, and should be
  readable without opening the implementation.
- Implementation classes don't need XML docs by default — prefer a clear method name and a
  small method body over a comment explaining an unclear one.
- No commented-out code committed, ever. Git history is where old code lives if it's needed
  again.

---

## File Organization

One public type per file; file name matches the type name exactly (`PropertyService.cs`
contains `class PropertyService`, not also a private helper class with its own public-looking
surface). This is what makes the Naming Conventions table in `project_architecture.md` actually
navigable — knowing the type name means knowing the file name, with no exceptions to remember.

---

## Code Review Checklist

In addition to the Security Checklist in `security_guide.md`, every PR is checked against:

- [ ] No `Microsoft.EntityFrameworkCore` or `Microsoft.AspNetCore` reference under any `Domain/` folder
- [ ] No `IQueryable<T>` returned from a Repository method
- [ ] New Application-layer DTOs are `record` types; new Domain entities are not
- [ ] New public interfaces in `Application/Interfaces/` have XML doc comments
- [ ] No `.Result` / `.Wait()` on a `Task`
- [ ] `dotnet format --verify-no-changes` and analyzer warnings are clean (CI enforces this, but it's still worth a human glance at *what* triggered a suppression, if any)
- [ ] New Service methods returning `ServiceResult<T>` cover their documented failure modes per `error_handling.md`, not just the happy path
- [ ] A new `Update`/`Patch` endpoint on a mutable entity uses `UseXminAsConcurrencyToken()` and surfaces/accepts `RowVersion`, not a silent last-write-wins save
- [ ] New configuration values are added to a `{Module}Options` class and bound via `AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart()` — not read via a raw `IConfiguration` indexer inside a Service or Repository

---

## Related Documentation

- [Project Architecture](./project_architecture.md) — the Clean Architecture layering these standards apply within
- [Data Model](./data_model.md) — EF Core configuration conventions, kept out of the Domain layer
- [DevOps & Environments](./devops_environments.md) — the `.env`/environment-variable source each module's `{Module}Options` class binds from, per environment
- [Error Handling & Resilience](./error_handling.md) — `ServiceResult<T>` vs. exceptions, Domain exceptions vs. business outcomes
- [Testing Strategy](./testing_strategy.md) — how Domain purity makes unit-testing business rules possible without a database
- [Security Guide](./security_guide.md) — the PR Security Checklist this document's Code Review Checklist sits alongside
- [Build Roadmap](./roadmap.md) — why the `xmin` concurrency convention is adopted in Phase 0, not retrofitted later
