# Notifications Design

Five requirements already assume this capability exists before this document does: a workflow
step needs to notify its assignee (BDR-WF-003), a document nearing expiry needs to alert
someone (BDR-DOC-003), a completed Owner report needs to reach an Owner (BDR-OWN-004), a rent
schedule falling into arrears needs to notify staff (BDR-RENT-004), and — as of the same gap
review that produced this document — a password reset needs to reach a user's inbox
(BDR-SEC-005) and a rejected file upload needs to tell its uploader (BDR-SEC-007). Until now,
every one of those was written as if "send an email" were free. `error_handling.md` even had a
resilience row for "Email/SMS gateway" as if it were a single already-designed thing. It wasn't
— it was five-plus modules each implicitly assuming someone else had built it. This document is
that module.

**Traceability note**, same pattern as Billing, Leasing, and Rent Collection before them: the
BDR-NOTIF IDs below are folded into `Requirements/Trellis_BRD_v0.4.docx` (Section 5.15),
alongside BDR-SEC-005–010 and BDR-LSE-008.

**BDR-NOTIF requirements (real as of BRD v0.4)**:

| ID | Requirement | Priority |
|----|-------------|----------|
| BDR-NOTIF-001 | The platform shall provide a single, shared mechanism for delivering an email or SMS notification to any recipient (staff, Owner, Vendor, or Prospective Tenant) on behalf of any other module, rather than each module integrating its own delivery. | Must |
| BDR-NOTIF-002 | The system shall support a platform-default template per notification type, overridable per tenant (subject, body, sender display name), so a Tenant Admin can present notifications under their own brand. | Should |
| BDR-NOTIF-003 | The system shall record every notification attempt — recipient, template, channel, status — for audit and delivery-troubleshooting purposes. | Must |
| BDR-NOTIF-004 | The system shall retry a failed delivery per a defined backoff policy before marking it permanently failed, and shall never silently drop a notification. | Must |
| BDR-NOTIF-005 | A Tenant Admin shall be able to configure which channel (email/SMS) a notification type is delivered on, wherever the underlying requirement allows a choice. | Could |
| BDR-NOTIF-006 | The system shall never block the triggering business operation on notification delivery succeeding — a notification failure is logged, never surfaced as an error on the underlying action. | Must |

BDR-NOTIF-006 is worth stating plainly because it's the failure mode this design is most at
risk of introducing by accident: a naive implementation that calls the email provider
synchronously inside, say, `WorkflowService.ApproveStepAsync` would make a flaky email provider
capable of failing a workflow approval. Nothing about a notification should ever be able to do
that — see Dispatch below.

---

## Module Placement

**Notification** is its own module (`trellis_notification` schema) — a leaf in one direction
(it depends on nothing else) and a foundation in the other (nearly every other module depends
on it). This makes it architecturally closer to Auth than to Billing or Leasing: it has to
exist before most other modules can be considered complete, not after.

| Module | Owns (schema) | Key BDR References | Notes |
|--------|--------------|----------------------|-------|
| **Notification** | `trellis_notification` | BDR-NOTIF-001–006 (real as of BRD v0.4) | Template resolution, delivery dispatch, delivery audit trail — no business logic of its own |

**Roadmap correction, flagged directly rather than folded in silently**: `roadmap.md`'s Phase 1
(Identity & Tenancy) now needs this module too — BDR-SEC-005's password reset email can't ship
without somewhere to send it from, and Phase 1 is where BDR-SEC-005 lives. Every later phase
(Document/Workflow in Phase 3, Leasing in Phase 9, Rent Collection in Phase 10) already assumed
a notification mechanism would exist by the time it needed one; this makes that assumption
concrete and pulls its actual build forward to Phase 1, alongside Tenant and Auth, rather than
leaving it to be discovered as a blocker when Phase 3 starts. `roadmap.md` is updated to match.

Every module reaches Notification through one interface, never `trellis_notification` directly
— identical discipline to every other cross-module boundary in `project_architecture.md`:

```csharp
// Notification/Application/Interfaces/INotificationService.cs
public interface INotificationService
{
    Task SendAsync(
        Guid? tenantId,               // null for a platform-level notification (e.g. Super Admin alerts)
        string templateKey,           // e.g. "PASSWORD_RESET", "DOCUMENT_EXPIRY_ALERT"
        string recipientAddress,      // email or phone number, per the template's channel
        object templateData,          // the values the template's placeholders are rendered against
        RelatedEntityRef? relatedEntity = null);   // e.g. (DOCUMENT, documentId) — for the audit trail
}
```

Every calling module treats this exactly like `IPaymentGateway` or `IScreeningProvider`: an
injected interface, never a direct dependency on a specific email/SMS vendor.

---

