# Support / Helpdesk Design (Minimal Scope)

This module is the smallest by design, and it's worth saying so plainly: even now that
BDR-SUP-001–004 are real requirements (Section 5.16), the honest alternative — a shared support
inbox, or a lightweight external helpdesk tool — remains a legitimate way to run Platform
Operator support for a good while, which is exactly why this module is Triggered, Not
Scheduled rather than phase-built. This document keeps the shape deliberately small rather than
designing a full helpdesk product.

**Traceability note**: the BDR-SUP IDs below are folded into `Requirements/Trellis_BRD_v0.4.docx`
(Section 5.16), alongside BDR-NOTIF, BDR-SEC-005–010, BDR-LSE-008, and BDR-NFR-006–011. Being a
real, numbered requirement set doesn't change the module's build status — it's still Triggered,
Not Scheduled (`roadmap.md`), since "in the BRD" and "on the roadmap" are independent questions.

**BDR-SUP requirements (real as of BRD v0.4)**:

| ID | Requirement | Priority |
|----|-------------|----------|
| BDR-SUP-001 | The system shall allow a Tenant Admin or Department Staff user to submit a support ticket to the Platform Operator, categorised by type (billing, technical, licensing, other). | Must |
| BDR-SUP-002 | The Platform Operator (Super Admin) shall have visibility into support tickets across all tenants, from a single view. | Must |
| BDR-SUP-003 | The system shall notify the submitting user when their ticket's status changes. | Should |
| BDR-SUP-004 | A ticket may optionally be linked to a specific related record (e.g. an invoice, a license) for context, but such linkage shall never be required to submit a ticket. | Could |

---

## What This Deliberately Is Not

Stated up front, because it's easier to add scope to something small than to walk back scope
from something over-built:

- **No SLA timers or escalation rules.** A ticket has a status; nothing here tracks response-
  time commitments or auto-escalates a stale ticket. That's a real feature, just not this one.
- **No routing/assignment automation.** Tickets land in one Super-Admin-visible list; who picks
  up which one is a human process, not a system one, until real volume says otherwise.
- **No knowledge base or self-service deflection.** Those are product decisions about how much
  investment support deserves, made once there's usage data to justify them — building them
  speculatively now would be exactly the kind of premature infrastructure this handbook
  otherwise avoids (`project_architecture.md`'s recurring theme).
- **No live chat.** Async ticket + email notification only.

If email support turns out to be entirely sufficient for a good while, this document is simply
never built — see the roadmap placement below.

---

## Module Placement

**Support** is its own module (`trellis_support` schema) — small enough that folding it into
Tenant was considered and rejected for one reason: Tenant's schema is about a PM company's own
configuration, and a support ticket is fundamentally cross-tenant data from the Platform
Operator's point of view, which deserves its own boundary rather than becoming an odd corner of
an unrelated module.

| Module | Owns (schema) | Key BDR References | Notes |
|--------|--------------|----------------------|-------|
| **Support** | `trellis_support` | BDR-SUP-001–004 (real as of BRD v0.4) | Cross-tenant ticket visibility for the Platform Operator — see "What This Deliberately Is Not" above |

## Data Model

```
trellis_support.support_ticket   — one row per submitted ticket
```

### trellis_support.support_ticket

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| ticket_id | UUID | PK | |
| tenant_id | UUID | NOT NULL | The submitting user's tenant |
| submitted_by_user_id | UUID | FK → `trellis_auth.app_user` NOT NULL | |
| category | VARCHAR(20) | NOT NULL | `BILLING`\|`TECHNICAL`\|`LICENSING`\|`OTHER` |
| subject | VARCHAR(200) | NOT NULL | |
| description | TEXT | NOT NULL | |
| status | VARCHAR(20) | NOT NULL DEFAULT 'OPEN' | `OPEN`\|`IN_PROGRESS`\|`RESOLVED`\|`CLOSED` |
| related_entity_type | VARCHAR(30) | NULLABLE | e.g. `INVOICE`, `LICENSE` (BDR-SUP-004) |
| related_entity_id | UUID | NULLABLE | Polymorphic, resolved by `related_entity_type` |
| created_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |
| resolved_at | TIMESTAMPTZ | NULLABLE | |

**RLS**: no new policy shape needed. The standard `tenant_isolation` policy
(`multi_tenant_design.md`) already includes the `app.is_system` bypass for Super Admin, which
is exactly the cross-tenant visibility BDR-SUP-002 needs — a Tenant Admin sees their own
tenant's tickets, Super Admin sees all of them, with the identical policy every other
tenant-owned table uses. No Vendor-visibility clause; a Vendor has no relationship to platform
support.

---

## Notification Integration

Status-change notifications (BDR-SUP-003) go through `INotificationService`
(`notifications_design.md`), same as every other module — no separate email integration built
here, consistent with the entire point of that module existing.

---

## Roadmap Placement: Triggered, Not Scheduled

Unlike Leasing or Rent Collection, this module has no other module depending on it and no BDR
elsewhere assumes it exists — nothing else in the roadmap breaks if it's never built. It's
listed in `roadmap.md`'s "Triggered, Not Scheduled" table rather than given a numbered phase,
with a stated trigger: **email-based support becomes operationally unmanageable (volume,
losing track of open issues), or a design partner explicitly asks for in-app ticketing.**
Specified now, at low cost, precisely so that if that trigger fires there's a ready design
rather than a scramble.

---

## Related Documentation

- [Multi-Tenant Design](./multi_tenant_design.md) — the `app.is_system` bypass this module reuses unchanged, no new RLS shape required
- [Notifications Design](./notifications_design.md) — where the ticket status-change notification is actually sent from
- [Data Model](./data_model.md) — schema conventions this table follows
- [Build Roadmap](./roadmap.md) — Triggered, Not Scheduled table, and the stated trigger above
