# Project Architecture

## Overview

Trellis is a multi-tenant SaaS platform for property management companies that manage
portfolios on behalf of multiple owners, covering departments, configurable workflow,
document management, inspections, vendor coordination, and targeted AI capabilities. It is
built as a **modular monolith** on **.NET 8 / ASP.NET Core**, with **PostgreSQL 16**
(schema-per-module) as the single data store.

This is a deliberate departure from a reference architecture we studied and borrowed heavily
from (a mature, independently-deployed 6-microservice notification platform). That system's
microservice split, message-broker-per-tenant queues, and per-service Vault namespaces are
calibrated for a funded team running a live platform at meaningful scale. Trellis has no
paying customers yet (see `Requirements/Trellis_BRD_v0.1.docx`, Roadmap) and is being built
largely by one person. A modular monolith gets us the architectural discipline that made that
reference good — strict module boundaries, tenant isolation enforced at multiple layers,
consistent internal structure — without paying the operational cost of distributed systems
before anything has been validated.

This document covers the `Trellis.Api` backend only. The client — a React + TypeScript SPA
(`trellis-web`) — is specified separately in `frontend_architecture.md`. Both live in the
**same repository** (see "Repository Structure" below): `src/backend/` and `src/frontend/`
are built, tested, and deployed independently of each other, and integrate only through the
API's OpenAPI contract (`api_standards.md`), never a shared codebase — the monorepo shares a
git history and a PR workflow, nothing else. See "Repository Structure" for the reasoning and
the trigger for reverting to two repos.

**All modules run in a single deployable process during this phase.** The one rule that
makes this safe: **no module may read or write another module's database schema directly.**
Cross-module data needs go through that module's own service interface (an in-process C#
method call, not HTTP — there is no network hop inside a monolith). This is the same
discipline the reference architecture enforces between its services via HTTP; we get the same
guarantee for free at compile time, and it means a module can be extracted into its own
deployable service later without a rewrite, if and when it needs independent scaling.

---

## Repository Structure

