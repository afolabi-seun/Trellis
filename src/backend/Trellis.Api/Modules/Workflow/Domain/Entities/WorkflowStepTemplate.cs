using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Workflow.Domain.ValueObjects;

namespace Trellis.Api.Modules.Workflow.Domain.Entities;

/// <summary>
/// One ordered step within a WorkflowTemplate. Owns its own WorkflowRoutingRules — a template is
/// edited as one coherent definition (BDR-WF-001's "workflow builder"), not assembled via many
/// small step-by-step API calls, so steps/rules are replaced wholesale by WorkflowTemplate.ReplaceSteps.
/// </summary>
public sealed class WorkflowStepTemplate : ITenantOwned
{
    public Guid WorkflowStepTemplateId { get; private set; }
    public Guid WorkflowTemplateId { get; private set; }
    public Guid TenantId { get; private set; }
    public int StepOrder { get; private set; }
    public string Name { get; private set; } = null!;
    public Guid DefaultDepartmentId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<WorkflowRoutingRule> _rules = [];
    public IReadOnlyList<WorkflowRoutingRule> Rules => _rules.AsReadOnly();

    private WorkflowStepTemplate() { }

    public static WorkflowStepTemplate Create(
        Guid tenantId, int stepOrder, string name, Guid defaultDepartmentId, IEnumerable<WorkflowRoutingRule> rules)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Step name is required.", nameof(name));
        }

        var step = new WorkflowStepTemplate
        {
            WorkflowStepTemplateId = Guid.NewGuid(),
            TenantId = tenantId,
            StepOrder = stepOrder,
            Name = name,
            DefaultDepartmentId = defaultDepartmentId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        foreach (var rule in rules)
        {
            rule.AssignToStepTemplate(step.WorkflowStepTemplateId);
            step._rules.Add(rule);
        }

        return step;
    }

    /// <summary>WorkflowTemplateId is set by EF Core's FK fixup when added to a tracked WorkflowTemplate.Steps collection.</summary>
    internal void AssignToTemplate(Guid workflowTemplateId) => WorkflowTemplateId = workflowTemplateId;

    /// <summary>Resolves this step's routed department: the first matching rule (in RuleOrder), else DefaultDepartmentId — routing always resolves to something.</summary>
    public Guid ResolveDepartment(WorkflowRoutingContext context) =>
        _rules.OrderBy(r => r.RuleOrder).FirstOrDefault(r => r.Matches(context))?.TargetDepartmentId ?? DefaultDepartmentId;
}
