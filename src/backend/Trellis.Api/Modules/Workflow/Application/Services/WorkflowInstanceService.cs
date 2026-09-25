using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Audit;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Notification.Application.Dtos;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.Workflow.Application.Dtos;
using Trellis.Api.Modules.Workflow.Application.Interfaces;
using Trellis.Api.Modules.Workflow.Domain.Entities;
using Trellis.Api.Modules.Workflow.Domain.Exceptions;
using Trellis.Api.Modules.Workflow.Domain.ValueObjects;

namespace Trellis.Api.Modules.Workflow.Application.Services;

public sealed class WorkflowInstanceService(
    IWorkflowInstanceRepository repository,
    IWorkflowTemplateRepository templateRepository,
    IAuditService auditService,
    INotificationService notificationService,
    IAuthService authService) : IWorkflowInstanceService
{
    private const string TenantAdminRole = "TENANT_ADMIN";
    private const string DepartmentStaffRole = "DEPARTMENT_STAFF";

    public async Task<ServiceResult<WorkflowInstanceResponse>> CreateAsync(WorkflowInstanceCreateRequest request, Guid createdBy)
    {
        var template = await templateRepository.GetByIdAsync(request.WorkflowTemplateId);
        if (template is null)
        {
            return ServiceResult<WorkflowInstanceResponse>.NotFound();
        }

        var tenantId = TenantContext.Current!.TenantId;
        var context = new WorkflowRoutingContext(request.PropertyId, request.RequestingDepartmentId, request.CostAmount);

        var stepInstances = template.Steps
            .OrderBy(s => s.StepOrder)
            .Select(step => WorkflowStepInstance.Create(
                tenantId, step.WorkflowStepTemplateId, step.StepOrder, step.Name, step.ResolveDepartment(context)))
            .ToList();

        var instance = WorkflowInstance.Create(
            tenantId, template.WorkflowTemplateId, request.PropertyId, request.RequestingDepartmentId,
            request.CostAmount, createdBy, stepInstances);

        await repository.AddAsync(instance);
        await repository.SaveChangesAsync();

        await auditService.RecordAsync(
            tenantId, createdBy, AuditActorType.HumanUser, AuditTypes.Workflow.WorkflowInstanceCreated,
            $"{{\"workflowInstanceId\":\"{instance.WorkflowInstanceId}\"}}");
        await NotifyActiveStepDepartmentAsync(instance);

        return ServiceResult<WorkflowInstanceResponse>.Success(ToResponse(instance));
    }

    public async Task<ServiceResult<WorkflowInstanceResponse>> GetByIdAsync(Guid workflowInstanceId)
    {
        var instance = await repository.GetByIdAsync(workflowInstanceId);
        return instance is null
            ? ServiceResult<WorkflowInstanceResponse>.NotFound()
            : ServiceResult<WorkflowInstanceResponse>.Success(ToResponse(instance));
    }

    public async Task<ServiceResult<PagedResponse<WorkflowInstanceResponse>>> ListAsync(string? cursor, int limit)
    {
        var decoded = CursorCodec.Decode(cursor);
        var (items, hasMore, totalCount) = await repository.ListAsync(decoded, limit);

        var nextCursor = hasMore && items.Count > 0
            ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].WorkflowInstanceId)
            : null;

        return ServiceResult<PagedResponse<WorkflowInstanceResponse>>.Success(
            new PagedResponse<WorkflowInstanceResponse>(items.Select(ToResponse).ToList(), nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<WorkflowInstanceResponse>> ApproveAsync(
        Guid workflowInstanceId, Guid callerUserId, string callerRole, IReadOnlyCollection<Guid> callerDepartmentIds, WorkflowActionRequest request)
    {
        var instance = await repository.GetByIdAsync(workflowInstanceId);
        if (instance is null)
        {
            return ServiceResult<WorkflowInstanceResponse>.NotFound();
        }

        var currentStep = instance.Steps.FirstOrDefault(s => s.Status == WorkflowStepInstanceStatus.InProgress);
        if (currentStep is null)
        {
            return ServiceResult<WorkflowInstanceResponse>.BusinessRuleViolation(ErrorCodeRegistry.Workflow.NoStepAwaitingDecision);
        }

        if (!IsAuthorizedForStep(callerRole, callerDepartmentIds, currentStep))
        {
            return ServiceResult<WorkflowInstanceResponse>.Forbidden(ErrorCodeRegistry.Workflow.NotAuthorizedForStepDepartment);
        }

        try
        {
            currentStep.Approve(callerUserId, request.Note);
            instance.AdvanceOrComplete();
            await repository.SaveChangesAsync();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<WorkflowInstanceResponse>.BusinessRuleViolation(ErrorCodeRegistry.Workflow.NoStepAwaitingDecision);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<WorkflowInstanceResponse>.ConcurrencyConflict();
        }

        await auditService.RecordAsync(
            instance.TenantId, callerUserId, AuditActorType.HumanUser, AuditTypes.Workflow.WorkflowStepApproved,
            $"{{\"workflowInstanceId\":\"{instance.WorkflowInstanceId}\",\"workflowStepInstanceId\":\"{currentStep.WorkflowStepInstanceId}\"}}");

        if (instance.Status == WorkflowInstanceStatus.InProgress)
        {
            await NotifyActiveStepDepartmentAsync(instance);
        }

        return ServiceResult<WorkflowInstanceResponse>.Success(ToResponse(instance));
    }

    public async Task<ServiceResult<WorkflowInstanceResponse>> RejectAsync(
        Guid workflowInstanceId, Guid callerUserId, string callerRole, IReadOnlyCollection<Guid> callerDepartmentIds, WorkflowActionRequest request)
    {
        var instance = await repository.GetByIdAsync(workflowInstanceId);
        if (instance is null)
        {
            return ServiceResult<WorkflowInstanceResponse>.NotFound();
        }

        var currentStep = instance.Steps.FirstOrDefault(s => s.Status == WorkflowStepInstanceStatus.InProgress);
        if (currentStep is null)
        {
            return ServiceResult<WorkflowInstanceResponse>.BusinessRuleViolation(ErrorCodeRegistry.Workflow.NoStepAwaitingDecision);
        }

        if (!IsAuthorizedForStep(callerRole, callerDepartmentIds, currentStep))
        {
            return ServiceResult<WorkflowInstanceResponse>.Forbidden(ErrorCodeRegistry.Workflow.NotAuthorizedForStepDepartment);
        }

        try
        {
            currentStep.Reject(callerUserId, request.Note);
            instance.RejectRemaining();
            await repository.SaveChangesAsync();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<WorkflowInstanceResponse>.BusinessRuleViolation(ErrorCodeRegistry.Workflow.NoStepAwaitingDecision);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<WorkflowInstanceResponse>.ConcurrencyConflict();
        }

        await auditService.RecordAsync(
            instance.TenantId, callerUserId, AuditActorType.HumanUser, AuditTypes.Workflow.WorkflowStepRejected,
            $"{{\"workflowInstanceId\":\"{instance.WorkflowInstanceId}\",\"workflowStepInstanceId\":\"{currentStep.WorkflowStepInstanceId}\"}}");

        return ServiceResult<WorkflowInstanceResponse>.Success(ToResponse(instance));
    }

    /// <summary>TenantAdminOrDepartmentLead — TENANT_ADMIN always passes; DEPARTMENT_STAFF passes iff a member of the active step's routed department. No seniority concept exists anywhere in the schema, so "Lead" is simplified to "any staff member of the required department."</summary>
    private static bool IsAuthorizedForStep(string callerRole, IReadOnlyCollection<Guid> callerDepartmentIds, WorkflowStepInstance step) =>
        callerRole == TenantAdminRole
        || (callerRole == DepartmentStaffRole && callerDepartmentIds.Contains(step.RoutedDepartmentId));

    private async Task NotifyActiveStepDepartmentAsync(WorkflowInstance instance)
    {
        var currentStep = instance.Steps.FirstOrDefault(s => s.Status == WorkflowStepInstanceStatus.InProgress);
        if (currentStep is null)
        {
            return;
        }

        var recipients = await authService.GetUsersByDepartmentAsync(instance.TenantId, currentStep.RoutedDepartmentId);
        foreach (var recipient in recipients)
        {
            await notificationService.SendAsync(
                instance.TenantId,
                "WORKFLOW_STEP_AWAITING_DECISION",
                recipient.Email,
                new { instance.WorkflowInstanceId, StepName = currentStep.Name },
                new RelatedEntityRef("WorkflowInstance", instance.WorkflowInstanceId));
        }
    }

    private static WorkflowInstanceResponse ToResponse(WorkflowInstance instance) => new(
        instance.WorkflowInstanceId,
        instance.WorkflowTemplateId,
        instance.PropertyId,
        instance.RequestingDepartmentId,
        instance.CostAmount,
        instance.Status.ToUpperSnakeCase(),
        instance.CurrentStepOrder,
        instance.CreatedBy,
        instance.Steps.OrderBy(s => s.StepOrder).Select(ToStepResponse).ToList(),
        instance.CreatedAt);

    private static WorkflowStepInstanceResponse ToStepResponse(WorkflowStepInstance step) => new(
        step.WorkflowStepInstanceId,
        step.StepOrder,
        step.Name,
        step.RoutedDepartmentId,
        step.Status.ToUpperSnakeCase(),
        step.DecidedBy,
        step.DecidedAt,
        step.DecisionNote);
}