**One repository, two independently built and deployed apps under `src/`** — `src/backend`
(this document) and `src/frontend` (`frontend_architecture.md`'s `trellis-web` SPA). This is a
deliberate change from an earlier version of this document, which specified `trellis-api` and
`trellis-web` as two separate repositories. Revisited before any application code existed, for
the same reason this handbook defers Kubernetes, Vault, and per-tenant encryption keys: the
thing the split was buying — independent deploy lifecycles, independent CI pipelines, no
accidental coupling — doesn't require two *repositories*, only two independently-built folders,
which a monorepo provides just as well via CI path filters (a frontend-only change never
triggers a backend build, and vice versa) and a lint rule against cross-imports between
`src/backend` and `src/frontend`. What two repos cost, that this avoids: two PRs to coordinate
for one full-stack feature, and a generated OpenAPI client that would otherwise need publishing
and versioning as a separate artifact between repos — real overhead with no offsetting benefit
for a single-person build with no paying tenants yet. **Trigger to revisit:** a dedicated
frontend team forms, the two need genuinely independent release cadences, or on-premises
packaging (`deployment_topologies.md`) needs the backend to ship without frontend source present
— any of those is a reason to split, at that point, not before.

**`src/` is deliberately self-contained, with one stated exception.** The CI workflows and the
local Postgres compose file live inside `src/` — every path in both is written relative to
`src/` as its own root — so `Handbook/`, `Requirements/`, `Pitch/`, and `Discovery/` (product,
not code) can stay outside it while `src/` alone is what checks into the eventual real
repository, per your direction. **`Trellis.sln` is the one piece that doesn't follow this rule**
— it sits at the workspace root by explicit choice, one level above `src/`, with its two project
paths carrying the `src\` prefix back (`src\backend\Trellis.Api\...`). This means the `.sln`
itself won't be part of `src/` if it's checked in as-is at extraction time, and
`.github/workflows/backend-ci.yml`'s bare `dotnet build`/`dotnet test` calls (which
auto-discover a `.sln` in the working directory) would have nothing to find — whoever cuts the
real repo needs to either add a `.sln` back inside `src/` at that point, or point those CI steps
at an explicit project file instead. Flagged here rather than solved now, since it's a real gap
only at that future point, not today.

```
Trellis/                                (project root — also contains Handbook/, Requirements/,
│                                        Pitch/, Discovery/, README.md)
│
├── Trellis.sln                          (the one exception — see above; everything else needed
│                                         to build/test/run lives inside src/. References
│                                         src\backend\Trellis.Api\... and
│                                         src\backend\Trellis.Tests\... — the frontend has no
│                                         .NET project to reference)
│
├── src/                                 (self-contained except for Trellis.sln — this subtree
│   │                                     is what becomes the real repo's root)
│   ├── .github/
│   │   ├── workflows/                 # backend-ci.yml, frontend-ci.yml, codeql.yml — path
│   │   │                              # filters below are relative to src/, e.g. "backend/**"
│   │   └── dependabot.yml
│   │
│   ├── infrastructure/
│   │   └── docker-compose.yml         # Local PostgreSQL for development
│   │
│   ├── backend/
│   │   ├── Directory.Build.props      # .NET solution-wide settings — coding_standards.md
│   │   ├── .editorconfig              # C#-specific rules, merges with the repo-root one
│   │   ├── .gitignore                 # bin/, obj/, .NET-specific
│   │   │
│   │   ├── Trellis.Api/
│   │   │   ├── Modules/               # Each module internally follows Clean Architecture —
│   │   │   │   │                     # Domain/ → Application/ → Infrastructure/ + Presentation/
│   │   │   │   │                     # (see "Module Internal Layering" below for the full tree)
│   │   │   │   ├── Auth/              # Users, roles, sessions, JWT issuance
│   │   │   │   ├── Tenant/            # PM company tenants, subscription tiers
│   │   │   │   ├── PropertyAsset/     # Owners, Properties, Units/Assets
│   │   │   │   ├── Notification/      # Template resolution, email/SMS dispatch, delivery audit
│   │   │   │   ├── Document/          # Documents, versions, expiry tracking
│   │   │   │   ├── Workflow/          # Workflow templates, instances, approvals
│   │   │   │   ├── Inspection/        # Inspections, checklist items, photos
│   │   │   │   ├── Vendor/            # Vendor identities, cross-tenant assignments
│   │   │   │   ├── Ai/                # Document extraction jobs, routing suggestions
│   │   │   │   ├── Reporting/         # Audit log, dashboards, Owner report generation
│   │   │   │   ├── Billing/           # Subscriptions, invoicing, Paystack integration
│   │   │   │   ├── Leasing/           # Vacancy listings, applications, screening, e-signature
│   │   │   │   ├── RentLedger/        # Rent schedules, recorded payments, arrears (tracking only)
│   │   │   │   └── Support/           # Cross-tenant support tickets — Triggered, Not Scheduled
│   │   │   │
│   │   │   ├── Core/                  # Shared cross-cutting code (ONE copy — see note below)
│   │   │   │   ├── Results/           # ServiceResult<T>, ServiceResultExtensions
│   │   │   │   ├── Dtos/              # ApiResponse<T> envelope
│   │   │   │   ├── Middleware/        # Exception handling, tenant scope, correlation id
│   │   │   │   ├── Attributes/        # Role-based auth attributes
│   │   │   │   └── Helpers/           # Pagination, error code registry
│   │   │   │
│   │   │   ├── Configuration/         # DI registration, environment/.env-sourced config
│   │   │   ├── Program.cs
│   │   │   ├── .env.example           # Structure only — devops_environments.md
│   │   │   └── Dockerfile
│   │   │
│   │   └── Trellis.Tests/             # Backend tests — a separate .csproj (test-only
│   │                                   # packages like xunit never ship in Trellis.Api's
│   │                                   # deployable), colocated here alongside what it tests
│   │                                   # rather than a top-level tests/ folder — the same
│   │                                   # colocation frontend/ already uses
│   │
│   └── frontend/                      # trellis-web — see frontend_architecture.md for its
│                                       # internal tree (src/api, src/features, tests/, etc.)
│
├── Handbook/, Requirements/, Pitch/, Discovery/   # product/business docs — not code, stay
│                                                   # outside src/ (see above)
└── README.md
```

**Note on `Core/`:** the reference architecture duplicates its `Core/` folder inside every
microservice, on purpose, because each service is independently deployed and must not share a
compiled package. Trellis is one deployable process, so `Core/` exists exactly once. If a
module is later extracted into its own service, its slice of `Core/` gets copied out at that
point — the same trade-off, just deferred until it's actually needed.

---

## Module Internal Layering: Clean Architecture, Per Module

Every module is internally structured as **Clean Architecture** — Domain at the centre,
dependencies pointing inward, nothing outside the module's own folder tree. This is a
deliberate correction from an earlier draft of this document, which described a plain
Controller→Service→Repository→Data layering: that gave us separation of concerns, but not the
one property Clean Architecture is actually for — a Domain layer that has zero knowledge of
EF Core, ASP.NET Core, or persistence at all, and can be unit-tested and reasoned about in
complete isolation.

We do this **within each module's existing folder, not as separate `.csproj` projects**. A
textbook Clean Architecture setup gives each layer its own project (`Domain.csproj`,
`Application.csproj`, `Infrastructure.csproj`, `Api.csproj`) so the compiler enforces the
dependency rule. Across 14 modules that's 50+ projects for a solo build to maintain — real
ceremony bought for a guarantee that folder discipline plus a Roslyn analyzer rule (see
`coding_standards.md`) gets us almost as reliably, at a fraction of the overhead. If a module
is later extracted into its own deployable service (see "Path to Extracting a Microservice
Later" below), *that's* the natural point to split it into its own projects too — the folder
structure below maps onto separate projects with no redesign needed.

```
Modules/PropertyAsset/
├── Domain/
│   ├── Entities/            # Property.cs, UnitAsset.cs, Owner.cs — plain C#, no EF attributes,
│   │                        # no [Table]/[Column], no ASP.NET Core references whatsoever
│   ├── ValueObjects/        # e.g. Address.cs — immutable, equality by value
│   └── Exceptions/          # Domain-specific exceptions (e.g. InvalidUnitStatusTransitionException)
│
├── Application/
│   ├── Interfaces/          # IPropertyRepository.cs — the contract Infrastructure implements
│   ├── Services/            # PropertyService.cs — orchestration, business rules, ServiceResult<T>
│   ├── Dtos/                # PropertyCreateRequest.cs, PropertyResponse.cs — records, not entities
│   └── Validators/          # PropertyCreateRequestValidator.cs (FluentValidation)
│
├── Infrastructure/
│   ├── Data/
│   │   ├── PropertyDbContext.cs
│   │   ├── Configurations/  # IEntityTypeConfiguration<Property> — Fluent API mapping lives HERE,
│   │   │                    # not as attributes on the Domain entity
│   │   └── Migrations/
│   └── Repositories/        # PropertyRepository.cs : IPropertyRepository
│
└── Presentation/
    └── Controllers/         # PropertiesController.cs
```

### The Dependency Rule

```
Presentation ──depends on──▶ Application ──depends on──▶ Domain
Infrastructure ──depends on──▶ Application (implements its interfaces) ──and──▶ Domain
Domain depends on nothing at all.
```

| Layer | Can Call | Cannot Call |
|-------|---------|-------------|
| **Presentation** (Controllers) | Application services (own module only) | Domain directly, Infrastructure directly, other modules' Controllers |
| **Application** (Services, Interfaces, Dtos) | Domain; its own `Interfaces/` (implemented by Infrastructure); other modules' Application services (in-process call) | Infrastructure concrete classes directly — only through an injected interface; other modules' Infrastructure |
| **Infrastructure** (Data, Repositories) | Domain (to map/persist entities); implements Application's interfaces | Application services, Presentation, another module's Infrastructure |
| **Domain** (Entities, Value Objects, Exceptions) | Nothing | Everything — this is what "zero dependencies" means in practice |

The one thing this adds over the previous draft: **the Domain entity and the EF Core mapping
are no longer the same concern living in the same file.** `Property.cs` in `Domain/Entities/`
is a plain class enforcing its own invariants (a private setter plus a validating factory
method, not a public setter anyone can call); `Configurations/PropertyConfiguration.cs` in
Infrastructure is the only place that knows `Property` maps to a `property` table in the
`trellis_property` schema. A change to how we persist something never touches the class that
defines what it *is* — that separation is the actual point of Clean Architecture, not the
folder names.

```csharp
// Domain/Entities/Property.cs — no EF Core, no [Table], no public setters
public class Property
{
    public Guid PropertyId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Address { get; private set; }
    public PropertyType PropertyType { get; private set; }

    private Property() { }  // EF Core needs this, but only Infrastructure ever calls it via reflection

    public static Property Create(Guid tenantId, string address, PropertyType type)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new DomainValidationException("Property address is required.");

        return new Property
        {
            PropertyId = Guid.NewGuid(),
            TenantId = tenantId,
            Address = address,
            PropertyType = type
        };
    }
}
```

```csharp
// Infrastructure/Data/Configurations/PropertyConfiguration.cs — all EF Core knowledge lives here
public class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("property", schema: "trellis_property");
        builder.HasKey(p => p.PropertyId);
        builder.Property(p => p.Address).IsRequired();
    }
}
```

A module needing data owned by another module still calls that module's Application service
directly (a plain C# method call — there's no HTTP round trip inside the monolith), exactly as
before. That part of the design didn't change; what changed is what's inside each module.

### Thin Controllers (Presentation Layer)

```csharp
[HttpPost]
public async Task<IActionResult> CreateInspection([FromBody] CreateInspectionRequest request) =>
    (await _inspectionService.CreateAsync(GetTenantId(), request)).ToActionResult();
