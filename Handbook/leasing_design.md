# Leasing & Vacancy Pipeline Design

Nothing in the current BRD covers filling a vacancy — the platform manages a property once
it's occupied (owners, documents, workflow, inspections) but has no concept of marketing a
vacant unit, taking an application, or executing a new lease. This is genuinely new product
scope, not a gap in an existing domain, and it's designed here at full depth per your choice:
vacancy listing through screening through digital lease execution, not just basic tracking.

**Traceability note**: BDR-LSE-001 through 007 were folded into
`Requirements/Trellis_BRD_v0.3.docx` (Section 5.13) alongside BDR-RENT. **BDR-LSE-008** was
added after that: a handbook gap review surfaced that `IssueApplicationLinkAsync` took an
applicant's email at face value before triggering a paid third-party screening check, with
nothing stopping an application being submitted against someone else's address. It's now real
too, folded into `Requirements/Trellis_BRD_v0.4.docx`, the same section, following the same
pattern the other seven followed before it.

**BDR-LSE requirements (real as of BRD v0.4)**:

| ID | Requirement | Priority |
|----|-------------|----------|
| BDR-LSE-001 | The system shall allow a PM company to list a Unit/Asset as vacant and available for lease, with configurable listing details (rent amount, availability date, description, photos). | Must |
| BDR-LSE-002 | The system shall allow a prospective tenant to submit a rental application against a listed vacancy via a secure, time-limited application link, without requiring a full platform user account. | Must |
| BDR-LSE-003 | The system shall support automated tenant screening (credit and background check) against a configurable third-party provider, triggered from a submitted application. | Should |
| BDR-LSE-004 | The system shall present screening results to authorised PM company staff for review, and shall never expose full screening report detail to any party other than the PM company. | Must |
| BDR-LSE-005 | The system shall support digital lease execution via e-signature integration, with the signed lease automatically filed as a versioned Document against the Property/Unit/Asset. | Should |
| BDR-LSE-006 | The system shall transition a Unit/Asset from vacant to occupied status automatically upon lease execution completion. | Must |
| BDR-LSE-007 | The system shall retain a full audit trail of an application's lifecycle (submitted, screened, approved/declined, leased). | Must |
| BDR-LSE-008 | The system shall verify a prospective tenant's control of the email address used to apply, before any screening request is triggered against that application. | Must |

BDR-LSE-008 is Must, not Should, because it's a guard in front of a paid, PII-handling
third-party call (BDR-LSE-003) — letting an unverified email trigger a real credit/background
check on a real person, one who never applied, is the specific failure it exists to prevent.

BDR-LSE-003 and BDR-LSE-005 are Should, not Must, despite being core to the pipeline you asked
for — both depend on a third-party integration this document deliberately doesn't lock to a
single named vendor yet (see below), so their completion date is somewhat outside Trellis's own
control. The rest of the pipeline (listing, application intake, staff review, status
transitions, audit trail) has no such external dependency and is Must.

---

## A New Actor: Prospective Tenant

This introduces an actor type the BRD's Section 3 doesn't have yet. A Prospective Tenant is
**not** a new role in the `app_user` table — following the exact pattern already established
for Owners (BDR-OWN-002: no login, report-only access via a secure link) and Owner report
delivery (BDR-SEC-003: role-based, time-limited access links), a Prospective Tenant interacts
with Trellis entirely through a **secure, single-purpose, time-limited application link** — no
account, no password, no `VENDOR_USER`-style persistent identity. This is a deliberate,
minimal-surface design: adding a fourth authenticated role to the Auth module for a one-time
interaction would be more machinery than the interaction warrants, and it would mean carrying
Prospective Tenant PII (screening-adjacent data) in the same identity system as staff and
Vendor credentials, which is worth avoiding on its own.

```csharp
public interface IApplicationLinkService
{
    Task<string> IssueApplicationLinkAsync(Guid vacancyListingId, string applicantEmail);
    Task<ApplicationLinkContext> ResolveAsync(string token);  // validates expiry + single-use
}
```

The resolved token grants access to exactly one application's submission and status-check flow
— nothing else in the platform, ever, regardless of what the underlying `app_user` or JWT
machinery elsewhere assumes about authenticated users. This endpoint sits outside the standard
JWT auth scheme entirely, similar in shape to the Paystack webhook endpoint in
`billing_design.md`, but authenticated by token validity rather than a cryptographic signature.

