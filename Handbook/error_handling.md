# Error Handling & Resilience

`api_standards.md` defines what an error *looks like* on the wire (the envelope, HTTP codes,
the `TRL-E-{module}-####` registry). This document defines how an error *gets there* — from a
failed database call or a business rule violation, through the Service and Controller layers,
to that envelope — and how Trellis behaves when something outside its own process fails (the
AI provider, an email/SMS gateway). None of this exists in the OMS reference in one place
either; it's assembled here from the patterns implied across its `security_guide.md` (never
leak internals) and `api_standards.md` (the envelope), plus resilience concerns that are new to
Trellis because it depends on external AI services the reference does not.

---

## Core Principle: Exceptions Don't Cross the Service Boundary

A Service method never lets an exception propagate up to its Controller as the normal way of
signalling failure. It catches what it expects to go wrong and returns a `ServiceResult<T>`
outcome instead — exceptions are reserved for truly unexpected faults, not business outcomes
like "not found" or "vendor not assigned." This is the same discipline
`project_architecture.md` already establishes for `ServiceResult<T>`; this document is where
its failure modes get enumerated.

```csharp
public async Task<ServiceResult<WorkflowInstance>> CloseAssignmentAsync(Guid vendorId, Guid workItemId)
{
    var assignment = await _repository.GetAssignmentAsync(vendorId, workItemId);
    if (assignment is null)
        return ServiceResult<WorkflowInstance>.NotFound();               // → 404

    if (assignment.Status == AssignmentStatus.Closed)
        return ServiceResult<WorkflowInstance>.Conflict(
            ErrorCodeRegistry.Vendor.AssignmentAlreadyClosed);           // → 409, TRL-E-VEN-7003

    // ... business logic ...
    return ServiceResult<WorkflowInstance>.Success(updated);
}
```

### ServiceResult\<T\> Outcome → HTTP Mapping

| `ServiceResult<T>` outcome | HTTP status | When a Service returns it |
|------------------------------|-------------|-------------------------------|
| `Success(data)` | 200 / 201 | Normal completion |
| `NotFound()` | 404 | Resource doesn't exist **or** exists but is outside the caller's tenant/vendor scope (see `multi_tenant_design.md` — always 404, never 403, to avoid confirming existence) |
| `ValidationFailed(details[])` | 400 | Request-shape problems caught before business logic runs (see Validation below) |
| `BusinessRuleViolation(code)` | 422 | Schema-valid request that violates a domain rule (approving a workflow step out of sequence, closing an assignment twice) |
| `Conflict(code)` | 409 | Duplicate creation, or a state conflict (assignment already closed) |
| `ConcurrencyConflict()` | 409 | A stale `RowVersion` was presented on an update — someone else changed this record first (BDR-NFR-006, real as of BRD v0.4; `coding_standards.md`). Same HTTP status as `Conflict`, but a distinct error code, so the client can tell "already exists" apart from "changed under you" and respond differently (a refetch-and-retry prompt, not a duplicate-name warning) |
| `Forbidden(code)` | 403 | Reserved for the rare case where confirming existence is *not* a leak (e.g. a valid Tenant Admin token attempting an action their role doesn't permit within their own tenant, or hitting a module their tenant hasn't enabled — `RequiresModuleEntitlement`, `billing_design.md`, BDR-BIL-009) |
| `Failure(code, message)` | 500 | An unexpected internal fault the Service explicitly caught and translated (e.g. a downstream AI provider failure — see Resilience below) |

`ToActionResult()` (in `Core/Results/`) is the single place this table is implemented in code
— controllers never re-derive an HTTP status from a `ServiceResult`, they just call it.

---

## Global Exception Handling Middleware

Reserved for the case a Service *didn't* catch — a genuine bug, an out-of-memory condition, an
unhandled `NullReferenceException`. This is the last line of defence, not the primary
mechanism:

