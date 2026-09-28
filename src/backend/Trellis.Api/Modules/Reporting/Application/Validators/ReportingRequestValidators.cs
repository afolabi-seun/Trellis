using FluentValidation;
using Trellis.Api.Modules.Reporting.Application.Dtos;

namespace Trellis.Api.Modules.Reporting.Application.Validators;

public sealed class OwnerReportCreateRequestValidator : AbstractValidator<OwnerReportCreateRequest>
{
    public OwnerReportCreateRequestValidator()
    {
        RuleFor(x => x.OwnerId).NotEmpty();
        RuleFor(x => x.PeriodType).NotEmpty().Must(s => s is "MONTHLY" or "QUARTERLY");
        RuleFor(x => x.PeriodEnd).GreaterThanOrEqualTo(x => x.PeriodStart);
    }
}