**Email verification (BDR-LSE-008) is this same mechanism, made explicit, not a separate
step.** A handbook gap review flagged that this needed stating precisely: the sequence is
strictly two-phase, and the boundary between them is what verification actually is.

```
Phase 1 — request:  prospective tenant enters an email on the public vacancy listing page
                     → IssueApplicationLinkAsync sends a link to that address
                     → no `application` row exists yet; nothing to screen
Phase 2 — apply:     prospective tenant clicks the emailed link
                     → ResolveAsync validates the token and returns ApplicationLinkContext
                     → only now does the application form exist to fill in
                     → on submit, the new `application.applicant_email` is copied from the
                       resolved token's context, never re-accepted as a client-supplied field
```

The applicant cannot reach the application form at all without having received and opened
that email, and the email address a screening check ultimately runs against is the one the
link was issued to, not one re-typed at submission time — so there's no path from "type an
email" to "a real background check runs" that skips proving control of that address. This was
implicit in the original design; BDR-LSE-008 makes it an explicit, testable requirement rather
than an assumption a future change could quietly break (e.g., a well-intentioned tweak that let
the submission form accept an editable email field).

---

## Module Placement

**Leasing** is its own module (`trellis_leasing` schema) — it has a genuinely distinct
lifecycle (vacant → listed → applications received → screened → leased → occupied) that
doesn't belong inside PropertyAsset, the same reasoning that gave Billing and Vendor their own
modules rather than folding them into Tenant or PropertyAsset respectively.

| Module | Owns (schema) | Key BDR References | Notes |
|--------|--------------|----------------------|-------|
| **Leasing** | `trellis_leasing` | BDR-LSE-001–008 (real as of BRD v0.4) | Vacancy listings, applications, screening, e-signature, lease execution |

## Data Model

```
trellis_leasing.vacancy_listing      — a Unit/Asset marketed as available
trellis_leasing.application          — one prospective tenant's submission against a listing
trellis_leasing.screening_result     — the third-party screening outcome for one application
trellis_leasing.application_event    — the audit trail (BDR-LSE-007)
```

### trellis_leasing.vacancy_listing

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| listing_id | UUID | PK | |
| tenant_id | UUID | NOT NULL | |
| unit_asset_id | UUID | FK → `trellis_property.unit_asset`, UNIQUE while active | One active listing per unit |
| rent_amount | NUMERIC(12,2) | NOT NULL | |
| currency | CHAR(3) | NOT NULL | |
| available_from | DATE | NOT NULL | |
| description | TEXT | NULLABLE | |
| status | VARCHAR(20) | NOT NULL DEFAULT 'LISTED' | LISTED\|UNDER_APPLICATION\|LEASED\|WITHDRAWN |

### trellis_leasing.application

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| application_id | UUID | PK | |
| listing_id | UUID | FK → vacancy_listing NOT NULL | |
| applicant_email | VARCHAR(255) | NOT NULL | No `app_user` row — identity is the email + link token only |
| status | VARCHAR(20) | NOT NULL DEFAULT 'SUBMITTED' | SUBMITTED\|SCREENING\|APPROVED\|DECLINED\|LEASED\|LEASE_ENDED |
| submitted_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |
| lease_start_date, lease_end_date | DATE | NULLABLE | BDR-LSE-009, Phase 11 — the term staff set at send-for-signature, carried to the lease record once signed. No end date with a start date means month-to-month; both null only for applications sent before term capture |
| lease_ended_date | DATE | NULLABLE | Set only by ending a Leased application — the staff-entered move-out date. *Why* it ended lives on the lease record (`trellis_property.lease`), not here; the old free-text `lease_ended_reason` column was moved there as `end_note` and dropped in Phase 11 |

