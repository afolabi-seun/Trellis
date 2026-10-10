namespace Trellis.Api.Modules.Leasing.Application.Dtos;

public sealed record VacancyListingCreateRequest(Guid UnitAssetId, decimal RentAmount, string Currency, DateOnly AvailableFrom, string? Description);

public sealed record VacancyListingUpdateRequest(decimal RentAmount, string Currency, DateOnly AvailableFrom, string? Description, string RowVersion);

public sealed record VacancyListingPhotoResponse(Guid VacancyListingPhotoId, string FileName, string ScanStatus, int DisplayOrder, DateTimeOffset CreatedAt);

public sealed record VacancyListingResponse(
    Guid VacancyListingId, Guid UnitAssetId, decimal RentAmount, string Currency, DateOnly AvailableFrom,
    string? Description, string Status, IReadOnlyList<VacancyListingPhotoResponse> Photos, string RowVersion, DateTimeOffset CreatedAt);

public sealed record PublicVacancyListingResponse(
    Guid VacancyListingId, string UnitLabel, decimal RentAmount, string Currency, DateOnly AvailableFrom, string? Description);

/// <summary>PhotoIds only — photo bytes are fetched via the separate public photo-download route, never inlined here.</summary>
public sealed record PublicVacancyListingDetailResponse(
    Guid VacancyListingId, string UnitLabel, decimal RentAmount, string Currency, DateOnly AvailableFrom,
    string? Description, IReadOnlyList<Guid> PhotoIds);

public sealed record ApplicationLinkIssueRequest(string ApplicantEmail);

public sealed record ApplicationLinkContextResponse(Guid VacancyListingId, string UnitLabel, decimal RentAmount, string Currency, string ApplicantEmail);

public sealed record ApplicationSubmitRequest(
    string FullName, string Phone, DateOnly DateOfBirth, string CurrentAddress,
    string? EmployerName, decimal? MonthlyIncome, bool ConsentToScreening);

public sealed record ApplicationSubmitResponse(Guid ApplicationId, string Status);

/// <summary>BDR-LSE-009 — the lease term travels with the signature request: an end date for a fixed term, or MonthToMonth true and no end date.</summary>
public sealed record SendForSignatureRequest(Guid DraftDocumentId, DateOnly LeaseStartDate, DateOnly? LeaseEndDate, bool MonthToMonth);

public sealed record ApplicationResponse(
    Guid ApplicationId, Guid VacancyListingId, string ApplicantEmail, string ApplicantFullName,
    string Status, string? ScreeningOutcomeSummary, DateTimeOffset SubmittedAt, DateOnly? LeaseStartDate, DateOnly? LeaseEndDate);

public sealed record ApplicationEventResponse(Guid ApplicationEventId, string EventType, string ActorType, string? DetailJson, DateTimeOffset CreatedAt);

/// <summary>BDR-LSE-011 — same shape and rules as PropertyAsset's LeaseEndRequest: Reason from the fixed list, EndedBy PM_COMPANY or LESSEE, Note required when Reason is OTHER.</summary>
public sealed record EndLeaseRequest(DateOnly EffectiveDate, string Reason, string EndedBy, string? Note);

public sealed record ViewScreeningReportRequest(string CurrentPassword);

/// <summary>BDR-LSE-004's one narrow exception to "never expose full report detail" — TENANT_ADMIN only, password re-entry required, every view audited. ReportJson is the provider's own plaintext report shape (NoOpScreeningProvider's own synthetic shape today).</summary>
public sealed record ScreeningReportResponse(string Provider, string OutcomeSummary, string ReportJson, DateTimeOffset ReceivedAt);

/// <summary>
/// BDR-LSE-010. ProposedTerm: FIXED (ProposedEndDate required, after the lease's current term) or
/// MONTH_TO_MONTH. ProposedRentAmount null keeps the rent as it is; when set, ProposedRentFrom is
/// required and defaults in the UI to the day after the current term ends.
/// </summary>
public sealed record RenewalOfferCreateRequest(
    Guid LeaseId, string ProposedTerm, DateOnly? ProposedEndDate, decimal? ProposedRentAmount, DateOnly? ProposedRentFrom, DateOnly RespondBy);

public sealed record RenewalOfferAcceptRequest(DateOnly RespondedOn);

/// <summary>Reason: one of the tenant's configured decline reasons, or "Other" with a Note.</summary>
public sealed record RenewalOfferDeclineRequest(DateOnly RespondedOn, string Reason, string? Note);

/// <summary>
/// Unit/lease details are resolved from the lease record for display. RentChangeApplied is set on
/// an accepted offer with a new rent: false when there was no rent schedule to change (no Rent
/// Collection, or none for this lease), so staff aren't told the rent moved when nothing tracks it.
/// </summary>
public sealed record RenewalOfferResponse(
    Guid RenewalOfferId, Guid LeaseId, Guid UnitAssetId, string UnitLabel, string PropertyAddress, DateOnly? CurrentLeaseEndDate,
    string ProposedTerm, DateOnly? ProposedEndDate, decimal? ProposedRentAmount, DateOnly? ProposedRentFrom, DateOnly RespondBy,
    bool RespondByPassed, string Status, DateOnly? OutcomeOn, string? DeclineReason, string? DeclineNote, bool? RentChangeApplied,
    DateTimeOffset CreatedAt);
