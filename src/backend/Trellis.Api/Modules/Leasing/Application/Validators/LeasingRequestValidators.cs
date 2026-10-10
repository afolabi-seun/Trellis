using FluentValidation;
using Trellis.Api.Modules.Leasing.Application.Dtos;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Application.Validators;

namespace Trellis.Api.Modules.Leasing.Application.Validators;

public sealed class VacancyListingCreateRequestValidator : AbstractValidator<VacancyListingCreateRequest>
{
    public VacancyListingCreateRequestValidator()
    {
        RuleFor(x => x.UnitAssetId).NotEmpty();
        RuleFor(x => x.RentAmount).GreaterThan(0);
        RuleFor(x => x.Currency).Length(3);
        RuleFor(x => x.AvailableFrom).NotEmpty();
    }
}

public sealed class VacancyListingUpdateRequestValidator : AbstractValidator<VacancyListingUpdateRequest>
{
    public VacancyListingUpdateRequestValidator()
    {
        RuleFor(x => x.RentAmount).GreaterThan(0);
        RuleFor(x => x.Currency).Length(3);
        RuleFor(x => x.AvailableFrom).NotEmpty();
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}

public sealed class ApplicationLinkIssueRequestValidator : AbstractValidator<ApplicationLinkIssueRequest>
{
    public ApplicationLinkIssueRequestValidator()
    {
        RuleFor(x => x.ApplicantEmail).NotEmpty().EmailAddress();
    }
}

public sealed class ApplicationSubmitRequestValidator : AbstractValidator<ApplicationSubmitRequest>
{
    public ApplicationSubmitRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.DateOfBirth)
            .LessThan(DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-18)))
            .WithMessage("Applicant must be at least 18 years old.");
        RuleFor(x => x.CurrentAddress).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ConsentToScreening).Equal(true)
            .WithMessage("Consent to screening is required to submit a rental application.");
    }
}

/// <summary>BDR-LSE-009 — the term is required on every new send: a fixed end date after the start, or month-to-month.</summary>
public sealed class SendForSignatureRequestValidator : AbstractValidator<SendForSignatureRequest>
{
    public SendForSignatureRequestValidator()
    {
        RuleFor(x => x.DraftDocumentId).NotEmpty();
        RuleFor(x => x.LeaseStartDate).NotEmpty();
        RuleFor(x => x.LeaseEndDate).NotNull().When(x => !x.MonthToMonth)
            .WithMessage("Set a lease end date, or mark the lease month-to-month.");
        RuleFor(x => x.LeaseEndDate).Null().When(x => x.MonthToMonth)
            .WithMessage("A month-to-month lease has no end date.");
        RuleFor(x => x.LeaseEndDate).GreaterThan(x => x.LeaseStartDate).When(x => x.LeaseEndDate is not null)
            .WithMessage("The lease end date must be after its start date.");
    }
}

public sealed class EndLeaseRequestValidator : AbstractValidator<EndLeaseRequest>
{
    public EndLeaseRequestValidator() => LeaseEndRules.Apply(this, x => x.EffectiveDate, x => x.Reason, x => x.EndedBy, x => x.Note);
}

/// <summary>BDR-LSE-010 — the shape of an offer; whether it actually extends the lease is checked against the lease itself in RenewalOfferService.</summary>
public sealed class RenewalOfferCreateRequestValidator : AbstractValidator<RenewalOfferCreateRequest>
{
    public RenewalOfferCreateRequestValidator()
    {
        RuleFor(x => x.LeaseId).NotEmpty();
        RuleFor(x => x.ProposedTerm).Must(t => t is "FIXED" or "MONTH_TO_MONTH")
            .WithMessage("ProposedTerm must be FIXED or MONTH_TO_MONTH.");
        RuleFor(x => x.ProposedEndDate).NotNull().When(x => x.ProposedTerm == "FIXED")
            .WithMessage("A fixed-term renewal needs an end date.");
        RuleFor(x => x.ProposedEndDate).Null().When(x => x.ProposedTerm == "MONTH_TO_MONTH")
            .WithMessage("A month-to-month renewal has no end date.");
        RuleFor(x => x.ProposedRentAmount).GreaterThan(0).When(x => x.ProposedRentAmount is not null);
        RuleFor(x => x.ProposedRentFrom).NotNull().When(x => x.ProposedRentAmount is not null)
            .WithMessage("Say from which date the new rent applies.");
        RuleFor(x => x.ProposedRentFrom).Null().When(x => x.ProposedRentAmount is null)
            .WithMessage("A rent start date needs a new rent amount.");
        RuleFor(x => x.RespondBy).GreaterThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("The respond-by date can't be in the past.");
    }
}

public sealed class RenewalOfferAcceptRequestValidator : AbstractValidator<RenewalOfferAcceptRequest>
{
    public RenewalOfferAcceptRequestValidator()
    {
        RuleFor(x => x.RespondedOn).NotEmpty().LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("The response date can't be in the future.");
    }
}

public sealed class RenewalOfferDeclineRequestValidator : AbstractValidator<RenewalOfferDeclineRequest>
{
    public RenewalOfferDeclineRequestValidator()
    {
        RuleFor(x => x.RespondedOn).NotEmpty().LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("The response date can't be in the future.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(RenewalOffer.MaxDeclineReasonLength);
        RuleFor(x => x.Note).MaximumLength(RenewalOffer.MaxNoteLength);
        RuleFor(x => x.Note).NotEmpty().When(x => string.Equals(x.Reason, RenewalOffer.OtherReason, StringComparison.OrdinalIgnoreCase))
            .WithMessage("A note is required when the reason is Other.");
    }
}
