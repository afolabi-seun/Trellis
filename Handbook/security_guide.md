# Security Guide

**A note on scope added across four handbook gap review rounds**: three real gaps surfaced
first — self-service password reset, explicit session revocation, and malware/type scanning on
uploaded files — that a security document this thorough shouldn't have left unaddressed. A
third review round added three more, smaller in scope but the same kind of gap: MFA recovery
codes (so losing a phone doesn't mean losing an account), rate-limiting on authentication
endpoints, and explicit cookie/CSRF hardening on the one credential Trellis does carry in a
cookie. A fourth round closed what was left in the same hardening/process vein: standard
security response headers, a blanket log-redaction rule (replacing a couple of one-off "never
logged" mentions with an actual policy), and — the one substantive gap left, since the
Compliance section named GDPR/POPIA/NDPR but never said what Trellis itself does if it's the
one breached — an incident response and breach notification commitment. All of it is now real:
BDR-SEC-005 through 010 and BDR-NFR-009 through 011 below are folded into
`Requirements/Trellis_BRD_v0.4.docx`, the same path Billing, Leasing, and Rent Collection took
before them.

**BDR-SEC additions (real as of BRD v0.4)**:

| ID | Requirement | Priority |
|----|-------------|----------|
| BDR-SEC-005 | The system shall allow a user to reset a forgotten password via a single-use, time-limited link sent to their registered email address, without revealing whether that email address has an account. | Must |
| BDR-SEC-006 | The system shall allow a user to explicitly end their own session (logout) and to revoke all of their other active sessions, and shall automatically revoke all sessions on a password reset. | Must |
| BDR-SEC-007 | The system shall validate the type and scan for malware every file uploaded to the platform, regardless of uploader identity, before that file is visible to any user other than its uploader. | Must |
| BDR-SEC-008 | The system shall allow a user with MFA enabled to generate a set of single-use recovery codes at enrollment, usable as an alternate second factor if their authenticator device is unavailable, and to regenerate that set on demand, invalidating all previously issued codes. | Must |
| BDR-SEC-009 | The system shall rate-limit authentication-related endpoints (login, password-reset request, MFA verification) per source IP, to slow credential-stuffing and brute-force attempts without requiring a separate infrastructure component. | Must |
| BDR-SEC-010 | The system operator shall follow a stated incident-detection, containment, and notification procedure — including notifying the relevant supervisory authority and affected data subjects within the timeframe the applicable regulation requires — in the event of a confirmed security incident involving tenant or platform data. | Must |
| BDR-NFR-009 | The system shall apply cookie- and header-level hardening (SameSite, Secure, narrow Path, and equivalent CSRF mitigations) to any cookie-based credential, even where the primary access token is not itself cookie-based. | Should |
| BDR-NFR-010 | The system shall apply standard security response headers (CSP, HSTS, X-Frame-Options, X-Content-Type-Options, Referrer-Policy) to every response served to a browser. | Should |
| BDR-NFR-011 | The system shall never write a raw secret, credential, or authentication-token value to application logs, enforced by a shared mechanism rather than left to individual call sites. | Must |

---

## Security Principles

| Principle | Implementation |
|-----------|---------------|
| **Zero Trust (adapted)** | No implicit trust between modules despite running in one process: every cross-module call goes through the target module's public Service interface, never its repository or `DbContext` directly (see `project_architecture.md`). There's no network hop to secure with mTLS, because there's no network hop. |
| **Least Privilege** | Every JWT, role, and Vendor assignment grants the minimum scope required. A Vendor's token grants no access beyond the specific `vendor_assignment` rows that name it. |
| **Defence in Depth** | Tenant isolation enforced at 3 independent layers (below) — narrower than the reference's 8, because Trellis has no service mesh, gateway, or per-tenant message queues to add layers at. The 3 that remain are the ones that actually stop a cross-tenant data leak. |
| **Secure by Default** | TLS 1.3 in transit. Argon2id password hashing. Secrets never in code or in a committed `.env` file. Audit log always on. |
| **Immutable Audit** | Append-only `trellis_audit.audit_record`, DB-trigger-enforced against UPDATE/DELETE — identical in spirit and implementation to the reference. |
| **Cryptographic Isolation (deferred)** | Per-tenant Vault Transit keys are the reference's answer to "make cross-tenant decryption infeasible even if RLS is bypassed." Trellis defers per-tenant key material to Phase 2 (see Secrets Management below) and relies on RLS plus the encryption-at-rest provided by the managed Postgres host for Phase 1. This is a real, named trade-off, not an oversight — revisit before onboarding any tenant with regulated PII beyond what's already scoped. |

---

## Defence-in-Depth Layers (Trellis's 3, mapped against the reference's 8)

| # | Layer | Reference has it via | Trellis implementation |
|---|-------|----------------------|-------------------------|
| 1 | Database RLS | Per-service DB role + RLS | Same — RLS policies on every tenant-owned table (`data_model.md`) |
| 2 | Application-layer query filter | Service-layer tenant filter in each microservice | `TenantAwareRepository<T>` / `VendorScopedRepository<T>` inject the tenant or vendor predicate on every query, independent of RLS |
| 3 | JWT claim validation at the boundary | Per-service JWT validation (no round trip to auth-svc) | ASP.NET Core `TenantScopeMiddleware` resolves `tenant_id`/`vendor_id` from the validated JWT once per request and sets it as `AsyncLocal` context read by both layers above |
| — | Network zone segmentation | Kong + Istio mesh zones | Not applicable — one process, no internal network to segment |
| — | Per-service DB credentials | Vault dynamic secrets per service | Not applicable — one process, one DB connection pool |
| — | Per-tenant encryption keys | Vault Transit per tenant | Deferred (see above) |
| — | mTLS between services | Istio CA | Not applicable — no service-to-service calls |
| — | WAF / gateway rate limiting | Kong | Deferred until Trellis has traffic worth protecting against abuse |

The three layers we keep are exactly the ones that don't depend on having multiple deployed
services — which is why a modular monolith doesn't mean weaker tenant isolation, just fewer
places isolation needs to be independently re-implemented.

---

## Authentication Architecture

### User Authentication Flow (Auth module)

```
1. POST /api/v1/auth/login  { username, password }
2. Validate Argon2id hash. Increment failed_login_count on failure.
3. If failed_login_count ≥ 5 within 15 min:
      → Lock account (locked_until = NOW + 30 min)
      → Write AUTH_LOCKOUT audit record
4. If MFA enabled (required for TENANT_ADMIN and SUPER_ADMIN, BDR-SEC-003):
      → Return { mfa_required: true, partial_token }  (5-min expiry)
5. POST /api/v1/auth/mfa/verify  { totp_code, partial_token }
      → Validate TOTP with 30-sec window ±1 step tolerance
6. Issue access token (JWT RS256, 15-min expiry)
      + refresh token (8-hour expiry)
7. Store session: trellis_auth.session (session_id, user_id, issued_at, expires_at)
8. Write AUTH_SUCCESS audit record (IP, user_agent, session_id)
```

Functionally identical to the reference's flow. The one storage difference: sessions live in
`trellis_auth.session` (Postgres), not Redis — Trellis's login volume in Phase 1 doesn't need
a separate cache tier, and one fewer moving part is one fewer thing to operate. Revisit if
session-lookup latency ever becomes a measured problem.

### Vendor Authentication

Same flow and same JWT mechanics (RS256, 15-minute access token) — a Vendor user is a first-
class `app_user` row (see `data_model.md`). The only branch is at token issuance: a Vendor's
JWT carries `vendor_id` instead of `tenant_id`, because a Vendor's access isn't fixed to one
tenant at login time. See "JWT Structure" below and `multi_tenant_design.md` for how that
claim is used downstream.

### JWT Structure

Tenant-side user:

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

Vendor user:

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

As in the reference, the JWT carries identity only — no `permissions[]` array. UI capability
codes are returned in the login response body. JWT is RS256-signed; the public key is served
at `/api/v1/auth/.well-known/jwks.json`. Every module validates the signature and the relevant
claim (`tenant_id` or `vendor_id`) independently via the shared `TenantScopeMiddleware` — there
is no per-request call back to the Auth module to re-validate, same principle as the
reference's "services don't call auth-svc on every request."

### Service-to-Service Authentication

**Not applicable.** See `api_standards.md` — modules call each other in-process. If a module
is later extracted (see `project_architecture.md`'s extraction triggers), that extraction is
the point at which a service-JWT scheme gets introduced for that one new boundary, modelled
directly on the reference's `ServiceJwtFactory`/`ServiceOnly` pattern, which was written
generally enough to carry over unchanged.

---

## Password Reset (BDR-SEC-005)

```
1. POST /api/v1/auth/password/forgot  { username }
2. Look up the account. Whether or not it exists, respond 200 with an identical body and
   after an identical delay — existence must never be inferable from timing or response shape
   (the same 404-not-403 discipline api_standards.md applies to tenant/vendor scope, applied
   here to account existence).
3. If the account exists: generate a random 32-byte token, store only its SHA-256 hash
   (trellis_auth.password_reset_token), and send the raw token as a link via the Notification
   module (notifications_design.md) — never logged, never stored in reversible form.
4. POST /api/v1/auth/password/reset  { token, new_password }
5. Hash the presented token, look up a matching, unexpired, unused row.
      → No match: generic 400, same message whether the token was wrong, expired, or reused.
6. Set the new Argon2id password_hash. Mark the token used_at = NOW().
7. Revoke every active session for this user (see Session Revocation below) — a password reset
   is exactly the moment every existing session should stop being trusted, including the
   attacker's, if the reset was triggered because an account was compromised.
8. Write a PASSWORD_RESET audit record.
```

```
// trellis_auth.password_reset_token
token_id       UUID PK
user_id        UUID NOT NULL FK -> app_user
token_hash     VARCHAR(64) NOT NULL   -- SHA-256 hex of the raw token; raw token never stored
expires_at     TIMESTAMPTZ NOT NULL   -- issued_at + 30 minutes
used_at        TIMESTAMPTZ NULLABLE   -- set once, then the row is dead (single-use)
requested_ip   INET NULLABLE          -- for the security audit trail only, not used in validation
```

30 minutes is deliberately short — a reset link is bearer-authenticated (whoever has the link
can use it), so its blast radius is time, not complexity. A used or expired token is never
distinguishable from an invalid one in the response, for the same reason existence isn't
revealed in step 2.

## Session Revocation & Logout (BDR-SEC-006)

Login and refresh were already fully specified; ending a session deliberately was not.

```csharp
[HttpPost("logout")]
public async Task<IActionResult> Logout()
{
    // Revoke only the calling session — the one identified by session_id in the current JWT
    await _sessionService.RevokeAsync(CurrentSessionId);
    Response.Cookies.Delete("refresh_token");
    return NoContent();
}

[HttpPost("logout/all-other-sessions")]
public async Task<IActionResult> LogoutAllOtherSessions()
{
    // A user-initiated "sign out everywhere else" action, e.g. after seeing an unfamiliar
    // session in an account activity view
    await _sessionService.RevokeAllExceptAsync(CurrentUserId, CurrentSessionId);
    return NoContent();
}
```

`trellis_auth.session` gets one new column: `revoked_at TIMESTAMPTZ NULLABLE`. The refresh
endpoint (`security_guide.md`'s existing flow, step 6 in Authentication Architecture) checks
`revoked_at IS NULL` before issuing a new access token — a revoked session's refresh token is
inert immediately, not just at its natural 8-hour expiry. This is the same mechanism the
password-reset flow above calls into for its own automatic revocation; there is only one way
to end a session, used both explicitly (logout) and implicitly (reset, and — a natural future
extension — a Tenant Admin force-revoking a suspicious staff session).

New `audit_type` values: `PASSWORD_RESET`, `SESSION_REVOKED`.

---

## File Upload Security (BDR-SEC-007)

Every file the platform accepts — a Document upload, an Inspection photo, Vendor-submitted
evidence (which, per `operational_flows.md`, is itself filed as a Document) — goes through the
same validation pipeline before it exists to anyone but its uploader. This matters most for
Vendor: it's the one identity in the whole system that's external and cross-tenant by design
(`multi_tenant_design.md`), which makes it the actor most worth assuming could submit something
malicious, whether maliciously or via a compromised device.

```csharp
// Core/Files/IFileScanner.cs — same provider-agnostic seam as IPaymentGateway, IScreeningProvider
public interface IFileScanner
{
    Task<ScanResult> ScanAsync(Stream content, string fileName, string declaredContentType);
}
```

Two checks happen, in order, before any file is persisted as accessible:

1. **Type validation** — the file's actual bytes (magic-number sniffing), not the
   client-supplied `Content-Type` header, are checked against an allow-list scoped to where
   it's being uploaded: documents accept PDF/DOCX/XLSX/JPG/PNG; inspection photos and Vendor
   evidence photos accept JPG/PNG/HEIC only. A mismatch (an `.exe` renamed to `.pdf`) is
   rejected at this step, before a scan is even attempted.
2. **Malware scan** — the file is scanned via `IFileScanner`, backed in Phase 1 by a managed
   cloud scanning API (the specific provider is a deployment-time choice, not fixed here,
   following the same "interface first, provider later" reasoning as every other external
   dependency in this handbook).

Until both checks clear, the file is stored with `scan_status = 'PENDING'` and is visible only
to its own uploader — not linked into any workflow, report, or another user's view. On a clean
result it flips to `CLEAN` and becomes visible per the normal RBAC/Vendor-scoping rules; on a
positive match it becomes `INFECTED` and is quarantined (kept, not deleted, for the audit
trail) with the uploader notified via `notifications_design.md`, never silently dropped.

`scan_status VARCHAR(20) NOT NULL DEFAULT 'PENDING'` (`PENDING|CLEAN|INFECTED|SCAN_FAILED`) is
added to `trellis_document.document_version` and `trellis_inspection.inspection_photo` — the
two tables that actually hold file bytes/references.

**Failure mode, per `error_handling.md`'s resilience pattern**: if the scanning provider itself
times out or is unavailable, the file stays `SCAN_FAILED` → retried on the same backoff policy
as other external dependencies, and remains invisible to everyone but the uploader in the
meantime. This is a deliberate fail-closed choice — the risk being mitigated is exactly
"a file becomes visible before anyone's checked it," so an unavailable scanner should never be
treated the same as a clean scan.

---

## MFA Recovery Codes (BDR-SEC-008)

MFA (Authentication Architecture, above) is required for `TENANT_ADMIN` and `SUPER_ADMIN`.
Until now, the handbook specified enrolling and verifying a TOTP device but never specified
what happens when that device is lost, wiped, or simply not at hand — the same class of gap as
password reset, just for the second factor instead of the first.

```
1. At MFA enrollment (TOTP secret confirmed), generate 10 recovery codes: cryptographically
   random, 10 bytes each, formatted for readability (e.g. XXXX-XXXX-XXXX in base32).
2. Display all 10 to the user exactly once, with an explicit "store these somewhere safe, we
   cannot show them again" notice — same one-time-reveal discipline as any credential-adjacent
   secret in this handbook (e.g. the Vendor API key pattern, where one exists).
3. Store only each code's SHA-256 hash (trellis_auth.mfa_recovery_code) — never the raw code,
   same convention as password_reset_token above.
4. POST /api/v1/auth/mfa/verify  { partial_token, recovery_code }
      — an alternate branch of the existing MFA verification step, alongside { totp_code }.
   Hash the presented code, look up an unused, matching row for the user identified by the
   partial_token. On match: mark used_at = NOW() (single-use, like a reset token), issue the
   session as normal, and write an MFA_RECOVERY_CODE_USED audit record — this is the one event
   in this flow worth surfacing to the user themselves, via a notification, since a recovery
   code being used is exactly the kind of thing its owner wants to know happened even when it
   was them.
5. POST /api/v1/auth/mfa/recovery-codes/regenerate  { current_password }
      — requires re-authentication (password confirmation), since minting a fresh set of
      account-recovery credentials is as sensitive as changing the password itself. Invalidates
      every previously issued code for that user (whether used or not) and returns a new set of
      10, following the same one-time-reveal rule as enrollment.
```

```
// trellis_auth.mfa_recovery_code
recovery_code_id  UUID PK
user_id           UUID NOT NULL FK -> app_user
code_hash         VARCHAR(64) NOT NULL   -- SHA-256 hex of the raw code; raw code never stored
created_at        TIMESTAMPTZ NOT NULL
used_at           TIMESTAMPTZ NULLABLE   -- set once, then the row is dead (single-use)
```

**The remaining case — both TOTP device and all recovery codes are lost or unavailable — is
deliberately not automated.** Self-service recovery from a fully locked-out MFA state is a
common account-takeover vector if built casually (it's the same surface as "forgot password,"
but for the factor that's supposed to survive a compromised password). Trellis handles it the
way the reference and most mature platforms do: the user opens a support ticket
(`support_design.md`), a Platform Operator manually verifies identity out-of-band, and a
`SUPER_ADMIN`-only administrative action clears the user's MFA enrollment, forcing them back
through setup at next login. This is intentionally a human-in-the-loop path, not a missing
feature — automating it is precisely what would reintroduce the risk MFA exists to close.

New `audit_type` values: `MFA_RECOVERY_CODE_USED`, `MFA_RECOVERY_CODES_REGENERATED`.

---

## Auth Endpoint Throttling (BDR-SEC-009)

Password reset (BDR-SEC-005) and login both already had lockout/existence-non-disclosure
behaviour specified per-account; nothing so far throttled attempts *per source*, which is the
control that actually slows a credential-stuffing run or a distributed password-guessing
attempt across many accounts from a small set of IPs.

Trellis uses ASP.NET Core's built-in rate-limiting middleware
(`Microsoft.AspNetCore.RateLimiting`, in the framework since .NET 7) rather than introducing
Redis or a gateway for this alone — same reasoning as deferring Kong in `project_architecture.md`:
the control is worth having now, the infrastructure to run it centrally is not yet justified.

```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth-sensitive", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(5);
        opt.QueueLimit = 0;   // reject immediately, no queueing — this is abuse mitigation,
                              // not backpressure smoothing
    });
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "300";
        return ValueTask.CompletedTask;
    };
});
```

Applied, via `[EnableRateLimiting("auth-sensitive")]`, to the three endpoints where a rejected
request is meaningful abuse rather than an ordinary retry: `POST /auth/login`,
`POST /auth/password/forgot`, and `POST /auth/mfa/verify`. The limiter key is the caller's
source IP (`HttpContext.Connection.RemoteIpAddress`, resolved through the load balancer's
`X-Forwarded-For` per the deployment topology in `devops_environments.md`). A request beyond
the limit gets `429 Too Many Requests` with a `Retry-After` header — added to the error
registry in `api_standards.md` alongside the existing 4xx/5xx rows.

**Explicit trade-off, stated rather than left implicit**: this limiter's counters are
per-process, in-memory. With a single API instance (Trellis's Phase 1 deployment topology,
`deployment_topologies.md`), that's the whole picture. If the API tier is later scaled
horizontally, each instance enforces the limit independently — an attacker spread across
requests hitting multiple instances gets a higher effective ceiling than the configured one.
That's an acceptable gap for now, not an oversight: revisit with a shared counter store (Redis,
already listed as deferred in the Handbook README, or a Postgres-backed limiter) at the same
point horizontal API scaling itself becomes real, not before.

---

## CSRF & Cookie Hardening (BDR-NFR-009)

Trellis's access token is bearer-in-header (`Authorization: Bearer <jwt>`), not a cookie — the
SPA attaches it explicitly to every API call (`frontend_architecture.md`). That design choice
already closes most of classic CSRF: a cross-site page can make the browser *send* a request to
Trellis, but it cannot make the browser attach an `Authorization` header the attacker doesn't
know, and setting one from cross-site JavaScript is blocked by CORS. There is exactly one
cookie-based credential in the system — the refresh token (Authentication Architecture, above)
— so it's the one thing left to harden, and doing so is belt-and-braces given the access-token
design already carries most of the weight.

```
Set-Cookie: refresh_token=<opaque>; HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth/refresh
```

- **`HttpOnly`** — already implicit in "never expose the refresh token to JavaScript," made
  explicit here as a checklist-verifiable attribute.
- **`Secure`** — never sent over plain HTTP; consistent with TLS 1.3 everywhere
  (Encryption Architecture, above).
- **`SameSite=Strict`** — the strictest setting, not `Lax`: the refresh cookie is never needed
  on a cross-site top-level navigation (there's no scenario where a user clicks a link from
  another site and should land already-refreshed), so there's no functional reason to accept
  the weaker `Lax` default.
- **`Path=/api/v1/auth/refresh`** — the cookie is attached only to the one endpoint that reads
  it, not to every request to the API's origin, which narrows both accidental exposure (e.g. in
  request logs for unrelated endpoints) and the attack surface for any future cookie-reading bug.

Given `SameSite=Strict`, `Path` scoping, and the fact that the credential an attacker would
actually want (the access token) isn't a cookie in the first place, Trellis does not add a
separate CSRF token/double-submit-cookie scheme on top — that would be defending a door that's
already this narrow with a second, redundant lock. Revisit only if a future feature introduces
a new cookie-based credential with broader scope than the refresh token.

---

## Security Response Headers (BDR-NFR-010)

Every browser-facing response — API and SPA alike — carries the same day-one set of hardening
headers. None of these replace a control specified elsewhere in this document; each is a
second, header-level layer behind an existing one, which is exactly why they're cheap to add
and worth adding regardless:

```
Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self' <api-origin>
Strict-Transport-Security: max-age=31536000; includeSubDomains
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
```

| Header | What it's a second layer behind |
|--------|-----------------------------------|
| `Content-Security-Policy` | A strict `'self'` default is viable because Trellis has no third-party embed today (no analytics pixel, no ad script, no third-party widget) — revisit the policy the moment one is added, not after |
| `Strict-Transport-Security` | TLS 1.3 is already mandatory everywhere (Encryption Architecture, below) — HSTS just stops a repeat visitor's browser from ever honoring a downgrade attempt |
| `X-Content-Type-Options: nosniff` | A second, header-level check behind the magic-byte type validation in File Upload Security (BDR-SEC-007) — stops a browser from executing an uploaded file as something other than its declared type |
| `X-Frame-Options: DENY` | Trellis is never meant to be iframed by another site; this closes the whole clickjacking class outright rather than depending on a correctly-written `frame-ancestors` CSP directive |
| `Referrer-Policy` | Keeps a full URL (which can carry a resource id in its path or query string) out of the `Referer` header sent to any third-party resource a page happens to load |

Applied via `Core/Middleware/SecurityHeadersMiddleware.cs` for API responses, and via the
static-hosting layer's own response-header configuration for the SPA's served assets
(`frontend_architecture.md`) — both need it, since an attacker only needs the weaker of the two
surfaces.

---

## Log PII Redaction (BDR-NFR-011)

Two places in this handbook already say a specific value is "never logged" — the raw
password-reset token (above) and, more loosely, a notification's `recipient_address`
(`notifications_design.md`). Stated as one-off mentions, that reads as case-by-case discipline;
it should be a blanket, mechanically enforced rule instead, the same way tenant scoping is a
base-class property (`TenantAwareRepository<T>`) rather than something each query has to
remember.

```csharp
// Core/Logging/RedactingEnricher.cs — a Serilog ILogEventEnricher applied globally in Program.cs,
// not opt-in per call site
public class RedactingEnricher : ILogEventEnricher
{
    private static readonly HashSet<string> RedactedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password_hash", "token", "refresh_token", "totp_code", "recovery_code",
        "authorization", "card_number", "national_id"
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        foreach (var name in RedactedProperties)
            if (logEvent.Properties.ContainsKey(name))
                logEvent.AddOrUpdateProperty(factory.CreateProperty(name, "***REDACTED***"));
    }
}
```

Because this is a global enricher rather than a per-call-site convention, a developer who logs
a request DTO that happens to contain a `password` field is protected by default rather than
required to remember to exclude it — the same "fix the failure mode once at the base class"
reasoning as `TenantAwareRepository<T>`. The request/response logging middleware
(`devops_environments.md`'s Serilog setup) runs through the same enricher, not a separate,
easier-to-forget redaction path.

This is not a blanket ban on logging anything personally identifying — an email address in a
`recipient_address` field is legitimately needed for delivery troubleshooting
(`notifications_design.md` already makes this call) — the rule is specifically about
credential- and secret-shaped values, which have no legitimate debugging use once written to a
log a wider set of engineers can read.

---

## Secrets Management

No secret appears in source code, `appsettings.json` (removed — `devops_environments.md`'s
Configuration & Secrets section), or a committed `.env` file, matching the reference's rule
exactly. Where we diverge is the mechanism:

| Secret Category | Reference (Vault) | Trellis Phase 1 |
|-----------------|--------------------|--------------------|
| Database credentials | Dynamic Vault secrets, 1-hour leases | Two connection strings, not one — `ConnectionStrings__Trellis` (unprivileged `trellis_app` role, what the running API connects as) and `ConnectionStrings__TrellisMigrations` (the privileged migration-owner role, `dotnet ef database update` only) — both as environment variables, `.env` (gitignored) locally, the hosting platform's own env var config elsewhere. Two connection strings because Postgres RLS doesn't apply to a table's owner or a superuser regardless of policy (`data_model.md`'s Row-Level Security section) — a single shared connection would make RLS inert. |
| JWT signing key (RS256) | Vault, rotated every 6 months | Environment variable, rotated every 6 months (manual for now) |
| Third-party API keys (AI provider, email/SMS) | Vault, per-tenant paths | Environment variable, single platform-level key per provider |
| Third-party API keys (screening provider, e-signature provider) | N/A — no reference equivalent | Same as above: platform-level key(s) per provider via `ISecretProvider`, one per active `IScreeningProvider`/`IESignatureProvider` implementation (`leasing_design.md`); rotated on the same 6-month cadence as other third-party keys until a provider-specific requirement says otherwise |
| Third-party API keys (malware scanning provider, notification/email/SMS providers) | N/A — no reference equivalent | Same pattern again: a platform-level key per active `IFileScanner` implementation (above) and per `notifications_design.md`'s provider interfaces |
| Per-tenant PII encryption key | Vault Transit, per-tenant, rotated 90 days | **Deferred** — see Cryptographic Isolation above |

Full HashiCorp Vault is deferred, not rejected: it's real operational overhead (a cluster to
run, unseal, and back up) that buys the most when there are many services and many tenants
with distinct compliance needs. Trellis gets the same abstraction the reference used to talk
to Vault, so swapping in Vault later is a provider change, not a rewrite. Unlike an earlier
version of this document, which split the Phase 1 implementation between User Secrets (Local)
and a cloud secrets-manager SDK client (Development/Staging/Production) — two different reads
for the same interface — **every environment now reads a secret the same way: an environment
variable**, per `devops_environments.md`'s Configuration & Secrets section. The only thing that
differs per environment is who set that variable (`DotNetEnv` from a local `.env` file, versus
the hosting platform's own dashboard), which `ISecretProvider`'s single implementation never
needs to know:

```csharp
// Core/Secrets/ISecretProvider.cs — same interface shape as the reference
public interface ISecretProvider
{
    Task<string> GetSecretAsync(string key);
}

// Phase 1: one implementation for every environment — Local's DotNetEnv.Load() and the
// hosting platform's own env var injection both land here the same way
public class EnvironmentSecretProvider : ISecretProvider
{
    public Task<string> GetSecretAsync(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value))
            throw new InvalidOperationException($"Required secret '{key}' is not set.");

        return Task.FromResult(value);
    }
}

// Future: backed by Vault, if/when justified — same interface, callers unchanged
public class VaultSecretProvider : ISecretProvider
{
    public async Task<string> GetSecretAsync(string key)
    {
        var response = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(key);
        return response.Data.Data["value"].ToString();
    }
}
```

Secrets are fetched at application startup and cached in memory for the process lifetime.
Never log secret values.

---

## Encryption Architecture

| Scope | Algorithm | Key Management |
|-------|-----------|-----------------|
| External TLS | TLS 1.3 | Managed cert (Let's Encrypt or cloud-provider-issued); auto-renewal |
| Database at rest | AES-256 | Managed by the Postgres hosting provider |
| Password hashes | Argon2id | Per-password salt, OWASP Password Storage Cheat Sheet baseline parameters: 19 MiB memory, 2 iterations, degree of parallelism 1 — a checkable fact, not "standard conventions" left to whichever library default happens to apply |
| MFA TOTP secrets | AES-256-GCM | Single platform-level key from `ISecretProvider`, rotated annually — per-tenant TOTP key isolation deferred alongside per-tenant PII keys |
| PII columns (owner contact details, vendor contact details) | Deferred to platform-level DB-at-rest encryption for Phase 1 | Revisit once cryptographic isolation (above) is scoped |

No internal mTLS or per-service TLS section, unlike the reference — there is no internal
network segment to secure.

---

## Compliance

| Regulation | Key Requirement | Trellis Implementation |
|------------|------------------|--------------------------|
| **POPIA** (South Africa) | Lawful basis; data subject access and deletion rights | Data export and erasure endpoints on the Tenant/PropertyAsset modules; all processing logged with a `lawful_basis` note in `audit_record.detail_json` |
| **Kenya DPA 2019** | Consent; data minimisation; registration with the ODPC for data controllers/processors above threshold | Consent tracking on Owner/tenant onboarding; `country_code` on `tenant` drives which jurisdiction's retention defaults apply |
| **NDPR** (Nigeria) | Consent; data minimisation; restrictions on cross-border transfer | Tenant data residency flag (mirrors the reference's approach) once a Nigerian tenant is onboarded — not yet exercised |
| **GDPR** | Only if a tenant or owner is EU-domiciled | Same erasure/export mechanism as POPIA above; evaluate applicability per tenant at onboarding |
| **PCI DSS** | Card data handling | Lightest scope (SAQ-A) — Trellis never receives, transmits, or stores raw card data; card capture happens entirely inside Paystack's hosted UI and only a reusable authorization token is stored (`billing_design.md`, BDR-BIL-005) |

**Screening data (POPIA and equivalent regional regimes)**: `leasing_design.md`'s
`screening_result.full_report_ciphertext` is the single most sensitive column in the schema —
a credit/background report is squarely regulated personal information. It's encrypted at rest,
never included in any API response (only a coarse `outcome_summary` is), and read access to the
full report is a narrow, separately-audited action distinct from ordinary application review
(BDR-LSE-004, Must). Treat this column with at least the same care as the erasure/export
mechanism below — an erasure request against an `application` row should cascade to its
`screening_result`, not leave the most sensitive part behind.

Erasure request handling: PII fields are nulled (not the row deleted, to preserve referential
and audit integrity) across `trellis_property`, `trellis_document`, and `trellis_vendor`
within a defined SLA, with an `AUDIT_TYPE = DATA_ERASURE` record written for the action itself.
This mirrors the reference's erasure workflow; Trellis needs no dedicated ops-svc to run it
since it's one in-process operation spanning a few module services.

**Audit log retention** is the other side of the data-minimisation coin these regulations care
about — see `data_model.md` for the retention/archival plan (BDR-NFR-008, real as of BRD v0.4) and its
explicit note that the retention *period* is a legal decision, not one this handbook makes.

---

## Incident Response & Breach Notification (BDR-SEC-010)

The Compliance table above names what Trellis does *for* a tenant under GDPR/POPIA/NDPR —
erasure, export, lawful-basis logging. It says nothing about what happens if Trellis itself is
the one breached, which is the scenario those same regulations most explicitly legislate. A
security document this thorough shouldn't leave that as an unstated assumption.

This is deliberately a stated *commitment and mechanism*, not a full incident-response
playbook — on-call rotations, a dedicated incident-management tool, and a rehearsed runbook are
an operational-maturity step for once there's a team big enough to staff one, the same
"not yet, revisit when justified" reasoning `security_guide.md` already applies to Vault. What
this closes is narrower and more urgent: that the obligation itself is written down, and that
the mechanism to act on it already exists rather than needing to be improvised mid-incident.

```
1. Detection: a confirmed incident is unauthorized access to, or exposure of, tenant or
   platform data, or a vulnerability actively exploited in production — surfaced by a
   monitoring alert (devops_environments.md), an external vulnerability report (below), or
   manual discovery. A suspected-but-unconfirmed report is triaged, not immediately declared.
2. Containment — using mechanisms that already exist in this handbook, not new tooling built
   under pressure: revoke the affected session(s) or force a password reset (Session
   Revocation, above) for a compromised account; move a specifically implicated tenant toward
   the isolation already built for offboarding (multi_tenant_design.md) if its data is the
   exposure; rotate the affected secret via ISecretProvider (Secrets Management, above) if a
   platform-level credential is involved.
3. Assessment: which tenants, which data categories (PII, screening reports, payment
   authorization tokens — never raw card data, per PCI scope above), and how many records —
   using `trellis_audit.audit_record` as the primary forensic source. This is exactly why that
   log is append-only and DB-trigger-protected against tampering (Security Principles, above):
   an audit trail that could itself be edited mid-incident is not one you can assess an
   incident from.
4. Notification: GDPR requires notifying the relevant supervisory authority within 72 hours of
   becoming aware of a breach involving EU-domiciled personal data, and affected data subjects
   "without undue delay" where the breach is high-risk; POPIA and NDPR impose comparable, if
   not identically timed, obligations. Trellis commits to the strictest applicable clock — 72
   hours — as one platform-wide internal target, rather than tracking a different deadline per
   tenant's jurisdiction.
5. External vulnerability reports: a stated contact channel (a security-reports mailbox, or the
   Support module's ticketing until a dedicated one exists) and an acknowledgement commitment,
   so "there's nowhere to report this" is never the reason a real vulnerability goes unreported.
```

New `audit_type` value: `SECURITY_INCIDENT_DECLARED`.

---

## Security Checklist for PRs

Before merging any PR that touches auth, data access, or schema changes:

- [ ] No secrets, connection strings, or API keys in code or test fixtures
- [ ] New endpoints have an auth attribute applied (no anonymous endpoints without explicit, documented intent)
- [ ] Any new tenant-owned table has an RLS policy added in the same migration
- [ ] Any new table a Vendor can see uses the `tenant_or_vendor_isolation` policy pattern, not the plain one (see `data_model.md`)
- [ ] Schema change reviewed for tenant/vendor isolation correctness
- [ ] No raw SQL that bypasses EF Core query filters or the repository layer
- [ ] Cross-module data access goes through the other module's Service interface, never its `DbContext` or repository
- [ ] Any new file-upload endpoint runs through `IFileScanner` and starts at `scan_status = 'PENDING'` — a file is never made visible to anyone but its uploader before a scan clears
- [ ] Any code path that changes a password, or should logically end a session, calls the same session-revocation path `logout` uses — never a second, parallel way to invalidate a session
- [ ] Any new authentication-adjacent endpoint (login, credential reset, MFA verification) is added to the `auth-sensitive` rate-limiting policy, not left unthrottled by omission
- [ ] Any new cookie-based credential sets `HttpOnly`, `Secure`, the narrowest workable `SameSite` and `Path` — the refresh token's attributes above are the floor, not a one-off
- [ ] Any new logged object containing a credential- or secret-shaped field runs through the shared `RedactingEnricher`, never a one-off manual exclusion
- [ ] A new dependency or a Dependabot/CodeQL alert on an existing one is resolved or explicitly risk-accepted before merge, not left unactioned (`devops_environments.md`)

The last two items exist in the reference in spirit ("no raw SQL bypass," "no direct service
DB access") — the checklist entries are just restated for a monolith's specific failure mode
(reaching across an in-process module boundary is easier to do by accident than reaching
across a network one).

---

## Related Documentation

- [Multi-Tenant Design](./multi_tenant_design.md) — the 3-layer isolation model in depth, Vendor cross-tenant scoping
- [Data Model](./data_model.md) — audit record schema, RLS policy definitions
- [API Standards](./api_standards.md) — JWT usage at the API boundary, RBAC attributes
- [DevOps & Environments](./devops_environments.md) — the `ISecretProvider` implementation per environment
- [Frontend Architecture](./frontend_architecture.md) — how the SPA stores and refreshes tokens
- [Billing Design](./billing_design.md) — PCI scope, Paystack secret key management via `ISecretProvider`
- [Leasing Design](./leasing_design.md) — screening-data POPIA scope, `IScreeningProvider`/`IESignatureProvider` secret management, and the applicant email verification step that precedes it
- [Notifications Design](./notifications_design.md) — where the password-reset, file-scan-result, and MFA-recovery-code-used emails in this document are actually sent from
- [Support Design](./support_design.md) — the manual, human-verified fallback when a user loses both their MFA device and their recovery codes; the ticketing channel Incident Response's external vulnerability-report path reuses until a dedicated one exists
- [DevOps & Environments](./devops_environments.md) — the CI dependency/vulnerability scanning gate the Security Checklist's new item refers to, and the monitoring alerts that can trigger Incident Response's detection step
- [Coding Standards](./coding_standards.md) — the Code Review Checklist this document's Security Checklist sits alongside
