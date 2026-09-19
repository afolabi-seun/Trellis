# Trellis Engineering Handbook

The architecture and specification set for Trellis, written before any application code, per
the decision to lock down guidelines first. Modelled on a 5-document reference handbook for an
unrelated, mature .NET 8 microservices platform ("OMS") — each file below states explicitly
where it follows that reference and where (and why) Trellis deliberately diverges.

Read in this order:

1. **[Project Architecture](./project_architecture.md)** — modular monolith rationale, repo
   structure, module boundaries, the 4-layer pattern every module follows, and the named
   triggers for extracting a module into its own service later.
2. **[Data Model](./data_model.md)** — the 9-schema Postgres design, core table definitions,
   Row-Level Security policies, and EF Core conventions.
3. **[API Standards](./api_standards.md)** — response envelope, versioning, error code
   registry, RBAC roles, pagination, idempotency, and OpenAPI specs grouped per module.
4. **[Security Guide](./security_guide.md)** — authentication flows (including self-service
   password reset, session revocation, and MFA recovery codes), auth-endpoint rate limiting,
   refresh-cookie/CSRF hardening, security response headers, a log PII-redaction policy, JWT
   structure, file-upload malware/type scanning, secrets management, encryption, regional
   compliance (POPIA, Kenya DPA, NDPR, GDPR), and an incident response & breach notification
   commitment.
5. **[Multi-Tenant Design](./multi_tenant_design.md)** — the 3-layer tenant isolation model,
   and the full **Vendor cross-tenant identity** design: the technical implementation of
   Trellis's core product differentiator (a single Vendor login usable across multiple PM
   company tenants, scoped only to explicitly assigned work items). Also covers the two
   lifecycle gaps a handbook review found provisioning had no inverse for: Tenant Offboarding
   and Vendor Relationship Termination.
6. **[Testing Strategy](./testing_strategy.md)** — the test pyramid, the multi-tenant
   isolation test suite (the highest-value tests in the project), and BDR-to-test traceability.
7. **[DevOps & Environments](./devops_environments.md)** — environment separation, branching,
   CI/CD pipeline, hosting recommendation, configuration/secrets per environment, and
   observability basics.
8. **[Frontend Architecture](./frontend_architecture.md)** — the React + TypeScript SPA
   serving all three personas (Tenant Admin, Vendor, Super Admin) via role-based routing,
   API integration, auth flow, and the design system carried over from the pitch deck.
9. **[Error Handling & Resilience](./error_handling.md)** — how an exception becomes the API's
   error envelope, the `ServiceResult<T>` failure-mode table, validation, resilience for
   external dependencies (the AI provider, email/SMS), and how the SPA renders each failure.
10. **[Billing Design](./billing_design.md)** — the 10th module: usage-based invoicing,
    per-module billing entitlements, Paystack integration, dunning, and PCI scope — built
    against the BDR-BIL requirement set, real as of BRD v0.2 (BDR-BIL-001–008) and BRD v0.5
    (BDR-BIL-009, module entitlements).
11. **[Deployment Topologies](./deployment_topologies.md)** — the second, deliberately
    deferred deployment shape: single-tenant on-premises, license-file based instead of
    Paystack billing, same codebase and RLS model as SaaS — specified now, built only once a
    real prospect makes it a deal condition.
12. **[Coding Standards](./coding_standards.md)** — language and tooling baseline, Domain
    purity rules for the Clean Architecture layering in Project Architecture, async/DI/DTO
    conventions, the strongly-typed `{Module}Options` configuration pattern, repository query
    encapsulation, and the code review checklist.
13. **[Build Roadmap](./roadmap.md)** — eleven phases (0 through 10) sequencing thirteen of
    Trellis's fourteen modules and their matching frontend slices, from foundation through
    Rent Collection, with an explicit exit criterion per phase and the capabilities
    deliberately left triggered rather than scheduled.
14. **[Operational Flows](./operational_flows.md)** — the Maintenance Request and Inspection
    journeys drawn end-to-end, connecting BDRs already scattered across Workflow, Vendor, AI,
    Document, and Inspection into one traceable story per flow, including the offline-capture
    walkthrough for Inspection.
15. **[Leasing Design](./leasing_design.md)** — the 11th module: vacancy listings through
    application intake, third-party screening, and e-signature-based lease execution, plus a
    new secure-link-based actor (Prospective Tenant) — built against the BDR-LSE requirement
    set, real as of BRD v0.3 (BDR-LSE-001–007) and BRD v0.4 (BDR-LSE-008).
16. **[Rent Collection Design](./rent_collection_design.md)** — the 12th module: recurring rent
    schedules, manually recorded payments, and arrears flagging — deliberately tracking only,
    with no payment processing or trust accounting — built against the BDR-RENT requirement
    set, real as of BRD v0.3.
