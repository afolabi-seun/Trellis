using FluentValidation;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Vendor.Application.Dtos;
using Trellis.Api.Modules.Vendor.Domain.Entities;

namespace Trellis.Api.Modules.Vendor.Application.Validators;

public sealed class VendorCreateRequestValidator : AbstractValidator<VendorCreateRequest>
{
    public VendorCreateRequestValidator()
    {
        RuleFor(x => x.VendorName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category)
            .Must(v => EnumStringConverter.TryParseUpperSnakeCase<VendorCategory>(v, out _))
            .WithMessage("category must be one of MAINTENANCE, LEGAL, INSPECTION, OTHER.");
    }
}

public sealed class VendorInviteUserRequestValidator : AbstractValidator<VendorInviteUserRequest>
{
    public VendorInviteUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
    }
}
