using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Trellis.Api.Core.Audit;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.Workflow.Application.Dtos;
using Trellis.Api.Modules.Workflow.Application.Interfaces;
using Trellis.Api.Modules.Workflow.Application.Services;
using Trellis.Api.Modules.Workflow.Domain.Entities;
using Xunit;

namespace Trellis.Tests.Workflow;

public sealed class WorkflowInstanceServiceTests
{
    private readonly IWorkflowInstanceRepository _repository = Substitute.For<IWorkflowInstanceRepository>();
    private readonly IWorkflowTemplateRepository _templateRepository = Substitute.For<IWorkflowTemplateRepository>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();

    private WorkflowInstanceService CreateSut() =>
        new(_repository, _templateRepository, _auditService, _notificationService, _authService);

    private static WorkflowInstance CreateSingleStepInstance(Guid tenantId, Guid routedDepartmentId)
    {
        var step = WorkflowStepInstance.Create(tenantId, Guid.NewGuid(), 0, "Approval", routedDepartmentId);
        return WorkflowInstance.Create(tenantId, Guid.NewGuid(), null, null, null, Guid.NewGuid(), [step]);
    }

    [Fact]
    public async Task ApproveAsync_WhenCallerIsTenantAdmin_Succeeds()
    {
        var tenantId = Guid.NewGuid();
        var instance = CreateSingleStepInstance(tenantId, Guid.NewGuid());
        _repository.GetByIdAsync(instance.WorkflowInstanceId).Returns(instance);

        var sut = CreateSut();
        var result = await sut.ApproveAsync(
            instance.WorkflowInstanceId, Guid.NewGuid(), "TENANT_ADMIN", [], new WorkflowActionRequest(null));

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
    }

    [Fact]
    public async Task ApproveAsync_WhenDepartmentStaffIsMemberOfRoutedDepartment_Succeeds()
    {
        var tenantId = Guid.NewGuid();
        var routedDepartmentId = Guid.NewGuid();
        var instance = CreateSingleStepInstance(tenantId, routedDepartmentId);
        _repository.GetByIdAsync(instance.WorkflowInstanceId).Returns(instance);

        var sut = CreateSut();
        var result = await sut.ApproveAsync(
            instance.WorkflowInstanceId, Guid.NewGuid(), "DEPARTMENT_STAFF", [routedDepartmentId], new WorkflowActionRequest(null));

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
    }

    [Fact]
    public async Task ApproveAsync_WhenDepartmentStaffIsNotMemberOfRoutedDepartment_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var instance = CreateSingleStepInstance(tenantId, Guid.NewGuid());
        _repository.GetByIdAsync(instance.WorkflowInstanceId).Returns(instance);

        var sut = CreateSut();
        var result = await sut.ApproveAsync(
            instance.WorkflowInstanceId, Guid.NewGuid(), "DEPARTMENT_STAFF", [Guid.NewGuid()], new WorkflowActionRequest(null));

        result.Outcome.Should().Be(ServiceResultOutcome.Forbidden);
    }

    [Fact]
    public async Task ApproveAsync_WhenNoStepIsInProgress_ReturnsBusinessRuleViolation()
    {
        var tenantId = Guid.NewGuid();
        var instance = CreateSingleStepInstance(tenantId, Guid.NewGuid());
        // Approve the only step directly so no step is left InProgress, without going through the
        // service (simulates an instance that's already fully resolved).
        instance.Steps[0].Approve(Guid.NewGuid(), null);
        _repository.GetByIdAsync(instance.WorkflowInstanceId).Returns(instance);

        var sut = CreateSut();
        var result = await sut.ApproveAsync(
            instance.WorkflowInstanceId, Guid.NewGuid(), "TENANT_ADMIN", [], new WorkflowActionRequest(null));

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
    }

    [Fact]
    public async Task ApproveAsync_WhenSaveThrowsConcurrencyException_ReturnsConcurrencyConflict()
    {
        var tenantId = Guid.NewGuid();
        var instance = CreateSingleStepInstance(tenantId, Guid.NewGuid());
        _repository.GetByIdAsync(instance.WorkflowInstanceId).Returns(instance);
        _repository.SaveChangesAsync().Returns(Task.FromException(new DbUpdateConcurrencyException()));

        var sut = CreateSut();
        var result = await sut.ApproveAsync(
            instance.WorkflowInstanceId, Guid.NewGuid(), "TENANT_ADMIN", [], new WorkflowActionRequest(null));

        result.Outcome.Should().Be(ServiceResultOutcome.ConcurrencyConflict);
    }

    [Fact]
    public async Task RejectAsync_WhenCallerIsTenantAdmin_MarksRemainingStepsSkipped()
    {
        var tenantId = Guid.NewGuid();
        var routedDepartmentId = Guid.NewGuid();
        var firstStep = WorkflowStepInstance.Create(tenantId, Guid.NewGuid(), 0, "First", routedDepartmentId);
        var secondStep = WorkflowStepInstance.Create(tenantId, Guid.NewGuid(), 1, "Second", routedDepartmentId);
        var instance = WorkflowInstance.Create(tenantId, Guid.NewGuid(), null, null, null, Guid.NewGuid(), [firstStep, secondStep]);
        _repository.GetByIdAsync(instance.WorkflowInstanceId).Returns(instance);

        var sut = CreateSut();
        var result = await sut.RejectAsync(
            instance.WorkflowInstanceId, Guid.NewGuid(), "TENANT_ADMIN", [], new WorkflowActionRequest("no budget"));

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.Status.Should().Be("REJECTED");
        result.Data!.Steps.Should().ContainSingle(s => s.Status == "SKIPPED");
    }
}
