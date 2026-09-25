namespace Trellis.Api.Core.Audit;

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
}
