# Billing Design

Multi-tenancy and billing are two separate concerns that meet at exactly one place: the
`trellis_tenant.tenant` table's `subscription_tier` column, stubbed in `data_model.md` from
the start but never actually wired to money changing hands. This document closes that gap —
it's a 10th module, with no equivalent anywhere in the OMS reference (a notification platform
doesn't bill its own tenants for usage the way a PM-facing SaaS does).

**A note on traceability before anything else**: `Requirements/Trellis_BRD_v0.1.docx` had no
Billing domain — its 11 domains stopped at Security/NFRs. The BDR-BIL requirements below were
folded into `Requirements/Trellis_BRD_v0.2.docx` (Section 5.12) as a real, traceable
requirement set — no longer provisional.

**BDR-BIL requirements (real as of BRD v0.2)**:

| ID | Requirement |
|----|-------------|
| BDR-BIL-001 | The platform shall track active unit/asset count per tenant, per billing period, as the basis for usage-based billing |
| BDR-BIL-002 | The platform shall generate one invoice per tenant per billing cycle, itemised by metered usage |
| BDR-BIL-003 | The platform shall support a single reusable, tokenised payment method per tenant for automatic recurring billing |
| BDR-BIL-004 | The platform shall retry a failed automatic payment per a defined dunning schedule before suspending a tenant |
| BDR-BIL-005 | The platform shall never store raw payment card data — payment credentials are represented only as gateway-issued tokens |
| BDR-BIL-006 | A Tenant Admin shall be able to view their tenant's invoice history and current payment method |
| BDR-BIL-007 | A Super Admin shall be able to view platform-wide billing and revenue status across all tenants |
| BDR-BIL-008 | The platform shall process payment gateway webhook events idempotently, deduplicated by the gateway's own event ID |

**BDR-BIL-009** (real as of `Requirements/Trellis_BRD_v0.5.docx`): *"The system shall support
enabling or disabling individual modules per tenant, and reflect each enabled paid module as its
own invoice line item, independent of usage-based unit metering."* This is the mechanism behind
"billing by module" below.

---

## Decisions Already Made

Per your direction: **Paystack** as the payment gateway (strong South Africa and Kenya
coverage in one integration), and **usage-based billing** — a tenant is charged per active
unit/asset under management, not a flat fee per tier.

This means `subscription_tier` (STARTER/PROFESSIONAL/ENTERPRISE) is repurposed as a **feature
and support tier**, decoupled from the actual invoice amount — it still gates which
capabilities a tenant can use (e.g., the Ai module reserved for PROFESSIONAL+), but the money
owed each cycle is driven entirely by metered usage, not by which tier row a tenant happens to
be on. This is a deliberate resolution of what would otherwise be a conflict between the
existing schema field and the usage-based model you chose — worth confirming this matches your
intent for how tiers should work going forward.

---

## Module Entitlements (Billing By Module)

Per your direction, individual modules — not just tiers — need to be independently billable, so
a tenant can add (and be charged for) e.g. Leasing or Vendor management without upgrading their
whole tier. This sits alongside, not instead of, the tier and usage-based models above:

- **Tier** (`subscription_tier`) still sets the *default bundle* of modules a new tenant starts
  with, and remains the gate for support level.
- **Usage-based billing** (active unit/asset count) still drives the base invoice amount.
- **Module entitlement** is the new, third axis: a tenant can have any module individually
  enabled or disabled, each optionally carrying its own flat add-on fee, independent of both of
  the above.

This does **not** require standalone/microservice deployment per module to achieve — it's a
data-driven access check inside the existing modular monolith, in the same spirit as the
tier-gating already in place. See `project_architecture.md`'s "Path to Extracting a Microservice
Later" for the (separate, later, trigger-based) question of when a module actually needs to run
as its own deployable service; that question and "can we bill per module" are independent, and
this section resolves the billing one now without waiting on the other.

### Data Model Addition

