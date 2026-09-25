using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Vendor.Application.Dtos;

namespace Trellis.Api.Modules.Vendor.Application.Interfaces;

public interface IVendorAssignmentService
{
    /// <summary>Validates an ACTIVE TenantVendorRelationship exists for the caller's tenant + the target vendor, and that the work item exists (via IWorkflowInstanceService.GetByIdAsync, tenant-scoped to the caller).</summary>
    Task<ServiceResult<VendorAssignmentResponse>> CreateAsync(VendorAssignmentCreateRequest request, Guid createdBy);

    Task<ServiceResult<VendorAssignmentResponse>> GetByIdAsync(Guid assignmentId);

    Task<ServiceResult<PagedResponse<VendorAssignmentResponse>>> ListForTenantAsync(string? cursor, int limit);

    Task<ServiceResult<PagedResponse<VendorAssignmentVendorView>>> ListForVendorAsync(Guid vendorId, string? cursor, int limit);

    /// <summary>Vendor-side detail, keyed by work item id (VendorAssignedToWorkItemAttribute already verified the caller's assignment before this runs) — assignment + the vendor-safe work item projection + the engaging tenant's display name.</summary>
    Task<ServiceResult<VendorAssignmentDetailView>> GetForVendorAsync(Guid vendorId, string workItemType, Guid workItemId);

    Task<ServiceResult<VendorAssignmentResponse>> StartAsync(Guid vendorId, string workItemType, Guid workItemId);

    Task<ServiceResult<VendorAssignmentResponse>> CompleteAsync(Guid vendorId, string workItemType, Guid workItemId);
}
