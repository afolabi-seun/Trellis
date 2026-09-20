# Multi-Tenant Design

This is the document that makes Trellis different from a generic multi-tenant SaaS template.
Two things need solving here: standard PM-company tenant isolation (well-trodden ground,
adapted directly from the reference architecture), and **Vendor cross-tenant identity**
(Trellis's core product differentiator — a single Vendor login usable across multiple PM
company tenants, with no equivalent in the reference architecture at all).

**A note on scope added in this revision**: Tenant Provisioning was fully specified; what ends a
tenant or a Vendor relationship was not — a real gap a handbook review surfaced. Two lifecycle
endpoints close it: Tenant Offboarding and Vendor Relationship Termination, below. Both are now
real: BDR-TEN-006 and BDR-VEN-006 are folded into `Requirements/Trellis_BRD_v0.4.docx`, the
same path the security and notification gaps closed elsewhere in this handbook took.

---

## Tenant Isolation Model

### The 3 Layers

Trellis enforces tenant isolation at 3 independent layers — fewer than the reference
architecture's 8, because several of its layers (network zone segmentation, per-service DB
credentials, mTLS between services) exist to secure boundaries *between deployed services*
that simply don't exist in a modular monolith. The 3 that remain are the ones doing the actual
tenant-isolation work, and each is independently sufficient — a bug in one should not expose
cross-tenant data if the other two hold:

1. **Database Row-Level Security** — active even for the application's own DB role. See
   `data_model.md` for the actual policy SQL.
2. **Application-layer scoping** — `TenantAwareRepository<T>` injects `WHERE tenant_id = @tenantId`
   on every query, independent of whether RLS is also doing it.
3. **JWT claim validation at the request boundary** — `TenantScopeMiddleware` resolves and
   validates `tenant_id` once per request, before any module code runs.

```csharp
// Core/Middleware/TenantScopeMiddleware.cs
public class TenantScopeMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        var vendorId = context.User.FindFirst("vendor_id")?.Value;

        if (tenantId is not null)
            TenantContext.Current = new TenantContext(Guid.Parse(tenantId));
        else if (vendorId is not null)
            VendorContext.Current = new VendorContext(Guid.Parse(vendorId));
        // Neither claim + SUPER_ADMIN role → platform-scope request; no context set

        await next(context);
    }
}
```

**Refined during Phase 1 implementation**: the built version also reads the `role` claim and sets
a third, explicit `SystemContext.IsSystem` flag only when `role == "SUPER_ADMIN"` — not inferred
from "neither `tenant_id` nor `vendor_id` is present" the way the comment above reads. That
inference would be fail-open: an unauthenticated or malformed request that somehow reached a
scoped query with no claims at all would get the Super Admin RLS bypass by default instead of
seeing nothing. `app.is_system` in every RLS policy is driven by this explicit flag, never by
absence of the other two.

`TenantContext.Current` and `VendorContext.Current` are `AsyncLocal<T>`-backed, exactly the
same idiom the reference uses for its own request-scoped tenant context — it flows correctly
through `async`/`await` without needing to be threaded through every method signature.

### TenantAwareRepository\<T\>

```csharp
// Core/Data/TenantAwareRepository.cs
public abstract class TenantAwareRepository<T> where T : class, ITenantOwned
{
    protected IQueryable<T> Scoped(DbSet<T> set) =>
        set.Where(e => e.TenantId == TenantContext.Current.TenantId);
}
```

Every module's repository classes inherit from this rather than querying `DbSet<T>` directly,
so "forgot the tenant filter" is a base-class problem to fix once, not a per-query risk spread
across fourteen modules.

### Super Admin Bypass

The reference architecture's core tenant-scoping idiom is:

```csharp
// Reference (OMS) pattern
var scopeTenantId = user.IsSuperAdmin ? null : GetTenantId();
```

Trellis keeps this exact idiom for the Super Admin case — a `null` scope means "no tenant
filter, see everything," gated behind the `SUPER_ADMIN` role and always paired with
`app.is_system = true` at the RLS layer, never invoked from a request path a Tenant Admin or
Department Staff user could reach. What Trellis does **not** do is reuse this same
null-bypass shape for Vendor access, even though a Vendor also needs to "see across tenants."
That distinction is the whole point of the next section.

