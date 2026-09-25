namespace Trellis.Api.Modules.Workflow.Application.Dtos;

public sealed record WorkflowRoutingRuleInput(string Criterion, string Operator, string ComparisonValue, Guid TargetDepartmentId);

public sealed record WorkflowStepTemplateInput(string Name, Guid DefaultDepartmentId, List<WorkflowRoutingRuleInput> Rules);

/// <summary>BDR-WF-001 — a template is edited as one coherent definition, so Create/Update both take the full step/rule list, replaced wholesale.</summary>
public sealed record WorkflowTemplateCreateRequest(string Name, string? Description, string RequestType, List<WorkflowStepTemplateInput> Steps);

public sealed record WorkflowTemplateUpdateRequest(
    string Name, string? Description, string RequestType, List<WorkflowStepTemplateInput> Steps, string RowVersion);

public sealed record WorkflowRoutingRuleResponse(
    Guid WorkflowRoutingRuleId, string Criterion, string Operator, string ComparisonValue, Guid TargetDepartmentId);

public sealed record WorkflowStepTemplateResponse(
    Guid WorkflowStepTemplateId, int StepOrder, string Name, Guid DefaultDepartmentId, List<WorkflowRoutingRuleResponse> Rules);

public sealed record WorkflowTemplateResponse(
    Guid WorkflowTemplateId,
    string Name,
    string? Description,
    string RequestType,
    string Status,
    List<WorkflowStepTemplateResponse> Steps,
    string RowVersion,
    DateTimeOffset CreatedAt);
