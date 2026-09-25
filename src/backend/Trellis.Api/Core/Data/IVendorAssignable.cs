namespace Trellis.Api.Core.Data;

/// <summary>
/// Marks a Domain entity as a "work item" a Vendor can be assigned to (currently only
/// <c>WorkflowInstance</c>; <c>Inspection</c> will implement this too in Phase 5). The one
/// property <see cref="VendorScopedRepository{T}"/> filters on, parallel to
/// <see cref="ITenantOwned"/>'s relationship to <see cref="TenantAwareRepository{T}"/>.
/// </summary>
public interface IVendorAssignable
{
    Guid WorkItemId { get; }
}
