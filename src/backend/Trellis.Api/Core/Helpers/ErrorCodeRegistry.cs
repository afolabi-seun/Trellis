namespace Trellis.Api.Core.Helpers;

/// <summary>
/// The single source of truth for every <c>TRL-E-{module}-{number}</c> error code
/// (api_standards.md, "Error Code Registry"). A Service never hardcodes an error message
/// directly — it resolves both code and message through here, so the same fault always
/// produces the same code.
///
/// Only the <see cref="Platform"/> bucket is populated in Phase 0 — codes aren't allocated
/// until a module actually needs one (api_standards.md's own stated principle), so the other
/// module buckets (Auth 1000–1999, PropertyAsset 3000–3999, ...) get added as each module is
/// built, not stubbed out empty ahead of time.
/// </summary>
public static class ErrorCodeRegistry
{
    public static class Platform
    {
        /// <summary>TRL-E-PLT-0001 — an unhandled exception caught by ExceptionHandlingMiddleware.</summary>
        public const string UnhandledFault = "TRL-E-PLT-0001";

        /// <summary>
        /// TRL-E-PLT-0404 — the module-agnostic "not found" code <see cref="Results.ServiceResultExtensions"/>
        /// uses for <see cref="Results.ServiceResultOutcome.NotFound"/>. Deliberately not module-specific:
        /// api_standards.md's own reasoning for returning 404 (not 403) out of tenant/vendor scope applies
        /// identically regardless of which module's resource was asked for.
        /// </summary>
        public const string NotFound = "TRL-E-PLT-0404";

        /// <summary>TRL-E-PLT-0400 — request-shape validation failure, before any module's business logic runs.</summary>
        public const string ValidationFailed = "TRL-E-PLT-0400";

        /// <summary>TRL-E-PLT-0409 — a stale RowVersion on update (coding_standards.md, "Optimistic Concurrency").</summary>
        public const string ConcurrencyConflict = "TRL-E-PLT-0409";
    }

    public static class Tenant
    {
        /// <summary>TRL-E-TEN-2000 — ProvisionTenantAsync's cross-module call into Auth failed; the tenant was rolled back, not left half-provisioned.</summary>
        public const string AdminCreationFailed = "TRL-E-TEN-2000";

        /// <summary>TRL-E-TEN-2001 — an offboard/status-transition request the tenant's current status doesn't allow.</summary>
        public const string InvalidStatusTransition = "TRL-E-TEN-2001";
    }

    public static class Auth
    {
        /// <summary>TRL-E-AUT-1000 — wrong username/password, or the account doesn't exist. Same code either way (no existence disclosure).</summary>
        public const string InvalidCredentials = "TRL-E-AUT-1000";

        /// <summary>TRL-E-AUT-1001 — account locked out after too many failed attempts (security_guide.md's lockout policy).</summary>
        public const string AccountLockedOut = "TRL-E-AUT-1001";

        /// <summary>TRL-E-AUT-1002 — an invalid, expired, or already-used partial token, TOTP code, or recovery code.</summary>
        public const string MfaVerificationFailed = "TRL-E-AUT-1002";

        /// <summary>TRL-E-AUT-1003 — the TOTP code presented to confirm enrollment didn't validate against the newly generated secret.</summary>
        public const string MfaEnrollmentConfirmationFailed = "TRL-E-AUT-1003";

        /// <summary>TRL-E-AUT-1004 — recovery-code regeneration requires re-authentication; the presented password didn't match.</summary>
        public const string ReauthenticationFailed = "TRL-E-AUT-1004";

        /// <summary>TRL-E-AUT-1005 — an invalid, expired, or already-used password reset token. Generic on purpose — never distinguishes which.</summary>
        public const string PasswordResetTokenInvalid = "TRL-E-AUT-1005";

        /// <summary>TRL-E-AUT-1006 — the refresh_token cookie is missing, doesn't match any session, or that session is revoked/expired.</summary>
        public const string RefreshTokenInvalid = "TRL-E-AUT-1006";
    }

    public static class PropertyAsset
    {
        /// <summary>TRL-E-AST-3000 — an Archive/status-transition request the entity's current status doesn't allow.</summary>
        public const string InvalidStatusTransition = "TRL-E-AST-3000";

        /// <summary>TRL-E-AST-3001 — can't archive an Owner while it still has an active Property (the FK would dangle in spirit, even though it's never hard-deleted).</summary>
        public const string OwnerHasActiveProperties = "TRL-E-AST-3001";

        /// <summary>TRL-E-AST-3002 — can't archive a Property while it still has an active UnitAsset (BDR-AST-003: documents/workflow attach at this level).</summary>
        public const string PropertyHasActiveUnitAssets = "TRL-E-AST-3002";
    }

    public static class Document
    {
        /// <summary>TRL-E-DOC-4000 — an Archive request the document's current status doesn't allow.</summary>
        public const string InvalidStatusTransition = "TRL-E-DOC-4000";

        /// <summary>TRL-E-DOC-4001 — the uploaded file's actual bytes (magic-number sniffed) don't match an allowed type for where it's being uploaded (security_guide.md's File Upload Security).</summary>
        public const string FileTypeNotAllowed = "TRL-E-DOC-4001";

        /// <summary>TRL-E-DOC-4002 — more than one of {propertyId, unitAssetId, ownerId} was set; a document attaches to at most one.</summary>
        public const string MultipleAttachmentTargets = "TRL-E-DOC-4002";

        /// <summary>TRL-E-DOC-4003 — the caller isn't this version's uploader, and scan_status isn't CLEAN yet (security_guide.md: invisible to everyone but the uploader until a scan clears).</summary>
        public const string DocumentNotYetVisible = "TRL-E-DOC-4003";
    }

    public static class Workflow
    {
        /// <summary>TRL-E-WF-5000 — an Archive/status-transition request the entity's current status doesn't allow.</summary>
        public const string InvalidStatusTransition = "TRL-E-WF-5000";

        /// <summary>TRL-E-WF-5001 — Approve/Reject called on a workflow instance that isn't IN_PROGRESS, or whose active step isn't the one being acted on.</summary>
        public const string NoStepAwaitingDecision = "TRL-E-WF-5001";

        /// <summary>TRL-E-WF-5002 — TenantAdminOrDepartmentLead: the caller is DEPARTMENT_STAFF but not a member of the active step's routed department.</summary>
        public const string NotAuthorizedForStepDepartment = "TRL-E-WF-5002";
    }
}