## Data Model

```
trellis_notification.notification_template     — platform-default + per-tenant-override templates
trellis_notification.notification_delivery     — one row per attempted send, for audit + retry
trellis_notification.tenant_notification_channel_pref — per-tenant channel choice (BDR-NOTIF-005)
```

### trellis_notification.notification_template

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| template_id | UUID | PK | |
| tenant_id | UUID | NULLABLE | `NULL` = platform default; a tenant-specific row overrides it (BDR-NOTIF-002) |
| template_key | VARCHAR(50) | NOT NULL | e.g. `PASSWORD_RESET`, `WORKFLOW_STEP_PENDING`, `DOCUMENT_EXPIRY_ALERT`, `OWNER_REPORT_READY`, `RENT_ARREARS_FLAGGED`, `APPLICATION_LINK`, `SCREENING_COMPLETE`, `FILE_SCAN_REJECTED`, `MFA_RECOVERY_CODE_USED`, `MFA_RECOVERY_CODES_REGENERATED`, `TENANT_OFFBOARDING_EXPORT_READY`, `VENDOR_RELATIONSHIP_TERMINATED` |
| channel | VARCHAR(10) | NOT NULL | `EMAIL`\|`SMS` |
| subject_template | TEXT | NULLABLE | Email only; placeholder syntax e.g. `{{tenantName}}` |
| body_template | TEXT | NOT NULL | |
| locale | VARCHAR(10) | NOT NULL DEFAULT 'en' | Room for per-market localisation later; not exercised until a non-English market is onboarded |

Unique on `(tenant_id, template_key, channel, locale)`. Resolution order: look for a row with
this tenant's `tenant_id`; if none, fall back to the `tenant_id IS NULL` platform default. A
brand-new tenant has zero rows here and gets fully working platform-default notifications from
day one — override is opt-in customisation, never a setup requirement.

### trellis_notification.notification_delivery

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| delivery_id | UUID | PK | |
| tenant_id | UUID | NULLABLE | `NULL` for a platform-level notification with no tenant context |
| recipient_type | VARCHAR(20) | NOT NULL | `APP_USER`\|`VENDOR_USER`\|`OWNER`\|`PROSPECTIVE_TENANT`\|`EXTERNAL` |
| recipient_address | VARCHAR(255) | NOT NULL | Email or phone — never logged elsewhere in plaintext beyond what's needed here |
| template_key | VARCHAR(50) | NOT NULL | |
| channel | VARCHAR(10) | NOT NULL | |
| status | VARCHAR(20) | NOT NULL DEFAULT 'QUEUED' | `QUEUED`\|`SENT`\|`FAILED`\|`BOUNCED` |
| provider_reference | VARCHAR(255) | NULLABLE | The provider's own message ID, for support lookups |
| related_entity_type | VARCHAR(30) | NULLABLE | e.g. `DOCUMENT`, `WORKFLOW_INSTANCE`, `APPLICATION`, `RENT_SCHEDULE` |
| related_entity_id | UUID | NULLABLE | Polymorphic, resolved by `related_entity_type` |
| created_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |
| sent_at | TIMESTAMPTZ | NULLABLE | |

This is the table BDR-NOTIF-003 and BDR-NOTIF-004 are built on — every send attempt, successful
or not, is a row here, which is what makes "did the arrears email actually go out" an ordinary
query instead of a support escalation guess.

### trellis_notification.tenant_notification_channel_pref

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| tenant_id | UUID | NOT NULL | |
| template_key | VARCHAR(50) | NOT NULL | |
| channel | VARCHAR(10) | NOT NULL | |
| enabled | BOOLEAN | NOT NULL DEFAULT TRUE | |

PK `(tenant_id, template_key, channel)`. Only consulted for template keys the Notification
module itself marks as "channel is a tenant choice" — most templates (password reset, a
security-relevant notice) are not overridable off entirely, only cosmetic/branding aspects are;
BDR-NOTIF-005 is a Could specifically because most notification types shouldn't be optional.

### Row-Level Security

Standard `tenant_isolation` on `notification_delivery` and `tenant_notification_channel_pref` —
no different from any other tenant-owned table. `notification_template` needs one adjustment,
since it deliberately holds rows that *aren't* tenant data:

```sql
CREATE POLICY tenant_or_platform_default ON trellis_notification.notification_template
  USING (
    current_setting('app.is_system', true) = 'true'
    OR tenant_id::text = current_setting('app.current_tenant_id', true)
    OR tenant_id IS NULL   -- every tenant can read the platform defaults it falls back to
  );
```

Text comparison, not `::uuid` — see `data_model.md`'s Row-Level Security section for why casting
the session variable to `::uuid` breaks for any request with no tenant context at all (an empty
`app.current_tenant_id`, not a `NULL` one, is what a no-context request actually produces).

