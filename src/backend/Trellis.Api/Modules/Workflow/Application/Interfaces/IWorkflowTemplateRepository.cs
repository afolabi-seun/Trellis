using Trellis.Api.Modules.Workflow.Domain.Entities;

namespace Trellis.Api.Modules.Workflow.Application.Interfaces;

public interface IWorkflowTemplateRepository
{
    Task AddAsync(WorkflowTemplate template);

    /// <summary>Always includes Steps and each step's Rules — a template is never useful without its full definition.</summary>
    Task<WorkflowTemplate?> GetByIdAsync(Guid workflowTemplateId);

    Task<(IReadOnlyList<WorkflowTemplate> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    string GetRowVersion(WorkflowTemplate template);

    void SetExpectedRowVersion(WorkflowTemplate template, string rowVersion);

    Task SaveChangesAsync();
}
