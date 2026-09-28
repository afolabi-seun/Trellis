using FluentValidation;
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
