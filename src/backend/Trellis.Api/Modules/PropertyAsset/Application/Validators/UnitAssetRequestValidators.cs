using FluentValidation;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;

namespace Trellis.Api.Modules.PropertyAsset.Application.Validators;

public sealed class UnitAssetCreateRequestValidator : AbstractValidator<UnitAssetCreateRequest>
{
    public UnitAssetCreateRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SizeSqft).GreaterThanOrEqualTo(0).When(x => x.SizeSqft is not null);
        RuleFor(x => x.AcquisitionValue).GreaterThanOrEqualTo(0).When(x => x.AcquisitionValue is not null);
    }
}

public sealed class UnitAssetUpdateRequestValidator : AbstractValidator<UnitAssetUpdateRequest>
{
    public UnitAssetUpdateRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SizeSqft).GreaterThanOrEqualTo(0).When(x => x.SizeSqft is not null);
        RuleFor(x => x.AcquisitionValue).GreaterThanOrEqualTo(0).When(x => x.AcquisitionValue is not null);
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}