This is a new RLS shape worth naming explicitly rather than reusing `tenant_isolation` or
`tenant_or_vendor_isolation` unchanged — it's "a tenant's own rows, plus the shared defaults,"
which is a different relationship than either existing policy expresses. Writes to a `tenant_id
IS NULL` row are restricted to `app.is_system` (platform-level template management), same
principle as the Super Admin bypass elsewhere.

---

## Provider Integration

```csharp
// Notification/Application/Interfaces/INotificationProvider.cs
public interface INotificationProvider
{
    Task<DeliveryResult> SendEmailAsync(string to, string subject, string htmlBody);
    Task<DeliveryResult> SendSmsAsync(string to, string message);
}
```

Same "interface first, provider later" reasoning as `IScreeningProvider` and `IFileScanner`:
email and SMS deliverability providers vary by market (an SMS gateway with strong South African
coverage may have weak Kenyan or Nigerian coverage), so the provider choice is deliberately not
fixed here. `INotificationService` (Application layer) resolves the template, renders it against
`templateData`, calls `INotificationProvider` (Infrastructure layer), and writes the
`notification_delivery` row — callers never see any of that, they only see `SendAsync`.

### Dispatch Is Never Synchronous-Blocking (BDR-NOTIF-006)

```csharp
// Application/Services/WorkflowService.cs — the calling pattern every module follows
public async Task<ServiceResult<WorkflowInstance>> ApproveStepAsync(Guid stepId, Guid approverId)
{
    var instance = await _repository.ApproveStepAsync(stepId, approverId);

    // Fire-and-forget through a queued background dispatch — never awaited inline with
    // the business transaction, and never able to turn a notification failure into a
    // failed approval
    _ = _notificationService.SendAsync(
        instance.TenantId, "WORKFLOW_STEP_PENDING", nextApprover.Email,
        new { instance.WorkflowName, StepName = nextStep.Name },
        new RelatedEntityRef("WORKFLOW_INSTANCE", instance.WorkflowInstanceId));

    return ServiceResult<WorkflowInstance>.Success(instance);
}
```

Concretely: `notification_delivery` starts at `QUEUED`, a background job (the same kind of
scheduled-job mechanism `rent_collection_design.md`'s arrears flagging uses) picks up queued
rows and actually calls `INotificationProvider`, applying the retry/backoff policy from
`error_handling.md`'s resilience table. The calling module's own transaction has already
committed by the time any of that runs — a notification can fail, retry, and eventually be
marked permanently `FAILED` without the workflow approval, document upload, or rent payment
record it was about ever being affected.

---

## Branding & Per-Tenant Customisation (BDR-NOTIF-002)

A Tenant Admin can override `subject_template`, `body_template`, and a sender display name
(e.g. "Acme Property Management" instead of "Trellis") per template key — this is the
notification-specific slice of the white-labelling question a growing platform eventually asks
in full; this document doesn't attempt the larger question (custom domains, full UI
white-labelling), only the part that's already needed because five other BDRs assume tenant-
branded communication is possible. Platform defaults ship in English with Trellis's own sender
identity, so a tenant that never touches this still gets fully working notifications.

## Consent Scope

Every template key in this document is transactional/operational (a password reset, a workflow
action needed, a document about to expire, rent falling into arrears) — none of it is
marketing communication, so the consent/unsubscribe machinery POPIA and the Kenya DPA require
for marketing messages doesn't apply here. Worth stating explicitly so a future marketing or
re-engagement email campaign doesn't get built on top of this table without that separate
compliance question being asked first.

---

## Related Documentation

- [Security Guide](./security_guide.md) — the password-reset, file-scan-rejection, and MFA-recovery-code-used notifications this module actually sends
- [Multi-Tenant Design](./multi_tenant_design.md) — the tenant-offboarding export-ready and Vendor-relationship-terminated notifications this module sends (BDR-TEN-006, BDR-VEN-006, real as of BRD v0.4)
- [Error Handling & Resilience](./error_handling.md) — the retry/backoff policy `notification_delivery` status transitions follow
- [Leasing Design](./leasing_design.md) — the application-link and screening-result emails (BDR-LSE-002, BDR-LSE-003)
- [Rent Collection Design](./rent_collection_design.md) — the arrears-flag notification (BDR-RENT-004)
- [Data Model](./data_model.md) — schema conventions and the new `tenant_or_platform_default` RLS shape introduced here
- [Project Architecture](./project_architecture.md) — the module boundary and interface-injection discipline this module follows like every other
- [Build Roadmap](./roadmap.md) — pulled into Phase 1, not a later phase, once BDR-SEC-005 made it a Phase 1 dependency
