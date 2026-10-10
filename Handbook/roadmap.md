# Build Roadmap

Every other document in this handbook answers "how do we build it." This one answers "in what
order" — sequencing thirteen of Trellis's fourteen backend modules (Support is Triggered, Not
Scheduled — see below) and their matching frontend slices into phases, grounded in two things:
the technical migration dependency order already fixed in `data_model.md` (`Tenant →
Notification → Auth → PropertyAsset → Vendor → Document → Workflow → Inspection → Ai → Billing →
Reporting → Leasing → RentLedger`), and a product-sequencing judgment about what a design
partner needs to see and when Trellis is actually ready to invoice someone.

**Relation to the BRD's own Roadmap section**: `Requirements/Trellis_BRD_v0.6.docx` (Section
9.2) already has a Roadmap/Out of Scope section describing business-level future scope. That
document is the source of truth for *what's in scope*; this one is the source of truth for
*build order*. The two are reconciled at every BRD revision — v0.2 folded in Billing, v0.3
folded in Leasing/Rent Collection, v0.4 folded in Notification, Support, and the security/
NFR/lifecycle additions, v0.5 folded in module entitlements, and v0.6 folded in the lease
lifecycle and portfolio reporting additions built as Phases 11–12 — rather than maintaining two
separate scope narratives. The one BDR
set still genuinely proposed, not yet in any BRD version, is BDR-LIC (on-premises/licensing,
`deployment_topologies.md`) — triggered by a qualifying enterprise deal, not scheduled to a
phase.

---

## Sequencing Principles

- **The migration dependency order is the technical floor** — a module can't be built before
  what it depends on exists, full stop. Everything else below is judgment layered on top of
  that hard constraint.
- **Ship vertical slices, not horizontal layers.** Each phase delivers a working backend module
  *and* its corresponding frontend screens together. A design partner conversation needs
  something clickable at the end of a phase, not an API with no UI, and not a shared UI shell
  with nothing real behind it.
- **The Vendor cross-tenant identity ships early enough to demo**, not last — it's one of the
  three differentiators named in the BRD (alongside the configurable workflow builder and
  targeted AI automation), and the best time to convince a skeptical design partner is with a
  working demo, not a slide.
- **Billing waits until there's something real to bill, and never ahead of a BRD Must-have.**
  Usage-based invoicing is technically buildable right after PropertyAsset (unit counts exist
  by then), but design partner conversations are about validation, not revenue yet. It's
  sequenced after the Ai module specifically because the BRD marks AI Document Intelligence and
  AI Request Triage as **Must-have Phase 1 scope** (BDR-AI-001–004), while Billing is a
  Should-have (BDR-TEN-005) that isn't even part of the BRD's own Phase 1 MVP list. Billing's
  only real technical dependency is PropertyAsset (Phase 2), so moving it later costs nothing —
  there's no reason a Should-have should ship before a Must-have just because it was drafted
  first.
- **Reporting waits for real usage data**, consistent with `data_model.md`'s own note that
  `trellis_reporting` materialized views are deferred until there's real data to build them
  against — building dashboards against fabricated data risks designing the wrong dashboards.
- **On-premises deployment and a genuinely native app stay outside the numbered phases
  entirely** — both already have a stated trigger condition in their own documents
  (`deployment_topologies.md`, `frontend_architecture.md`) and get built when that trigger
  fires, not on a calendar slot.
- **Maintenance requests don't get their own phase or module** — see `operational_flows.md`.
  A maintenance request is a composition of capabilities Phases 1–6 already build (Workflow
  routing, Vendor assignment, AI triage, Document attachment), not a new domain, so there's
  nothing to schedule beyond the phases that already build its parts.
- **Optimistic concurrency and an accessibility baseline are Phase 0 conventions, not
  a later cleanup pass** — both are far cheaper to build in from the first entity/first
  component than to retrofit once real data or a real design system exists
  (`coding_standards.md`, `frontend_architecture.md`; BDR-NFR-006/007, real as of BRD v0.4).
- **Support ticketing is Triggered, Not Scheduled, not simply omitted** — it's fully specified
  (`support_design.md`) but genuinely optional: email-based support is a legitimate way to run
  this for a good while, and nothing else in this roadmap depends on it existing.
