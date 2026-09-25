using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Vendor.Application.Interfaces;
using Trellis.Api.Modules.Vendor.Domain.Entities;
using Trellis.Api.Modules.Vendor.Infrastructure.Data;

namespace Trellis.Api.Modules.Vendor.Infrastructure.Repositories;

public sealed class TenantVendorRelationshipRepository(VendorDbContext dbContext)
    : TenantAwareRepository<TenantVendorRelationship>, ITenantVendorRelationshipRepository
{
    public async Task AddAsync(TenantVendorRelationship relationship) => await dbContext.TenantVendorRelationships.AddAsync(relationship);

    public Task<TenantVendorRelationship?> GetByIdAsync(Guid tenantVendorRelationshipId) =>
        Scoped(dbContext.TenantVendorRelationships).FirstOrDefaultAsync(r => r.TenantVendorRelationshipId == tenantVendorRelationshipId);

    public Task<TenantVendorRelationship?> GetByTenantAndVendorAsync(Guid tenantId, Guid vendorId) =>
        dbContext.TenantVendorRelationships.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.VendorId == vendorId);

    public async Task<(IReadOnlyList<TenantVendorRelationship> Items, bool HasMore, int TotalCount)> ListForTenantAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        var totalCount = await Scoped(dbContext.TenantVendorRelationships).CountAsync();

        var query = Scoped(dbContext.TenantVendorRelationships);
        if (cursor is { } c)
        {
            query = query.Where(r => r.CreatedAt > c.CreatedAt || (r.CreatedAt == c.CreatedAt && r.TenantVendorRelationshipId > c.Id));
        }

        var page = await query.OrderBy(r => r.CreatedAt).ThenBy(r => r.TenantVendorRelationshipId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
