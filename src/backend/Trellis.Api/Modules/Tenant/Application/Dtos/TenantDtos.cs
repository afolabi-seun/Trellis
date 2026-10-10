namespace Trellis.Api.Modules.Tenant.Application.Dtos;

/// <summary>api_standards.md's `POST /platform/tenants` request body.</summary>
/// <summary>Slug is optional — when omitted it is derived from the tenant name (see SlugPolicy).</summary>
public sealed record TenantProvisionRequest(string TenantName, string CountryCode, string AdminEmail, string? Slug = null);

public sealed record TenantSlugUpdateRequest(string Slug);

// No RowVersion field: coding_standards.md's UseXminAsConcurrencyToken()/RowVersion pattern
// applies to entities with an Update/Patch endpoint, and Tenant has neither in Phase 1 — only
// provisioning (create) and the offboard action endpoint, not a generic field update. The
// concurrency token is still active at the EF Core level regardless (TenantConfiguration);
// nothing here needs to round-trip it yet. Auth's password reset flow is where Phase 1 actually
// exercises and tests optimistic concurrency (coding_standards.md's negative-path requirement).
public sealed record TenantResponse(
    Guid TenantId,
    string TenantName,
    string CountryCode,
    string Status,
    string SubscriptionTier,
    DateTimeOffset CreatedAt,
    string Slug = "",
    string DefaultCurrency = "USD");

/// <summary>Internal resolution result — carries the TenantId, so it must never be returned from an anonymous endpoint (see <see cref="PublicTenantResponse"/>).</summary>
public sealed record TenantPublicInfo(Guid TenantId, string Name, string Slug);

/// <summary>What an anonymous visitor may learn about a tenant: its display name and slug. Never its id.</summary>
public sealed record PublicTenantResponse(string Name, string Slug);

public sealed record TenantDomainCreateRequest(string Domain);

/// <summary>The verification record is only populated while PENDING_VERIFICATION — once verified, the token has done its job.</summary>
public sealed record TenantDomainResponse(
    Guid TenantDomainId, string Domain, string Status, string? VerificationRecordName, string? VerificationRecordValue,
    DateTimeOffset? VerifiedAt, DateTimeOffset CreatedAt);

public sealed record NotificationSettingsDto(IReadOnlyList<string> ApplicationSubmittedRoles, IReadOnlyList<string> RentArrearsRoles);

/// <summary>BDR-LSE-010 — the tenant's own list; "Other" (with a note) is always offered as well and is never part of it.</summary>
public sealed record RenewalDeclineReasonsDto(IReadOnlyList<string> Reasons);

/// <summary>What staff can read about their own tenant's settings. DefaultCurrency comes from the tenant's country and defaults the currency on listing and rent-schedule forms.</summary>
public sealed record TenantSettingsResponse(
    string DefaultCurrency, string CountryCode, NotificationSettingsDto Notifications, IReadOnlyList<string> RenewalDeclineReasons);

public sealed record DataSubjectExportRequest(string SubjectEmail);

/// <summary>Keyed by IPersonalDataHandler.ModuleName; a module with nothing for this subject is simply absent from the bundle, not present with a null value.</summary>
public sealed record DataSubjectExportResponse(IReadOnlyDictionary<string, object> Modules);

/// <summary>ConfirmEmail must match SubjectEmail exactly (validated) — a typed-confirmation guard against erasing the wrong person by a single mistyped character. Reason is free text, kept only in the audit record's own detail_json, never against the subject's own erased data.</summary>
public sealed record EraseDataSubjectRequest(string SubjectEmail, string ConfirmEmail, string CurrentPassword, string? Reason);

public sealed record ModuleErasureCount(string ModuleName, int RecordsAffected);

public sealed record DataSubjectErasureResponse(IReadOnlyList<ModuleErasureCount> Counts);
