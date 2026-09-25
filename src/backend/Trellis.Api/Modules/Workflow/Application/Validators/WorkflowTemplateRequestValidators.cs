using FluentValidation;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Workflow.Application.Dtos;
using Trellis.Api.Modules.Workflow.Domain.Entities;

namespace Trellis.Api.Modules.Workflow.Application.Validators;

public sealed class WorkflowRoutingRuleInputValidator : AbstractValidator<WorkflowRoutingRuleInput>
{
    public WorkflowRoutingRuleInputValidator()
    {
        RuleFor(x => x.Criterion)
            .Must(v => EnumStringConverter.TryParseUpperSnakeCase<RoutingCriterion>(v, out _))
            .WithMessage("criterion must be one of PROPERTY, COST_THRESHOLD, REQUESTING_DEPARTMENT.");
        RuleFor(x => x.Operator)
            .Must(v => EnumStringConverter.TryParseUpperSnakeCase<RoutingOperator>(v, out _))
            .WithMessage("operator must be one of EQUALS, GREATER_THAN_OR_EQUAL.");
        RuleFor(x => x.ComparisonValue).NotEmpty();
        RuleFor(x => x.TargetDepartmentId).NotEqual(Guid.Empty);
    }
}

public sealed class WorkflowStepTemplateInputValidator : AbstractValidator<WorkflowStepTemplateInput>
{
    public WorkflowStepTemplateInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DefaultDepartmentId).NotEqual(Guid.Empty);
        RuleForEach(x => x.Rules).SetValidator(new WorkflowRoutingRuleInputValidator());
    }
}

public sealed class WorkflowTemplateCreateRequestValidator : AbstractValidator<WorkflowTemplateCreateRequest>
{
    public WorkflowTemplateCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RequestType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Steps).NotEmpty().WithMessage("A template needs at least one step.");
        RuleForEach(x => x.Steps).SetValidator(new WorkflowStepTemplateInputValidator());
    }
}

public sealed class WorkflowTemplateUpdateRequestValidator : AbstractValidator<WorkflowTemplateUpdateRequest>
{
    public WorkflowTemplateUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RequestType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Steps).NotEmpty().WithMessage("A template needs at least one step.");
        RuleForEach(x => x.Steps).SetValidator(new WorkflowStepTemplateInputValidator());
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}
