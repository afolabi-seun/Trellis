namespace Trellis.Api.Modules.Workflow.Domain.ValueObjects;

/// <summary>
/// BDR-WF-002's routing criteria, captured once at WorkflowInstance creation and evaluated
/// against every step's WorkflowRoutingRules to resolve each step's department up front. Request
/// type isn't part of this context — it selects which WorkflowTemplate an instance uses in the
/// first place, so re-checking it per step would be redundant (a template is already scoped to
/// one request type).
/// </summary>
public sealed record WorkflowRoutingContext(Guid? PropertyId, Guid? RequestingDepartmentId, decimal? CostAmount);
