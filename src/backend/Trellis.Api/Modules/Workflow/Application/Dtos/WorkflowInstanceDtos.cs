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

/// <summary>
/// BDR-VEN-002 — a Vendor never gets CostAmount or RequestingDepartmentId (financials/internal
/// routing detail), even though the tenant_or_vendor_isolation RLS grant would technically let
/// the row itself through. A deliberate app-layer narrowing on top of what RLS alone allows, same
/// "defense in depth beyond RLS" reasoning as Document's scan_status gate.
/// </summary>
public sealed record VendorWorkflowInstanceView(
    Guid WorkflowInstanceId, Guid? PropertyId, string Status, int CurrentStepOrder, List<WorkflowStepInstanceResponse> Steps, DateTimeOffset CreatedAt);