```

No try/catch, no response mapping, no business logic, and — new under this structure — no
Domain types crossing into Presentation at all. A Controller only ever sees Application-layer
DTOs (`CreateInspectionRequest`, `InspectionResponse`), never a Domain entity directly; that's
what keeps the Domain layer genuinely isolated rather than just conventionally separated.

---

## Module Catalogue

The **Billable** column reflects `billing_design.md`'s module entitlement model (BDR-BIL-009,
real as of BRD v0.5): **Core** is bundled into every tenant's tier at no extra charge; **Add-on**
can be independently enabled/disabled and invoiced per tenant; **N/A** means the module isn't a
tenant-facing capability at all, so entitlement doesn't apply to it (Billing bills tenants, it
isn't itself something a tenant enables; Support is the Platform Operator's own ops tooling).
A module lands in Core, not Add-on, whenever gating it would break the product for anyone who
hasn't paid for it — either because another Core module depends on it (Document, required by
Leasing's e-signature flow, Inspection's photos, and Ai's extraction jobs) or because it's the
capability the product is actually sold on (Workflow, the departmental routing/approval engine
named in the platform's own first-sentence description; Vendor, the cross-tenant identity model
`multi_tenant_design.md` calls Trellis's core product differentiator). This is a real pricing
decision, confirmed as of this round, not left as a placeholder — see `billing_design.md`'s
"Module Entitlements" section for the reasoning per module. It is orthogonal to "Path to
Extracting a Microservice Later" below: a module can be a billable add-on and still run
in-process, and a Core module could in principle still be extracted to its own service if a
scaling/compliance trigger fires — whether a module is sold separately and whether it's deployed
separately are two different questions with two different triggers.

| Module | Owns (schema) | Billable | Key BDR References | Notes |
|--------|--------------|----------|---------------------|-------|
| **Auth** | `trellis_auth` | Core | BDR-DEPT-002, BDR-SEC-001–004, BDR-SEC-005–006/008–010 (real as of BRD v0.4), BDR-NFR-009–011 (real as of BRD v0.4) | Users, roles, sessions, JWT issuance/refresh, password reset, MFA recovery codes, auth throttling, incident response — see `security_guide.md` |
| **Tenant** | `trellis_tenant` | Core | BDR-TEN-001–005, BDR-TEN-006 (real as of BRD v0.4) | PM company tenants, subscription tier, config, offboarding — see `multi_tenant_design.md` |
| **Notification** | `trellis_notification` | Core | BDR-NOTIF-001–006 (real as of BRD v0.4) | Shared email/SMS dispatch, template resolution, delivery audit — depended on by nearly every other module — see `notifications_design.md` |
| **PropertyAsset** | `trellis_property` | Core | BDR-OWN-001–005, BDR-AST-001–004 | Owners, Properties, Units/Assets |
| **Document** | `trellis_document` | Core | BDR-DOC-001–005 | Documents, versions, expiry alerts |
| **Workflow** | `trellis_workflow` | Core | BDR-WF-001–005, BDR-DEPT-001–004 | Departments, workflow templates/instances, approvals |
| **Inspection** | `trellis_inspection` | Add-on | BDR-INSP-001–005 | Inspections, checklist items, photos, offline sync |
| **Vendor** | `trellis_vendor` | Core | BDR-VEN-001–005, BDR-VEN-006 (real as of BRD v0.4) | Vendor identities, cross-tenant work assignments, relationship termination — see `multi_tenant_design.md` |
| **Ai** | `trellis_ai` | Add-on | BDR-AI-001–005 | Document extraction jobs, routing suggestions, override log |
| **Reporting** | `trellis_audit`, `trellis_reporting` | Core | BDR-RPT-001–003, BDR-SEC-002 | Append-only audit log, dashboards, Owner report generation |
| **Billing** | `trellis_billing` | N/A | BDR-BIL-001–008 (real as of BRD v0.2), BDR-BIL-009 (real as of BRD v0.5) | Usage-based invoicing, module entitlements, Paystack integration, dunning — see `billing_design.md` |
| **Leasing** | `trellis_leasing` | Add-on | BDR-LSE-001–008 (real as of BRD v0.4) | Vacancy listings, application intake, screening, e-signature, lease execution — see `leasing_design.md` |
| **Rent Collection** | `trellis_rentledger` | Add-on | BDR-RENT-001–006 (real as of BRD v0.3) | Rent schedules, recorded payments, arrears flagging — tracking only, no payment processing — see `rent_collection_design.md` |
| **Support** | `trellis_support` | N/A | BDR-SUP-001–004 (real as of BRD v0.4) | Cross-tenant support ticketing for the Platform Operator — Triggered, Not Scheduled (`roadmap.md`), not a numbered phase — see `support_design.md` |

BDR IDs refer to `Requirements/Trellis_BRD_v0.5.docx` (the current version; earlier IDs trace
back through v0.1–v0.4 as noted per module above). Where a module's implementation diverges from
or extends a BDR, note it in that module's own `README.md`, not here.

---

## Naming Conventions

### Files & Classes

| Type | Folder | Convention | Example |
|------|--------|-----------|---------|
| Entity | `Domain/Entities/` | PascalCase, singular | `Property.cs` |
| Value Object | `Domain/ValueObjects/` | PascalCase, singular, immutable | `Address.cs` |
| Domain exception | `Domain/Exceptions/` | `{Description}Exception` | `InvalidUnitStatusTransitionException.cs` |
| Repository interface | `Application/Interfaces/` | `I{Entity}Repository` | `IPropertyRepository.cs` |
| Service interface | `Application/Interfaces/` | `I{Entity}Service` | `IPropertyService.cs` |
| Service implementation | `Application/Services/` | `{Entity}Service` | `PropertyService.cs` |
| DTO | `Application/Dtos/` | `{Entity}{Action}Request/Response` | `PropertyCreateRequest.cs` |
| Validator | `Application/Validators/` | `{DtoName}Validator` | `PropertyCreateRequestValidator.cs` |
| EF Core mapping | `Infrastructure/Data/Configurations/` | `{Entity}Configuration` | `PropertyConfiguration.cs` |
| Repository implementation | `Infrastructure/Repositories/` | `{Entity}Repository` | `PropertyRepository.cs` |
| Controller | `Presentation/Controllers/` | `{Domain}Controller` | `PropertiesController.cs` |
| Cross-cutting middleware | `Core/Middleware/` | `{Purpose}Middleware` | `TenantScopeMiddleware.cs` |

See `coding_standards.md` for the language-level conventions (nullable reference types, async
naming, DI registration, Domain purity rules) that apply on top of this file layout.

### Database

| Type | Convention | Example |
|------|-----------|---------|
| Schema | `trellis_{module}` | `trellis_property`, `trellis_vendor` |
| Table | snake_case, singular | `property`, `vendor_assignment` |
| Column | snake_case | `tenant_id`, `owner_id`, `expires_at` |
| PK column | `{table}_id` | `property_id`, `tenant_id` |
| Index | `ix_{table}_{columns}` | `ix_document_expires_at` |

---

## Path to Extracting a Microservice Later

A module is a candidate for extraction only when at least one of these becomes true — not
before:

1. It has a scaling profile clearly different from the rest of the platform (the AI module,
   running document extraction jobs, is the most likely first candidate).
2. It needs its own deployment cadence independent of the rest of the platform (e.g., a
   compliance requirement to isolate it).
3. A dedicated team is assigned to own it.

Until then, the module boundary rule above is what protects us — extraction becomes a matter
of standing up a new process and swapping in-process calls for HTTP calls, not a rewrite.

### Making That Swap a Config Switch, Not a Rewrite

The boundary rule (Services call other modules' Services, never their repositories or
`DbContext`) already does most of the work. What makes a module's eventual extraction
mechanical rather than a redesign is one more discipline, applied from the start: **every
cross-module dependency is injected as an interface, and that interface has two possible
implementations — an in-process one and an HTTP one — selected by configuration, not by code
changes at the call site.**

```csharp
// The Workflow module depends on this interface — never on VendorService directly
public interface IVendorAssignmentReader
{
    Task<IReadOnlySet<Guid>> GetAssignedWorkItemIdsAsync(Guid vendorId, string workItemType);
}

