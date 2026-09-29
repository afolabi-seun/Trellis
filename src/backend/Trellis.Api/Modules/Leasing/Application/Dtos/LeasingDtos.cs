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

public sealed record SendForSignatureRequest(Guid DraftDocumentId);

public sealed record ApplicationResponse(
    Guid ApplicationId, Guid VacancyListingId, string ApplicantEmail, string ApplicantFullName,
    string Status, string? ScreeningOutcomeSummary, DateTimeOffset SubmittedAt);

public sealed record ApplicationEventResponse(Guid ApplicationEventId, string EventType, string ActorType, string? DetailJson, DateTimeOffset CreatedAt);

public sealed record EndLeaseRequest(DateOnly EffectiveDate, string? Reason);
