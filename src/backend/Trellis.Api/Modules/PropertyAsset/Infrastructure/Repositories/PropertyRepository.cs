using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.PropertyAsset.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Infrastructure.Data;

namespace Trellis.Api.Modules.PropertyAsset.Infrastructure.Repositories;

public sealed class PropertyRepository(PropertyDbContext dbContext) : TenantAwareRepository<Property>, IPropertyRepository
{
    public async Task AddAsync(Property property) => await dbContext.Properties.AddAsync(property);

    public Task<Property?> GetByIdAsync(Guid propertyId) =>
        Scoped(dbContext.Properties).FirstOrDefaultAsync(p => p.PropertyId == propertyId);

    public async Task<(IReadOnlyList<Property> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        var query = Scoped(dbContext.Properties);

        if (cursor is { } c)
        {
            query = query.Where(p => p.CreatedAt > c.CreatedAt || (p.CreatedAt == c.CreatedAt && p.PropertyId > c.Id));
        }

        var totalCount = await Scoped(dbContext.Properties).CountAsync();

        var page = await query.OrderBy(p => p.CreatedAt).ThenBy(p => p.PropertyId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public Task<bool> HasActiveUnitAssetsAsync(Guid propertyId) =>
        dbContext.UnitAssets.AnyAsync(u =>
            u.TenantId == TenantContext.Current!.TenantId && u.PropertyId == propertyId && u.Status == UnitAssetStatus.Active);

    public async Task<IReadOnlyList<Guid>> ListIdsByOwnerAsync(Guid ownerId) =>
        await Scoped(dbContext.Properties).Where(p => p.OwnerId == ownerId).Select(p => p.PropertyId).ToListAsync();

    public string GetRowVersion(Property property) => RowVersionHelper.Get(dbContext, property);

    public void SetExpectedRowVersion(Property property, string rowVersion) => RowVersionHelper.SetExpected(dbContext, property, rowVersion);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
