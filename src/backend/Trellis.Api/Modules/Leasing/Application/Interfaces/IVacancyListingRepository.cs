using Trellis.Api.Modules.Leasing.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Application.Interfaces;

public interface IVacancyListingRepository
{
    Task AddAsync(VacancyListing listing);

    /// <summary>The normal, authenticated staff path — Scoped() to TenantContext.Current.</summary>
    Task<VacancyListing?> GetByIdAsync(Guid vacancyListingId);

    /// <summary>Pre-check before relying on the partial unique index alone — a friendlier error than a raw constraint violation.</summary>
    Task<bool> HasActiveListingForUnitAsync(Guid unitAssetId);

    Task<(IReadOnlyList<VacancyListing> Items, bool HasMore, int TotalCount)> ListForTenantAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    /// <summary>The public browsing path — explicit tenantId, never Scoped(); mirrors IOwnerReportRepository.GetByIdForTenantAsync.</summary>
    Task<VacancyListing?> GetByIdForTenantAsync(Guid tenantId, Guid vacancyListingId);

    Task<(IReadOnlyList<VacancyListing> Items, bool HasMore, int TotalCount)> ListPublicForTenantAsync(
        Guid tenantId, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    /// <summary>Cross-tenant, fully unscoped — the e-signature webhook's only way to discover which tenant a listing belongs to before TenantContext.Current can be set. Mirrors ReportShareLinkRepository.ResolveByTokenHashAsync's own unscoped shape.</summary>
    Task<VacancyListing?> GetByIdAcrossTenantsAsync(Guid vacancyListingId);

    string GetRowVersion(VacancyListing listing);

    void SetExpectedRowVersion(VacancyListing listing, string rowVersion);

    Task SaveChangesAsync();
}
