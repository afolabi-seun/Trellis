namespace Trellis.Api.Core.Data;

/// <summary>
/// The one legitimate cross-module read into Vendor's own schema (multi_tenant_design.md) — any
/// module that owns a <see cref="IVendorAssignable"/> entity (Workflow's WorkflowInstance today,
/// Inspection's own entity in Phase 5) calls this to find out what a given vendor may see,
/// instead of querying <c>trellis_vendor.vendor_assignment</c> directly. Defined here in Core
/// (like <see cref="ITenantOwned"/>) rather than in the Vendor module itself, so
/// <see cref="VendorScopedRepository{T}"/> — a Core base class every module's vendor-facing
/// repository extends — can depend on the contract without Core depending on a module. The Vendor
/// module's own repository implements it; every other module only ever sees this interface.
/// </summary>
public interface IVendorAssignmentReader
{
    /// <summary>Every work item id of <paramref name="workItemType"/> (see <see cref="WorkItemTypes"/>) this vendor currently has an assignment for, across every tenant.</summary>
    Task<IReadOnlyList<Guid>> GetAssignedWorkItemIdsAsync(Guid vendorId, string workItemType);

    Task<bool> IsAssignedAsync(Guid vendorId, string workItemType, Guid workItemId);

    /// <summary>The one piece of vendor_assignment data a caller outside Vendor legitimately needs beyond "am I assigned" — which tenant to attribute a vendor-originated write to (Document's evidence upload), resolved from the assignment itself rather than trusted from the caller.</summary>
    Task<Guid?> GetAssignmentTenantIdAsync(Guid vendorId, string workItemType, Guid workItemId);
}
