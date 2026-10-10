namespace Trellis.Api.Modules.Reporting.Domain.Entities;

/// <summary>
/// The <c>audit_type</c> registry (data_model.md:225-228, extended by each module's own design
/// doc as needed — additive only, never renamed once shipped). Mirrors
/// <see cref="Helpers.ErrorCodeRegistry"/>'s shape: a static class per concern, so a caller never
/// hand-types a magic string.
/// </summary>
public static class AuditTypes
{
    public static class Tenant
    {
        public const string TenantProvisioned = "TENANT_PROVISIONED";
        public const string TenantOffboardingInitiated = "TENANT_OFFBOARDING_INITIATED";
        public const string TenantExportReady = "TENANT_EXPORT_READY";
        public const string TenantOffboarded = "TENANT_OFFBOARDED";

        /// <summary>billing_design.md's dunning state machine calls ITenantService.SuspendAsync — this is Tenant's own generic record of the transition; Billing separately records its own more specific TenantSuspendedNonpayment for its own audit trail.</summary>
        public const string TenantSuspended = "TENANT_SUSPENDED";

        public const string TenantReactivated = "TENANT_REACTIVATED";

        /// <summary>Renaming breaks every public link shared with the old slug, so it is always recorded (SUPER_ADMIN only).</summary>
        public const string TenantSlugChanged = "TENANT_SLUG_CHANGED";

        /// <summary>Who receives which alert changed — recorded with the role lists only (never any user data).</summary>
        public const string TenantSettingsUpdated = "TENANT_SETTINGS_UPDATED";

        public const string TenantDomainAdded = "TENANT_DOMAIN_ADDED";
        public const string TenantDomainVerified = "TENANT_DOMAIN_VERIFIED";
        public const string TenantDomainRemoved = "TENANT_DOMAIN_REMOVED";

        /// <summary>security_guide.md's Compliance table shape — detail_json carries a SHA-256 hash of the normalised subject email, per-module record counts, and the actor, never the email itself.</summary>
        public const string DataSubjectExported = "DATA_SUBJECT_EXPORTED";

        /// <summary>Irreversible. Same detail_json shape as DataSubjectExported, plus a lawful-basis note.</summary>
        public const string DataErasure = "DATA_ERASURE";
    }

    public static class Auth
    {
        public const string AuthSuccess = "AUTH_SUCCESS";
        public const string AuthLockout = "AUTH_LOCKOUT";
        public const string PasswordReset = "PASSWORD_RESET";
        public const string SessionRevoked = "SESSION_REVOKED";
        public const string MfaRecoveryCodeUsed = "MFA_RECOVERY_CODE_USED";
        public const string MfaRecoveryCodesRegenerated = "MFA_RECOVERY_CODES_REGENERATED";

        /// <summary>IAuthService.ReauthenticateAsync's own failure path — a wrong password entered for a narrow re-auth-gated action (e.g. viewing a decrypted screening report), not a full login attempt (that's AuthLockout's own territory).</summary>
        public const string ReauthenticationFailed = "REAUTHENTICATION_FAILED";
    }

    /// <summary>BDR-WF-004 — every routing/approval decision is audited, not just visible via WorkflowStepInstance's own history columns.</summary>
    public static class Workflow
    {
        public const string WorkflowInstanceCreated = "WORKFLOW_INSTANCE_CREATED";
        public const string WorkflowStepApproved = "WORKFLOW_STEP_APPROVED";
        public const string WorkflowStepRejected = "WORKFLOW_STEP_REJECTED";
    }

    /// <summary>data_model.md's audit_type registry names these two explicitly.</summary>
    public static class Document
    {
        public const string DocumentUploaded = "DOCUMENT_UPLOADED";
        public const string DocumentExpiryAlerted = "DOCUMENT_EXPIRY_ALERTED";
    }

    /// <summary>data_model.md's audit_type registry names all three explicitly.</summary>
    public static class Vendor
    {
        public const string VendorAssigned = "VENDOR_ASSIGNED";
        public const string VendorStatusUpdated = "VENDOR_STATUS_UPDATED";
        public const string VendorRelationshipTerminated = "VENDOR_RELATIONSHIP_TERMINATED";
    }

    public static class Inspection
    {
        public const string InspectionScheduled = "INSPECTION_SCHEDULED";
        public const string InspectionStatusUpdated = "INSPECTION_STATUS_UPDATED";
    }

