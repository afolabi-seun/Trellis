using System.Globalization;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Workflow.Domain.ValueObjects;

namespace Trellis.Api.Modules.Workflow.Domain.Entities;

/// <summary>BDR-WF-002's three step-level routing criteria — REQUEST_TYPE isn't here, it selects the WorkflowTemplate itself (see WorkflowRoutingContext).</summary>
public enum RoutingCriterion
{
    Property,
    CostThreshold,
    RequestingDepartment,
}

/// <summary>Only meaningful for CostThreshold — Property/RequestingDepartment are always equality matches.</summary>
public enum RoutingOperator
{
    Equals,
    GreaterThanOrEqual,
}

/// <summary>
/// One conditional-routing rule on a WorkflowStepTemplate. A step can have several, evaluated in
/// RuleOrder; the first match wins. If none match, the step falls back to its own
/// DefaultDepartmentId — routing must always resolve to *something*.
/// </summary>
public sealed class WorkflowRoutingRule : ITenantOwned
{
    public Guid WorkflowRoutingRuleId { get; private set; }
    public Guid WorkflowStepTemplateId { get; private set; }
    public Guid TenantId { get; private set; }
    public int RuleOrder { get; private set; }
    public RoutingCriterion Criterion { get; private set; }
    public RoutingOperator Operator { get; private set; }

    /// <summary>Stringly-typed on purpose — a property id, a department id, or a numeric threshold depending on Criterion, opaque to storage.</summary>
    public string ComparisonValue { get; private set; } = null!;

    public Guid TargetDepartmentId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private WorkflowRoutingRule() { }

    public static WorkflowRoutingRule Create(
        Guid tenantId, int ruleOrder, RoutingCriterion criterion, RoutingOperator @operator,
        string comparisonValue, Guid targetDepartmentId)
    {
        if (string.IsNullOrWhiteSpace(comparisonValue))
        {
            throw new ArgumentException("Comparison value is required.", nameof(comparisonValue));
        }

        return new WorkflowRoutingRule
        {
            WorkflowRoutingRuleId = Guid.NewGuid(),
            TenantId = tenantId,
            RuleOrder = ruleOrder,
            Criterion = criterion,
            Operator = @operator,
            ComparisonValue = comparisonValue,
            TargetDepartmentId = targetDepartmentId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>WorkflowStepTemplateId is set by EF Core's FK fixup when added to a tracked WorkflowStepTemplate.Rules collection — not settable directly, matching the aggregate-owns-children pattern.</summary>
    internal void AssignToStepTemplate(Guid workflowStepTemplateId) => WorkflowStepTemplateId = workflowStepTemplateId;

    public bool Matches(WorkflowRoutingContext context) => Criterion switch
    {
        RoutingCriterion.Property => context.PropertyId is { } propertyId
            && propertyId.ToString() == ComparisonValue,
        RoutingCriterion.RequestingDepartment => context.RequestingDepartmentId is { } departmentId
            && departmentId.ToString() == ComparisonValue,
        RoutingCriterion.CostThreshold => context.CostAmount is { } cost
            && MatchesCostThreshold(cost),
        _ => false,
    };

    private bool MatchesCostThreshold(decimal cost)
    {
        var threshold = decimal.Parse(ComparisonValue, CultureInfo.InvariantCulture);
        return Operator == RoutingOperator.GreaterThanOrEqual ? cost >= threshold : cost == threshold;
    }
}
