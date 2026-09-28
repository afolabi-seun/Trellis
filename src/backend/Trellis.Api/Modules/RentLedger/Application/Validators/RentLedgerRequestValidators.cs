using FluentValidation;
using Trellis.Api.Modules.RentLedger.Application.Dtos;

namespace Trellis.Api.Modules.RentLedger.Application.Validators;

public sealed class RentScheduleCreateRequestValidator : AbstractValidator<RentScheduleCreateRequest>
{
    public RentScheduleCreateRequestValidator()
    {
        RuleFor(x => x.UnitAssetId).NotEmpty();
        RuleFor(x => x.RentAmount).GreaterThan(0);
        RuleFor(x => x.Currency).Length(3);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.DueDayOfMonth).InclusiveBetween(1, 31);
        RuleFor(x => x.GracePeriodDays).InclusiveBetween(0, 60).When(x => x.GracePeriodDays.HasValue);
    }
}

public sealed class RentScheduleUpdateRequestValidator : AbstractValidator<RentScheduleUpdateRequest>
{
    public RentScheduleUpdateRequestValidator()
    {
        RuleFor(x => x.RentAmount).GreaterThan(0);
        RuleFor(x => x.DueDayOfMonth).InclusiveBetween(1, 31);
        RuleFor(x => x.GracePeriodDays).InclusiveBetween(0, 60);
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}

public sealed class RentPaymentRecordCreateRequestValidator : AbstractValidator<RentPaymentRecordCreateRequest>
{
    public RentPaymentRecordCreateRequestValidator()
    {
        RuleFor(x => x.AmountReceived).GreaterThan(0);
        RuleFor(x => x.DateReceived).NotEmpty().LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow));
        RuleFor(x => x.MethodReference).MaximumLength(255);
    }
}

public sealed class RentPaymentRecordVoidRequestValidator : AbstractValidator<RentPaymentRecordVoidRequest>
{
    public RentPaymentRecordVoidRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
