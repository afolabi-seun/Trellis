using FluentValidation;
using Trellis.Api.Modules.Ai.Application.Dtos;

namespace Trellis.Api.Modules.Ai.Application.Validators;

public sealed class RoutingSuggestionCreateRequestValidator : AbstractValidator<RoutingSuggestionCreateRequest>
{
    public RoutingSuggestionCreateRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
    }
}

public sealed class ExtractionConfirmRequestValidator : AbstractValidator<ExtractionConfirmRequest>
{
    public ExtractionConfirmRequestValidator()
    {
        RuleFor(x => x.Fields).NotEmpty();
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}

public sealed class RoutingSuggestionLinkRequestValidator : AbstractValidator<RoutingSuggestionLinkRequest>
{
    public RoutingSuggestionLinkRequestValidator()
    {
        RuleFor(x => x.WorkflowInstanceId).NotEqual(Guid.Empty);
    }
}