17. **[Notifications Design](./notifications_design.md)** — the 13th module, and a foundation
    rather than a late addition: the shared email/SMS dispatch, per-tenant template
    override, and delivery audit trail that five-plus other BDRs across this handbook had been
    silently assuming existed. Pulled into Phase 1 of the build roadmap, alongside self-service
    password reset, session revocation, and file-upload malware scanning (all in
    `security_guide.md`) and a Prospective Tenant email-verification correction in
    `leasing_design.md` — four gaps a handbook review surfaced, built against the
    BDR-NOTIF / BDR-SEC-005–007 / BDR-LSE-008 requirement set, real as of BRD v0.4.
18. **[Support Design](./support_design.md)** — the 14th module, and the smallest by design: a
    minimal, cross-tenant support ticket for the Platform Operator, deliberately without SLA
    timers, routing automation, or a knowledge base. Along with an optimistic-concurrency
    convention (`coding_standards.md`), a WCAG 2.1 AA accessibility target
    (`frontend_architecture.md`), and an audit-log retention/archival plan (`data_model.md`),
    this closes the last four items from the same handbook gap review — the two smaller-but-real
    ones (concurrency, accessibility) and the two lower-urgency ones (audit retention, support),
    built against the BDR-SUP-001–004 / BDR-NFR-006–008 requirement set, real as of BRD v0.4.
    Support itself is Triggered, Not Scheduled in the build roadmap, not a numbered phase.

**A third handbook gap review** added no new numbered document, but closed four more real
gaps directly inside the documents above: MFA recovery codes, auth-endpoint rate limiting, and
refresh-cookie/CSRF hardening (all in `security_guide.md`, item 4), and the two lifecycle gaps
Tenant Provisioning had no inverse for — Tenant Offboarding and Vendor Relationship Termination
(both in `multi_tenant_design.md`, item 5). Built against the BDR-SEC-008/009 / BDR-NFR-009 /
BDR-TEN-006 / BDR-VEN-006 requirement set, real as of BRD v0.4, same as every other BDR folded
into this handbook.

**A fourth handbook gap review** closed what was left in the same vein, all inside
`security_guide.md` (item 4): standard security response headers, a blanket log-redaction
policy replacing a couple of one-off "never logged" mentions, and — the one genuinely
substantive gap this round found — an incident response and breach notification commitment,
since the Compliance section named GDPR/POPIA/NDPR but never said what Trellis does if it's the
one breached. A CI dependency/vulnerability scanning gate was also added to
`devops_environments.md`, needing no BDR of its own, the same way the existing Lint step
doesn't. Built against the BDR-SEC-010 / BDR-NFR-010–011 requirement set, real as of BRD v0.4.
This round also caught and fixed a stale cross-reference in `devops_environments.md`'s
migration order, which had drifted out of sync with the canonical order in `data_model.md`.

By this fourth round, the remaining gaps found were narrower and more procedural than
structural — a sign this handbook is nearing the point of diminishing returns for further gap
review before real application code exists. All four rounds' BDRs are now folded into
`Requirements/Trellis_BRD_v0.4.docx`, the current BRD version — see that document's Revision
History for the full reconciliation trail from v0.1 through v0.4. The one exception is
BDR-LIC-001–007 (on-premises/licensing capability, `deployment_topologies.md`), which remains
genuinely proposed and deferred, not part of any BRD version yet, until a qualifying enterprise
deal triggers it.