- **Notification moved into Phase 1, not a later phase** — a handbook gap review found five-plus
  BDRs across multiple phases silently assuming an email/SMS mechanism existed (`notifications_design.md`).
  The earliest of those, BDR-SEC-005's password reset, is itself Phase 1 scope, so Notification
  has to exist by Phase 1 or Phase 1 ships with a half-built password reset flow. This is a
  build-order correction flagged directly, the same way the Ai/Billing swap and the offline
  inspection scope were — not folded in silently.
- **Tenant offboarding, Vendor relationship termination, MFA recovery codes, auth-endpoint
  throttling, and refresh-cookie hardening are lifecycle and hardening details on modules
  already in this roadmap, not new modules of their own** — a third handbook gap review
  surfaced them, and each lands in the phase that already owns its module rather than getting
  a phase of its own: BDR-TEN-006 and BDR-SEC-008/009/BDR-NFR-009 fold into Phase 1 (Tenant,
  Auth), and BDR-VEN-006 folds into Phase 4 (Vendor).
- **Security response headers, log redaction, and dependency scanning are Phase 0 conventions,
  not a later hardening pass** — the same reasoning already applied to concurrency and
  accessibility above: cheaper to build in from the first middleware/first CI run than retrofit
  once real traffic or a real dependency tree exists. Incident response (BDR-SEC-010) is the one
  exception worth naming — it's a stated procedure, not code, so it has no phase dependency at
  all; it's written down by Phase 1, alongside the rest of this round's security-guide work, on
  the same "should already exist" logic as everything else this fourth gap review found.
- **Phase 11 records data before Phase 12 reports on it** — BRD v0.6's additions came out of a
  competitive product review, and split cleanly into two kinds: new fields captured at the
  moment something happens (lease term, termination reason, deposit, vacant-since date, work
  assignment cost), and reports over those fields. The capture side goes first and goes soon,
  because every lease executed or ended before it exists is permanently missing from expiry,
  renewal, and turnover reporting; nothing can be honestly backfilled. That is also why
  BDR-LSE-009 and BDR-LSE-011 are Musts in a set that is otherwise Should/Could.

---

## Phase 0 — Foundation

No BDRs directly — this is the scaffolding everything else stands on.

| | |
|---|---|
| **Backend** | `Trellis.sln` structure per `project_architecture.md`; `Core/` cross-cutting code (`ApiResponse<T>`, `ServiceResult<T>` — including the `ConcurrencyConflict` outcome, `ExceptionHandlingMiddleware`, `TenantScopeMiddleware`, `ErrorCodeRegistry`, `SecurityHeadersMiddleware`, and the global `RedactingEnricher` — `security_guide.md`, BDR-NFR-010/011 real as of BRD v0.4); the `UseXminAsConcurrencyToken()` convention adopted as a baseline in every module's EF Core configurations from the first migration, not retrofitted later (`coding_standards.md`, BDR-NFR-006 real as of BRD v0.4); local `docker-compose` Postgres; `.editorconfig` + analyzers wired per `coding_standards.md` |
| **Frontend** | `trellis-web` scaffold (Vite, router shell, typed API client generation wired to a starter OpenAPI spec, auth scaffolding placeholder); component library chosen with WCAG 2.1 AA support as a stated criterion, not an afterthought (`frontend_architecture.md`, BDR-NFR-007 real as of BRD v0.4) |
| **CI/CD** | Build + test gate live per `devops_environments.md`, even before there's a module to test against; `axe-core` wired into the Playwright pipeline from the start so accessibility is a gate from the first page built, not bolted on later; Dependabot/CodeQL dependency scanning live from the first PR, not added once there are enough dependencies to worry about |
| **Exit criteria** | `dotnet build` and `dotnet test` green in CI on an empty solution; `npm run build` green; both repos have the Clean Architecture folder skeleton (`Domain/Application/Infrastructure/Presentation`) per module, empty but present |

---

## Phase 1 — Identity & Tenancy

