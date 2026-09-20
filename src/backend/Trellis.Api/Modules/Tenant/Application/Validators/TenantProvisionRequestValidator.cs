using FluentValidation;
using Trellis.Api.Modules.Tenant.Application.Dtos;

namespace Trellis.Api.Modules.Tenant.Application.Validators;

public sealed class TenantProvisionRequestValidator : AbstractValidator<TenantProvisionRequest>
{
    public TenantProvisionRequestValidator()
    {
        RuleFor(x => x.TenantName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress().MaximumLength(100);
    }
}
