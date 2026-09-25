using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Workflow.Application.Dtos;

namespace Trellis.Api.Modules.Workflow.Application.Interfaces;

public interface IWorkflowTemplateService
{
    Task<ServiceResult<WorkflowTemplateResponse>> CreateAsync(WorkflowTemplateCreateRequest request);

    Task<ServiceResult<WorkflowTemplateResponse>> GetByIdAsync(Guid workflowTemplateId);

    Task<ServiceResult<PagedResponse<WorkflowTemplateResponse>>> ListAsync(string? cursor, int limit);

    Task<ServiceResult<WorkflowTemplateResponse>> UpdateAsync(Guid workflowTemplateId, WorkflowTemplateUpdateRequest request);

    Task<ServiceResult<WorkflowTemplateResponse>> ArchiveAsync(Guid workflowTemplateId);
}