| | |
|---|---|
| **Modules** | Tenant, Notification, Auth |
| **BDRs** | BDR-TEN-001–005, BDR-TEN-006 (real as of BRD v0.4), BDR-SEC-001–004, BDR-SEC-005–006/008–010 (real as of BRD v0.4), BDR-NFR-009–011 (real as of BRD v0.4), BDR-DEPT-002, BDR-NOTIF-001–004/006 (real as of BRD v0.4) |
| **Backend** | Single-transaction tenant provisioning and its inverse, tenant offboarding (`multi_tenant_design.md`); the Notification module (template resolution, dispatch, delivery audit — `notifications_design.md`) built before Auth needs it; `app_user` + JWT login/MFA, with MFA recovery codes issued at enrollment; self-service password reset and session revocation, auth-endpoint rate limiting, and refresh-cookie hardening (`security_guide.md`); `trellis_audit.audit_record` created as a range-partitioned table from its first migration, even though archival automation is deferred (`data_model.md`, BDR-NFR-008 real as of BRD v0.4); Row-Level Security policies live on real tables for the first time |
| **Frontend** | Login page, forgot-password flow, token refresh flow, role-based route guards, Super Admin tenant list/provisioning screen |
| **Exit criteria** | **The multi-tenant isolation test suite (`testing_strategy.md`) passes against real Postgres with RLS enabled.** This is the single most important gate in the entire roadmap — it's the earliest point the 3-layer isolation model is even testable, and nothing after this phase should ship if it doesn't hold. |

---

## Phase 2 — Property & Asset Management

| | |
|---|---|
| **Module** | PropertyAsset |
| **BDRs** | BDR-OWN-001–005, BDR-AST-001–004 |
| **Backend** | Owners, Properties, Units/Assets — full CRUD through the Domain/Application/Infrastructure layering |
| **Frontend** | Tenant Admin property and unit management screens, responsive per the breakpoint table in `frontend_architecture.md` |
| **Exit criteria** | A Tenant Admin can create an owner, a property, and its units end-to-end through the UI — this is the first phase a design partner could plausibly click through unassisted |

---

## Phase 3 — Documents & Workflow

| | |
|---|---|
| **Modules** | Document, Workflow |
| **BDRs** | BDR-DOC-001–005, BDR-WF-001–005, BDR-DEPT-001–004, BDR-SEC-007 (real as of BRD v0.4) |
| **Backend** | Document upload/versioning/expiry alerts, each upload validated and malware-scanned before it's visible to anyone but its uploader (`security_guide.md`); department-scoped workflow templates, instances, and approvals; both BDR-WF-003 and BDR-DOC-003 dispatch through the Notification module built in Phase 1, not a new integration of their own |
| **Frontend** | Document library, workflow board/approval screens |
| **Exit criteria** | A document expiry alert fires end-to-end without manual intervention; a workflow instance can be created, routed to the correct department, and approved by a Department Staff user with the right role |

---

## Phase 4 — Vendor Cross-Tenant Identity (a core differentiator)

| | |
|---|---|
| **Module** | Vendor |
| **BDRs** | BDR-VEN-001–005, BDR-VEN-006 (real as of BRD v0.4) |
| **Backend** | Vendor identity, `vendor_assignment`, `VendorScopedRepository`, the `tenant_or_vendor_isolation` RLS policy live for the first time; `tenant_vendor_relationship` built alongside `vendor_assignment` from the start, since relationship termination is cheapest to model at the same time as the assignment table it gates, not retrofitted after (`multi_tenant_design.md`) |
| **Frontend** | The Vendor portal (`/vendor/*`) — assigned work item list, close-out flow. Mobile-first per `frontend_architecture.md`'s responsive priority table, since this is the persona actually working from a phone. |
| **Exit criteria** | A single Vendor login sees and closes assignments across **two different tenants**, and cannot see anything else — this is the flagship moment to show a skeptical design partner, and the phase worth prioritizing schedule pressure toward protecting. |

---

## Phase 5 — Inspections

