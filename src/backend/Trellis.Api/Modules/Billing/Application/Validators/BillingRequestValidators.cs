using FluentValidation;
using Trellis.Api.Modules.Billing.Application.Dtos;

namespace Trellis.Api.Modules.Billing.Application.Validators;

public sealed class ConfirmCardCaptureRequestValidator : AbstractValidator<ConfirmCardCaptureRequest>
{
    public ConfirmCardCaptureRequestValidator()
    {
        RuleFor(x => x.Reference).NotEmpty();
    }
}

public sealed class ModuleEntitlementSetRequestValidator : AbstractValidator<ModuleEntitlementSetRequest>
{
    public ModuleEntitlementSetRequestValidator()
    {
        RuleFor(x => x.Status).NotEmpty().Must(s => s is "ACTIVE" or "DISABLED");
        RuleFor(x => x.MonthlyPrice).GreaterThanOrEqualTo(0);
    }
}
