using FluentValidation;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Domain.Entities;

namespace Trellis.Api.Modules.PropertyAsset.Application.Validators;

public sealed class PropertyCreateRequestValidator : AbstractValidator<PropertyCreateRequest>
{
    public PropertyCreateRequestValidator()
    {
        RuleFor(x => x.OwnerId).NotEqual(Guid.Empty);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.PropertyType).Must(v => Enum.TryParse<PropertyType>(v, true, out _))
            .WithMessage("propertyType must be one of RESIDENTIAL, COMMERCIAL, MIXED_USE.");
        RuleFor(x => x.SizeSqft).GreaterThanOrEqualTo(0).When(x => x.SizeSqft is not null);
        RuleFor(x => x.AcquisitionValue).GreaterThanOrEqualTo(0).When(x => x.AcquisitionValue is not null);
        RuleFor(x => x.CurrentValuation).GreaterThanOrEqualTo(0).When(x => x.CurrentValuation is not null);
    }
}

public sealed class PropertyUpdateRequestValidator : AbstractValidator<PropertyUpdateRequest>
{
    public PropertyUpdateRequestValidator()
    {
        RuleFor(x => x.OwnerId).NotEqual(Guid.Empty);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.PropertyType).Must(v => Enum.TryParse<PropertyType>(v, true, out _))
            .WithMessage("propertyType must be one of RESIDENTIAL, COMMERCIAL, MIXED_USE.");
        RuleFor(x => x.SizeSqft).GreaterThanOrEqualTo(0).When(x => x.SizeSqft is not null);
        RuleFor(x => x.AcquisitionValue).GreaterThanOrEqualTo(0).When(x => x.AcquisitionValue is not null);
        RuleFor(x => x.CurrentValuation).GreaterThanOrEqualTo(0).When(x => x.CurrentValuation is not null);
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}
