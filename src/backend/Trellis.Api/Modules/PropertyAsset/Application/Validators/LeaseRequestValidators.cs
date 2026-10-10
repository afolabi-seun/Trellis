using FluentValidation;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;

namespace Trellis.Api.Modules.PropertyAsset.Application.Validators;

/// <summary>
/// BDR-LSE-011's end rules, shared by every request that ends a lease — PropertyAsset's own, and
/// Leasing's end-lease and Rent Collection's end-schedule requests, which carry the same four
/// fields — so the allowed reasons can't drift between the three entry points. The effective date
/// can't be in the future: a lease ended today vacates its unit today, and a future move-out is a
/// notice, which this doesn't model.
/// </summary>
public static class LeaseEndRules
{
    public static readonly string[] Reasons = ["NON_RENEWAL", "LESSEE_BREACH", "MUTUAL_AGREEMENT", "PROPERTY_SALE", "RENOVATION", "OTHER"];
    public static readonly string[] EndedByValues = ["PM_COMPANY", "LESSEE"];

    public static void Apply<T>(
        AbstractValidator<T> validator, Func<T, DateOnly> effectiveDate, Func<T, string> reason, Func<T, string> endedBy, Func<T, string?> note)
    {
        validator.RuleFor(x => effectiveDate(x)).NotEmpty()
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithName("EffectiveDate")
            .WithMessage("The end date can't be in the future.");
        validator.RuleFor(x => reason(x)).Must(r => Reasons.Contains(r)).WithName("Reason")
            .WithMessage($"Reason must be one of: {string.Join(", ", Reasons)}.");
        validator.RuleFor(x => endedBy(x)).Must(e => EndedByValues.Contains(e)).WithName("EndedBy")
            .WithMessage($"EndedBy must be one of: {string.Join(", ", EndedByValues)}.");
        validator.RuleFor(x => note(x)).MaximumLength(500).WithName("Note");
        validator.RuleFor(x => note(x)).NotEmpty().When(x => reason(x) == "OTHER").WithName("Note")
            .WithMessage("A note is required when the reason is OTHER.");
    }
}

public sealed class LeaseEndRequestValidator : AbstractValidator<LeaseEndRequest>
{
    public LeaseEndRequestValidator() => LeaseEndRules.Apply(this, x => x.EffectiveDate, x => x.Reason, x => x.EndedBy, x => x.Note);
}

public sealed class LeaseTermRequestValidator : AbstractValidator<LeaseTermRequest>
{
    public LeaseTermRequestValidator()
    {
        RuleFor(x => x.TermType).Must(t => t is "FIXED" or "MONTH_TO_MONTH")
            .WithMessage("TermType must be FIXED or MONTH_TO_MONTH.");
        RuleFor(x => x.EndDate).NotNull().When(x => x.TermType == "FIXED")
            .WithMessage("A fixed-term lease needs an end date.");
        RuleFor(x => x.EndDate).Null().When(x => x.TermType == "MONTH_TO_MONTH")
            .WithMessage("A month-to-month lease has no end date.");
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}