| | |
|---|---|
| **Module** | Inspection |
| **BDRs** | BDR-INSP-001–005, BDR-NFR-001, BDR-SEC-007 (real as of BRD v0.4) |
| **Backend** | Inspections, checklist items, photo attachments — depends on Vendor for assignment; every captured photo runs through the same upload validation/scanning pipeline as Document (Phase 3), which matters most here since a Vendor is the most common uploader |
| **Frontend** | Inspection checklist UI, photo capture, built as a PWA with offline-first storage and sync-on-reconnect per `frontend_architecture.md` — this is Must-have scope, not deferred |
| **Exit criteria** | A Vendor completes an assigned inspection with photos **while offline**, and it syncs automatically the moment connectivity returns — this is the actual BDR-INSP-003 acceptance test, not just "works on a phone browser" |

---

## Phase 6 — AI Assist

| | |
|---|---|
| **Module** | Ai |
| **BDRs** | BDR-AI-001–005 (Must-have Phase 1 scope per the BRD — see Sequencing Principles above) |
| **Backend** | Document extraction jobs, routing suggestions, override log; the retry/fallback resilience policy from `error_handling.md` |
| **Frontend** | Extraction review queue, routing suggestion UI |
| **Exit criteria** | An AI extraction failure degrades to manual review **without blocking the underlying document or workflow** (BDR-AI-005) — this negative path gets tested explicitly, not just the happy path, since AI was designed from the start as an assist, not a dependency |

---

## Phase 7 — Billing

| | |
|---|---|
| **Module** | Billing |
| **BDRs** | BDR-BIL-001–008 (real as of BRD v0.2), BDR-BIL-009 (real as of BRD v0.5 — module entitlements) |
| **Backend** | Usage snapshot job, invoice generation, module entitlement checks (`RequiresModuleEntitlement`), Paystack integration, dunning — per `billing_design.md` |
| **Frontend** | Tenant Admin Billing settings page (including per-module enable/disable), Super Admin platform-wide revenue view |
| **Dependencies** | PropertyAsset (unit counts to meter), Auth (Tenant Admin login), Tenant (status transitions for suspension) |
| **Exit criteria** | A real design partner can be invoiced and successfully charged for a full billing cycle, including at least one add-on module entitlement on the invoice — this is the point Trellis can accept its first paying tenant, not before |

---

## Phase 8 — Reporting

| | |
|---|---|
| **Module** | Reporting |
| **BDRs** | BDR-RPT-001–003, BDR-SEC-002 |
| **Backend** | Dashboards, Owner report generation; `trellis_reporting` materialized views built against real usage data collected from Phases 1–7 |
| **Frontend** | Dashboard views, Owner report export |
| **Exit criteria** | An Owner report reflects real portfolio data end-to-end, generated without a Trellis engineer hand-assembling it |

---

## Phase 9 — Leasing & Vacancy Pipeline

| | |
|---|---|
| **Module** | Leasing (new) |
| **BDRs** | BDR-LSE-001–008 (real as of BRD v0.4) |
| **Backend** | Vacancy listings, application intake via secure link with email verification built into the same link-click mechanism (BDR-LSE-008), third-party screening integration, e-signature integration for lease execution, automatic unit status transition — per `leasing_design.md` |
| **Frontend** | Vacancy listing management, application review queue, screening result view (staff-only, POPIA-scoped per `security_guide.md`) |
| **Dependencies** | PropertyAsset (unit status), Document (filed signed lease), Auth (staff review), Notification (application-link and screening-result emails) |
| **Exit criteria** | A prospective tenant submits an application via a secure link with no platform account, staff review a screening result, and a completed e-signature automatically flips the unit to occupied and files the lease as a versioned document |

---

## Phase 10 — Rent Collection (Tracking Only)

| | |
|---|---|
| **Module** | Rent Collection (new) |
| **BDRs** | BDR-RENT-001–006 (real as of BRD v0.3) |
| **Backend** | Rent schedules, manually recorded payments, arrears flagging and staff notification — explicitly no payment processing, no trust accounting — per `rent_collection_design.md` |
| **Frontend** | Rent schedule view, payment recording form, arrears list |
| **Dependencies** | PropertyAsset, Leasing (a rent schedule is created on lease execution), Notification (arrears-flag alerts) |
| **Exit criteria** | A staff member records a rent payment against a schedule, an unpaid entry past its grace period surfaces as an arrears flag, and both appear correctly in the Owner portfolio report |