**A fifth round**, prompted by a product-direction question (bill tenants per module, not just
per tier or usage) rather than a handbook gap review, added: per-module billing entitlements
(`billing_design.md`'s new "Module Entitlements" section and `trellis_billing.module_entitlement`
table, `project_architecture.md`'s Module Catalogue `Billable` column, BDR-BIL-009); OpenAPI
specs grouped per module rather than one undifferentiated document (`api_standards.md`); and a
strongly-typed `{Module}Options` configuration pattern replacing scattered raw `IConfiguration`
reads (`coding_standards.md`, cross-referenced from `devops_environments.md`). This round
explicitly reaffirmed, rather than reversed, the modular monolith decision in
`project_architecture.md` — module-level billing turned out to need a billing-schema and
authorization addition, not a deployment-architecture change; standalone per-module services
remain deferred to the same extraction triggers as before. BDR-BIL-009 is real as of
`Requirements/Trellis_BRD_v0.5.docx`, folded in the same day as this round, per that document's
own Revision History.

**A sixth round**, found during Phase 0 scaffolding itself rather than a design-time review: a
dev-only Postgres password ended up committed in `appsettings.Development.json`, exposing that
the appsettings/User-Secrets/cloud-secrets-manager split (`devops_environments.md`'s
Configuration & Secrets section) was two systems to keep straight, and nothing actually enforced
"no secrets in this file." Replaced with one mechanism everywhere: environment variables — `.env`
(gitignored) locally via `DotNetEnv`, the hosting platform's own env var config in
Development/Staging/Production. `appsettings.json` is removed from the codebase entirely.
`coding_standards.md`'s `{Module}Options` pattern and `security_guide.md`'s `ISecretProvider`
(now one `EnvironmentSecretProvider` implementation for every environment, not a
User-Secrets/cloud-SDK split) both updated to match. No BDR — a configuration-mechanism fix, the
same way the Lint and Dependabot additions in earlier rounds carried none.

**A seventh round**, prompted by explicit direction that the eventual real repository will check
in only what's under `src/`: `Trellis.Tests` colocated under `src/backend/` (matching
`src/frontend/tests/`'s existing pattern, not a top-level `tests/` folder); `.editorconfig` and
`.gitignore` split so only genuinely cross-cutting rules stay at the workspace root, with
language-specific rules colocated per app (the root files were later removed entirely, per
explicit direction, despite the tradeoff — `.env`/OS/IDE files outside `src/` are no longer
gitignored, and EditorConfig has no `root = true` boundary in this workspace); and
`Directory.Build.props`, `Trellis.sln`, `.github/` (CI workflows, Dependabot), and
`infrastructure/` (docker-compose) all moved inside `src/`, with every path inside them rewritten
relative to `src/` as its own root. `src/` is now genuinely self-contained — see
`project_architecture.md`'s Repository Structure for the full tree and reasoning. No BDR — a
repository-layout decision, not a requirement.

Every technical decision here traces back to a BDR ID in `Requirements/Trellis_BRD_v0.4.docx`
(the current version; earlier IDs trace back through v0.1–v0.3 as noted per document above).
Where an implementation detail extends or narrows a BDR, that's called out in the relevant
document rather than left implicit.

## What's deliberately deferred, and why

Several things in the OMS reference are *not* in this handbook yet, on purpose, not by
oversight — each has a stated trigger for revisiting it:

| Deferred | Reference has it via | Trellis revisits when |
|----------|------------------------|---------------------------|
| Independent microservices | 6 deployed services | A module hits one of the 3 extraction triggers in `project_architecture.md` |
| HashiCorp Vault | Full Vault cluster, dynamic secrets, Transit encryption | Per-tenant cryptographic isolation becomes a real compliance requirement |
| Per-tenant encryption keys | Vault Transit per tenant | Same trigger as above |
| Redis (sessions, idempotency, caching) | Used throughout | Measured latency or scale problem that a Postgres-backed store can't absorb |
| API gateway (Kong), rate limiting | Kong Gateway | External/API-key clients beyond Vendor logins are scoped |
| Service-to-service JWT | `ServiceJwtFactory` per service | The first module extraction (see above) |
| Outbound webhooks | HMAC-signed webhook delivery | A concrete BDR for notifying tenant systems is scoped |
| A genuinely native app (app-store distribution, native push) | N/A — no reference equivalent | App-store presence or native push notifications specifically become a requirement. Offline capture itself is **not** deferred — it's Phase 5, Must-have, built as a PWA (see `frontend_architecture.md`) |
| On-premises deployment packaging, licensing, and support tooling | N/A — no reference equivalent | An ENTERPRISE-tier prospect makes on-premises an explicit deal condition (see `deployment_topologies.md`) |
| Full tenant rent payment processing (funds movement, trust/escrow accounting) | N/A — no reference equivalent | An explicit business/legal decision to pursue trust-accounting licensing in each operating jurisdiction — not an engineering trigger (see `rent_collection_design.md`) |
| Full white-labelling (custom domains, full UI branding) | N/A — no reference equivalent | Only the notification-branding slice is built now (`notifications_design.md`); the rest revisits when an ENTERPRISE-tier prospect makes it a deal condition |
| Marketing/re-engagement email campaigns | N/A — no reference equivalent | `notifications_design.md` is scoped to transactional notifications only; a marketing use case needs its own consent/unsubscribe design before reusing this module |
| In-app support ticketing | N/A — no reference equivalent | Email-based support becomes operationally unmanageable, or a design partner explicitly asks for it (see `support_design.md`) — fully specified, deliberately not phase-scheduled |
| Audit log archival automation (partition detach + cold storage) | N/A — no reference equivalent | Real production data volume, or a specific tenant's confirmed retention requirement, makes it worth building — the table is partitioned from day one either way (see `data_model.md`) |

This table is the honest map of "what we borrowed, what we didn't, and under what condition
we would."
