using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Workflow.Application.Dtos;
using Trellis.Api.Modules.Workflow.Application.Interfaces;
using Trellis.Api.Modules.Workflow.Domain.Entities;
using Trellis.Api.Modules.Workflow.Domain.Exceptions;

namespace Trellis.Api.Modules.Workflow.Application.Services;

public sealed class WorkflowTemplateService(IWorkflowTemplateRepository repository) : IWorkflowTemplateService
{
    public async Task<ServiceResult<WorkflowTemplateResponse>> CreateAsync(WorkflowTemplateCreateRequest request)
    {
        var tenantId = TenantContext.Current!.TenantId;
        var template = WorkflowTemplate.Create(
            tenantId, request.Name, request.Description, request.RequestType, BuildSteps(tenantId, request.Steps));

        await repository.AddAsync(template);
        await repository.SaveChangesAsync();

        return ServiceResult<WorkflowTemplateResponse>.Success(ToResponse(template));
    }

    public async Task<ServiceResult<WorkflowTemplateResponse>> GetByIdAsync(Guid workflowTemplateId)
    {
        var template = await repository.GetByIdAsync(workflowTemplateId);
        return template is null
            ? ServiceResult<WorkflowTemplateResponse>.NotFound()
            : ServiceResult<WorkflowTemplateResponse>.Success(ToResponse(template));
    }

    public async Task<ServiceResult<PagedResponse<WorkflowTemplateResponse>>> ListAsync(string? cursor, int limit)
    {
        var decoded = CursorCodec.Decode(cursor);
        var (items, hasMore, totalCount) = await repository.ListAsync(decoded, limit);

        var nextCursor = hasMore && items.Count > 0
            ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].WorkflowTemplateId)
            : null;

        return ServiceResult<PagedResponse<WorkflowTemplateResponse>>.Success(
            new PagedResponse<WorkflowTemplateResponse>(items.Select(ToResponse).ToList(), nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<WorkflowTemplateResponse>> UpdateAsync(Guid workflowTemplateId, WorkflowTemplateUpdateRequest request)
    {
        var template = await repository.GetByIdAsync(workflowTemplateId);
        if (template is null)
        {
            return ServiceResult<WorkflowTemplateResponse>.NotFound();
        }

        template.Update(request.Name, request.Description, request.RequestType, BuildSteps(template.TenantId, request.Steps));

        try
        {
            repository.SetExpectedRowVersion(template, request.RowVersion);
            await repository.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException or FormatException or OverflowException)
        {
            return ServiceResult<WorkflowTemplateResponse>.ConcurrencyConflict();
        }

        return ServiceResult<WorkflowTemplateResponse>.Success(ToResponse(template));
    }

    public async Task<ServiceResult<WorkflowTemplateResponse>> ArchiveAsync(Guid workflowTemplateId)
    {
        var template = await repository.GetByIdAsync(workflowTemplateId);
        if (template is null)
        {
            return ServiceResult<WorkflowTemplateResponse>.NotFound();
        }

        try
        {
            template.Archive();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<WorkflowTemplateResponse>.BusinessRuleViolation(ErrorCodeRegistry.Workflow.InvalidStatusTransition);
        }

        await repository.SaveChangesAsync();

        return ServiceResult<WorkflowTemplateResponse>.Success(ToResponse(template));
    }

    private static List<WorkflowStepTemplate> BuildSteps(Guid tenantId, List<WorkflowStepTemplateInput> steps) =>
        steps.Select((step, index) =>
        {
            var rules = step.Rules.Select((rule, ruleIndex) => WorkflowRoutingRule.Create(
                tenantId,
                ruleIndex,
                EnumStringConverter.ParseUpperSnakeCase<RoutingCriterion>(rule.Criterion),
                EnumStringConverter.ParseUpperSnakeCase<RoutingOperator>(rule.Operator),
                rule.ComparisonValue,
                rule.TargetDepartmentId));

            return WorkflowStepTemplate.Create(tenantId, index, step.Name, step.DefaultDepartmentId, rules);
        }).ToList();

    private WorkflowTemplateResponse ToResponse(WorkflowTemplate template) => new(
        template.WorkflowTemplateId,
        template.Name,
        template.Description,
        template.RequestType,
        template.Status.ToString().ToUpperInvariant(),
        template.Steps.Select(ToStepResponse).ToList(),
        repository.GetRowVersion(template),
        template.CreatedAt);

    private static WorkflowStepTemplateResponse ToStepResponse(WorkflowStepTemplate step) => new(
        step.WorkflowStepTemplateId,
        step.StepOrder,
        step.Name,
        step.DefaultDepartmentId,
        step.Rules.Select(ToRuleResponse).ToList());

    private static WorkflowRoutingRuleResponse ToRuleResponse(WorkflowRoutingRule rule) => new(
        rule.WorkflowRoutingRuleId,
        rule.Criterion.ToUpperSnakeCase(),
        rule.Operator.ToUpperSnakeCase(),
        rule.ComparisonValue,
        rule.TargetDepartmentId);
}
