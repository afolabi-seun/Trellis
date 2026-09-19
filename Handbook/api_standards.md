# API Standards

All Trellis endpoints — across every module — follow these conventions. Consistency matters
more here than in the reference architecture even though there's only one deployable process:
a Vendor's client integration, a future mobile app, and Owner-report exports all depend on one
predictable API surface, not nine module-flavoured ones.

---

## Design Principles

| Standard | Specification |
|----------|--------------|
| Specification Format | OpenAPI 3.1, generated from the ASP.NET Core controllers via Swashbuckle; hosted at `/api-docs`, grouped per module (see below) |
| Versioning | URL path prefix: `/api/v1/`. Breaking changes require a new version; additive changes stay on the current one. With one deployable, a version bump ships everything at once — no cross-service version skew to coordinate. |
| Base URL | `https://api.trellis-platform.com/api/v1/` — tenant is resolved from the JWT, never from a subdomain (BDR-SEC-004: no tenant-identifying data in URLs) |
| Content Type | `Content-Type: application/json` required on all write requests. `Accept: application/json` assumed. |
| Idempotency | `POST` endpoints that create a resource accept `X-Idempotency-Key`. Same key within 24 hours returns the cached response. Backed by a `trellis_core.idempotency_key` table (see below) — no Redis dependency for Phase 1. |
| Pagination | Cursor-based: `?cursor={opaque_token}&limit={n}` (default 25, max 100). Response includes `next_cursor` and `has_more`. |
| Field Projection | Deferred until a real payload-size problem shows up — Trellis's list responses are small (properties, units, documents), unlike OMS's high-volume event streams. |
| Rate Limit Headers | Deferred to when Trellis has external/API-key clients beyond Vendor logins — see Roadmap in the BRD. |

We keep OpenAPI, versioning, idempotency, and cursor pagination from the reference because
they cost little and prevent real problems (double-submitted forms, unbounded list responses).
We drop field projection and rate-limiting for now because nothing in Trellis's Phase 1 traffic
profile needs them yet — see `project_architecture.md`'s philosophy on deferring OMS's
heavier infrastructure until a concrete trigger appears.

---

## OpenAPI Grouping By Module

Every module's controllers are tagged with a Swashbuckle `GroupName` matching that module's key
in the Module Catalogue (`project_architecture.md`) — `"auth"`, `"billing"`, `"leasing"`, and so
on. `SwaggerGenOptions` registers one `SwaggerDoc` per module, plus one combined `"all"` doc:

```csharp
// Configuration/SwaggerConfiguration.cs
services.AddSwaggerGen(options =>
{
    foreach (var module in ModuleRegistry.Keys)   // "auth", "billing", "leasing", ...
        options.SwaggerDoc(module, new OpenApiInfo { Title = $"Trellis — {module}", Version = "v1" });

    options.SwaggerDoc("all", new OpenApiInfo { Title = "Trellis API", Version = "v1" });
    options.DocInclusionPredicate((docName, apiDesc) =>
        docName == "all" || apiDesc.GroupName == docName);
});
```