```csharp
// Core/Middleware/ExceptionHandlingMiddleware.cs
public class ExceptionHandlingMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.TraceIdentifier;

            _logger.LogError(ex, "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);

            var response = ApiResponse<object>.Failure(new ApiError
            {
                Code = ErrorCodeRegistry.Platform.UnhandledFault,   // TRL-E-PLT-0001
                Message = "An unexpected error occurred. Reference this ID if reporting the issue.",
                RequestId = correlationId
            });

            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
```

Two rules this middleware exists to enforce, matching the reference architecture's "secure by
default" principle even though this document has no direct reference equivalent:

- **The client never sees an exception's message, stack trace, or type name** — only the
  generic message above plus the correlation ID. The full exception detail goes to the
  structured log (`devops_environments.md`'s Serilog setup), which is where debugging actually
  happens.
- **Every 500 is logged with its correlation ID and is findable** — a user reporting "I got an
  error" with that ID is a direct log lookup, not a guessing exercise.

---

## Validation Errors

Two validation stages, kept distinct:

1. **Request-shape validation** (missing required field, wrong type, string too long) — via
   FluentValidation validators (`{DtoName}Validator`, per the naming convention in
   `project_architecture.md`), run automatically before the controller action executes. Failure
   short-circuits straight to a 400 with a populated `details[]` array — the Service method
   never even runs.

   ```json
   {
     "status": "error",
     "error": {
       "code": "TRL-E-PLT-0002",
       "message": "One or more fields failed validation",
       "details": [
         { "field": "expiresAt", "issue": "must be a future date" }
       ]
     }
   }
   ```

2. **Business rule validation** (a workflow step approved out of sequence, a Vendor assigned to
   an already-closed work item) — this is not a shape problem, so it's a 422
   `BusinessRuleViolation` returned explicitly by the Service, per the table above, not caught
   generically by a validator.

---

## Resilience for External Dependencies

This is the part with no OMS equivalent: Trellis's AI module and notification delivery
(email/SMS for Owner reports, document expiry alerts) call external services that can be slow,
rate-limited, or briefly unavailable — in-process module calls never fail this way, so this
section applies narrowly to the few real network boundaries Trellis has.

| Dependency | Failure mode | Trellis behaviour |
|------------|----------------|----------------------|
| AI extraction/routing provider | Timeout, rate limit, outage | Retry with exponential backoff (Polly, 3 attempts: 2s/8s/20s); on final failure, the extraction job is marked `FAILED` (not silently dropped) and the document is routed to manual review — the AI module is explicitly designed as an assist, not a hard dependency, so its failure never blocks a workflow (BDR-AI-005) |
| Notification delivery (email/SMS, via `INotificationProvider`) | Timeout, rate limit | Retry with backoff (Polly, same policy); after final failure, the delivery is marked `FAILED` in `notification_delivery` (`notifications_design.md`) and queued for a scheduled retry pass rather than lost, and logged as a delivery failure (not surfaced as an error to the end user who triggered it, since it's typically async) |
| File malware scanner (`IFileScanner`) | Timeout, outage | Retry with backoff (Polly, same policy); on final failure the file stays `SCAN_FAILED`, fail-closed — it remains invisible to everyone but its uploader rather than being treated as clean (BDR-SEC-007, `security_guide.md`) |
| Managed Postgres | Transient connection drop | EF Core's own connection resiliency (`EnableRetryOnFailure`), not custom retry code — this is a solved problem in the Npgsql provider |

```csharp
// Core/Resilience/ResiliencePolicies.cs — named ExternalProviderPolicy, not the AiProviderPolicy
// an earlier version of this doc used: Notification (Phase 1) reuses this same policy well
// before the Ai module (a much later phase) exists, so an AI-specific name would have been
// wrong the moment a second caller showed up. The table above already called this "the same
// policy" shared across every external-dependency call — the name now matches that.
public static readonly IAsyncPolicy ExternalProviderPolicy = Policy
    .Handle<HttpRequestException>()
    .Or<TaskCanceledException>()
    .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(4, attempt) / 2));
```

No circuit breaker in Phase 1 — with a single AI provider integration and moderate request
volume, a circuit breaker's main benefit (protecting a struggling downstream from a retry
storm) isn't yet a real risk. Add one if a provider outage ever causes a visible retry pile-up.

---

## Frontend Error Handling

The SPA's handling mirrors the `ApiResponse<T>` envelope directly, per `frontend_architecture.md`:

- **Field-level (400)**: `details[]` maps directly onto `react-hook-form` field errors, shown
  inline next to the relevant input.
- **Business rule (422) / Conflict (409)**: shown as a toast or inline banner using the
  envelope's `message`, since these are meaningful to the end user (e.g. "This vendor is
  already assigned to this job").
- **Concurrency conflict (409, distinct code)**: never treated like a plain Conflict toast — the
  form shows "This record was changed by someone else" and offers to refetch the current
  version rather than silently discarding the user's edits, since the whole point of surfacing
  this instead of overwriting is to let the user decide how to reconcile the two versions.
- **Not Found (404)**: routes to a generic "not found or you don't have access" view — the
  frontend never distinguishes "doesn't exist" from "exists but out of scope," matching the
  backend's own refusal to leak that distinction.
- **Module not entitled (403, distinct code)**: the one `Forbidden` case the frontend treats as
  actionable rather than a dead end — an "enable this module" upsell instead of a generic
  permission-denied message, per `frontend_architecture.md`'s Module Entitlement Gating section.
- **Server fault (500)**: a generic toast ("Something went wrong — reference {requestId}") with
  the correlation ID visible, so a user reporting the issue can hand it over verbatim.
- **Network/timeout**: TanStack Query's built-in retry (capped at 2 attempts for `GET`
  requests only — mutations are never auto-retried, to avoid double-submission alongside the
  `X-Idempotency-Key` mechanism in `api_standards.md`).

