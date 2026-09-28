# Rent Collection Design (Tracking Only)

This document specifies the second piece of new scope from the same conversation that produced
`leasing_design.md`, and it exists specifically because of a choice you made after being shown
the alternative. Trellis does not process, hold, or move tenant funds anywhere in this design —
it records rent schedules and payments that happened elsewhere (bank transfer, cash, an external
payment app) so a PM company has one place to see what's owed, what's paid, and what's overdue.

**Why not full payment processing.** The obvious next step after "track rent" is "collect rent" —
let a tenant pay through Trellis directly. That was on the table, and it's explicitly not what's
built here, because it changes what Trellis legally is. The moment Trellis holds tenant funds
before they reach an Owner, several jurisdictions treat that as trust/escrow accounting subject
to separate licensing and audit obligations — South Africa's PPRA trust account rules for
property practitioners are one concrete example. That's a real regulatory question, not an
engineering one, and it isn't answered here: *I'm not a lawyer and this isn't legal advice.* The
scope below is the one you chose specifically to stay out of that territory for now — see
"Triggered, Not Scheduled" at the end for what would need to be true before revisiting it.

**Traceability note**, same pattern as Leasing: the BDR-RENT IDs below were folded into
`Requirements/Trellis_BRD_v0.3.docx` (Section 5.14) alongside BDR-LSE.

**BDR-RENT requirements (real as of BRD v0.3)**:

| ID | Requirement | Priority |
|----|-------------|----------|
| BDR-RENT-001 | The system shall allow a recurring rent schedule (amount, currency, due day of month) to be defined for an occupied Unit/Asset, created automatically on lease execution (BDR-LSE-006). | Must |
| BDR-RENT-002 | The system shall allow authorised staff to record a rent payment received against a schedule, capturing amount, date received, and method (as free text/reference, not processed) — this is a record of an external event, not a transaction. | Must |
| BDR-RENT-003 | The system shall automatically flag a schedule as in arrears when no matching payment is recorded within a configurable grace period after the due date. | Must |
| BDR-RENT-004 | The system shall notify configured staff when a new arrears flag is raised. | Should |
| BDR-RENT-005 | The system shall include rent collection status (current, overdue, arrears count) in the Owner portfolio report (BDR-OWN-003/BDR-RPT-002). | Should |
| BDR-RENT-006 | The system shall not represent itself, in any UI text, document, or API response, as processing, holding, or transferring tenant funds — every payment-related screen and record shall be worded as a record of a payment that occurred outside the platform. | Must |

BDR-RENT-006 reads unusually for a requirement — it's a safeguard against scope creep in
wording, not behavior, and it's Must precisely because getting the language wrong (a button that
says "Pay Rent" instead of "Record Payment Received") is the exact kind of drift that turns a
tracking tool into something that looks, to a regulator or a confused tenant, like a payment
processor it isn't licensed to be.

---

## Module Placement

