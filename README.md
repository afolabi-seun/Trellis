# Trellis — Multi-Tenant Property Asset Management Platform

Project workspace for Trellis, a FusionAxis product concept: a multi-tenant
SaaS for property management companies managing multiple owners' portfolios,
with departments, configurable workflow, document management, inspections,
vendor management, and targeted AI capabilities.

## Contents

- **Requirements/** — Business Requirements Document (BRD) with BDR-numbered
  requirements, traceable by domain (tenancy, owners, departments, vendors,
  assets, workflow, documents, inspections, AI, reporting, security, billing,
  leasing & vacancy management, rent collection, platform notifications,
  cross-tenant support ticketing). v0.5 is current; v0.1 through v0.4 kept
  for history.
- **Pitch/** — Customer-facing pitch deck used to validate the concept with
  prospective property management company partners.
- **Discovery/** — Materials for customer discovery: outreach targets,
  discovery scripts, and feedback notes from early conversations (to be
  added as this progresses).
- **Handbook/** — Engineering handbook and specifications for the .NET 8 /
  ASP.NET Core + PostgreSQL modular monolith backend and the React +
  TypeScript frontend: architecture, data model, API standards, security
  (including password reset, session revocation, MFA recovery codes,
  auth-endpoint rate limiting, refresh-cookie/CSRF hardening, and
  file-upload malware scanning), multi-tenant/vendor design (including
  tenant offboarding and vendor relationship termination), testing
  strategy, DevOps & environments, frontend architecture (including a WCAG
  2.1 AA accessibility target), billing, deployment topologies, coding
  standards (including an optimistic-concurrency convention), build
  roadmap, operational flows (Maintenance/Inspection journeys), the
  Leasing and Rent Collection module designs, a Notifications module
  (shared email/SMS dispatch), and a minimal Support module. Read
  Handbook/README.md first.
- **src/** — the application code: `backend/` (.NET 8 API), `frontend/`
  (`trellis-web`, React + TypeScript), plus `Trellis.sln`, `.github/`
  (CI workflows, Dependabot), and `infrastructure/` (local Postgres compose
  file). Deliberately self-contained — everything the code needs to build,
  test, and run lives inside `src/`, nothing outside it — so this subtree
  can become a standalone repository's own root later with no path rework,
  checking in only what's under `src/` at that point. See
  `Handbook/project_architecture.md`'s Repository Structure for the full
  tree and reasoning.

## Status

- [x] Concept scoped, multi-tenant model and vendor differentiation defined
- [x] BRD v0.1 drafted (Requirements/Trellis_BRD_v0.1.docx)
- [x] Customer pitch deck built (see Pitch/README.md for the link)
- [x] Discovery script and target list
- [x] Engineering handbook (Handbook/ — 18 documents covering backend
      architecture [Clean Architecture per module: Domain/Application/
      Infrastructure/Presentation], data model, API standards, security,
      multi-tenant/vendor design, testing strategy, DevOps/environments,
      frontend architecture, error handling & resilience, billing design,
      deployment topologies [SaaS + deferred on-premises/licensing],
      coding standards, and an 11-phase build roadmap by module)
- [x] Operational Flows, Leasing Design, and Rent Collection Design added to
      the Handbook (Handbook/operational_flows.md, leasing_design.md,
      rent_collection_design.md) — Maintenance and Inspection journeys
      formalised end-to-end; two new modules specified (Leasing: vacancy
      listing through screening and e-signature lease execution; Rent
      Collection: schedule/payment tracking only, no payment processing)
- [x] BRD v0.2 (Requirements/Trellis_BRD_v0.2.docx) — folds in BDR-BIL-001-008
      (Section 5.12, Platform Billing) and BDR-LIC-001-007 (Section 9.1, triggered
      on-premises capability); reconciled against Handbook/roadmap.md, which was
      itself corrected to build Ai (BRD Must-have) before Billing (Should-have)
- [x] BRD v0.3 (Requirements/Trellis_BRD_v0.3.docx) — adds the Prospective
      Tenant actor (Section 3), Section 5.13 (Leasing & Vacancy Management,
      BDR-LSE-001-007) and Section 5.14 (Rent Collection — Tracking,
      BDR-RENT-001-006), and updates the Section 9.2 Roadmap table; fully
      reconciled against Handbook/leasing_design.md, rent_collection_design.md,
      operational_flows.md, and roadmap.md
- [x] Handbook gap review — Notifications module added (13th module,
      Handbook/notifications_design.md), pulled into Phase 1 since Auth's own
      password reset depends on it; self-service password reset and session
      revocation, and file-upload malware/type scanning, added to
      Handbook/security_guide.md; Prospective Tenant email verification made
      explicit in Handbook/leasing_design.md — BDR-NOTIF,
      BDR-SEC-005-007, and BDR-LSE-008 requirement sets, real as of BRD v0.4
- [x] Handbook gap review, part 2 — Support module added (14th module,
      Handbook/support_design.md), deliberately minimal and Triggered/Not
      Scheduled rather than phase-built; optimistic-concurrency convention
      (xmin/RowVersion) added to Handbook/coding_standards.md as a Phase 0
      baseline; WCAG 2.1 AA accessibility target added to
      Handbook/frontend_architecture.md; audit log retention/archival plan
      (partitioned from day one, archival deferred) added to
      Handbook/data_model.md — BDR-SUP-001-004 and
      BDR-NFR-006-008 requirement sets, real as of BRD v0.4
- [x] Handbook gap review, part 3 — Tenant Offboarding and Vendor
      Relationship Termination added to Handbook/multi_tenant_design.md
      (the inverse of tenant provisioning, previously unspecified); MFA
      recovery codes, auth-endpoint rate limiting, and refresh-cookie/CSRF
      hardening added to Handbook/security_guide.md — BDR-TEN-006,
      BDR-VEN-006, BDR-SEC-008-009, and BDR-NFR-009 requirement sets, real
      as of BRD v0.4
- [x] Handbook gap review, part 4 — security response headers, a log
      PII-redaction policy, and an incident response/breach notification
      commitment added to Handbook/security_guide.md; CI dependency/
      vulnerability scanning (Dependabot + CodeQL) added to
      Handbook/devops_environments.md; a stale migration-order
      cross-reference in that same file (out of sync with
      Handbook/data_model.md) was caught and fixed — BDR-SEC-010
      and BDR-NFR-010-011 requirement sets, real as of BRD v0.4. Remaining
      gaps are now narrower/more procedural than structural.
- [x] BRD v0.4 (Requirements/Trellis_BRD_v0.4.docx) — folds in all four
      handbook gap review rounds: Section 5.15 (Platform Notifications,
      BDR-NOTIF-001-006) and Section 5.16 (Cross-Tenant Support Ticketing,
      BDR-SUP-001-004) as new sections; elaborations to Section 5.1 (Tenant
      Offboarding, BDR-TEN-006), Section 5.4 (Vendor Relationship
      Termination, BDR-VEN-006), Section 5.11 (Security & Compliance,
      BDR-SEC-005 through 010), Section 5.13 (Leasing, BDR-LSE-008), and
      Section 6 NFRs (BDR-NFR-006 through 011); fully reconciled against
      the Handbook, including two pre-existing stale cross-references
      (Handbook/billing_design.md, Handbook/rent_collection_design.md) that
      predated this reconciliation pass
- [x] Handbook gap review, part 5 — per-module billing entitlements added (`billing_design.md`'s
      Module Entitlements section, `trellis_billing.module_entitlement` table, BDR-BIL-009);
      OpenAPI specs grouped per module (`api_standards.md`); strongly-typed `{Module}Options`
      configuration pattern replacing raw `IConfiguration` reads (`coding_standards.md`).
      Prompted by a product-direction question on billing by module, not a gap review —
      reaffirmed the modular-monolith decision in `project_architecture.md` rather than
      reversing it; standalone per-module services remain deferred to the existing extraction
      triggers.
- [x] BRD v0.5 (Requirements/Trellis_BRD_v0.5.docx) — adds BDR-BIL-009 to Section 5.12
      (Platform Billing & Subscription Management), elaborating BDR-BIL-001-008 with per-module
      billing entitlements; updates the Section 9.2 Roadmap table's Phase 1 MVP line. Reconciled
      against the engineering handbook's fifth round (`Handbook/billing_design.md`,
      `Handbook/project_architecture.md`, `Handbook/api_standards.md`,
      `Handbook/coding_standards.md`, `Handbook/frontend_architecture.md`,
      `Handbook/error_handling.md`, `Handbook/data_model.md`, `Handbook/roadmap.md`)
- [ ] Early design partner conversations
- [ ] Application scaffolding (.NET solution + React SPA)

## Last updated

19 September 2026 (updated)
