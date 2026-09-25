using FluentAssertions;
using Trellis.Api.Modules.Workflow.Domain.Entities;
using Trellis.Api.Modules.Workflow.Domain.ValueObjects;
using Xunit;

namespace Trellis.Tests.Workflow;

public sealed class WorkflowRoutingRuleTests
{
    [Fact]
    public void Matches_PropertyCriterion_WhenPropertyIdEquals_ReturnsTrue()
    {
        var propertyId = Guid.NewGuid();
        var rule = WorkflowRoutingRule.Create(
            Guid.NewGuid(), 0, RoutingCriterion.Property, RoutingOperator.Equals, propertyId.ToString(), Guid.NewGuid());

        rule.Matches(new WorkflowRoutingContext(propertyId, null, null)).Should().BeTrue();
    }

    [Fact]
    public void Matches_PropertyCriterion_WhenPropertyIdDiffers_ReturnsFalse()
    {
        var rule = WorkflowRoutingRule.Create(
            Guid.NewGuid(), 0, RoutingCriterion.Property, RoutingOperator.Equals, Guid.NewGuid().ToString(), Guid.NewGuid());

        rule.Matches(new WorkflowRoutingContext(Guid.NewGuid(), null, null)).Should().BeFalse();
    }

    [Theory]
    [InlineData(500, 1000, false)]
    [InlineData(1000, 1000, true)]
    [InlineData(1500, 1000, true)]
    public void Matches_CostThresholdGreaterThanOrEqual_EvaluatesBoundaryCorrectly(decimal cost, decimal threshold, bool expected)
    {
        var rule = WorkflowRoutingRule.Create(
            Guid.NewGuid(), 0, RoutingCriterion.CostThreshold, RoutingOperator.GreaterThanOrEqual,
            threshold.ToString(System.Globalization.CultureInfo.InvariantCulture), Guid.NewGuid());

        rule.Matches(new WorkflowRoutingContext(null, null, cost)).Should().Be(expected);
    }

    [Fact]
    public void Matches_RequestingDepartmentCriterion_WhenDepartmentIdEquals_ReturnsTrue()
    {
        var departmentId = Guid.NewGuid();
        var rule = WorkflowRoutingRule.Create(
            Guid.NewGuid(), 0, RoutingCriterion.RequestingDepartment, RoutingOperator.Equals, departmentId.ToString(), Guid.NewGuid());

        rule.Matches(new WorkflowRoutingContext(null, departmentId, null)).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenContextFieldIsNull_ReturnsFalse()
    {
        var rule = WorkflowRoutingRule.Create(
            Guid.NewGuid(), 0, RoutingCriterion.CostThreshold, RoutingOperator.GreaterThanOrEqual, "1000", Guid.NewGuid());

        rule.Matches(new WorkflowRoutingContext(null, null, null)).Should().BeFalse();
    }
}
