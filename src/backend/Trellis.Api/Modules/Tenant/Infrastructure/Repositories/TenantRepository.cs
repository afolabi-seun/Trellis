using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Trellis.Api.Modules.Tenant.Infrastructure.Data;
using TenantEntity = Trellis.Api.Modules.Tenant.Domain.Entities.Tenant;

namespace Trellis.Api.Modules.Tenant.Infrastructure.Repositories;

/// <summary>
/// Deliberately not a <c>TenantAwareRepository&lt;T&gt;</c> — <c>tenant</c> is the aggregate
/// root RLS isolates *around*, not a tenant-owned child table (project_architecture.md's Tenant
/// module notes). Access control here is RBAC (<c>SUPER_ADMIN</c> only), not row-level security.
/// </summary>
public sealed class TenantRepository(TenantDbContext dbContext) : ITenantRepository
{
    public async Task AddAsync(TenantEntity tenant) => await dbContext.Tenants.AddAsync(tenant);

    public Task<TenantEntity?> GetByIdAsync(Guid tenantId) =>
        dbContext.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId);

    public async Task<(IReadOnlyList<TenantEntity> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        var query = dbContext.Tenants.AsQueryable();

        if (cursor is { } c)
        {
            query = query.Where(t => t.CreatedAt > c.CreatedAt || (t.CreatedAt == c.CreatedAt && t.TenantId > c.Id));
        }

        var totalCount = await dbContext.Tenants.CountAsync();

        // Fetch one extra row to determine HasMore without a second round-trip.
        var page = await query.OrderBy(t => t.CreatedAt).ThenBy(t => t.TenantId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
