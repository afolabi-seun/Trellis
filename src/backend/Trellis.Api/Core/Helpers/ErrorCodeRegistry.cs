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

        /// <summary>TRL-E-TEN-2002 — custom domains are an ENTERPRISE-tier capability and the caller's tenant isn't on that tier.</summary>
        public const string CustomDomainRequiresEnterprise = "TRL-E-TEN-2002";

        /// <summary>TRL-E-TEN-2003 — the requested slug is already used by another tenant (or is a reserved word).</summary>
        public const string SlugUnavailable = "TRL-E-TEN-2003";

        /// <summary>TRL-E-TEN-2004 — the domain isn't a valid, registrable-looking hostname, or is the platform's own host/subdomain.</summary>
        public const string InvalidDomain = "TRL-E-TEN-2004";

        /// <summary>TRL-E-TEN-2005 — VerifyAsync: the DNS TXT record wasn't found (yet), or the domain is already verified by another tenant.</summary>
        public const string DomainVerificationFailed = "TRL-E-TEN-2005";

        /// <summary>TRL-E-TEN-2006 — DataSubjectService.EraseAsync: the subject is the caller's own login, or the tenant's last active TENANT_ADMIN.</summary>
        public const string CannotEraseSelfOrLastAdmin = "TRL-E-TEN-2006";
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

        /// <summary>TRL-E-AST-3003 — starting a lease on a unit that already has an ACTIVE lease (friendly pre-check ahead of the partial unique index).</summary>
        public const string UnitAlreadyLeased = "TRL-E-AST-3003";

        /// <summary>TRL-E-AST-3004 — a lease term that doesn't hold together: a fixed term without an end date or ending on/before its start, an end date on a month-to-month term, or recording NOT_RECORDED as a term.</summary>
        public const string InvalidLeaseTerm = "TRL-E-AST-3004";

        /// <summary>TRL-E-AST-3005 — ending a lease without a valid BDR-LSE-011 reason and party, before its start date, or with reason OTHER and no note.</summary>
        public const string InvalidLeaseEnd = "TRL-E-AST-3005";
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

    public static class Vendor
    {
        /// <summary>TRL-E-VEN-7000 — an Archive/status-transition request the entity's current status doesn't allow.</summary>
        public const string InvalidStatusTransition = "TRL-E-VEN-7000";

        /// <summary>TRL-E-VEN-7001 — BDR-VEN-006: no ACTIVE TenantVendorRelationship exists for the caller's tenant and the target vendor (never engaged, or already terminated).</summary>
        public const string NoActiveVendorRelationship = "TRL-E-VEN-7001";

        /// <summary>TRL-E-VEN-7002 — IAuthService.CreateVendorUserAsync failed (e.g. the username was already taken by an unrelated account).</summary>
        public const string VendorUserCreationFailed = "TRL-E-VEN-7002";
    }

    public static class Inspection
    {
        /// <summary>TRL-E-INS-8000 — a status-transition request the inspection's current status doesn't allow.</summary>
        public const string InvalidStatusTransition = "TRL-E-INS-8000";

        /// <summary>TRL-E-INS-8001 — neither or both of {propertyId, unitAssetId} were set; an inspection attaches to exactly one.</summary>
        public const string MultipleAttachmentTargets = "TRL-E-INS-8001";

        /// <summary>TRL-E-INS-8002 — the uploaded photo's actual bytes (magic-number sniffed) aren't JPG/PNG/HEIC (security_guide.md's narrower inspection-photo allow-list).</summary>
        public const string FileTypeNotAllowed = "TRL-E-INS-8002";

        /// <summary>TRL-E-INS-8003 — the caller isn't this photo's uploader, and scan_status isn't CLEAN yet.</summary>
        public const string PhotoNotYetVisible = "TRL-E-INS-8003";

        /// <summary>TRL-E-INS-8004 — VendorAssignmentService.CreateAsync: the target inspection already has an internal-staff assignee (BDR-INSP-001's assignment is mutually exclusive).</summary>
        public const string AlreadyStaffAssigned = "TRL-E-INS-8004";
    }

    public static class Ai
    {
        /// <summary>TRL-E-AI-9000 — billing_design.md: the Ai module is reserved for PROFESSIONAL+ tenants; the caller's tenant is still on STARTER.</summary>
        public const string TierNotEntitled = "TRL-E-AI-9000";

        /// <summary>TRL-E-AI-9001 — ConfirmAsync called on an extraction job that isn't in a confirmable state (already Confirmed, or still Pending).</summary>
        public const string ExtractionAlreadyReviewed = "TRL-E-AI-9001";

        /// <summary>TRL-E-AI-9002 — LinkAsync called on a routing suggestion already linked to a WorkflowInstance.</summary>
        public const string RoutingSuggestionAlreadyLinked = "TRL-E-AI-9002";
    }

    /// <summary>api_standards.md's registry table: Billing | TRL-E-BIL | 10000–10999 — a 5-digit range, unlike every earlier module's 4-digit codes.</summary>
    public static class Billing
    {
        /// <summary>TRL-E-BIL-10000 — RequiresModuleEntitlementAttribute: the caller's tenant doesn't have this module ACTIVE.</summary>
        public const string ModuleNotEntitled = "TRL-E-BIL-10000";

        /// <summary>TRL-E-BIL-10001 — a Subscription/Invoice/ModuleEntitlement transition the entity's current status doesn't allow.</summary>
        public const string InvalidStatusTransition = "TRL-E-BIL-10001";

        /// <summary>TRL-E-BIL-10002 — an automatic charge was attempted but the tenant has no default payment method on file.</summary>
        public const string NoDefaultPaymentMethod = "TRL-E-BIL-10002";

        /// <summary>TRL-E-BIL-10003 — IPaymentGateway.ChargeAsync returned a decline/error result.</summary>
        public const string ChargeFailed = "TRL-E-BIL-10003";

        /// <summary>TRL-E-BIL-10004 — IPaymentGateway.VerifyTransactionAsync couldn't confirm the card capture (expired/invalid reference, or the card itself was declined).</summary>
        public const string CardVerificationFailed = "TRL-E-BIL-10004";
    }

    /// <summary>
    /// api_standards.md's registry table documents Reporting as TRL-E-RPT | 9000–9999, but Ai's
    /// own real shipped codes already occupy TRL-E-AI-9000/9001/9002 — a pre-existing doc/
    /// implementation drift from Phase 6, confirmed via direct read of this file, not fixed here.
    /// Reporting's own codes start at 9100 to avoid that collision.
    /// </summary>
    public static class Reporting
    {
        /// <summary>TRL-E-RPT-9100 — an OwnerReport/ReportShareLink transition its current status doesn't allow.</summary>
        public const string InvalidStatusTransition = "TRL-E-RPT-9100";

        /// <summary>TRL-E-RPT-9101 — DeliverByEmailAsync called for an Owner with no email on file.</summary>
        public const string OwnerHasNoEmail = "TRL-E-RPT-9101";
    }

    /// <summary>api_standards.md's registry table: Leasing | TRL-E-LSE | 11000–11999 — the doc reserves the block but enumerates no individual codes.</summary>
    public static class Leasing
    {
        /// <summary>TRL-E-LSE-11000 — a VacancyListing/Application/ApplicationLinkToken transition its current status doesn't allow.</summary>
        public const string InvalidStatusTransition = "TRL-E-LSE-11000";

        /// <summary>TRL-E-LSE-11001 — CreateAsync: the target UnitAsset already has a LISTED/UNDER_APPLICATION listing (friendly pre-check ahead of the partial unique index).</summary>
        public const string UnitAssetAlreadyListed = "TRL-E-LSE-11001";

        /// <summary>TRL-E-LSE-11002 — SubmitAsync: the resolved token's listing is LEASED or WITHDRAWN, no longer accepting applications.</summary>
        public const string ListingNotAcceptingApplications = "TRL-E-LSE-11002";

        /// <summary>TRL-E-LSE-11003 — SendForSignatureAsync called on an application that isn't APPROVED, or already sent.</summary>
        public const string ApplicationNotReadyForSignature = "TRL-E-LSE-11003";

        /// <summary>TRL-E-LSE-11004 — UploadPhotoAsync: the uploaded file's actual bytes aren't JPG/PNG/HEIC, or the listing is already at MaxPhotosPerListing.</summary>
        public const string PhotoRejected = "TRL-E-LSE-11004";

        /// <summary>TRL-E-LSE-11005 — SendForSignatureAsync: the chosen draft isn't a LEASE document on this application's unit (the executed lease is filed as its next version, so it must be one).</summary>
        public const string DraftDocumentInvalid = "TRL-E-LSE-11005";

        /// <summary>TRL-E-LSE-11006 — CreateAsync (VacancyListing): the target unit is still OCCUPIED, so it can't be listed until its current lease is ended.</summary>
        public const string UnitNotVacant = "TRL-E-LSE-11006";
    }

    /// <summary>api_standards.md's registry table: Rent Collection | TRL-E-RENT | 12000–12999 — the doc reserves the block but enumerates no individual codes.</summary>
    public static class RentLedger
    {
        /// <summary>TRL-E-RENT-12000 — a RentSchedule/RentPaymentRecord transition its current status doesn't allow (ending an ended schedule, voiding a voided record).</summary>
        public const string InvalidStatusTransition = "TRL-E-RENT-12000";

        /// <summary>TRL-E-RENT-12001 — CreateAsync: the unit already has an ACTIVE rent schedule (friendly pre-check ahead of the partial unique index).</summary>
        public const string UnitAlreadyHasActiveSchedule = "TRL-E-RENT-12001";

        /// <summary>TRL-E-RENT-12002 — RecordAsync/UpdateAsync against an ENDED schedule.</summary>
        public const string ScheduleNotActive = "TRL-E-RENT-12002";

        /// <summary>TRL-E-RENT-12003 — CreateAsync on a unit with no active lease, without a lease term: the schedule would start a lease, and BDR-LSE-009 needs its term.</summary>
        public const string LeaseTermRequired = "TRL-E-RENT-12003";

        /// <summary>TRL-E-RENT-12004 — EndAsync on a schedule whose lease was started manually, without the BDR-LSE-011 end details that ending its tenancy needs.</summary>
        public const string LeaseEndDetailsRequired = "TRL-E-RENT-12004";
    }
}
