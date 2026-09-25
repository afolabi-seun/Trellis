namespace Trellis.Api.Modules.Workflow.Application.Dtos;

public sealed record WorkflowInstanceCreateRequest(
    Guid WorkflowTemplateId, Guid? PropertyId, Guid? RequestingDepartmentId, decimal? CostAmount);

/// <summary>Approve/Reject are actions, not generic Updates (same category as Property's Archive()) — no client-presented RowVersion needed.</summary>
public sealed record WorkflowActionRequest(string? Note);

public sealed record WorkflowStepInstanceResponse(
    Guid WorkflowStepInstanceId,
    int StepOrder,
    string Name,
    Guid RoutedDepartmentId,
    string Status,
    Guid? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? DecisionNote);

public sealed record WorkflowInstanceResponse(
    Guid WorkflowInstanceId,
    Guid WorkflowTemplateId,
    Guid? PropertyId,
    Guid? RequestingDepartmentId,
    decimal? CostAmount,
    string Status,
    int CurrentStepOrder,
    Guid CreatedBy,
    List<WorkflowStepInstanceResponse> Steps,
    DateTimeOffset CreatedAt);
