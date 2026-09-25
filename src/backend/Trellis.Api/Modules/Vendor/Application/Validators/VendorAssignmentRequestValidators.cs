using FluentValidation;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Vendor.Application.Dtos;

namespace Trellis.Api.Modules.Vendor.Application.Validators;

public sealed class VendorAssignmentCreateRequestValidator : AbstractValidator<VendorAssignmentCreateRequest>
{
    public VendorAssignmentCreateRequestValidator()
    {
        RuleFor(x => x.VendorId).NotEqual(Guid.Empty);
        RuleFor(x => x.WorkItemType)
            .Must(v => v is WorkItemTypes.WorkflowInstance or WorkItemTypes.Inspection)
            .WithMessage("workItemType must be WORKFLOW_INSTANCE or INSPECTION.");
        RuleFor(x => x.WorkItemId).NotEqual(Guid.Empty);
    }
}
