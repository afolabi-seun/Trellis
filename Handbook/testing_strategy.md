# Testing Strategy

The OMS reference doesn't include a standalone testing document — its testing discipline is
implicit in per-service CI configs we don't have visibility into. Trellis gets an explicit one,
partly because a modular monolith's biggest risk is a module boundary quietly eroding (a
repository reaching into another module's schema, a Service bypassing tenant scoping), and
tests are how that risk gets caught before a customer does, not after.

---

## Test Pyramid

| Layer | Framework | What it covers | Runs against |
|-------|-----------|------------------|----------------|
| Unit | xUnit + FluentAssertions + NSubstitute | Service-layer business logic, in isolation from the database | Mocked repositories |
| Integration | xUnit + Testcontainers.PostgreSql | Repository queries, EF Core mappings, **RLS policies themselves** | A real, ephemeral Postgres 16 container per test run |
| API / Contract | xUnit + `WebApplicationFactory<Program>` | Controller routing, auth attributes, the `ApiResponse<T>` envelope, status codes | The full app in-memory, real HTTP pipeline, test database |
| Manual UAT | Checklist tied to BDR acceptance criteria | End-to-end flows a script can't easily assert (visual review, Owner report readability) | Staging environment |

**Repository-layer tests are never mocked against `DbContext`.** This is a deliberate
departure from a common .NET testing shortcut: an in-memory EF Core provider does not enforce
Row-Level Security, so a repository test using it would pass even if the RLS policy from
`data_model.md` were missing entirely. Testcontainers spins up real Postgres with the actual
migrations applied, so a broken or missing RLS policy shows up as a failing test, not a
production incident.

---

## Multi-Tenant Isolation Test Suite

This is the highest-value test suite in the whole project and gets written before most feature
tests, not after. It exists specifically to prove the 3 defence-in-depth layers from
`multi_tenant_design.md` actually hold, individually and together:

```csharp
[Fact]
public async Task GetProperty_WhenTenantContextMismatched_Returns404NotLeak()
{
    // Arrange: seed a property under Tenant A
    var propertyId = await SeedPropertyAsync(tenantId: TenantA);

    // Act: request it while TenantContext is set to Tenant B
    TenantContext.Current = new TenantContext(TenantB);
    var result = await _propertyService.GetByIdAsync(propertyId);

    // Assert: not found, not forbidden — existence isn't confirmed (BDR-SEC-002)
    result.Should().BeOfType<ServiceResult<Property>.NotFound>();
}

[Fact]
public async Task GetProperty_WithRlsDisabledAtAppLayer_StillBlockedByDatabase()
{
    // Arrange: bypass TenantAwareRepository entirely, query DbContext directly
    // with the wrong app.current_tenant_id set at the connection level.
    // Assert: the database itself returns zero rows — RLS holds even if the
    // application-layer filter is somehow skipped.
}
```

Required negative-path coverage before any module ships:

- A Tenant Admin/Department Staff request for another tenant's data → 404, never 403.
- A Vendor token requesting a `work_item_id` not present in their own `vendor_assignment` rows
  → 404, including when the ID is guessed/enumerated (IDOR-style).
- A Vendor token requesting a work item that **was** assigned but has since moved to
  `CLOSED` status → confirm the intended behaviour (read-only vs. fully hidden) is what the
  BDR actually specifies, not an accidental side effect of the query.
- `app.is_system` bypass is reachable only from Super Admin / background-job code paths —
  a test that attempts to set it from a simulated request context should fail to compile or
  fail at runtime, not silently succeed.

Two more negative-path categories added by a later handbook gap review, held to the same bar:

- **Concurrency**: updating an entity with a stale `RowVersion` returns `ConcurrencyConflict`
  (409), never a silent overwrite of the other user's change (BDR-NFR-006, `coding_standards.md`).
- **Accessibility**: the Playwright/`axe-core` scan (`frontend_architecture.md`) is a CI gate,
  not an optional report — a page introducing a new WCAG 2.1 AA violation fails the build the
  same way a failing unit test does (BDR-NFR-007).

---

## BDR Traceability

Every BDR in `Requirements/Trellis_BRD_v0.1.docx` that describes user-facing behaviour gets at
least one test whose name or `[Trait]` attribute references it:

```csharp
[Fact]
[Trait("BDR", "BDR-VEN-003")]
public async Task AssignVendor_ToSameWorkItemTwice_ReturnsConflict() { ... }
```

This makes `dotnet test --filter Trait=BDR=BDR-VEN-003` a direct way to answer "is this
requirement covered," and gives a mechanical way to build the traceability matrix flagged as a
gap earlier — it can be generated from the test suite rather than maintained by hand.

---

## Test Data & Seeding

| Environment | Seed approach |
|-------------|-----------------|
| Unit tests | In-memory builders (`TestDataBuilder.Property().WithTenant(...).Build()`) — no database |
| Integration tests | Testcontainers Postgres, migrated fresh, seeded per-test via the same `TenantProvisionRequest` path real provisioning uses (dogfooding the provisioning flow itself) |
| Local development | `dotnet run --seed-demo-data` flag loads 2–3 fake tenants, a handful of properties/vendors, so the app isn't empty on first run |
| Staging | A controlled, hand-picked dataset that mirrors real usage patterns closely enough for UAT against BDR acceptance criteria, without using any real prospect's data |

No production data is ever copied into a lower environment, including anonymised — Trellis
has no production tenants yet, but this rule is worth stating now so it's already policy
before the first real tenant is provisioned.

---

## Naming Convention

`MethodUnderTest_StateUnderTest_ExpectedBehavior`, e.g.
`CloseAssignment_WhenVendorNotAssigned_Returns404`. Consistent naming makes a failing-test list
readable as a plain-English description of what broke, without opening the file.

---

## CI Gate

Every PR runs, in order: unit tests → integration tests (Testcontainers) → API/contract tests.
A PR touching `Modules/*/Data/` or any RLS-adjacent code additionally requires the multi-tenant
isolation suite to pass explicitly (it's part of integration tests, but called out here because
it's the one category that must never be skipped or marked `[Skip]` under deadline pressure).
See `devops_environments.md` for where this fits in the pipeline itself.

---

## Related Documentation

- [Multi-Tenant Design](./multi_tenant_design.md) — the isolation model this suite verifies
- [DevOps & Environments](./devops_environments.md) — where these tests run in CI
- [Data Model](./data_model.md) — RLS policies under test
- [Error Handling & Resilience](./error_handling.md) — the failure-mode table every negative-path test targets
- [Coding Standards](./coding_standards.md) — Domain purity is what makes business-rule unit tests possible without a database at all
- [Operational Flows](./operational_flows.md) — testing a flow composed across module boundaries, not just one module in isolation
- [Leasing Design](./leasing_design.md) / [Rent Collection Design](./rent_collection_design.md) / [Notifications Design](./notifications_design.md) — the newest modules' BDR IDs this suite's traceability convention extends to
- [Security Guide](./security_guide.md) — negative-path coverage this suite should add for password reset (token reuse, expiry) and file-upload scanning (rejected type, infected file, scanner outage)
- [Build Roadmap](./roadmap.md) — the exit criteria per phase that this test suite's coverage is measured against
