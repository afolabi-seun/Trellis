using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Workflow.Application.Dtos;

namespace Trellis.Api.Modules.Workflow.Application.Interfaces;

public interface IWorkflowInstanceService
{
    Task<ServiceResult<WorkflowInstanceResponse>> CreateAsync(WorkflowInstanceCreateRequest request, Guid createdBy);

    Task<ServiceResult<WorkflowInstanceResponse>> GetByIdAsync(Guid workflowInstanceId);

    Task<ServiceResult<PagedResponse<WorkflowInstanceResponse>>> ListAsync(string? cursor, int limit);

    /// <summary>
    /// TenantAdminOrDepartmentLead, resolved here rather than a custom IAuthorizationHandler
    /// (api_standards.md names the attribute but never specifies its check) — TENANT_ADMIN
    /// always passes; DEPARTMENT_STAFF passes iff <paramref name="callerDepartmentIds"/> contains
    /// the active step's RoutedDepartmentId. Both read from JWT claims by the controller, never a
    /// callback to Auth (security_guide.md's stated no-per-request-revalidation principle).
    /// </summary>
    Task<ServiceResult<WorkflowInstanceResponse>> ApproveAsync(
        Guid workflowInstanceId, Guid callerUserId, string callerRole, IReadOnlyCollection<Guid> callerDepartmentIds, WorkflowActionRequest request);

    Task<ServiceResult<WorkflowInstanceResponse>> RejectAsync(
        Guid workflowInstanceId, Guid callerUserId, string callerRole, IReadOnlyCollection<Guid> callerDepartmentIds, WorkflowActionRequest request);

    /// <summary>
    /// The vendor-facing read — real RLS read under the caller's own VendorContext (layer 1,
    /// tenant_or_vendor_isolation) + an explicit IVendorAssignmentReader check (layer 2) +
    /// VendorWorkflowInstanceView's own field omission (BDR-VEN-002). Called from Vendor's own
    /// endpoint, never reachable through WorkflowInstancesController's TENANT_ADMIN/
    /// DEPARTMENT_STAFF-only [Authorize].
    /// </summary>
    Task<ServiceResult<VendorWorkflowInstanceView>> GetForVendorAsync(Guid vendorId, Guid workflowInstanceId);
}
