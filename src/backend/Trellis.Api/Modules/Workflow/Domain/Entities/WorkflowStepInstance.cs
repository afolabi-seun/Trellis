using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Workflow.Domain.Exceptions;

namespace Trellis.Api.Modules.Workflow.Domain.Entities;

public enum WorkflowStepInstanceStatus
{
    Pending,
    InProgress,
    Approved,
    Rejected,
    Skipped,
}

/// <summary>
/// One row per WorkflowStepTemplate, materialized (with RoutedDepartmentId already resolved)
/// when its parent WorkflowInstance is created — BDR-WF-004's audit trail needs every step's
/// outcome visible, not just the current one. Name is denormalized from the template so history
/// reads correctly even if the template is edited later.
/// </summary>
public sealed class WorkflowStepInstance : ITenantOwned
{
    public Guid WorkflowStepInstanceId { get; private set; }
    public Guid WorkflowInstanceId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid WorkflowStepTemplateId { get; private set; }
    public int StepOrder { get; private set; }
    public string Name { get; private set; } = null!;
    public Guid RoutedDepartmentId { get; private set; }
    public WorkflowStepInstanceStatus Status { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public string? DecisionNote { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private WorkflowStepInstance() { }

    public static WorkflowStepInstance Create(
        Guid tenantId, Guid workflowStepTemplateId, int stepOrder, string name, Guid routedDepartmentId) => new()
        {
            WorkflowStepInstanceId = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowStepTemplateId = workflowStepTemplateId,
            StepOrder = stepOrder,
            Name = name,
            RoutedDepartmentId = routedDepartmentId,
            Status = WorkflowStepInstanceStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    /// <summary>WorkflowInstanceId is set by EF Core's FK fixup when added to a tracked WorkflowInstance.Steps collection.</summary>
    internal void AssignToInstance(Guid workflowInstanceId) => WorkflowInstanceId = workflowInstanceId;

    internal void Activate()
    {
        if (Status != WorkflowStepInstanceStatus.Pending)
        {
            throw new InvalidStatusTransitionException("WorkflowStepInstance", Status.ToString(), WorkflowStepInstanceStatus.InProgress.ToString());
        }

        Status = WorkflowStepInstanceStatus.InProgress;
    }

    public void Approve(Guid userId, string? note)
    {
        if (Status != WorkflowStepInstanceStatus.InProgress)
        {
            throw new InvalidStatusTransitionException("WorkflowStepInstance", Status.ToString(), WorkflowStepInstanceStatus.Approved.ToString());
        }

        Status = WorkflowStepInstanceStatus.Approved;
        DecidedBy = userId;
        DecidedAt = DateTimeOffset.UtcNow;
        DecisionNote = note;
    }

    public void Reject(Guid userId, string? note)
    {
        if (Status != WorkflowStepInstanceStatus.InProgress)
        {
            throw new InvalidStatusTransitionException("WorkflowStepInstance", Status.ToString(), WorkflowStepInstanceStatus.Rejected.ToString());
        }

        Status = WorkflowStepInstanceStatus.Rejected;
        DecidedBy = userId;
        DecidedAt = DateTimeOffset.UtcNow;
        DecisionNote = note;
    }

    /// <summary>Called on every still-Pending step once the instance is rejected — a rejection short-circuits the rest of the chain.</summary>
    internal void Skip()
    {
        if (Status == WorkflowStepInstanceStatus.Pending)
        {
            Status = WorkflowStepInstanceStatus.Skipped;
        }
    }
}