### Identity Resolution Before Tenant Is Known

`app_user` gets the standard `tenant_isolation` policy from data_model.md, same as any other
tenant-owned table — but four of Auth's own code paths run *entirely* before any tenant context
exists, because they're the paths that establish it: `LoginAsync`, `VerifyMfaAsync`,
`RequestPasswordResetAsync`, and `ResetPasswordAsync`. Each is anonymous by nature (a username, a
partial token, or a bearer-authenticated reset link — never a JWT `tenant_id` claim), and each
does more than one lookup before returning: `LoginAsync` alone reads the user by username, then
(on the matching path) writes the lockout counter, the `AUTH_SUCCESS`/`AUTH_LOCKOUT` audit
record, and — via `IssueSessionAsync` — the new session row and another audit write.

**The whole method runs under `SystemContext.Enter()` (`Core/Middleware/TenantContext.cs`), not
just the initial lookup.** The first version of this wired only the lookup, on the reasoning that
resolving identity was the one step that needed the bypass — wrong: every write in the rest of
each method (the lockout update, the audit record, `notification_delivery`'s insert for a
password-reset or MFA-recovery notification) hits a table with the same standard
`tenant_isolation` policy, and by the time those writes ran, `TenantContext.Current` was still
`null` and the earlier `Enter()` scope had already been disposed. Found the hard way: every real
login threw `42501: new row violates row-level security policy for table "audit_record"` the
first time this ran against actual Postgres instead of a mocked repository — `ResetPasswordAsync`
was worse, since it had no `Enter()` at all and its `app_user` lookup simply returned `null`
under RLS, which looked like a correctly-generic "invalid token" response and would have shipped
silently broken. `Enter()` scopes to the whole flow now (`using var systemScope = SystemContext.Enter();`
at the top of each method), restored automatically when the method returns.

