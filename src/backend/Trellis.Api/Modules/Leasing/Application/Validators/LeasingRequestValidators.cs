using FluentValidation;
using Trellis.Api.Modules.Leasing.Application.Dtos;

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