```
trellis_billing.module_entitlement — one row per (tenant, module) pair
```

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| entitlement_id | UUID | PK | |
| tenant_id | UUID | NOT NULL | |
| module_key | VARCHAR(30) | NOT NULL | The module's upper-snake-case key, as used by `[RequiresModuleEntitlement(...)]` — shipped keys: `INSPECTION`, `AI` (both default-free), `LEASING`, `RENT_COLLECTION` (add-ons with no default entitlement) |
| status | VARCHAR(20) | NOT NULL DEFAULT 'ACTIVE' | `ACTIVE`\|`DISABLED` |
| monthly_price | NUMERIC(10,2) | NOT NULL DEFAULT 0 | Flat add-on fee while `ACTIVE`; `0` for a module bundled into the tenant's tier at no extra charge |
| enabled_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |
| disabled_at | TIMESTAMPTZ | NULLABLE | |

`UNIQUE (tenant_id, module_key)`. Row-Level Security: same `tenant_isolation` policy as every
other table in this schema.

Which modules are core (bundled into every tier, not separately billable) versus optional
add-ons is a pricing decision, not an engineering one — this schema makes it tenant-editable
data (set by a Super Admin, per `IBillingService`, not hardcoded), rather than picking the split
in code. The confirmed split (`project_architecture.md`'s Module Catalogue `Billable` column):

- **Core** — Auth, Tenant, Notification, PropertyAsset, Reporting, Document, Workflow, Vendor.
  Auth/Tenant/Notification/PropertyAsset/Reporting are foundational plumbing no tenant use case
  works without. Document, Workflow, and Vendor are Core for a sharper reason: gating them would
  break the product for whoever hasn't paid for them. Document is a hard dependency of other
  modules (Leasing's e-signature flow, Inspection's photos, Ai's extraction jobs all need it to
  exist), so an add-on that depended on it could end up sold-but-nonfunctional. Workflow is the
  departmental routing/approval engine named in the platform's own product description — without
  it, a tenant has a property list, not Trellis. Vendor is the cross-tenant identity model
  `multi_tenant_design.md` calls **the platform's core product differentiator**; gating the thing
  the product is sold on undercuts every sales conversation before a prospect has paid for it.
- **Add-on** — Inspection, Ai, Leasing, Rent Collection. Each is a genuinely separable capability
  a tenant can operate the platform without: Inspection has its own offline-PWA investment and a
  plausible "add it later" story; Ai already carries its own variable inference cost and is
  tier-gated today (see below) — textbook add-on economics; Leasing and Rent Collection are
  workflows some PM companies outsource (to a leasing brokerage, or existing accounting software)
  rather than run inside Trellis at all.
- **N/A, not in the entitlement table** — Billing and Support. Neither is a capability a tenant
  chooses to turn on: Billing is the mechanism doing the charging, and Support is the Platform
  Operator's own ops tooling, not tenant-facing product surface. "Core or Add-on" is a category
  error for both.

### Enforcement

```csharp
// Billing/Application/Interfaces/IBillingService.cs
public interface IBillingService
{
    // ...existing members...
    Task<bool> IsModuleEntitledAsync(Guid tenantId, string moduleKey);
}
```

```csharp
// Core/Attributes/RequiresModuleEntitlementAttribute.cs — same pattern as the RBAC role
// attributes in api_standards.md, resolved via IBillingService as a plain in-process call
[HttpGet]
[RequiresModuleEntitlement("LEASING")]
public async Task<IActionResult> GetVacancyListings() => ...
```

A tenant without the module enabled gets `403` with a dedicated error code in the `TRL-E-BIL`
range (`api_standards.md`'s registry, 10000–10999) distinguishing "not entitled" from an RBAC
failure — the frontend uses this to show an upsell/"enable this module" prompt instead of a bare
permission-denied message.

### Invoicing

Each cycle's invoice gets one `invoice_line_item` per usage-based charge (unchanged) plus one
additional line per `ACTIVE` module entitlement with a non-zero `monthly_price` — e.g. `"142
active units × R{rate}"` alongside `"Leasing module — R{flat_fee}/mo"`. Disabling a module
mid-cycle takes effect at the next billing period, not prorated, for the same "simplest version
that's still accurate" reason `usage_snapshot` isn't prorated today.

---

## Module Placement

**Billing** is its own module (`trellis_billing` schema), not folded into Tenant, because it
owns a genuinely distinct concern — payment gateway integration, invoicing, and dunning — with
its own external dependency and its own compliance surface (see PCI note below). It sits in the
Module Catalogue from `project_architecture.md` alongside the other nine:

| Module | Owns (schema) | Key BDR References | Notes |
|--------|--------------|----------------------|-------|
| **Billing** | `trellis_billing` | BDR-BIL-001–008 (real as of BRD v0.2) | Usage metering, invoicing, Paystack integration, dunning |

Like every other module, Billing exposes its capabilities to the rest of the app only through
its own Service interface (`IBillingService`) — the Tenant module calls it to check a tenant's
payment status before letting an admin action proceed, never by querying `trellis_billing`
directly.

---

## Data Model

```
trellis_billing.subscription        — one row per tenant: cycle anchor, currency, status
trellis_billing.payment_method      — tokenised card reference (never raw card data)
trellis_billing.usage_snapshot      — active unit/asset count captured per billing period
trellis_billing.invoice             — one per tenant per cycle
trellis_billing.invoice_line_item   — itemised usage lines on an invoice
trellis_billing.payment_transaction — each attempt against the gateway, success or failure
trellis_billing.webhook_event       — every gateway webhook received, for idempotency
```

### trellis_billing.subscription

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| subscription_id | UUID | PK | |
| tenant_id | UUID | NOT NULL, UNIQUE | One active subscription per tenant |
| status | VARCHAR(20) | NOT NULL DEFAULT 'TRIALING' | TRIALING\|ACTIVE\|PAST_DUE\|SUSPENDED\|CANCELED |
| currency | CHAR(3) | NOT NULL | ISO 4217 — derived from `tenant.country_code` at provisioning (ZAR, KES). The country→currency map itself lives in `Core/Helpers/CurrencyDefaults` (`TenantResponse.DefaultCurrency` and `rent_collection_design.md`'s Owner-report figures reuse it — one map, not one per module) |
| billing_cycle_anchor_day | SMALLINT | NOT NULL | Day of month the cycle rolls over |
| created_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |

### trellis_billing.usage_snapshot

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| snapshot_id | UUID | PK | |
| tenant_id | UUID | NOT NULL | |
| billing_period_start | DATE | NOT NULL | |
| billing_period_end | DATE | NOT NULL | |
| active_unit_count | INT | NOT NULL | `COUNT(*)` of `trellis_property.unit_asset` where `status = 'ACTIVE'` for this tenant, captured at period end |
| captured_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |

Captured once at cycle-end by a scheduled job (not continuously metered) for Phase 1 — this is
the simplest version of BDR-BIL-001 that's still accurate; proration for mid-cycle unit
changes is an explicit non-goal until real usage data shows customers need it.

### trellis_billing.invoice / invoice_line_item

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| invoice_id | UUID | PK | |
| tenant_id | UUID | NOT NULL | |
| subscription_id | UUID | FK → subscription | |
| billing_period_start / end | DATE | NOT NULL | |
| currency | CHAR(3) | NOT NULL | |
| subtotal_amount, tax_amount, total_amount | NUMERIC(12,2) | NOT NULL | |
| status | VARCHAR(20) | NOT NULL DEFAULT 'DRAFT' | DRAFT\|OPEN\|PAID\|FAILED\|VOID |
| due_at, paid_at | TIMESTAMPTZ | NULLABLE | |

`invoice_line_item` (invoice_id FK, description, quantity, unit_amount, amount) holds the
itemised breakdown — for Phase 1, one line: `"142 active units × R{rate}"`.

### trellis_billing.payment_method

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| payment_method_id | UUID | PK | |
| tenant_id | UUID | NOT NULL | |
| provider | VARCHAR(20) | NOT NULL DEFAULT 'PAYSTACK' | |
| provider_authorization_code | VARCHAR(255) | NOT NULL | Paystack's reusable authorization token — **not a card number** |
| card_last4 | CHAR(4) | NULLABLE | Display only |
| card_brand | VARCHAR(20) | NULLABLE | Display only |
| is_default | BOOLEAN | NOT NULL DEFAULT TRUE | |

Satisfies BDR-BIL-005 by construction: there is no column in this schema capable of holding a
full card number, CVV, or expiry — the table simply doesn't have the shape to violate that
requirement.

### trellis_billing.payment_transaction / webhook_event

`payment_transaction` (transaction_id, invoice_id, tenant_id, provider_reference, amount,
status, provider_response_json) records every charge attempt. `webhook_event`
(webhook_event_id, provider_event_id UNIQUE, event_type, payload_json, received_at,
processed_at) is what makes webhook processing idempotent — see below.

### Row-Level Security

Same `tenant_isolation` policy as every other tenant-owned table (`data_model.md`). No Vendor
visibility clause anywhere in this schema — a Vendor has no billing relationship with any
tenant and should never be able to construct a query that reaches this schema at all.

---

## Paystack Integration

### Charging a Tenant

```csharp
// Billing/Services/PaymentGatewayService.cs
public interface IPaymentGateway
{
    Task<ChargeResult> ChargeAsync(string authorizationCode, decimal amount, string currency, string reference);
    Task<TokenizeResult> InitializeCardCaptureAsync(Guid tenantId, string email);
    Task<VerifyResult> VerifyTransactionAsync(string reference);
}

// Paystack-specific implementation — the only class that knows about Paystack's API shape
public class PaystackGateway : IPaymentGateway { /* ... */ }
```

`IPaymentGateway` is the seam that makes "not sure yet, decide later" from the provider
question still cheap to change: the rest of the Billing module (invoice generation, dunning
scheduling) depends on this interface, never on Paystack's SDK directly. Swapping to
Flutterwave later, if a market Paystack doesn't cover well shows up, means one new
implementation class, not a Billing module rewrite.

`PaystackGateway` is written for real (typed `HttpClient`, `ResiliencePolicies.ExternalProviderPolicy`
retries, secret `PaystackSecretKey` via `ISecretProvider` per call) but is **not verified against a
live Paystack sandbox** — no real test-mode credentials exist yet, so `NoOpPaymentGateway` stays the
default registration (`Modules:Billing:PaymentGateway` = `NoOp` | `Paystack` in
`BillingModuleExtensions`) in every environment until a real Paystack account exists. A network fault
is retried and, if still failing, reported back as a failed `ChargeResult`/`VerifyResult` rather than
thrown — `DunningRetryBackgroundService`'s loop over many tenants has no per-tenant try/catch, so one
tenant's Paystack outage must never abort the whole run; `InitializeCardCaptureAsync` is the
exception, since it has no "failed" shape to return today and surfaces a gateway fault as a genuine
500 instead.

Card capture happens via Paystack's own hosted flow (Inline/Popup) — Trellis's frontend never
handles a raw card number at any point, satisfying BDR-BIL-005 at the UI layer too, not just
the database layer. Paystack returns a reusable `authorization_code` after the first
successful charge, retrieved by `VerifyTransactionAsync` (Paystack's own `GET
transaction/verify/{reference}`) once the applicant returns from the hosted page — this is what
gets stored in `payment_method`.

### Inbound Webhook Handling

Paystack notifies Trellis of `charge.success`, `charge.failed`, and subscription lifecycle
events via webhook — this is the reverse direction from the outbound webhooks `api_standards.md`
deferred (Trellis has no outbound webhook consumers yet, but it does need to *receive* them
from its payment gateway now):

```csharp
[HttpPost("webhooks/paystack")]
[AllowAnonymous]  // Paystack can't authenticate as a Trellis user — verified by signature instead
public async Task<IActionResult> HandlePaystackWebhook()
{
    var signature = Request.Headers["x-paystack-signature"];
    var body = await new StreamReader(Request.Body).ReadToEndAsync();

    if (!_signatureValidator.IsValid(body, signature))
        return Unauthorized();  // Not a ServiceResult path — this never reaches a Service

    var eventId = ExtractEventId(body);
    if (await _webhookEventRepository.ExistsAsync(eventId))
        return Ok();  // Already processed — Paystack retries webhooks; this is BDR-BIL-008

    await _webhookEventRepository.RecordAsync(eventId, body);
    await _billingService.ProcessWebhookEventAsync(body);
    return Ok();
}
```

Signature verification (HMAC-SHA512 with the Paystack secret key, from `ISecretProvider` per
`security_guide.md`) happens before anything else runs — an unverified payload never reaches
business logic. This endpoint is intentionally outside the standard JWT auth scheme (there's no
Trellis user on the other end) but is not "unauthenticated" in the loose sense the Security
Checklist warns about — the signature check *is* its authentication.

---

## Dunning

On `charge.failed`: the subscription moves to `PAST_DUE`, and up to 2 further retries are
attempted over the following 7 days (day 2, day 5). If all retries fail, the subscription and
tenant both move to `SUSPENDED` — this is the one place Billing reaches into the Tenant module,
through `ITenantService.SuspendAsync(tenantId, reason)`, never by writing to
`trellis_tenant.tenant` directly. Every transition is audit-logged
(`AUDIT_TYPE = SUBSCRIPTION_PAST_DUE`, `TENANT_SUSPENDED_NONPAYMENT`) per the existing audit
registry in `data_model.md`.

Reactivation on a subsequent successful charge reverses both the subscription and tenant status
automatically — no manual intervention required for the common case of a tenant updating an
expired card.

---

## Compliance Note: PCI Scope

Because Trellis never receives, transmits, or stores a raw card number — card capture happens
entirely inside Paystack's hosted UI, and only the resulting token is ever seen by Trellis —
the platform's PCI DSS scope is the lightest tier (SAQ-A), not the full merchant scope that
would apply to a system that touches card data directly. This is worth stating explicitly in
`security_guide.md`'s compliance table once this module ships, since it's a direct consequence
of BDR-BIL-005 and the payment_method schema shape above, not a separate control to build.

---

## Frontend

A single **Billing settings page** in the Tenant Admin area of `trellis-web` (per
`frontend_architecture.md`'s persona routing): current usage and projected invoice amount,
invoice history with downloadable receipts, and payment method management (adding/replacing a
card via Paystack's hosted widget, never a Trellis-built card form). Not reachable from the
Vendor or Super Admin route trees — Super Admin gets a separate, platform-wide revenue view
under `/platform/billing`, which is genuinely different data (aggregate across tenants), not
the same page reused. `/platform/` (vs. `/api/v1/`) is `api_standards.md`'s convention for
exactly this shape of endpoint — `SUPER_ADMIN`-only, cross-tenant by nature.

---

## Testing

Per `testing_strategy.md`'s conventions, the highest-value additions here: a test proving a
duplicated webhook delivery (same `provider_event_id` twice) only processes once (BDR-BIL-008),
a test proving `usage_snapshot` counts are correctly tenant-scoped (a snapshot job bug that
double-counts or cross-counts units is a direct billing error, not just a data bug), and a test
proving a disabled module's endpoints return the dedicated `403` for a tenant lacking that
entitlement while remaining fully reachable for a tenant that has it (BDR-BIL-009).

---

## Related Documentation

- [Data Model](./data_model.md) — schema conventions and RLS this module follows
- [Project Architecture](./project_architecture.md) — module boundary rules; the `IPaymentGateway` seam mirrors the dual-mode module hosting pattern; the Module Catalogue's `Billable` column for the confirmed core/add-on/N-A split
- [Security Guide](./security_guide.md) — `ISecretProvider` for the Paystack secret key; PCI scope note
- [Error Handling & Resilience](./error_handling.md) — webhook signature failures, gateway timeout retries
- [Frontend Architecture](./frontend_architecture.md) — the Billing settings page and Paystack hosted card capture
- [Deployment Topologies](./deployment_topologies.md) — how on-premises deployments replace this module's Paystack flow with a signed license file
- [Rent Collection Design](./rent_collection_design.md) — a module that deliberately does **not** use `IPaymentGateway` or share any code with this one, and why that separation matters
- [Build Roadmap](./roadmap.md) — Phase 7, sequenced after Ai specifically because the BRD marks AI Must-have and Billing Should-have