This is a third, narrower case than the Super Admin/background-job uses `SystemContext` was built
for above, but the same flag: crossing tenants here is inherent to what these flows *are*, not a
role-driven exception. `tenant_id` is nullable on `app_user` (a Super Admin's own row has none);
SQL's three-valued logic already makes those platform-level rows invisible to every tenant filter
and visible only under `app.is_system`, the same behavior `audit_record`'s nullable `tenant_id`
relies on.

`session`, `password_reset_token`, and `mfa_recovery_code` carry no `tenant_id` column at all and
get **no RLS policy** — deliberately, not an oversight. Every read against them is an exact,
unforgeable-key lookup (a `session_id` pulled from an already-validated JWT, a SHA-256 hash of a
token or recovery code), never a tenant-scoped listing a policy would need to filter. A join back
to `app_user.tenant_id` to gate these would add a subquery (itself subject to `app_user`'s own RLS)
to the hottest path in the app — every authenticated request checks its session — for no isolation
gain over the exact-key lookup the application layer already enforces. Isolation for these three
tables is the unforgeable-key layer, not RLS.

---

## Vendor Cross-Tenant Identity

### Why this isn't just "Super Admin, but smaller"

A naive extension of the Super Admin idiom would give a Vendor a `null` tenant scope too,
relying on some other filter to narrow it back down. That's the wrong shape for two reasons:
a Vendor's visibility isn't "all tenants minus some," it's "a specific, small, explicitly
granted set of individual work items," and that set changes per work item, not per tenant —
a Vendor assigned one inspection at Tenant A has no visibility into anything else at Tenant A,
even though they do have a real, standing relationship with Tenant A. So the scope isn't a
tenant boolean at all; it's a set of `(tenant_id, work_item_id)` pairs, and it has to be
computed fresh from the assignment table on every request, not cached as a claim.

### The Assignment Table

`trellis_vendor.vendor_assignment` (full column definitions in `data_model.md`) is the single
source of truth for what a Vendor can see:

```
vendor_id | tenant_id | work_item_type      | work_item_id | status
----------|-----------|----------------------|--------------|----------
V-101     | T-A       | WORKFLOW_INSTANCE    | WF-9001      | ASSIGNED
V-101     | T-B       | INSPECTION           | INS-4400     | IN_PROGRESS
V-101     | T-A       | INSPECTION           | INS-4401     | COMPLETED
```

A single Vendor identity (`V-101`) here holds assignments across two different tenants
(`T-A`, `T-B`) — this table alone is what makes a single Vendor login viable platform-wide,
without Vendor being either a full tenant or a plain contact record on each one (BDR-VEN-004).

### VendorContext and VendorScopedRepository

```csharp
// Core/Data/VendorScopedRepository.cs
public abstract class VendorScopedRepository<T> where T : class, IVendorAssignable
{
    private readonly IVendorAssignmentReader _assignments;

    protected async Task<IQueryable<T>> ScopedAsync(DbSet<T> set)
    {
        var allowedIds = await _assignments.GetAssignedWorkItemIdsAsync(
            VendorContext.Current.VendorId, typeof(T).Name);

        return set.Where(e => allowedIds.Contains(e.WorkItemId));
    }
}
```

`IVendorAssignmentReader` is a small read-only interface the Vendor module exposes to any
other module's repository layer — note this is a repository-to-repository dependency, which
looks like it breaks the "modules only talk through Services" rule in
`project_architecture.md`. It doesn't: `IVendorAssignmentReader` is itself implemented inside
the Vendor module and injected via DI as an abstraction, the same way `ITenantOwned` is a
marker interface rather than a cross-module data reach. The Workflow and Inspection modules
depend on an interface the Vendor module publishes, not on the Vendor module's tables.

### RLS for Vendor-Visible Tables

See `data_model.md` for the full `tenant_or_vendor_isolation` policy SQL. The important
property: a Vendor's DB session sets `app.current_vendor_id`, never `app.current_tenant_id` —
those two settings are mutually exclusive per request, matching the JWT claim shape from
`security_guide.md` (a Vendor token carries `vendor_id`, never `tenant_id`).

### Vendor-Assigned Auth Attribute

```csharp
// Core/Attributes/VendorAssignedToWorkItemAttribute.cs
public class VendorAssignedToWorkItemAttribute : TypeFilterAttribute
{
    public VendorAssignedToWorkItemAttribute() : base(typeof(VendorAssignmentFilter)) { }
}

// The filter itself resolves { vendorId, workItemId } from the route + JWT
// and checks vendor_assignment directly before the controller action runs —
// a 404, not 403, on no match (see api_standards.md on why).
```

### What a Vendor Never Gets

To keep this model narrow and auditable, a Vendor identity explicitly:

- Has no `tenant_id` of its own, ever — not even a "home" tenant.
- Cannot list, search, or browse a tenant's properties, owners, or other work items — only
  the exact `work_item_id`s it has been assigned.
- Cannot see another Vendor's assignments, even at the same tenant.
- Cannot be granted platform-wide scope the way Super Admin can — there is no `IsSuperVendor`
  concept, and there should never be one; broadening a Vendor's access always means adding a
  specific row to `vendor_assignment`, never loosening a filter.

This is the concrete technical form of the product idea from the BRD: a maintenance vendor
is a first-class user who can log in and close out their own tickets, without ever becoming a
sub-tenant or a passive contact record (BDR-VEN-001–005).

### Vendor Relationship Termination (BDR-VEN-006, real as of BRD v0.4)

`vendor_assignment` rows specify what a Vendor can see *today*, but nothing so far specified
how a PM company stops working with a Vendor going forward — the assignment table alone
doesn't distinguish "this Vendor finished this job" from "this Vendor is no longer someone we
do business with." Left unspecified, a Tenant Admin's only lever would be to stop creating new
assignments, which leaves the relationship itself in an ambiguous, undeclared state and gives
no single place to check "are we still working with this Vendor."

```
// trellis_vendor.tenant_vendor_relationship
tenant_vendor_relationship_id  UUID PK
tenant_id                      UUID NOT NULL
vendor_id                      UUID NOT NULL
status                         VARCHAR(20) NOT NULL DEFAULT 'ACTIVE'   -- ACTIVE|TERMINATED
terminated_at                  TIMESTAMPTZ NULLABLE
terminated_by_user_id          UUID NULLABLE FK -> app_user
UNIQUE (tenant_id, vendor_id)
```

```csharp
// Core/Vendor/ITenantVendorRelationshipService.cs
public interface ITenantVendorRelationshipService
{
    Task<ServiceResult> TerminateAsync(Guid tenantId, Guid vendorId, Guid terminatedByUserId);
}
```

`TerminateAsync` sets `status = TERMINATED` and **gates only new assignments** — a Tenant Admin
or Department Staff user can no longer create a `vendor_assignment` row for this
`(tenant, vendor)` pair once terminated. It deliberately does not revoke the Vendor's visibility
into work items already assigned: an in-progress or recently completed inspection or
maintenance ticket keeps its existing assignment and audit trail exactly as `data_model.md`'s
append-only philosophy would suggest — terminating a business relationship shouldn't rewrite
history or strand a job the Vendor was actively completing. If a Tenant Admin needs to cut off
an in-progress assignment immediately (e.g. a trust or conduct issue), that's a separate,
narrower action — closing or reassigning the specific `vendor_assignment` row, already covered
by the existing Vendor/Workflow closure mechanics — not something Termination does implicitly.

Because a single Vendor identity works across many tenants (the whole point of this document),
termination is always scoped to one `(tenant_id, vendor_id)` pair, never to the Vendor identity
globally — Tenant A ending its relationship with a Vendor has no effect on that Vendor's
standing, or assignments, at Tenant B. The one place a Vendor's assignments are terminated
platform-wide rather than per-tenant is when the tenant itself is offboarded (below), which
terminates every `tenant_vendor_relationship` row for that tenant as one step of that flow.

New `audit_type` value: `VENDOR_RELATIONSHIP_TERMINATED`.

---

## Tenant Provisioning

The reference architecture provisions a new tenant via a 12-step distributed saga, because
provisioning has to create records consistently across 6 independently deployed services with
their own databases (or database credentials, in some cases) and message queues. Trellis has
one database and one process, so the equivalent operation is a single local transaction:

```csharp
public async Task<ServiceResult<Tenant>> ProvisionTenantAsync(TenantProvisionRequest request)
{
    await using var transaction = await _tenantDbContext.Database.BeginTransactionAsync();

    var tenant = await _tenantRepository.CreateAsync(request.ToTenant());
    var adminUser = await _authService.CreateTenantAdminAsync(tenant.TenantId, request.AdminEmail);
    await _auditService.RecordAsync(AuditType.TenantProvisioned, tenant.TenantId, adminUser.UserId);

    await transaction.CommitAsync();
    return ServiceResult<Tenant>.Success(tenant);
}
```

`_authService` here is a genuine cross-module call (Tenant module's service calling Auth
module's service, in-process) participating in the same `DbContext` transaction scope — this
works cleanly today because everything shares one database; if the Auth module is ever
extracted to its own service, provisioning would need to become a saga at that point, and the
reference architecture's own saga implementation is the direct model to copy when that day
comes.

---

## Tenant Offboarding (BDR-TEN-006, real as of BRD v0.4)

Provisioning was fully specified; its inverse — what happens when a PM company's contract with
Trellis ends — was not. Churn is a normal part of a SaaS lifecycle, not an edge case, and
leaving it unspecified means it happens ad hoc, via a one-off support/engineering request, the
first time it actually occurs. This closes that gap with an explicit, staged flow rather than
an immediate hard delete — the same "erasure is a process, not a DELETE statement" principle
`security_guide.md`'s Compliance section already applies to individual erasure requests,
applied here at the tenant level. The full Tenant CRUD/provisioning contract, including this
endpoint's `/platform/` prefix convention, is specified in `api_standards.md`'s "Tenant
Management Endpoints (Platform)" section.

```
1. POST /platform/tenants/{id}/offboard   (SUPER_ADMIN only — not self-service; this begins
                                            an irreversible-after-the-window process)
2. tenant.status → 'OFFBOARDING' (new value alongside ACTIVE/SUSPENDED, data_model.md).
      → All active sessions for this tenant's users are revoked (the same session-revocation
        path `security_guide.md` uses for logout and password reset — no second mechanism).
      → New logins for this tenant's users are rejected with a clear, non-generic message
        (offboarding is an intentional, known state — unlike account-existence, there's no
        disclosure risk in saying so).
   Existing `tenant_vendor_relationship` rows for this tenant are set to TERMINATED (above) as
   part of the same step — a tenant that can no longer log in shouldn't still be creating new
   Vendor assignments, and no Vendor should retain a live relationship with a tenant that's
   winding down.
3. An async export job compiles the tenant's data — properties, leases, rent records, documents
   (metadata and content), inspection history — into a downloadable package, delivered via the
   same secure, single-use, time-limited link pattern used for password reset and the
   Prospective Tenant application flow (`security_guide.md`, `leasing_design.md`) and sent
   through `notifications_design.md`. This is the tenant's copy of their own data on the way
   out, not a Trellis-internal artifact.
4. The tenant sits in `OFFBOARDING` for a configurable retention window — a **90-day default is
   offered here for planning purposes only**; the actual period is a contractual and legal
   decision (this handbook is not legal advice), following the exact same caveat
   `data_model.md`'s audit-retention section already states for the same reason. Throughout the
   window, all three tenant-isolation layers above continue to apply unchanged: an offboarding
   tenant's data is inert, not unprotected.
5. At window expiry, PII is erased using the identical mechanism `security_guide.md` already
   specifies for individual erasure requests (fields nulled across `trellis_property`,
   `trellis_document`, `trellis_vendor` — rows kept, not deleted, for referential and audit
   integrity), applied tenant-wide rather than to one subject. The Billing subscription is
   cancelled via the existing `IPaymentGateway` cancel-subscription call (`billing_design.md`).
   `tenant.status` moves to the terminal value `OFFBOARDED`.
```

New `audit_type` values: `TENANT_OFFBOARDING_INITIATED`, `TENANT_EXPORT_READY`,
`TENANT_OFFBOARDED`. New notification template key: `TENANT_OFFBOARDING_EXPORT_READY`
(`notifications_design.md`).

This is deliberately the same shape as every other item folded into BRD v0.4 in this
handbook: a real operational gap, closed with the smallest mechanism that reuses what already
exists (session revocation, secure links, the erasure pipeline, subscription cancellation)
rather than inventing new machinery for an event that, while infrequent, isn't optional to
handle correctly.

---

## Related Documentation

- [Data Model](./data_model.md) — RLS policy SQL, `vendor_assignment` schema, `tenant.status` values
- [Security Guide](./security_guide.md) — the 3 isolation layers in the context of overall security posture; the session-revocation and PII-erasure mechanisms Tenant Offboarding reuses
- [Project Architecture](./project_architecture.md) — the module-boundary rule that keeps `IVendorAssignmentReader` an abstraction, not a schema reach
- [Testing Strategy](./testing_strategy.md) — the isolation test suite that proves this model holds
- [Frontend Architecture](./frontend_architecture.md) — why hiding UI for a Vendor is UX, not enforcement
- [Leasing Design](./leasing_design.md) / [Rent Collection Design](./rent_collection_design.md) — two modules with no Vendor-visibility clause at all, and why; the secure-link pattern Tenant Offboarding's data export reuses
- [Notifications Design](./notifications_design.md) — where the Tenant Offboarding export-ready notification is sent from
- [Billing Design](./billing_design.md) — the subscription-cancellation call Tenant Offboarding makes on completion
- [Operational Flows](./operational_flows.md) — how the Vendor scoping mechanism here is reused identically by both the Maintenance and Inspection journeys
- [Build Roadmap](./roadmap.md) — why Phase 1's isolation test suite is this roadmap's hardest gate
