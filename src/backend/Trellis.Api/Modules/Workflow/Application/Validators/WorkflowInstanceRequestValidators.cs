using FluentValidation;
using Trellis.Api.Modules.Workflow.Application.Dtos;

namespace Trellis.Api.Modules.Workflow.Application.Validators;

public sealed class WorkflowInstanceCreateRequestValidator : AbstractValidator<WorkflowInstanceCreateRequest>
{
    public WorkflowInstanceCreateRequestValidator()
    {
        RuleFor(x => x.WorkflowTemplateId).NotEqual(Guid.Empty);
        RuleFor(x => x.CostAmount).GreaterThanOrEqualTo(0).When(x => x.CostAmount is not null);
    }
}
