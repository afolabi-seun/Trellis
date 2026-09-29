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
| lease_ended_date | DATE | NULLABLE | Set only by ending a Leased application — the staff-entered move-out date |
| lease_ended_reason | VARCHAR(500) | NULLABLE | Optional free text |

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

There is no separate Lease entity in this schema — the `application` row that reached `LEASED` *is*
the lease record, so ending a lease is a staff action against that same row: `POST
applications/{id}/end-lease` (`LEASED` only → `LEASE_ENDED`, with an effective date and an optional
reason). It mirrors the lease-execution webhook's own retry-safe ordering, tolerating "already done"
at each step so a retry can always complete: end the unit's rent schedule if one exists
(`IRentScheduleService.EndForLeaseAsync`, a no-op if Rent Collection isn't entitled or there was
none), mark the unit vacant (`IUnitAssetService.MarkVacantAsync`, tolerating already-vacant), then
close out the application. The `vacancy_listing` row is left `LEASED` as history — re-letting the
unit is a fresh listing, and `VacancyListingService.CreateAsync` now refuses to list a unit that is
still `OCCUPIED` (a signed lease must be ended first).

---

## POPIA and Screening Data

`screening_result.full_report_ciphertext` is the single most sensitive piece of data in the
Trellis schema so far — a credit/background report is squarely POPIA-regulated personal
information, and BDR-LSE-004's restriction ("never expose full screening report detail to any
party other than the PM company") is a Must, not a Should, for that reason. Concretely: the
full report is encrypted at rest, never included in any API response body (only
`outcome_summary` is), and access to view it is a narrow, separately-audited action distinct
from ordinary application review — this is worth its own line in `security_guide.md`'s
Compliance table once this module ships, alongside the existing POPIA erasure/export mechanism.

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
