using FluentValidation;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Domain.ValueObjects;

namespace Trellis.Api.Modules.Tenant.Application.Validators;

public sealed class TenantProvisionRequestValidator : AbstractValidator<TenantProvisionRequest>
{
    public TenantProvisionRequestValidator()
    {
        RuleFor(x => x.TenantName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress().MaximumLength(100);
        RuleFor(x => x.Slug!).Must(SlugPolicy.IsValid)
            .When(x => !string.IsNullOrEmpty(x.Slug))
            .WithMessage("Slug must be 3-63 lowercase letters, digits or hyphens (no leading/trailing hyphen) and not a reserved word.");
    }
}

public sealed class TenantSlugUpdateRequestValidator : AbstractValidator<TenantSlugUpdateRequest>
{
    public TenantSlugUpdateRequestValidator()
    {
        RuleFor(x => x.Slug).Must(SlugPolicy.IsValid)
            .WithMessage("Slug must be 3-63 lowercase letters, digits or hyphens (no leading/trailing hyphen) and not a reserved word.");
    }
}

public sealed class TenantDomainCreateRequestValidator : AbstractValidator<TenantDomainCreateRequest>
{
    public TenantDomainCreateRequestValidator()
    {
        RuleFor(x => x.Domain).NotEmpty().MaximumLength(253);
    }
}

public sealed class NotificationSettingsDtoValidator : AbstractValidator<NotificationSettingsDto>
{
    public NotificationSettingsDtoValidator()
    {
        RuleFor(x => x.ApplicationSubmittedRoles).Must(r => StaffRoles.IsValidSelection(r?.ToList()))
            .WithMessage("Choose at least one of TENANT_ADMIN, DEPARTMENT_STAFF (no duplicates).");
        RuleFor(x => x.RentArrearsRoles).Must(r => StaffRoles.IsValidSelection(r?.ToList()))
            .WithMessage("Choose at least one of TENANT_ADMIN, DEPARTMENT_STAFF (no duplicates).");
    }
}

public sealed class RenewalDeclineReasonsDtoValidator : AbstractValidator<RenewalDeclineReasonsDto>
{
    public RenewalDeclineReasonsDtoValidator()
    {
        RuleFor(x => x.Reasons).Must(r => RenewalDeclineReasons.IsValidList(r?.ToList()))
            .WithMessage($"Give 1–{RenewalDeclineReasons.MaxReasons} distinct reasons of at most {RenewalDeclineReasons.MaxReasonLength} characters, not including \"Other\" (always offered).");
    }
}

public sealed class DataSubjectExportRequestValidator : AbstractValidator<DataSubjectExportRequest>
{
    public DataSubjectExportRequestValidator()
    {
        RuleFor(x => x.SubjectEmail).NotEmpty().MaximumLength(255);
    }
}

/// <summary>ConfirmEmail must match SubjectEmail exactly (case-insensitive) — the typed-confirmation guard against erasing the wrong person over a single mistyped character.</summary>
public sealed class EraseDataSubjectRequestValidator : AbstractValidator<EraseDataSubjectRequest>
{
    public EraseDataSubjectRequestValidator()
    {
        RuleFor(x => x.SubjectEmail).NotEmpty().MaximumLength(255);
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.ConfirmEmail)
            .Must((request, confirmEmail) => string.Equals(confirmEmail?.Trim(), request.SubjectEmail?.Trim(), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Confirmation email must match the subject email exactly.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
