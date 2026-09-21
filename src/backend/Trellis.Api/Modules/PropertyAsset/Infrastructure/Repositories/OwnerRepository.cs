using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.PropertyAsset.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Infrastructure.Data;

namespace Trellis.Api.Modules.PropertyAsset.Infrastructure.Repositories;

/// <summary>
/// PropertyAsset's first real use of <see cref="TenantAwareRepository{T}"/> — Owner is genuinely
/// tenant-owned child data (unlike Tenant itself, which deliberately doesn't use this base
/// class). Layer 2 of the 3-layer isolation model, on top of the `tenant_isolation` RLS policy
/// (layer 1) the migration applies directly to `trellis_property.owner`.
/// </summary>
public sealed class OwnerRepository(PropertyDbContext dbContext) : TenantAwareRepository<Owner>, IOwnerRepository
{
    public async Task AddAsync(Owner owner) => await dbContext.Owners.AddAsync(owner);

    public Task<Owner?> GetByIdAsync(Guid ownerId) =>
        Scoped(dbContext.Owners).FirstOrDefaultAsync(o => o.OwnerId == ownerId);

    public async Task<(IReadOnlyList<Owner> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        var query = Scoped(dbContext.Owners);

        if (cursor is { } c)
        {
            query = query.Where(o => o.CreatedAt > c.CreatedAt || (o.CreatedAt == c.CreatedAt && o.OwnerId > c.Id));
        }

        var totalCount = await Scoped(dbContext.Owners).CountAsync();

        var page = await query.OrderBy(o => o.CreatedAt).ThenBy(o => o.OwnerId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public Task<bool> HasActivePropertiesAsync(Guid ownerId) =>
        dbContext.Properties.AnyAsync(p =>
            p.TenantId == TenantContext.Current!.TenantId && p.OwnerId == ownerId && p.Status == PropertyStatus.Active);

    public string GetRowVersion(Owner owner) => RowVersionHelper.Get(dbContext, owner);

    public void SetExpectedRowVersion(Owner owner, string rowVersion) => RowVersionHelper.SetExpected(dbContext, owner, rowVersion);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