This gives every module its own browsable spec at `/api-docs/{module}/swagger.json` — useful for
onboarding (a new contributor only needs the one module's spec open, not all fourteen) and, per
`project_architecture.md`'s extraction path, exactly the document that ships as-is if that module
is ever pulled out into its own deployable service, with no redesign needed. It does **not**
change the integration contract: the frontend's typed client (`frontend_architecture.md`) is
still generated from the single `"all"` document, so the SPA sees one coherent API regardless of
how the spec is split for browsing.

---

## Response Envelope

Every response, success or error, uses the same envelope:

```json
{
  "status": "success",
  "data": { ... },
  "meta": {
    "request_id": "7f3a2b1c-...",
    "timestamp": "2026-09-18T09:00:00Z"
  }
}
```

```json
{
  "status": "error",
  "error": {
    "code": "TRL-E-VEN-2001",
    "message": "Vendor is already assigned to this work item",
    "details": [
      { "field": "vendor_id", "issue": "duplicate assignment for this work_item_id" }
    ]
  },
  "meta": {
    "request_id": "7f3a2b1c-...",
    "timestamp": "2026-09-18T09:00:00Z"
  }
}
```

### ApiResponse\<T\> in .NET

Identical shape to the reference — this is the one piece of `Core/` every module depends on:

```csharp
// Core/Dtos/ApiResponse.cs
public class ApiResponse<T>
{
    public string Status { get; init; } = "success";
    public T? Data { get; init; }
    public ApiError? Error { get; init; }
    public ApiMeta Meta { get; init; } = ApiMeta.Now();

    public static ApiResponse<T> Success(T data) => new() { Status = "success", Data = data };
    public static ApiResponse<T> Failure(ApiError error) => new() { Status = "error", Error = error };
}
```

`ServiceResult<T>` (returned by every Service method) maps to `ApiResponse<T>` at the
controller boundary via a single `ToActionResult()` extension — this is what keeps controllers
thin (see `project_architecture.md`).

---

## HTTP Status Code Usage

| Code | When |
|------|------|
| 200 OK | Successful GET, PATCH, PUT |
| 201 Created | Successful POST that creates a resource |
| 204 No Content | Successful DELETE |
| 400 Bad Request | Malformed request / missing required field |
| 401 Unauthorized | Missing or invalid JWT |
| 403 Forbidden | Valid token but insufficient role, or a Vendor token scoped to a different work item |
| 404 Not Found | Resource does not exist for this tenant (or, for a Vendor, not in their assignment set) |
| 409 Conflict | Duplicate creation (unique constraint), a business rule conflict (e.g. closing an already-closed workflow step), or a `ConcurrencyConflict` — a stale `RowVersion` presented on an update (BDR-NFR-006, real as of BRD v0.4; distinct error code from the other two cases — see `error_handling.md`) |
| 422 Unprocessable Entity | Schema-valid request but business rule violation (e.g. approving a workflow step out of sequence) |
| 429 Too Many Requests | The `auth-sensitive` rate-limiting policy rejected the request (login, password-reset request, or MFA verification exceeded its per-IP window) — `Retry-After` header set (BDR-SEC-009, real as of BRD v0.4, `security_guide.md`) |
| 500 Internal Server Error | Unexpected server fault |

We deliberately return **404**, not 403, when a tenant or Vendor asks for a resource outside
their scope — this matches the reference architecture's own reasoning: a 403 confirms the
resource exists, which is itself a cross-tenant information leak (BDR-SEC-002).

---

## Error Code Registry

Format: `TRL-E-{module}-{number}`.

| Module | Prefix | Range |
|--------|--------|-------|
| Platform / Global | `TRL-E-PLT` | 0001–0999 |
| Auth | `TRL-E-AUT` | 1000–1999 |
| Tenant | `TRL-E-TEN` | 2000–2999 (reserved) |
| PropertyAsset | `TRL-E-AST` | 3000–3999 |
| Document | `TRL-E-DOC` | 4000–4999 |
| Workflow | `TRL-E-WF` | 5000–5999 |
| Inspection | `TRL-E-INS` | 6000–6999 |
| Vendor | `TRL-E-VEN` | 7000–7999 |
| Ai | `TRL-E-AI` | 8000–8999 |
| Reporting | `TRL-E-RPT` | 9000–9999 |
| Billing | `TRL-E-BIL` | 10000–10999 |
| Leasing | `TRL-E-LSE` | 11000–11999 |
| Rent Collection | `TRL-E-RENT` | 12000–12999 |
| Notification | `TRL-E-NOTIF` | 13000–13999 |
| Support | `TRL-E-SUP` | 14000–14999 (reserved — Triggered, Not Scheduled) |

Note the Tenant module took the `2000` range in the table above (aligned to its position in
the module list) even though it's marked reserved — codes aren't allocated until a module
actually needs one, to avoid an empty registry full of placeholder codes.

Error codes and their default messages live in a static `ErrorCodeRegistry` class in
`Core/Helpers/`, not a database table — with one process and no independent service releases
to coordinate, there's no need for OMS's Redis-cached-over-database lookup. Never hardcode an
error message directly in service code; always resolve it through the registry so the same
fault always produces the same code and message.

---

## Authentication

### JWT Bearer (all portal and mobile users)

```
Authorization: Bearer eyJhbGciOiJSUzI1NiJ9...
```

RS256-signed. Payload for a Tenant-side user:

```json
{
  "sub": "user_id",
  "tenant_id": "uuid",
  "role": "TENANT_ADMIN",
  "email": "user@example.com",
  "session_id": "uuid",
  "exp": 1758186000,
  "iss": "Trellis",
  "aud": "trellis-platform"
}
```

Payload for a Vendor user — note there is **no `tenant_id` claim**, because a Vendor's access
spans tenants and is resolved per-request from `vendor_assignment`, not fixed at login:

```json
{
  "sub": "vendor_user_id",
  "vendor_id": "uuid",
  "role": "VENDOR_USER",
  "email": "vendor@example.com",
  "session_id": "uuid",
  "exp": 1758186000,
  "iss": "Trellis",
  "aud": "trellis-platform"
}
```

Like the reference, the JWT carries identity only. There is no `permissions[]` array in the
token; the login response body returns the resolved capability list for the frontend to read.
See `security_guide.md` for the full authentication flow.

### Service-to-Service Authentication

**Not applicable in Phase 1.** The reference architecture needs this because its 6 services
communicate over HTTP; Trellis's modules call each other in-process (see
`project_architecture.md`), so there is no network boundary to authenticate across. If a
module is ever extracted into its own deployable service, that extraction is exactly the point
at which a service-JWT scheme (modelled on the reference's `ServiceJwtFactory`) gets
introduced for that one boundary — not before.

### Password Reset & Session Endpoints (BDR-SEC-005/006, real as of BRD v0.4)

```
POST /api/v1/auth/password/forgot        { username }              → always 200, generic body
POST /api/v1/auth/password/reset         { token, new_password }   → 200, or 400 (generic)
POST /api/v1/auth/logout                 (bearer token)             → 204
POST /api/v1/auth/logout/all-other-sessions  (bearer token)         → 204
```

All four sit under the standard `/api/v1/` versioning and envelope like every other endpoint —
the only special case is `password/forgot` and `password/reset`, which return the identical
response shape on both a real and a non-existent/invalid case, per `security_guide.md`'s
existence-non-disclosure rule. `login`, `password/forgot`, and `mfa/verify` also carry the
`auth-sensitive` rate-limiting policy (BDR-SEC-009, real as of BRD v0.4) — see the 429 row above and
`security_guide.md` for the policy itself.

### MFA Recovery Code Endpoints (BDR-SEC-008, real as of BRD v0.4)

```
POST /api/v1/auth/mfa/verify                       { partial_token, recovery_code }  → same
                                                     response shape as the existing
                                                     { partial_token, totp_code } branch
POST /api/v1/auth/mfa/recovery-codes/regenerate     { current_password }             → 200,
                                                     body: { codes: string[10] }, one-time reveal
```

`mfa/verify` is one endpoint with two mutually exclusive request shapes, not two endpoints —
a recovery code is simply an alternate second factor, not a different flow. `regenerate`
requires re-authentication via `current_password` (see `security_guide.md`) and is itself
subject to standard auth-scoped rate limiting like any other authenticated write.

### API Key (future — external integrations)

Deferred. No Trellis client needs machine-to-machine API key auth in Phase 1; Vendors and
Tenant staff both authenticate as JWT-bearing human users. Revisit if/when an Owner-facing
integration or a third-party accounting system integration is scoped.

---

## RBAC Roles

| Role | Access Level |
|------|-------------|
| SUPER_ADMIN | All tenants; platform configuration; suspend/reactivate tenants |
| TENANT_ADMIN | Full access within own tenant |
| DEPARTMENT_STAFF | Access scoped to their assigned department(s) within their tenant (BDR-DEPT-003) |
| VENDOR_USER | No tenant scope at all — access resolved per-request against `vendor_assignment` (BDR-VEN-001–005) |

There is deliberately no `OWNER` row here — Owners don't authenticate against the API in
Phase 1 (BDR-OWN-002); they receive generated reports out-of-band. This is a smaller role set
than the reference's 7 roles because Trellis has no template-approval workflow or
compliance-export role yet — both are candidates to add once the corresponding BDRs mature
past Phase 1.

Role is expressed as an ASP.NET Core auth attribute, same pattern as the reference:

```csharp
[HttpPost("{id}/approve")]
[TenantAdminOrDepartmentLead]   // Custom auth attribute
public async Task<IActionResult> ApproveWorkflowStep(Guid id) => ...
```

A **Vendor-specific attribute** has no equivalent in the reference architecture, because OMS
has no cross-tenant identity concept. It checks the assignment table, not a static role claim:

```csharp
[HttpPost("{workItemId}/close")]
[VendorAssignedToWorkItem]     // Resolves via vendor_assignment, not a role claim alone
public async Task<IActionResult> CloseAssignment(Guid workItemId) => ...
```

See `multi_tenant_design.md` for how `VendorAssignedToWorkItem` resolves its scope.

---

## Pagination Pattern

```json
GET /api/v1/properties?cursor=eyJwcm9wZXJ0eV9pZCI6Ii4uLiJ9&limit=25

{
  "status": "success",
  "data": {
    "items": [ ... ],
    "next_cursor": "eyJwcm9wZXJ0eV9pZCI6Ii4uLiJ9",
    "has_more": true,
    "total_count": 143
  }
}
```

Unlike the reference, Trellis's tenant-scoped tables are never partitioned in Phase 1 (no
tenant will have millions of rows for years), so `total_count` is safe to compute and return —
we don't inherit OMS's "null on partitioned tables" caveat. Revisit if a table's row count ever
makes `COUNT(*)` a real cost.

