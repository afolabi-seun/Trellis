using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Workflow.Application.Dtos;
using Trellis.Api.Modules.Workflow.Application.Interfaces;
using Trellis.Api.Modules.Workflow.Application.Services;
using Trellis.Api.Modules.Workflow.Domain.Entities;
using Xunit;

namespace Trellis.Tests.Workflow;

public sealed class WorkflowTemplateServiceTests
{
    public WorkflowTemplateServiceTests() => TenantContext.Current = new TenantContext(Guid.NewGuid());

    private static WorkflowTemplateCreateRequest CreateRequest(string criterion, string @operator) => new(
        "Maintenance Approval",
        null,
        "MAINTENANCE",
        [
            new WorkflowStepTemplateInput(
                "Manager Review",
                Guid.NewGuid(),
                [new WorkflowRoutingRuleInput(criterion, @operator, "1000", Guid.NewGuid())]),
        ]);

    [Fact]
    public async Task CreateAsync_RoundTripsCostThresholdCriterionAndGreaterThanOrEqualOperator()
    {
        var repository = Substitute.For<IWorkflowTemplateRepository>();
        var sut = new WorkflowTemplateService(repository);

        var result = await sut.CreateAsync(CreateRequest("COST_THRESHOLD", "GREATER_THAN_OR_EQUAL"));

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        var rule = result.Data!.Steps.Single().Rules.Single();
        rule.Criterion.Should().Be("COST_THRESHOLD");
        rule.Operator.Should().Be("GREATER_THAN_OR_EQUAL");
    }

    [Fact]
    public async Task UpdateAsync_WhenSaveThrowsConcurrencyException_ReturnsConcurrencyConflict()
    {
        var tenantId = TenantContext.Current!.TenantId;
        var template = WorkflowTemplate.Create(tenantId, "Maintenance Approval", null, "MAINTENANCE", []);
        var repository = Substitute.For<IWorkflowTemplateRepository>();
        repository.GetByIdAsync(template.WorkflowTemplateId).Returns(template);
        repository.SaveChangesAsync().Returns(Task.FromException(new DbUpdateConcurrencyException()));

        var sut = new WorkflowTemplateService(repository);
        var result = await sut.UpdateAsync(
            template.WorkflowTemplateId,
            new WorkflowTemplateUpdateRequest("Maintenance Approval", null, "MAINTENANCE", [], "stale-version"));

        result.Outcome.Should().Be(ServiceResultOutcome.ConcurrencyConflict);
    }
}
