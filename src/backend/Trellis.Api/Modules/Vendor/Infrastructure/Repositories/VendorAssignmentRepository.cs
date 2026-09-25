using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Vendor.Application.Interfaces;
using Trellis.Api.Modules.Vendor.Domain.Entities;
using Trellis.Api.Modules.Vendor.Infrastructure.Data;

namespace Trellis.Api.Modules.Vendor.Infrastructure.Repositories;

/// <summary>
/// Extends TenantAwareRepository&lt;T&gt; for the tenant-side query methods only
/// (ListForTenantAsync) — the vendor-side methods (ListForVendorAsync,
/// GetByVendorAndWorkItemAsync, and the IVendorAssignmentReader implementation) query
/// dbContext.VendorAssignments directly, relying on vendor_assignment's own three-way-OR RLS
/// policy rather than the tenant-only Scoped() filter, which would throw for a vendor-context
/// caller with no TenantContext.Current.
/// </summary>
public sealed class VendorAssignmentRepository(VendorDbContext dbContext)
    : TenantAwareRepository<VendorAssignment>, IVendorAssignmentRepository, IVendorAssignmentReader
{
    public async Task AddAsync(VendorAssignment assignment) => await dbContext.VendorAssignments.AddAsync(assignment);

    public Task<VendorAssignment?> GetByIdAsync(Guid assignmentId) =>
        Scoped(dbContext.VendorAssignments).FirstOrDefaultAsync(a => a.AssignmentId == assignmentId);

    public Task<VendorAssignment?> GetByVendorAndWorkItemAsync(Guid vendorId, string workItemType, Guid workItemId) =>
        dbContext.VendorAssignments.FirstOrDefaultAsync(
            a => a.VendorId == vendorId && a.WorkItemType == workItemType && a.WorkItemId == workItemId);

    public async Task<(IReadOnlyList<VendorAssignment> Items, bool HasMore, int TotalCount)> ListForTenantAsync(
        (DateTimeOffset AssignedAt, Guid Id)? cursor, int limit)
    {
        var totalCount = await Scoped(dbContext.VendorAssignments).CountAsync();

        var query = Scoped(dbContext.VendorAssignments);
        if (cursor is { } c)
        {
            query = query.Where(a => a.AssignedAt > c.AssignedAt || (a.AssignedAt == c.AssignedAt && a.AssignmentId > c.Id));
        }

        var page = await query.OrderBy(a => a.AssignedAt).ThenBy(a => a.AssignmentId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public async Task<(IReadOnlyList<VendorAssignment> Items, bool HasMore, int TotalCount)> ListForVendorAsync(
        Guid vendorId, (DateTimeOffset AssignedAt, Guid Id)? cursor, int limit)
    {
        IQueryable<VendorAssignment> baseQuery = dbContext.VendorAssignments.Where(a => a.VendorId == vendorId);
        var totalCount = await baseQuery.CountAsync();

        var query = baseQuery;
        if (cursor is { } c)
        {
            query = query.Where(a => a.AssignedAt > c.AssignedAt || (a.AssignedAt == c.AssignedAt && a.AssignmentId > c.Id));
        }

        var page = await query.OrderBy(a => a.AssignedAt).ThenBy(a => a.AssignmentId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public string GetRowVersion(VendorAssignment assignment) => RowVersionHelper.Get(dbContext, assignment);

    public void SetExpectedRowVersion(VendorAssignment assignment, string rowVersion) => RowVersionHelper.SetExpected(dbContext, assignment, rowVersion);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();

    // IVendorAssignmentReader — the Core-level, cross-module-consumed contract.

    public async Task<IReadOnlyList<Guid>> GetAssignedWorkItemIdsAsync(Guid vendorId, string workItemType) =>
        await dbContext.VendorAssignments
            .Where(a => a.VendorId == vendorId && a.WorkItemType == workItemType)
            .Select(a => a.WorkItemId)
            .ToListAsync();

    public Task<bool> IsAssignedAsync(Guid vendorId, string workItemType, Guid workItemId) =>
        dbContext.VendorAssignments.AnyAsync(a => a.VendorId == vendorId && a.WorkItemType == workItemType && a.WorkItemId == workItemId);

    public Task<Guid?> GetAssignmentTenantIdAsync(Guid vendorId, string workItemType, Guid workItemId) =>
        dbContext.VendorAssignments
            .Where(a => a.VendorId == vendorId && a.WorkItemType == workItemType && a.WorkItemId == workItemId)
            .Select(a => (Guid?)a.TenantId)
            .FirstOrDefaultAsync();
}