---

## Idempotency

```
POST /api/v1/workflow-instances
X-Idempotency-Key: client-ref-20260918-001
```

```csharp
// Core/Helpers/IdempotencyExecutor.cs
public async Task<ServiceResult<T>> ExecuteIdempotentAsync(
    Guid tenantId, string idempotencyKey, Func<Task<ServiceResult<T>>> operation)
{
    var cached = await _idempotencyStore.GetAsync<ServiceResult<T>>(tenantId, idempotencyKey);
    if (cached != null) return cached;

    var result = await operation();
    await _idempotencyStore.SetAsync(tenantId, idempotencyKey, result, TimeSpan.FromHours(24));
    return result;
}
```

`_idempotencyStore` is backed by a plain Postgres table (`trellis_core.idempotency_key`:
`tenant_id`, `idempotency_key`, `response_json`, `expires_at`) with a nightly cleanup job for
expired rows — not Redis. This trades a small amount of write latency for one less piece of
infrastructure to run and back up during Phase 1; swapping in Redis later, if request volume
ever demands it, only touches this one class.

---

## Webhook Callbacks

**Deferred.** OMS's outbound webhook signing (HMAC-SHA256, retry-with-backoff) exists to
notify tenant systems of delivery events. Trellis has no analogous outbound event stream yet —
the closest candidate is notifying a Tenant's own systems of a completed inspection or an
expiring document, which is realistic future scope but not a Phase 1 BDR. When it is scoped,
this section should be filled in directly from the reference's pattern, which transfers
without modification (signing and retry logic don't care about the module boundary work
underneath).

