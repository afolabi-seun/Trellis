using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Workflow.Domain.Entities;

public enum WorkflowInstanceStatus
{
    InProgress,
    Approved,
    Rejected,
    Cancelled,
}

/// <summary>
/// A running execution of a WorkflowTemplate. PropertyId/RequestingDepartmentId/CostAmount are
/// captured once at creation — the routing context every step's rules are evaluated against
/// (Domain.ValueObjects.WorkflowRoutingContext) — and never change afterward.
/// </summary>
public sealed class WorkflowInstance : ITenantOwned
{
    public Guid WorkflowInstanceId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid WorkflowTemplateId { get; private set; }
    public Guid? PropertyId { get; private set; }
    public Guid? RequestingDepartmentId { get; private set; }
    public decimal? CostAmount { get; private set; }
    public WorkflowInstanceStatus Status { get; private set; }
    public int CurrentStepOrder { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<WorkflowStepInstance> _steps = [];
    public IReadOnlyList<WorkflowStepInstance> Steps => _steps.AsReadOnly();

    private WorkflowInstance() { }

    /// <summary>
    /// Steps must already be created (their RoutedDepartmentId resolved against this instance's
    /// routing context) by the Service before calling this — the entity itself doesn't run
    /// routing-rule evaluation, that lives on WorkflowStepTemplate.ResolveDepartment.
    /// </summary>
    public static WorkflowInstance Create(
        Guid tenantId, Guid workflowTemplateId, Guid? propertyId, Guid? requestingDepartmentId,
        decimal? costAmount, Guid createdBy, IEnumerable<WorkflowStepInstance> steps)
    {
        var now = DateTimeOffset.UtcNow;
        var instance = new WorkflowInstance
        {
            WorkflowInstanceId = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowTemplateId = workflowTemplateId,
            PropertyId = propertyId,
            RequestingDepartmentId = requestingDepartmentId,
            CostAmount = costAmount,
            Status = WorkflowInstanceStatus.InProgress,
            CurrentStepOrder = 0,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var step in steps.OrderBy(s => s.StepOrder))
        {
            step.AssignToInstance(instance.WorkflowInstanceId);
            instance._steps.Add(step);
        }

        instance._steps.FirstOrDefault()?.Activate();
        instance.CurrentStepOrder = instance._steps.FirstOrDefault()?.StepOrder ?? 0;

        return instance;
    }

    /// <summary>Called by the Service after the current step is approved — activates the next Pending step, or completes the instance once none remain.</summary>
    public void AdvanceOrComplete()
    {
        var next = _steps.Where(s => s.Status == WorkflowStepInstanceStatus.Pending).MinBy(s => s.StepOrder);
        if (next is null)
        {
            Status = WorkflowInstanceStatus.Approved;
        }
        else
        {
            next.Activate();
            CurrentStepOrder = next.StepOrder;
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Called by the Service after the current step is rejected — a rejection short-circuits the rest of the chain.</summary>
    public void RejectRemaining()
    {
        foreach (var step in _steps.Where(s => s.Status == WorkflowStepInstanceStatus.Pending))
        {
            step.Skip();
        }

        Status = WorkflowInstanceStatus.Rejected;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
