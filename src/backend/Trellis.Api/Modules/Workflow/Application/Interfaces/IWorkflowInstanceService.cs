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
}
