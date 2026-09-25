using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Workflow.Domain.Exceptions;

namespace Trellis.Api.Modules.Workflow.Domain.Entities;

public enum WorkflowTemplateStatus
{
    Active,
    Archived,
}

/// <summary>
/// BDR-WF-001 — a Tenant Admin's configurable approval chain, one per request type (e.g.
/// MAINTENANCE, LEASE_RENEWAL). Creating a WorkflowInstance picks a template by RequestType.
/// </summary>
public sealed class WorkflowTemplate : ITenantOwned
{
    public Guid WorkflowTemplateId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string RequestType { get; private set; } = null!;
    public WorkflowTemplateStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<WorkflowStepTemplate> _steps = [];
    public IReadOnlyList<WorkflowStepTemplate> Steps => _steps.AsReadOnly();

    private WorkflowTemplate() { }

    public static WorkflowTemplate Create(
        Guid tenantId, string name, string? description, string requestType, IEnumerable<WorkflowStepTemplate> steps)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Template name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(requestType))
        {
            throw new ArgumentException("Request type is required.", nameof(requestType));
        }

        var now = DateTimeOffset.UtcNow;
        var template = new WorkflowTemplate
        {
            WorkflowTemplateId = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Description = description,
            RequestType = requestType,
            Status = WorkflowTemplateStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        template.ReplaceSteps(steps);
        return template;
    }

    public void Update(string name, string? description, string requestType, IEnumerable<WorkflowStepTemplate> steps)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Template name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(requestType))
        {
            throw new ArgumentException("Request type is required.", nameof(requestType));
        }

        Name = name;
        Description = description;
        RequestType = requestType;
        ReplaceSteps(steps);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Edited as one coherent definition (BDR-WF-001) — the whole step/rule list is replaced, not patched incrementally.</summary>
    private void ReplaceSteps(IEnumerable<WorkflowStepTemplate> steps)
    {
        _steps.Clear();
        foreach (var step in steps)
        {
            step.AssignToTemplate(WorkflowTemplateId);
            _steps.Add(step);
        }
    }

    /// <summary>Never a hard delete — WorkflowInstance.WorkflowTemplateId must keep resolving for historical instances.</summary>
    public void Archive()
    {
        if (Status != WorkflowTemplateStatus.Active)
        {
            throw new InvalidStatusTransitionException("WorkflowTemplate", Status.ToString(), WorkflowTemplateStatus.Archived.ToString());
        }

        Status = WorkflowTemplateStatus.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
