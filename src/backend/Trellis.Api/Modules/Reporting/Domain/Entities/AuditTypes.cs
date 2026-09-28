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
    }

    public static class Auth
    {
        public const string AuthSuccess = "AUTH_SUCCESS";
        public const string AuthLockout = "AUTH_LOCKOUT";
        public const string PasswordReset = "PASSWORD_RESET";
        public const string SessionRevoked = "SESSION_REVOKED";
        public const string MfaRecoveryCodeUsed = "MFA_RECOVERY_CODE_USED";
        public const string MfaRecoveryCodesRegenerated = "MFA_RECOVERY_CODES_REGENERATED";
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
}
