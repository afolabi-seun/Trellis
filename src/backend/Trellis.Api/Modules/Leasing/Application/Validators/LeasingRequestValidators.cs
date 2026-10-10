using FluentValidation;
using Trellis.Api.Modules.Leasing.Application.Dtos;
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
