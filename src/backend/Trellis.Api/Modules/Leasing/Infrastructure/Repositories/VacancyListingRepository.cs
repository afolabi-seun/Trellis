using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Leasing.Infrastructure.Data;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Repositories;

public sealed class VacancyListingRepository(LeasingDbContext dbContext) : TenantAwareRepository<VacancyListing>, IVacancyListingRepository
{
    public async Task AddAsync(VacancyListing listing) => await dbContext.VacancyListings.AddAsync(listing);

    public Task<VacancyListing?> GetByIdAsync(Guid vacancyListingId) =>
        Scoped(dbContext.VacancyListings).FirstOrDefaultAsync(l => l.VacancyListingId == vacancyListingId);

    public Task<bool> HasActiveListingForUnitAsync(Guid unitAssetId) =>
        Scoped(dbContext.VacancyListings).AnyAsync(l =>
            l.UnitAssetId == unitAssetId
            && (l.Status == VacancyListingStatus.Listed || l.Status == VacancyListingStatus.UnderApplication));

    public async Task<(IReadOnlyList<VacancyListing> Items, bool HasMore, int TotalCount)> ListForTenantAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit) =>
        await PageAsync(Scoped(dbContext.VacancyListings), cursor, limit);

    public Task<VacancyListing?> GetByIdForTenantAsync(Guid tenantId, Guid vacancyListingId) =>
        dbContext.VacancyListings.FirstOrDefaultAsync(l => l.TenantId == tenantId && l.VacancyListingId == vacancyListingId);

    public async Task<(IReadOnlyList<VacancyListing> Items, bool HasMore, int TotalCount)> ListPublicForTenantAsync(
        Guid tenantId, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit) =>
        await PageAsync(
            dbContext.VacancyListings.Where(l =>
                l.TenantId == tenantId
                && (l.Status == VacancyListingStatus.Listed || l.Status == VacancyListingStatus.UnderApplication)),
            cursor, limit);

    public Task<VacancyListing?> GetByIdAcrossTenantsAsync(Guid vacancyListingId) =>
        dbContext.VacancyListings.FirstOrDefaultAsync(l => l.VacancyListingId == vacancyListingId);

    public string GetRowVersion(VacancyListing listing) => RowVersionHelper.Get(dbContext, listing);

    public void SetExpectedRowVersion(VacancyListing listing, string rowVersion) => RowVersionHelper.SetExpected(dbContext, listing, rowVersion);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();

    private static async Task<(IReadOnlyList<VacancyListing> Items, bool HasMore, int TotalCount)> PageAsync(
        IQueryable<VacancyListing> baseQuery, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        var totalCount = await baseQuery.CountAsync();

        var query = baseQuery;
        if (cursor is { } c)
        {
            query = query.Where(l => l.CreatedAt > c.CreatedAt || (l.CreatedAt == c.CreatedAt && l.VacancyListingId > c.Id));
        }

        var page = await query.OrderBy(l => l.CreatedAt).ThenBy(l => l.VacancyListingId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }
}
