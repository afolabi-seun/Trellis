using Trellis.Api.Modules.Vendor.Domain.Entities;

namespace Trellis.Api.Modules.Vendor.Application.Interfaces;

public interface IVendorAssignmentRepository
{
    Task AddAsync(VendorAssignment assignment);

    Task<VendorAssignment?> GetByIdAsync(Guid assignmentId);

    /// <summary>Vendor-side action endpoints (start/complete/detail) key off the work item id (matching VendorAssignedToWorkItemAttribute's own route param), not the assignment's own PK.</summary>
    Task<VendorAssignment?> GetByVendorAndWorkItemAsync(Guid vendorId, string workItemType, Guid workItemId);

    /// <summary>Tenant-side — this tenant's own assignments, RLS-scoped via the standard TenantContext.Current path.</summary>
    Task<(IReadOnlyList<VendorAssignment> Items, bool HasMore, int TotalCount)> ListForTenantAsync(
        (DateTimeOffset AssignedAt, Guid Id)? cursor, int limit);

    /// <summary>
    /// Vendor-side — every assignment across every tenant for this vendor id, RLS-scoped via the
    /// vendor_assignment table's own three-way-OR policy (VendorContext.Current, not
    /// TenantContext.Current). No app-layer VendorScopedRepository filter needed here — unlike
    /// WorkflowInstance/Document, this table IS the source of truth vendor_assignment reads are
    /// filtering against, not an entity that references it indirectly.
    /// </summary>
    Task<(IReadOnlyList<VendorAssignment> Items, bool HasMore, int TotalCount)> ListForVendorAsync(
        Guid vendorId, (DateTimeOffset AssignedAt, Guid Id)? cursor, int limit);

    string GetRowVersion(VendorAssignment assignment);

    void SetExpectedRowVersion(VendorAssignment assignment, string rowVersion);

    Task SaveChangesAsync();
}
