using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Vendor.Application.Interfaces;
using Trellis.Api.Modules.Vendor.Domain.Entities;
using Trellis.Api.Modules.Vendor.Infrastructure.Data;

namespace Trellis.Api.Modules.Vendor.Infrastructure.Repositories;

/// <summary>
/// Not a TenantAwareRepository&lt;T&gt; subclass — VendorRecord has no TenantId (it's a global,
/// platform-wide identity, per its own doc comment) and `vendor` carries no RLS policy at all
/// (GRANT-only, same precedent as trellis_tenant.tenant).
/// </summary>
public sealed class VendorRepository(VendorDbContext dbContext) : IVendorRepository
{
    public async Task AddAsync(VendorRecord vendor) => await dbContext.Vendors.AddAsync(vendor);

    public Task<VendorRecord?> GetByIdAsync(Guid vendorId) =>
        dbContext.Vendors.FirstOrDefaultAsync(v => v.VendorId == vendorId);

    public async Task<(IReadOnlyList<VendorRecord> Items, bool HasMore, int TotalCount)> SearchAsync(
        string? nameFilter, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        IQueryable<VendorRecord> baseQuery = dbContext.Vendors;
        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            baseQuery = baseQuery.Where(v => EF.Functions.ILike(v.VendorName, $"%{nameFilter}%"));
        }

        var totalCount = await baseQuery.CountAsync();

        var query = baseQuery;
        if (cursor is { } c)
        {
            query = query.Where(v => v.CreatedAt > c.CreatedAt || (v.CreatedAt == c.CreatedAt && v.VendorId > c.Id));
        }

        var page = await query.OrderBy(v => v.CreatedAt).ThenBy(v => v.VendorId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
