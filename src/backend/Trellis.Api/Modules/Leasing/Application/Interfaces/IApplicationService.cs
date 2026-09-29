using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Leasing.Application.Dtos;

namespace Trellis.Api.Modules.Leasing.Application.Interfaces;

/// <summary>
/// Screening is NOT a separate IScreeningService — BDR-LSE-003's own flow ("triggered from a
/// submitted application") makes it a synchronous step inside SubmitAsync itself (the NoOp
/// provider returns instantly; a future real provider becoming async/webhook-driven is a
/// documented future change, not built now).
/// </summary>
public interface IApplicationService
{
    /// <summary>Public — the resolved token IS the caller's authentication for this call. ApplicantEmail is copied from ApplicationLinkContext, never accepted from the request body (BDR-LSE-008).</summary>
    Task<ServiceResult<ApplicationSubmitResponse>> SubmitAsync(string rawLinkToken, ApplicationSubmitRequest request);

    // Staff
    Task<ServiceResult<PagedResponse<ApplicationResponse>>> ListByListingAsync(Guid vacancyListingId, string? cursor, int limit);

    Task<ServiceResult<ApplicationResponse>> GetByIdAsync(Guid applicationId);

    Task<ServiceResult<IReadOnlyList<ApplicationEventResponse>>> GetEventsAsync(Guid applicationId);

    Task<ServiceResult<ApplicationResponse>> ApproveAsync(Guid applicationId, Guid staffUserId);

    Task<ServiceResult<ApplicationResponse>> DeclineAsync(Guid applicationId, Guid staffUserId);

    /// <summary>draftDocumentId: staff must already have uploaded the unsigned lease via the normal Document upload flow (DocumentType=LEASE, UnitAssetId set) — IESignatureProvider.SendForSignatureAsync's own required documentId parameter has nowhere else to come from.</summary>
    Task<ServiceResult<ApplicationResponse>> SendForSignatureAsync(Guid applicationId, Guid draftDocumentId, Guid staffUserId);

    /// <summary>
    /// A staff-initiated move-out on a Leased application: ends the unit's rent schedule (if any),
    /// marks the unit vacant, and closes out the application as LeaseEnded. The listing itself is
    /// left as history (still LEASED) — re-letting the unit is a fresh listing, guarded by
    /// VacancyListingService.CreateAsync's own vacant-unit check.
    /// </summary>
    Task<ServiceResult<ApplicationResponse>> EndLeaseAsync(Guid applicationId, EndLeaseRequest request, Guid staffUserId);
}
