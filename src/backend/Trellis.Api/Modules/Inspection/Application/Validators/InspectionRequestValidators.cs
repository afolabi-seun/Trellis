using FluentValidation;
using Trellis.Api.Modules.Inspection.Application.Dtos;
using Trellis.Api.Modules.Inspection.Domain.Entities;

namespace Trellis.Api.Modules.Inspection.Application.Validators;

public sealed class InspectionCreateRequestValidator : AbstractValidator<InspectionCreateRequest>
{
    public InspectionCreateRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => (x.PropertyId is null) != (x.UnitAssetId is null))
            .WithMessage("Exactly one of propertyId or unitAssetId is required.");
        RuleFor(x => x.ScheduledAt).NotEqual(default(DateTimeOffset));
    }
}

public sealed class InspectionItemAddRequestValidator : AbstractValidator<InspectionItemAddRequest>
{
    public InspectionItemAddRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(100);
    }
}

public sealed class InspectionItemUpdateRequestValidator : AbstractValidator<InspectionItemUpdateRequest>
{
    public InspectionItemUpdateRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(v => Enum.GetNames<InspectionItemStatus>().Any(n => string.Equals(n, v.Replace("_", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase)))
            .WithMessage("status must be one of PENDING, PASS, FAIL, NOT_APPLICABLE.");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