---

## Phase 11 — Lease Lifecycle Tracking

| | |
|---|---|
| **Modules** | Leasing, Rent Collection, PropertyAsset, Notification (no new module) |
| **BDRs** | BDR-LSE-009–011, BDR-RENT-007–008, BDR-AST-005, BDR-NOTIF-007 (all real as of BRD v0.6) |
| **Backend** | Lease term (start date, end date or month-to-month) captured at lease execution; the existing end-lease flow takes a reason from a fixed list plus the ending party, with the existing free-text `LeaseEndedReason` kept as the optional note; renewal offers and outcomes, with an accepted renewal extending the existing lease and its rent schedule rather than creating a new application; deposit records on the rent ledger, worded as records per BDR-RENT-006; on-time rate and average days late calculated from `RentPaymentRecord.DateReceived` against each schedule's due day and grace period; a vacant-since date on Unit/Asset set on every transition to vacant; threshold alerts built on the alert-recipient settings already in tenant settings for arrears, each alert type offered only when the tenant is entitled to the module it reads from |
| **Frontend** | Lease term fields on the send-for-signature step; an "ending within 30/60/90 days" list; renewal offer and outcome recording; structured end-lease dialog; deposit section on the rent schedule; days-vacant column and filter on the units table; alert settings page with per-alert on/off, threshold, channel, and recipients |
| **Dependencies** | Leasing (Phase 9), Rent Collection (Phase 10), Inspection (Phase 5, for the overdue-inspection alert), Notification (Phase 1), Billing entitlements (Phase 7) |
| **Decisions** (10 October 2026) | (1) **A separate lease record, in PropertyAsset.** `Application` stops doubling as the lease: `trellis_property.lease` holds one row per tenancy (term, status, BDR-LSE-011 end details), and a unit is OCCUPIED exactly when it has an ACTIVE lease — only `LeaseService` changes occupancy. Renewals (BDR-LSE-010) will extend this record rather than an application. (2) **It lives in Core, so Rent Collection doesn't depend on Leasing.** Leasing and Rent Collection are independently entitled add-ons, so the lease can't belong to either: Leasing starts a LEASING lease at e-signature; a manually created rent schedule starts a MANUAL lease (or attaches to the unit's active one), and ending that schedule ends the tenancy. Deposits (BDR-RENT-008) will hang off the lease id, inside Rent Collection. (3) **Deposit rules ship with no legal defaults.** Refund period and interest rate are per-tenant settings that start empty; no overdue-refund alert fires until a tenant sets its refund period, and the settings screen names the governing law (e.g. South Africa's Rental Housing Act) as something to confirm with the tenant's own advisor rather than presenting a Trellis default as compliant. `RentLedgerArchitectureTests` forbids "Deposit"/"Refund" in RentLedger's public API, so that slice also adds a documented, narrow exception: a deposit is a lease term recorded, never funds held (BDR-RENT-006 still applies). |
| **Progress** | Slice 1 shipped: lease record, term capture, structured termination, expiring-lease list, vacant-since, and the backfill of every existing tenancy (BDR-LSE-009, BDR-LSE-011, BDR-AST-005 — the two Musts plus vacancy duration). Slice 2 shipped: renewal offers (BDR-LSE-010) — offers on any active lease, accept/decline/expire, decline reasons configurable per tenant, acceptance extending the same lease and changing the rent only from its date (new `rent_change` table; arrears now price each period at the rent in effect on its due date). Remaining: payment timeliness (BDR-RENT-007), deposit records (BDR-RENT-008), threshold alerts (BDR-NOTIF-007). |
| **Exit criteria** | A lease executed after this phase has a term and appears in the 30/60/90-day list; ending it records a structured reason; leases executed before this phase show "term not recorded", never a guessed date; a recorded deposit whose refund is overdue raises the configured alert; the Owner report shows on-time rate alongside the existing rent status |

---

## Phase 12 — Portfolio Reporting & Onboarding

