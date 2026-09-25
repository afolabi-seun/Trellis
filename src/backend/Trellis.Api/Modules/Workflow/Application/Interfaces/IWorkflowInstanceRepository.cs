using Trellis.Api.Modules.Workflow.Domain.Entities;

namespace Trellis.Api.Modules.Workflow.Application.Interfaces;

public interface IWorkflowInstanceRepository
{
    Task AddAsync(WorkflowInstance instance);

    /// <summary>Always includes Steps — an instance is never useful without its step history (BDR-WF-004).</summary>
    Task<WorkflowInstance?> GetByIdAsync(Guid workflowInstanceId);

    Task<(IReadOnlyList<WorkflowInstance> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    Task SaveChangesAsync();
}