### trellis_leasing.screening_result

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| screening_result_id | UUID | PK | |
| application_id | UUID | FK → application NOT NULL | |
| provider | VARCHAR(30) | NOT NULL | e.g. `TRANSUNION_ZA`, `METROPOL_KE` — see provider note below |
| outcome_summary | VARCHAR(20) | NOT NULL | PASS\|FLAGGED\|FAIL — coarse result only |
| full_report_ciphertext | TEXT | NULLABLE | Encrypted at rest (`security_guide.md`'s encryption architecture); never returned via any API response, read only through a narrow, audited internal view |
| received_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |

**RLS**: standard `tenant_isolation` on all four tables (`data_model.md`). No Vendor-visibility
clause — a Vendor has no relationship to leasing, ever.

---

## Third-Party Screening: Provider-Agnostic by Design

South Africa and Kenya don't share a common credit bureau — TransUnion is the dominant player
in South Africa; Kenya's Credit Reference Bureau ecosystem (Metropol, TransUnion Kenya, and
others under CBK oversight) is a separate integration entirely. Rather than picking one now,
this follows the exact seam pattern `deployment_topologies.md` established for cloud-vs-on-prem
swapping:

```csharp
public interface IScreeningProvider
{
    Task<ScreeningResult> RequestScreeningAsync(ApplicantDetails details);
}
```

A per-tenant configuration selects the active provider based on `tenant.country_code` — a South
African tenant's applications route to a TransUnion implementation, a Kenyan tenant's to a
Metropol (or equivalent) implementation, without the Leasing module's Application logic knowing
or caring which. **This document deliberately does not commit to a specific provider or
contract** — that's a business/legal decision (data-sharing agreements, per-check cost,
regulatory registration in each market) outside engineering scope, and the interface is what
lets that decision be made later without redesigning anything.

## Digital Lease Execution: E-Signature Integration

Same seam pattern:

```csharp
public interface IESignatureProvider
{
    Task<string> SendForSignatureAsync(Guid documentId, IEnumerable<Signer> signers);
    Task<SignatureStatus> GetStatusAsync(string envelopeId);
}
```

On completed signature (via webhook, verified the same way the Paystack webhook is in
`billing_design.md` — signature-verified, idempotency-deduplicated), the signed lease is filed
as a new `Document` version (BDR-DOC-002) against the Property/Unit/Asset, and
`BDR-LSE-006`'s automatic status transition fires: `vacancy_listing.status → LEASED`,
`unit_asset.status → OCCUPIED`. Provider choice (DocuSign, Adobe Sign, or a more regionally
common alternative) is left open behind `IESignatureProvider` (still `NoOpESignatureProvider` —
no vendor account exists). The webhook's signature check itself is built for real regardless:
`HmacESignatureSignatureValidator` (HMAC-SHA256 of the raw body, constant-time compare, secret
`ESignatureWebhookSecret` via `ISecretProvider`, fails closed — a missing secret makes every
signature invalid, never valid) replaces the earlier always-valid `NoOpESignatureSignatureValidator`
stub, exact `PaystackSignatureValidator` precedent: the cryptography needs no vendor credentials to
be correct, only the envelope contents on the other side of it do.

---

## Lease Termination

Since Phase 11 the lease itself is a separate record, `trellis_property.lease` (see
`data_model.md`), not the `application` row — the application records how a tenancy was *won*,
the lease records the tenancy. Execution creates it (`ILeaseService.StartAsync`, idempotent by
application id, with the term from send-for-signature; a signed lease supersedes any earlier active
lease on the unit rather than being blocked by it). Ending one is still a staff action on the
application, `POST applications/{id}/end-lease` (`LEASED` only → `LEASE_ENDED`), now with BDR-LSE-011's
structured reason: an effective date (not in the future, not before the lease started), a reason
from a fixed list, who ended it, and a note required when the reason is OTHER. It keeps the
lease-execution webhook's retry-safe ordering, tolerating "already done" at each step: end the
unit's rent schedule if one exists (`IRentScheduleService.EndForLeaseAsync`, a no-op if Rent
Collection isn't entitled or there was none), end the lease (`ILeaseService.EndAsync`, which marks
the unit vacant from that date — skipped if already ended), then close out the application. The `vacancy_listing` row is left `LEASED` as history — re-letting the
unit is a fresh listing, and `VacancyListingService.CreateAsync` now refuses to list a unit that is
still `OCCUPIED` (a signed lease must be ended first).

---

## Lease Renewals (BDR-LSE-010, Phase 11)

Staff record a renewal offer on any ACTIVE lease — LEASING or MANUAL, since renewing a tenancy
doesn't depend on how it was won — and then the lessee's answer, because lessees have no platform
access. `trellis_leasing.renewal_offer` holds the proposed term (a fixed end date after the current
term, or month-to-month), an optional new rent with the date it applies from, a respond-by date,
and the outcome: ACCEPTED, DECLINED (with a reason), or EXPIRED (recordable only once the
respond-by date has passed). At most one OFFERED offer per lease (partial unique index).