**Rent Collection** is its own module (`trellis_rentledger` schema — named deliberately unlike
Billing's `trellis_billing`, since confusing "what a tenant owes a PM company" with "what a PM
company owes Trellis" would be a real design error, not just a naming inconsistency). It depends
on Leasing (a schedule originates from a lease) and PropertyAsset, but has **no dependency on,
and no shared code with, `IPaymentGateway`** — Billing's Paystack integration exists to charge
PM companies for using Trellis; this module never touches a payment gateway at all, by design.

| Module | Owns (schema) | Key BDR References | Notes |
|--------|--------------|----------------------|-------|
| **Rent Collection** | `trellis_rentledger` | BDR-RENT-001–006 (real as of BRD v0.3) | Rent schedules, manually recorded payments, arrears flagging — no payment processing |

## Data Model

```
trellis_rentledger.rent_schedule          — recurring expected rent for one occupied Unit/Asset
trellis_rentledger.rent_payment_record    — one recorded (not processed) payment against a schedule
```

### trellis_rentledger.rent_schedule

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| rent_schedule_id | UUID | PK | |
| tenant_id | UUID | NOT NULL | |
| unit_asset_id | UUID | FK → `trellis_property.unit_asset` NOT NULL | |
| lease_application_id | UUID | FK → `trellis_leasing.application` NULLABLE | Set when created via BDR-LSE-006; nullable so a schedule can also be entered manually for a unit occupied before Leasing existed |
| rent_amount | NUMERIC(12,2) | NOT NULL | |
| currency | CHAR(3) | NOT NULL | |
| due_day_of_month | SMALLINT | NOT NULL CHECK (1–31) | |
| grace_period_days | SMALLINT | NOT NULL DEFAULT 5 | Configurable per BDR-RENT-003 |
| status | VARCHAR(20) | NOT NULL DEFAULT 'ACTIVE' | ACTIVE\|ENDED |
| start_date | DATE | NOT NULL | *Added in the Phase 10 build.* "How many periods are owed" is undefined without it. Lease-created schedules use the listing's `available_from`; manual ones are staff-entered. The first due date is the first `due_day_of_month` on/after this date. |
| last_arrears_alerted_period | DATE | NULLABLE | *Added in the Phase 10 build.* The due date of the latest past-grace unpaid period when the last arrears alert fired — a **per-period** dedupe key (see Arrears Flagging), not a once-ever timestamp |
| ended_at, created_by, created_at, updated_at | | | Standard bookkeeping; `created_by` is null only for system-created rows |

**Uniqueness (partial indexes):** at most one `ACTIVE` schedule per `unit_asset_id`, and at most one schedule per `lease_application_id` (which makes the lease-execution webhook's retry idempotent). A new lease on a unit with a still-`ACTIVE` schedule ends the prior one rather than failing — a signed lease is never blocked. A schedule created from a lease takes `due_day_of_month = min(available_from.day, 28)`; it is editable afterwards.

### trellis_rentledger.rent_payment_record

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| rent_payment_record_id | UUID | PK | |
| rent_schedule_id | UUID | FK → rent_schedule NOT NULL | |
| amount_received | NUMERIC(12,2) | NOT NULL | |
| date_received | DATE | NOT NULL | |
| method_reference | VARCHAR(255) | NULLABLE | Free-text, e.g. "EFT ref 88213" — never a card/account number (see below) |
| recorded_by_user_id | UUID | FK → `trellis_auth.app_user` NOT NULL | Who entered this record — required for the audit trail, since this is a manual claim of fact, not a system-observed event |
| recorded_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |
| tenant_id | UUID | NOT NULL | *Added in the Phase 10 build* — the design's own "standard `tenant_isolation`" RLS needs it on this table too |
| voided_at, voided_by, void_reason | TIMESTAMPTZ / UUID / VARCHAR(500) | NULLABLE | *Added in the Phase 10 build.* Amount, date, and reference are immutable; a mistaken entry is **voided** (excluded from every total, kept for audit), never edited or deleted — the app role has no DELETE grant |

**RLS**: standard `tenant_isolation` on both tables (`data_model.md`). No Vendor-visibility
clause — a Vendor has no relationship to rent collection, ever, same reasoning as Leasing.

**A note on `method_reference`**: this field is free text specifically so staff can note *how*
a payment arrived (bank transfer, cash, mobile money reference) without Trellis ever storing or
touching a card number, account number, or payment credential — there's no field shaped like
one, and `security_guide.md`'s PCI scope statement (Trellis never touches raw card data) holds
exactly as it does for Billing, for a different reason: Billing never touches it because Paystack
does; Rent Collection never touches it because nothing here processes a payment at all.

---

## Arrears Flagging

BDR-RENT-003's flag is computed, not stored as a separate mutable status a user sets — an
arrears flag is a query result (a scheduled job or on-read calculation, consistent with the
resilience patterns in `error_handling.md`): for a given `rent_schedule`, is there a
`rent_payment_record` covering the current due period, and has `due_day_of_month +
grace_period_days` passed without one? This keeps the two tables append-only and avoids a
correctness class of bug where a stored "is_overdue" flag drifts from the underlying records
after a late payment is entered.

**How "covers the current due period" is decided.** A cumulative model, not per-period matching: the total recorded as received (non-voided) is compared with the total rent due so far. `Current` = nothing due is unpaid; `Overdue` = something is unpaid but every unpaid period is still inside its grace window; `InArrears` = something is unpaid past its grace period. Late, partial, early, and multiple payments all just work with no allocation rules. A due day beyond a short month's length falls on that month's last day. The status is computed on read and by the alert job; no status is stored.

**Alert dedupe.** Unlike Document expiry (a once-ever `last_alerted_at`), a monthly recurring schedule must alert again when another period slips past grace, so `last_arrears_alerted_period` holds the latest past-grace unpaid due date at the last alert; a new alert fires only when that date advances. Recipients are the roles the tenant chose for rent-arrears alerts (Settings → Notifications, stored under `tenant.config_json.notifications.rentArrearsRoles`; default both `TENANT_ADMIN` and `DEPARTMENT_STAFF`, at least one required), read once per tenant per run; the job checks the tenant's `RENT_COLLECTION` entitlement before alerting.

BDR-RENT-004's staff notification reuses the same notification delivery mechanism Document
expiry alerts already use (`data_model.md`), rather than introducing a second notification
pathway for what's structurally the same kind of event: a scheduled check finding something that
needs a human's attention.

---

## What This Deliberately Does Not Do

- **No payment gateway integration.** `IPaymentGateway` (`billing_design.md`) is not referenced
  anywhere in this module, and no future refactor should quietly wire the two together — they
  answer different questions (what a PM company owes Trellis vs. what a tenant owes a PM
  company) and merging them would be exactly the confusion BDR-RENT-006 exists to prevent.
- **No trust/escrow accounting.** No ledger of held funds, no disbursement to Owners, no bank
  reconciliation — this module has no concept of Trellis being party to money moving anywhere.
- **No tenant-facing login or payment UI.** A tenant does not interact with Rent Collection at
  all in this design; every screen here is staff-facing (recording what a tenant already paid
  elsewhere), consistent with the "record of an external event" framing in BDR-RENT-002.

## Triggered, Not Scheduled: Full Payment Processing

A future version of this capability — a tenant pays rent through Trellis, funds move, Trellis
disburses to Owners — is a real, plausible product direction, and is deliberately left as an
unbuilt, untriggered option rather than a roadmap phase, following the same pattern
`deployment_topologies.md` and `frontend_architecture.md` use for on-premises deployment and a
native app:

| Capability | Trigger |
|------------|---------|
| Full tenant rent payment processing (funds movement, trust/escrow accounting) | An explicit business/legal decision to pursue the relevant licensing and compliance obligations in each operating jurisdiction (e.g. PPRA trust account registration in South Africa) — **not** an engineering readiness question. Nothing in this module's design blocks building it later; `IPaymentGateway`'s existence for Billing means the technical seam pattern is already proven, it's simply not applied here yet. |

This row belongs in `roadmap.md`'s own "Triggered, Not Scheduled" table too — see the
cross-reference there.

---

## Related Documentation

- [Leasing Design](./leasing_design.md) — where a rent schedule originates (BDR-LSE-006)
- [Data Model](./data_model.md) — schema conventions, RLS, and the notification mechanism reused for arrears alerts
- [Billing Design](./billing_design.md) — the `IPaymentGateway` pattern this module deliberately does not use, and why
- [Security Guide](./security_guide.md) — PCI scope statement, which holds here for a different reason than it does for Billing
- [Error Handling & Resilience](./error_handling.md) — the scheduled-job pattern arrears flagging follows
- [Operational Flows](./operational_flows.md) — how this module stays outside the Maintenance/Inspection flows entirely
- [Build Roadmap](./roadmap.md) — Phase 10
