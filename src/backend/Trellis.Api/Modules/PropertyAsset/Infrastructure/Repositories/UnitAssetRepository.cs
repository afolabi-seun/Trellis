using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.PropertyAsset.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Infrastructure.Data;

namespace Trellis.Api.Modules.PropertyAsset.Infrastructure.Repositories;

public sealed class UnitAssetRepository(PropertyDbContext dbContext) : TenantAwareRepository<UnitAsset>, IUnitAssetRepository
{
    public async Task AddAsync(UnitAsset unitAsset) => await dbContext.UnitAssets.AddAsync(unitAsset);

    public Task<UnitAsset?> GetByIdAsync(Guid unitAssetId) =>
        Scoped(dbContext.UnitAssets).FirstOrDefaultAsync(u => u.UnitAssetId == unitAssetId);

    public async Task<(IReadOnlyList<UnitAsset> Items, bool HasMore, int TotalCount)> ListByPropertyAsync(
        Guid propertyId, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        var scoped = Scoped(dbContext.UnitAssets).Where(u => u.PropertyId == propertyId);
        var query = scoped;

        if (cursor is { } c)
        {
            query = query.Where(u => u.CreatedAt > c.CreatedAt || (u.CreatedAt == c.CreatedAt && u.UnitAssetId > c.Id));
        }

        var totalCount = await scoped.CountAsync();

        var page = await query.OrderBy(u => u.CreatedAt).ThenBy(u => u.UnitAssetId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public async Task<IReadOnlyList<Guid>> ListIdsByPropertiesAsync(IReadOnlyCollection<Guid> propertyIds) =>
        await Scoped(dbContext.UnitAssets).Where(u => propertyIds.Contains(u.PropertyId)).Select(u => u.UnitAssetId).ToListAsync();

    public Task<int> CountActiveForTenantAsync(Guid tenantId) =>
        dbContext.UnitAssets.CountAsync(u => u.TenantId == tenantId && u.Status == UnitAssetStatus.Active);

    public string GetRowVersion(UnitAsset unitAsset) => RowVersionHelper.Get(dbContext, unitAsset);

    public void SetExpectedRowVersion(UnitAsset unitAsset, string rowVersion) => RowVersionHelper.SetExpected(dbContext, unitAsset, rowVersion);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