**Accepting extends the same lease** (`ILeaseService.RenewAsync`) rather than creating a new one or
a new application, and — for a new rent — adds a dated `rent_change` to the lease's active
schedule (`IRentScheduleService.ScheduleRentChangeForLeaseAsync`; see `rent_collection_design.md`),
so periods before that date keep the old rent. The rent step runs first because it is idempotent by
date: if the lease step then fails, a retry repeats it harmlessly. Both are re-validated against the
lease as it is at acceptance (its term may have been recorded since the offer). When the tenant
has no Rent Collection, or the lease has no schedule, the rent step is a no-op and the offer records
`rent_change_applied = false`, so staff aren't told the rent moved when nothing tracks it.

**Decline reasons are configurable per tenant** (BRD: "a configurable list"), stored in
`tenant.config_json` under `leasing.renewalDeclineReasons` and edited on the Settings page, with
defaults until a tenant sets its own. "Other" is never in the list — it is always offered, and needs
a note. The chosen reason is copied onto the offer as text, so editing the list later never rewrites
history.

## POPIA and Screening Data

`screening_result.full_report_ciphertext` is the single most sensitive piece of data in the
Trellis schema so far — a credit/background report is squarely POPIA-regulated personal
information, and BDR-LSE-004's restriction ("never expose full screening report detail to any
party other than the PM company") is a Must, not a Should, for that reason. Built for real:

- **Encryption at rest.** `IScreeningProvider.RequestScreeningAsync` hands back plaintext
  (`ScreeningOutcome.FullReportJson`) — the provider itself never touches keys.
  `ApplicationService.RunScreeningAsync` encrypts it via `Core/Security/IEncryptionService`
  (AES-256-GCM, `security_guide.md`'s Encryption Architecture table) before it ever reaches
  `ScreeningResult.Create`, bound via associated data to `{tenantId}:{applicationId}` so a
  ciphertext copied onto a different application's row fails to decrypt rather than silently
  decrypting as the wrong report. `NoOpScreeningProvider` returns a small, clearly-labelled
  synthetic report (`{"simulated":true,...}`) rather than `null`, so this whole path — including
  the view below — is exercised end to end without a real vendor.
- **Never in an ordinary response.** `ApplicationResponse` carries only `ScreeningOutcomeSummary`;
  no field on it is shaped like report content at all (asserted by a reflection test, not just a
  reviewer's promise).
- **The one narrow, separately-audited view.** `POST applications/{id}/screening-report`
  (TENANT_ADMIN only, `[EnableRateLimiting]` on the same policy as login) requires the caller to
  re-enter their password (`IAuthService.ReauthenticateAsync` — a wrong password is itself
  audited as `REAUTHENTICATION_FAILED` and denied with `TRL-E-AUT-1004`). On success, an
  `application_event` (`SCREENING_REPORT_VIEWED`) and an `audit_record`
  (`AuditTypes.Leasing.ScreeningReportViewed`) are written and durably saved *before* the report
  is decrypted — a failure at that step means the report is never decrypted, never a race where
  plaintext could escape and the audit write fail afterwards. The response carries
  `Cache-Control: no-store` and is never logged.

This is now its own line in `security_guide.md`'s Compliance table, alongside the existing POPIA
erasure/export mechanism.

---

## Related Documentation

- [Data Model](./data_model.md) — schema conventions and RLS this module follows
- [Security Guide](./security_guide.md) — POPIA scope for screening data, encryption architecture
- [Multi-Tenant Design](./multi_tenant_design.md) — why there's no Vendor-visibility clause here
- [Billing Design](./billing_design.md) — the `IPaymentGateway`/webhook pattern this module's `IScreeningProvider`/`IESignatureProvider` mirror
- [Rent Collection Design](./rent_collection_design.md) — the rent schedule created automatically on lease execution
- [Operational Flows](./operational_flows.md) — how a maintenance request later in a tenancy's life reuses none of this (deliberately separate lifecycles)
- [Notifications Design](./notifications_design.md) — where the application link and screening-result emails are actually sent from
- [Build Roadmap](./roadmap.md) — Phase 9
