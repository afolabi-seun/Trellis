using FluentValidation;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;

namespace Trellis.Api.Modules.PropertyAsset.Application.Validators;

public sealed class PropertyCreateRequestValidator : AbstractValidator<PropertyCreateRequest>
{
    public PropertyCreateRequestValidator()
    {
        RuleFor(x => x.OwnerId).NotEqual(Guid.Empty);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.PropertyType).Must(PropertyRequestValidation.IsValidPropertyType)
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
        RuleFor(x => x.PropertyType).Must(PropertyRequestValidation.IsValidPropertyType)
            .WithMessage("propertyType must be one of RESIDENTIAL, COMMERCIAL, MIXED_USE.");
        RuleFor(x => x.SizeSqft).GreaterThanOrEqualTo(0).When(x => x.SizeSqft is not null);
        RuleFor(x => x.AcquisitionValue).GreaterThanOrEqualTo(0).When(x => x.AcquisitionValue is not null);
        RuleFor(x => x.CurrentValuation).GreaterThanOrEqualTo(0).When(x => x.CurrentValuation is not null);
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}

/// <summary>
/// A bare Enum.TryParse(v, true, out _) doesn't strip underscores, so it rejects the very value
/// PropertyService itself sends on the wire ("MIXED_USE" vs the enum member's literal
/// "MixedUse") — invisible until now because this validator, like every other module's, was
/// never actually wired into the request pipeline before ValidationActionFilter (Core/Validation)
/// existed. Mirrors PropertyType's ToXString/ParseX switch-expression pattern already used in
/// PropertyService and PropertyConfiguration, rather than introducing a third copy of it.
/// </summary>
internal static class PropertyRequestValidation
{
    public static bool IsValidPropertyType(string value) =>
        value == "MIXED_USE" || Enum.TryParse<Domain.Entities.PropertyType>(value, true, out _);
}