---

## Testing Error Paths

Per `testing_strategy.md`'s BDR-traceability convention, every documented `ServiceResult`
failure outcome for a BDR gets its own test — a Service that can return `Conflict` needs a test
proving it does, not just a test proving the happy path. The multi-tenant isolation suite
already covers the 404-not-403 rule explicitly; this document's job is making sure the *rest*
of the failure surface (validation, business rules, external dependency failures) gets the same
treatment rather than being an afterthought discovered in Staging.

---

## Related Documentation

- [API Standards](./api_standards.md) — the error envelope and code registry this document populates
- [Project Architecture](./project_architecture.md) — `ServiceResult<T>`, thin controllers
- [Multi-Tenant Design](./multi_tenant_design.md) — the 404-not-403 rule for tenant/vendor scope
- [Testing Strategy](./testing_strategy.md) — coverage requirements for failure paths
- [DevOps & Environments](./devops_environments.md) — where unhandled-exception logs actually go
- [Frontend Architecture](./frontend_architecture.md) — how the SPA renders each error shape
- [Deployment Topologies](./deployment_topologies.md) — license-cap `BusinessRuleViolation` handling on-premises
- [Leasing Design](./leasing_design.md) — the `IScreeningProvider`/`IESignatureProvider` webhook and idempotency pattern that mirrors this document's resilience table
- [Rent Collection Design](./rent_collection_design.md) — the scheduled-job pattern arrears flagging follows, consistent with this document's approach to background checks
- [Notifications Design](./notifications_design.md) / [Security Guide](./security_guide.md) — the `INotificationProvider` and `IFileScanner` resilience rows above, in full
- [Coding Standards](./coding_standards.md) — the `xmin`/`RowVersion` mechanism behind the `ConcurrencyConflict` outcome above
- [Coding Standards](./coding_standards.md) — Domain exceptions vs. `ServiceResult<T>` failures, async exception propagation
