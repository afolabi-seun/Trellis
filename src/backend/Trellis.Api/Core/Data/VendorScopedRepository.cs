namespace Trellis.Api.Core.Data;

/// <summary>
/// Layer 2 of the vendor-visibility model, parallel to <see cref="TenantAwareRepository{T}"/>'s
/// relationship to plain <c>tenant_isolation</c>: even though the DB-level
/// <c>tenant_or_vendor_isolation</c> RLS policy (layer 1) already restricts what a vendor-context
/// connection can see, every vendor-facing repository re-checks at the app layer too
/// (multi_tenant_design.md's own documented reasoning — the same redundant-by-design belt+braces
/// every tenant-scoped repository already has). Unlike <see cref="TenantAwareRepository{T}"/>,
/// this can't read an ambient static for "what's allowed" — the allow-list is a real query
/// (<see cref="IVendorAssignmentReader"/>), not a single id — so it's injected rather than static.
/// </summary>
public abstract class VendorScopedRepository<T>(IVendorAssignmentReader vendorAssignmentReader, string workItemType)
    where T : class, IVendorAssignable
{
    protected async Task<IQueryable<T>> ScopedAsync(IQueryable<T> set, Guid vendorId)
    {
        var allowedIds = await vendorAssignmentReader.GetAssignedWorkItemIdsAsync(vendorId, workItemType);
        return set.Where(e => allowedIds.Contains(e.WorkItemId));
    }
}