| | |
|---|---|
| **Modules** | Reporting, Vendor, PropertyAsset, Tenant, Auth (no new module) |
| **BDRs** | BDR-RPT-004–007, BDR-VEN-007, BDR-OWN-006, BDR-AST-006, BDR-TEN-007, BDR-DEPT-005 (all real as of BRD v0.6); also BDR-AST-004, scoped to Phase 2 but not yet delivered |
| **Backend** | Trend reports over 3/6/12/24 months by property, Owner, and department, every roll-up weighted by its underlying count and keeping the existing null-not-zero convention for "no data" (BDR-RPT-005 is a Must); configurable RAG thresholds with platform defaults; an activity timeline read from `trellis_audit.audit_record`, scoped per BDR-RPT-003; estimated and actual cost with an expense category on `vendor_assignment`, with AI invoice extraction allowed to propose the actual cost under the usual review step; property expense records feeding a net figure in the Owner report; CSV import of owners, properties, and units with a per-row created/skipped result; tenant logo, colour, and display name applied to every external-facing surface; a read-only financial reporting role with no write permissions and no screening-report access |
| **Frontend** | Reporting pages in one consistent layout (summary figures, trend chart, breakdown table, period switch); activity timeline with category filters; cost fields on work assignments; expense recording; import screen with template download and a results table; branding settings with a live preview; role picker updated for the new role |
| **Dependencies** | Phase 11 (most trend reports read the data it captures), Vendor (Phase 4), Ai (Phase 6), Reporting (Phase 8) |
| **Exit criteria** | Portfolio occupancy reads the same on every screen that shows it; a report on a module the tenant isn't entitled to is omitted, not zero; a 200-row import with deliberate bad rows creates the good ones and lists each bad one with its reason; a Finance Viewer user can export every financial report and is refused on every write endpoint and on the screening report |

---

## Triggered, Not Scheduled

One capability is fully specified elsewhere in this handbook but deliberately excluded from the
numbered phases above — it gets built only if its stated trigger fires, not on a fixed timeline:

| Capability | Spec | Trigger |
|------------|------|---------|
| On-premises deployment & licensing | `deployment_topologies.md` | An ENTERPRISE-tier prospect makes on-premises an explicit deal condition |
| A genuinely native app (app-store distribution, native push) | `frontend_architecture.md` | App-store presence or native push notifications specifically become a requirement — **not** the same trigger as offline capability, which is now Phase 5, Must-have scope, built as a PWA |
| Full tenant rent payment processing (funds movement, trust/escrow accounting) | `rent_collection_design.md` | An explicit business/legal decision to pursue the relevant licensing and compliance obligations in each operating jurisdiction — **not** an engineering readiness question. Phase 10 ships tracking only, by deliberate choice, to stay out of that territory until it's actually decided |
| Support / helpdesk module | `support_design.md` | Email-based support becomes operationally unmanageable, or a design partner explicitly asks for in-app ticketing |

---

## Related Documentation

- [Data Model](./data_model.md) — the migration dependency order this roadmap's phase sequence is built on
- [Multi-Tenant Design](./multi_tenant_design.md) — why Phase 1's isolation test suite is the hardest gate in the roadmap
- [Testing Strategy](./testing_strategy.md) — the test coverage each phase's exit criteria depends on
- [Project Architecture](./project_architecture.md) — the Module Catalogue and BDR ranges each phase draws from
- [Billing Design](./billing_design.md) — real as of BRD v0.2; [Deployment Topologies](./deployment_topologies.md) — the one remaining module with proposed, not-yet-real BDR IDs (BDR-LIC), triggered rather than phase-scheduled
- [Frontend Architecture](./frontend_architecture.md) — the responsive-priority table and the PWA offline architecture for Phase 5
- [Operational Flows](./operational_flows.md) — the Maintenance and Inspection journeys this roadmap's phases build the pieces of
- [Leasing Design](./leasing_design.md) — Phase 9
- [Rent Collection Design](./rent_collection_design.md) — Phase 10
- [Notifications Design](./notifications_design.md) — Phase 1, and why it moved there
- [Security Guide](./security_guide.md) — the Phase 1 password-reset/session-revocation and the Phase 3/5 file-upload-security additions
- [Coding Standards](./coding_standards.md) — the Phase 0 optimistic-concurrency baseline
- [Support Design](./support_design.md) — Triggered, Not Scheduled, and why