// Today: registered by default — a plain in-process call, no network hop
public class InProcessVendorAssignmentReader : IVendorAssignmentReader
{
    private readonly VendorService _vendorService;
    public Task<IReadOnlySet<Guid>> GetAssignedWorkItemIdsAsync(Guid vendorId, string workItemType) =>
        _vendorService.GetAssignedWorkItemIdsAsync(vendorId, workItemType);
}

// Available from day one, unused until a module is actually extracted
public class HttpVendorAssignmentReader : IVendorAssignmentReader
{
    private readonly HttpClient _client;
    public async Task<IReadOnlySet<Guid>> GetAssignedWorkItemIdsAsync(Guid vendorId, string workItemType) =>
        await _client.GetFromJsonAsync<HashSet<Guid>>($"/internal/vendor-assignments/{vendorId}?type={workItemType}");
}

// Program.cs — the only place the choice is made
services.AddScoped<IVendorAssignmentReader>(sp =>
    config["Modules:Vendor:Hosting"] == "Remote"
        ? sp.GetRequiredService<HttpVendorAssignmentReader>()
        : sp.GetRequiredService<InProcessVendorAssignmentReader>());
```

What this buys, concretely:

| Requirement for a module to run standalone | True today | What activates it |
|----------------------------------------------|:---:|-----------------------|
| Owns its schema exclusively | Yes | — |
| Owns its own `DbContext`, mapped only to that schema | Yes | — |
| Every other module reaches it only through an interface | Yes | — |
| That interface has both an in-process and an HTTP implementation | Not yet | Write the `Http{Module}Client` adapter once, alongside the in-process one |
| A thin host project can run just that module | Not yet | A small `Trellis.{Module}.Host` — its own `Program.cs`, its own connection string scoped to its one schema, reusing the module's existing code unchanged |
| Callers don't need to change when the module moves | Yes, by construction | — |

We don't build the HTTP adapter or the thin host for all fourteen modules today — that would be
paying the reference architecture's operational cost before any of its triggers have fired.
But writing every cross-module dependency as an interface, from the first line of code, means
that when a trigger *does* fire (the Ai module is still the most likely candidate — see above),
extracting it is: write one adapter class, write one small host project, flip one config value.
No call site anywhere else in the codebase changes. That is what "each module can be
standalone" means here — not that they run standalone today, but that nothing about how
they're built stands in the way of it later.

---

## Related Documentation

- [Data Model](./data_model.md) — schema definitions, RLS policies, EF Core conventions
- [API Standards](./api_standards.md) — response envelope, versioning, error codes, OpenAPI grouped per module
- [Security Guide](./security_guide.md) — auth architecture, secrets, encryption
- [Multi-Tenant Design](./multi_tenant_design.md) — isolation layers, Vendor cross-tenant model
- [Testing Strategy](./testing_strategy.md) — how module boundaries and RLS get verified
- [DevOps & Environments](./devops_environments.md) — CI/CD, hosting, environment separation
- [Frontend Architecture](./frontend_architecture.md) — the `trellis-web` client and its integration contract with this API
- [Error Handling & Resilience](./error_handling.md) — `ServiceResult<T>` failure modes, exception middleware, external-dependency retries
- [Billing Design](./billing_design.md) — the 10th module, and the `IPaymentGateway` seam that mirrors this document's dual-mode hosting pattern
- [Deployment Topologies](./deployment_topologies.md) — the same interface-and-DI pattern applied to a SaaS-vs-on-premises config switch
- [Coding Standards](./coding_standards.md) — Domain purity enforcement, DI registration pattern, repository query encapsulation
- [Leasing Design](./leasing_design.md) / [Rent Collection Design](./rent_collection_design.md) — the 11th and 12th modules, both real as of BRD v0.3
- [Notifications Design](./notifications_design.md) — the 13th module, a foundation nearly everything else depends on, pulled forward to Phase 1
- [Support Design](./support_design.md) — the 14th module, deliberately minimal, Triggered rather than phase-scheduled
- [Operational Flows](./operational_flows.md) — why Maintenance composes existing modules instead of getting its own
- [Build Roadmap](./roadmap.md) — the phase sequence built on this document's Module Catalogue and BDR ranges
