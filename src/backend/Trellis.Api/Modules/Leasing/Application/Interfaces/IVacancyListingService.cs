using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Leasing.Application.Dtos;

namespace Trellis.Api.Modules.Leasing.Application.Interfaces;

/// <summary>Own record, not a reuse of Document's DocumentDownloadResult — matches InspectionPhotoDownloadResult's own precedent of each module owning its own download-result shape rather than coupling to a sibling module's local type for a plain 3-field record.</summary>
public sealed record VacancyListingPhotoDownloadResult(Stream Content, string FileName, string ContentType);

public interface IVacancyListingService
{
    // Staff (TENANT_ADMIN + DEPARTMENT_STAFF)
    Task<ServiceResult<VacancyListingResponse>> CreateAsync(VacancyListingCreateRequest request, Guid createdBy);

    Task<ServiceResult<VacancyListingResponse>> UpdateAsync(Guid vacancyListingId, VacancyListingUpdateRequest request);

    Task<ServiceResult<VacancyListingResponse>> WithdrawAsync(Guid vacancyListingId);

    Task<ServiceResult<PagedResponse<VacancyListingResponse>>> ListForTenantAsync(string? cursor, int limit);

    Task<ServiceResult<VacancyListingResponse>> GetByIdAsync(Guid vacancyListingId);

    Task<ServiceResult<VacancyListingPhotoResponse>> UploadPhotoAsync(
        Guid vacancyListingId, Stream content, string fileName, string contentType, long fileSizeBytes, Guid uploadedBy);

    /// <summary>Staff, any scan status — a listing's own uploader-visibility restriction doesn't apply to staff viewing their own tenant's photos.</summary>
    Task<ServiceResult<VacancyListingPhotoDownloadResult>> DownloadPhotoAsync(Guid photoId);

    // Public / anonymous
    Task<ServiceResult<PagedResponse<PublicVacancyListingResponse>>> BrowsePublicAsync(Guid tenantId, string? cursor, int limit);

    Task<ServiceResult<PublicVacancyListingDetailResponse>> GetPublicByIdAsync(Guid tenantId, Guid vacancyListingId);

    Task<ServiceResult<VacancyListingPhotoDownloadResult>> GetPublicPhotoAsync(Guid tenantId, Guid photoId);
}
