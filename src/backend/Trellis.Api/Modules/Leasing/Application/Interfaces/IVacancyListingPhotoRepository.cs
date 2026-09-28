using Trellis.Api.Modules.Leasing.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Application.Interfaces;

public interface IVacancyListingPhotoRepository
{
    Task AddAsync(VacancyListingPhoto photo);

    /// <summary>Staff path — any scan status, exact InspectionPhoto precedent.</summary>
    Task<VacancyListingPhoto?> GetByIdAsync(Guid vacancyListingPhotoId);

    Task<IReadOnlyList<VacancyListingPhoto>> ListByListingAsync(Guid vacancyListingId);

    /// <summary>Public path — explicit tenantId, ScanStatus==Clean only (audience is unauthenticated).</summary>
    Task<IReadOnlyList<VacancyListingPhoto>> ListPublicByListingAsync(Guid tenantId, Guid vacancyListingId);

    /// <summary>Public photo download — explicit tenantId, Clean-only, mirrors ListPublicByListingAsync's own visibility rule for the single-photo case.</summary>
    Task<VacancyListingPhoto?> GetPublicByIdAsync(Guid tenantId, Guid vacancyListingPhotoId);

    Task<int> CountByListingAsync(Guid vacancyListingId);

    Task SaveChangesAsync();
}
