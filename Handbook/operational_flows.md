# Operational Flows

Two of Trellis's core day-to-day journeys — a maintenance request and a property inspection —
are already fully covered by requirements scattered across several BDR domains (Workflow,
Vendor, AI, Document, Inspection), but neither has ever been drawn as one connected story. This
document does that: no new requirements, no new modules, just making an existing, implicit
design explicit and traceable end-to-end.

---

## Maintenance Request Flow

### Why This Isn't Its Own Module

The most important design decision here is what Trellis does *not* build: a dedicated
`Maintenance` module with its own schema. A maintenance request has no shape of its own beyond
being a specific way of using four modules that already exist:

```
Workflow    → the approval/routing engine a maintenance request runs through
Vendor      → who actually does the work, if it's external
Ai          → how the request gets triaged to the right destination
Document    → invoices, completion photos, and evidence attached to it
```

Building a separate Maintenance module would mean re-implementing routing, assignment, and
audit logging that Workflow and Vendor already do generically — the same reasoning
`project_architecture.md` uses to justify *not* building nine microservices before there's a
reason to. A maintenance request is a `workflow_instance` whose department is Maintenance,
optionally carrying a `vendor_assignment`, with `document` and `audit_record` rows attached the
same way any other workflow's evidence would be. Nothing about "maintenance" needs a table of
its own.

### The Flow

```
 ┌──────────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
 │  1. ORIGIN   │ ──▶ │  2. TRIAGE   │ ──▶ │  3. EXECUTE  │ ──▶ │  4. CLOSE    │
 └──────────────┘     └──────────────┘     └──────────────┘     └──────────────┘
```

**1. Origin** — a maintenance request enters the system one of three ways:
- A Department Staff user logs it directly against a Property/Unit/Asset.
- It's raised as a finding from a completed Inspection (BDR-INSP-004's structured report links
  directly to a new workflow instance rather than requiring re-entry).
- It arrives as an inbound request (e.g., a tenant complaint relayed by staff) and is triaged
  automatically by the Ai module (BDR-AI-003) to the Maintenance department or a Vendor
  category.

**2. Triage/Routing** — the Workflow engine (BDR-WF-002) routes the request based on
configured criteria: department, property, cost threshold, or request type. If AI triage was
used, its suggestion is presented for confirmation, not auto-applied silently — a human can
always override (BDR-AI-004), and the override is logged.

**3. Execution** — one of two paths:
- **Internal**: a Department Staff member handles it within the Workflow engine's step
  tracking.
- **External**: a `vendor_assignment` row links the work item to a Vendor (BDR-VEN-003), who
  updates status, uploads evidence (photos, invoices, completion notes), and communicates
  within the scope of that one work item — see `multi_tenant_design.md` for how a Vendor's
  visibility stays scoped to exactly this assignment and nothing else.

**4. Close-out** — if the PM company has configured an approval/sign-off requirement
(BDR-VEN-005), a Department Staff or Tenant Admin user approves the closure; otherwise it
closes on Vendor/staff completion. Either way, the full state-transition history is captured in
the audit trail (BDR-WF-004), and any uploaded evidence is filed as a versioned Document
(BDR-DOC-001, BDR-DOC-002) linked back to the originating Property/Unit/Asset.

### What Makes This Traceable

Every step above already has a BDR ID; what was missing was the connective narrative. A test
suite verifying "a maintenance request works end-to-end" is really a composed test across
Workflow, Vendor, and Document test suites (`testing_strategy.md`) — this document is what a
QA engineer or a new team member reads to understand why those pieces fit together the way
they do, without having to reverse-engineer it from five separate BDR tables.

---

## Inspection Flow

Unlike Maintenance, Inspection **is** its own module — it has a genuinely distinct data shape
(checklist items, photos, structured comparison data) that doesn't reduce to Workflow or
Vendor. But it still composes with both, and with the newly-corrected offline architecture in
`frontend_architecture.md`, worth walking through end-to-end.

```
 ┌───────────────┐   ┌───────────────┐   ┌───────────────┐   ┌───────────────┐
 │ 1. SCHEDULE   │ ─▶│ 2. CAPTURE    │ ─▶│ 3. SYNC       │ ─▶│ 4. REPORT     │
 │               │   │  (may be      │   │  (if offline  │   │               │
 │               │   │  offline)     │   │  during 2)    │   │               │
 └───────────────┘   └───────────────┘   └───────────────┘   └───────────────┘
```

**1. Schedule** — an inspection is scheduled against a Property/Unit/Asset and assigned to
either internal staff or an external Vendor (BDR-INSP-001). If Vendor-assigned, it creates the
same kind of `vendor_assignment` row a maintenance work item would, reusing the identical
cross-tenant scoping mechanism from `multi_tenant_design.md` — an inspecting Vendor is scoped
exactly the same way an assigned-maintenance Vendor is.

**2. Capture** — the assigned party completes the inspection through the PWA-based mobile
interface: checklist responses, photo capture, geolocation tagging (BDR-INSP-002). This step
works fully offline, per BDR-INSP-003/BDR-NFR-001 and the corrected architecture in
`frontend_architecture.md` — data is written to IndexedDB the moment it's entered, not held in
memory waiting for a network check.

**3. Sync** — the moment connectivity returns, queued inspection data is submitted via the
same idempotent `POST` pattern every other Trellis write uses (`api_standards.md`), so a flaky
connection retrying the same submission never creates a duplicate inspection record.

**4. Report** — on completion, a structured inspection report generates automatically
(BDR-INSP-004), linked to the Property/Unit/Asset and visible to authorised users per the RBAC
and Vendor-scoping rules already established. Where a prior inspection exists for the same
asset, a comparison view is available (BDR-INSP-005, Could-priority) to show condition change
over time.

**If the inspection surfaces an issue** — a failed checklist item, a flagged defect — that's
the origin point for a Maintenance Request Flow (above): the inspection report links directly
into a new `workflow_instance` rather than requiring the finding to be manually re-typed into a
separate request.

---

## Related Documentation

- [Multi-Tenant Design](./multi_tenant_design.md) — the Vendor scoping mechanism both flows share
- [Project Architecture](./project_architecture.md) — why Maintenance composes existing modules instead of getting its own
- [Frontend Architecture](./frontend_architecture.md) — the PWA offline architecture Inspection's capture step depends on
- [Testing Strategy](./testing_strategy.md) — testing a composed flow across module boundaries
- [Build Roadmap](./roadmap.md) — which phases build each piece these flows compose