---

## Related Documentation

- [Security Guide](./security_guide.md) — auth flows, JWT structure, RBAC enforcement
- [Data Model](./data_model.md) — error registry storage, idempotency table schema
- [Multi-Tenant Design](./multi_tenant_design.md) — how `VendorAssignedToWorkItem` resolves scope
- [Frontend Architecture](./frontend_architecture.md) — the typed client generated from this API's OpenAPI spec
- [Testing Strategy](./testing_strategy.md) — the API/contract test layer against this envelope
- [Error Handling & Resilience](./error_handling.md) — how an exception or `ServiceResult` failure becomes this envelope
- [Billing Design](./billing_design.md) — the `TRL-E-BIL` error range, module entitlement `403`s, and inbound Paystack webhook handling
- [Leasing Design](./leasing_design.md) / [Rent Collection Design](./rent_collection_design.md) — the `TRL-E-LSE` and `TRL-E-RENT` error ranges
- [Notifications Design](./notifications_design.md) — the `TRL-E-NOTIF` error range
- [Support Design](./support_design.md) — the reserved `TRL-E-SUP` range, and [Coding Standards](./coding_standards.md) for the `ConcurrencyConflict` outcome referenced in the status code table above
- [Security Guide](./security_guide.md) — the password-reset and session-revocation flows behind the endpoints above
