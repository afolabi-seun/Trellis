using FluentAssertions;
using Trellis.Api.Modules.Workflow.Domain.Entities;
using Trellis.Api.Modules.Workflow.Domain.ValueObjects;
using Xunit;

namespace Trellis.Tests.Workflow;

public sealed class WorkflowStepTemplateTests
{
    [Fact]
    public void ResolveDepartment_WhenNoRuleMatches_FallsBackToDefaultDepartment()
    {
        var defaultDepartmentId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var rule = WorkflowRoutingRule.Create(
            tenantId, 0, RoutingCriterion.Property, RoutingOperator.Equals, Guid.NewGuid().ToString(), Guid.NewGuid());
        var step = WorkflowStepTemplate.Create(tenantId, 0, "Approval", defaultDepartmentId, [rule]);

        var resolved = step.ResolveDepartment(new WorkflowRoutingContext(Guid.NewGuid(), null, null));

        resolved.Should().Be(defaultDepartmentId);
    }

    [Fact]
    public void ResolveDepartment_WhenFirstRuleMatches_UsesFirstRuleNotLater()
    {
        var tenantId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var firstTargetDepartmentId = Guid.NewGuid();
        var secondTargetDepartmentId = Guid.NewGuid();

        // Both rules match the same context — RuleOrder 0 must win over RuleOrder 1.
        var firstRule = WorkflowRoutingRule.Create(
            tenantId, 0, RoutingCriterion.Property, RoutingOperator.Equals, propertyId.ToString(), firstTargetDepartmentId);
        var secondRule = WorkflowRoutingRule.Create(
            tenantId, 1, RoutingCriterion.Property, RoutingOperator.Equals, propertyId.ToString(), secondTargetDepartmentId);

        var step = WorkflowStepTemplate.Create(tenantId, 0, "Approval", Guid.NewGuid(), [secondRule, firstRule]);

        var resolved = step.ResolveDepartment(new WorkflowRoutingContext(propertyId, null, null));

        resolved.Should().Be(firstTargetDepartmentId);
    }

    [Fact]
    public void ResolveDepartment_WhenALaterRuleMatchesButAnEarlierOneDoesNotApply_UsesTheMatchingRule()
    {
        var tenantId = Guid.NewGuid();
        var costTargetDepartmentId = Guid.NewGuid();

        var propertyRule = WorkflowRoutingRule.Create(
            tenantId, 0, RoutingCriterion.Property, RoutingOperator.Equals, Guid.NewGuid().ToString(), Guid.NewGuid());
        var costRule = WorkflowRoutingRule.Create(
            tenantId, 1, RoutingCriterion.CostThreshold, RoutingOperator.GreaterThanOrEqual, "1000", costTargetDepartmentId);

        var step = WorkflowStepTemplate.Create(tenantId, 0, "Approval", Guid.NewGuid(), [propertyRule, costRule]);

        var resolved = step.ResolveDepartment(new WorkflowRoutingContext(null, null, 5000m));

        resolved.Should().Be(costTargetDepartmentId);
    }
}