    /// <summary>BDR-AI-004 — every extraction/routing decision is audited, not just visible via the entity's own reviewed_by/reviewed_at columns (Workflow's own AuditTypes precedent). Fires alongside, not instead of, the dedicated trellis_ai.override_log table for the override case specifically.</summary>
    public static class Ai
    {
        public const string ExtractionCompleted = "AI_EXTRACTION_COMPLETED";
        public const string ExtractionConfirmed = "AI_EXTRACTION_CONFIRMED";
        public const string RoutingSuggestionCreated = "AI_ROUTING_SUGGESTION_CREATED";
        public const string RoutingSuggestionLinked = "AI_ROUTING_SUGGESTION_LINKED";
        public const string SuggestionOverridden = "AI_SUGGESTION_OVERRIDDEN";
    }

    /// <summary>billing_design.md's dunning state machine names SubscriptionPastDue and TenantSuspendedNonpayment explicitly.</summary>
    public static class Billing
    {
        public const string SubscriptionCreated = "SUBSCRIPTION_CREATED";
        public const string SubscriptionPastDue = "SUBSCRIPTION_PAST_DUE";
        public const string SubscriptionReactivated = "SUBSCRIPTION_REACTIVATED";
        public const string TenantSuspendedNonpayment = "TENANT_SUSPENDED_NONPAYMENT";
        public const string InvoiceGenerated = "INVOICE_GENERATED";
        public const string InvoicePaid = "INVOICE_PAID";
        public const string PaymentMethodAdded = "PAYMENT_METHOD_ADDED";
        public const string ModuleEntitlementChanged = "MODULE_ENTITLEMENT_CHANGED";
        public const string WebhookEventProcessed = "WEBHOOK_EVENT_PROCESSED";
    }

    /// <summary>BDR-OWN-002/004 — Owner report generation and delivery, this module's own first use of the registry it now owns.</summary>
    public static class Reporting
    {
        public const string OwnerReportGenerated = "OWNER_REPORT_GENERATED";
        public const string OwnerReportDelivered = "OWNER_REPORT_DELIVERED";
        public const string ReportShareLinkCreated = "REPORT_SHARE_LINK_CREATED";
        public const string ReportShareLinkRevoked = "REPORT_SHARE_LINK_REVOKED";
    }

    /// <summary>BDR-LSE-007 — an application's lifecycle. Fires alongside, not instead of, the dedicated trellis_leasing.application_event table (Ai's OverrideLogEntry dual-write precedent).</summary>
    /// <summary>Lease records (BDR-LSE-009/011) — whichever add-on started the lease, the lease's own lifecycle is audited here once, not by each caller.</summary>
    public static class PropertyAsset
    {
        public const string LeaseStarted = "LEASE_STARTED";
        public const string LeaseEnded = "LEASE_ENDED";
        public const string LeaseTermRecorded = "LEASE_TERM_RECORDED";
        public const string LeaseRenewed = "LEASE_RENEWED";
    }

    public static class Leasing
    {
        public const string VacancyListingCreated = "VACANCY_LISTING_CREATED";
        public const string RenewalOffered = "RENEWAL_OFFERED";
        public const string RenewalAccepted = "RENEWAL_ACCEPTED";
        public const string RenewalDeclined = "RENEWAL_DECLINED";
        public const string RenewalExpired = "RENEWAL_EXPIRED";
        public const string VacancyListingWithdrawn = "VACANCY_LISTING_WITHDRAWN";
        public const string ApplicationSubmitted = "APPLICATION_SUBMITTED";
        public const string ApplicationApproved = "APPLICATION_APPROVED";
        public const string ApplicationDeclined = "APPLICATION_DECLINED";
        public const string LeaseSentForSignature = "LEASE_SENT_FOR_SIGNATURE";
        public const string LeaseExecuted = "LEASE_EXECUTED";

        /// <summary>BDR-LSE-004's "never expose full report detail" restriction — the one narrow, separately-audited exception, so viewing the decrypted report is always attributable even though nothing about the report content itself is ever written here (detail_json stays IDs-only, security_guide.md).</summary>
        public const string ScreeningReportViewed = "SCREENING_REPORT_VIEWED";
    }

    /// <summary>BDR-RENT-002/004 — every recorded (not processed) rent event. Rent payments are staff-entered records of an external event, so who recorded or voided one is part of the trail.</summary>
    public static class RentLedger
    {
        public const string RentScheduleCreated = "RENT_SCHEDULE_CREATED";
        public const string RentScheduleUpdated = "RENT_SCHEDULE_UPDATED";
        public const string RentScheduleEnded = "RENT_SCHEDULE_ENDED";
        public const string RentPaymentRecorded = "RENT_PAYMENT_RECORDED";
        public const string RentPaymentVoided = "RENT_PAYMENT_VOIDED";
        public const string RentArrearsFlagged = "RENT_ARREARS_FLAGGED";
        public const string RentChangeScheduled = "RENT_CHANGE_SCHEDULED";
    }
}
