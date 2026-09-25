using FluentValidation;
using Trellis.Api.Modules.Workflow.Application.Dtos;

namespace Trellis.Api.Modules.Workflow.Application.Validators;

public sealed class DepartmentCreateRequestValidator : AbstractValidator<DepartmentCreateRequest>
{
    public DepartmentCreateRequestValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
}

public sealed class DepartmentUpdateRequestValidator : AbstractValidator<DepartmentUpdateRequest>
{
    public DepartmentUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}
