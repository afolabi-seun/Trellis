using FluentValidation;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;

namespace Trellis.Api.Modules.PropertyAsset.Application.Validators;

public sealed class OwnerCreateRequestValidator : AbstractValidator<OwnerCreateRequest>
{
    public OwnerCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(255).When(x => x.Email is not null);
        RuleFor(x => x.Phone).MaximumLength(30).When(x => x.Phone is not null);
    }
}

public sealed class OwnerUpdateRequestValidator : AbstractValidator<OwnerUpdateRequest>
{
    public OwnerUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(255).When(x => x.Email is not null);
        RuleFor(x => x.Phone).MaximumLength(30).When(x => x.Phone is not null);
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}
