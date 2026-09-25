using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using Trellis.Api.Core.Results;

namespace Trellis.Api.Core.Validation;

/// <summary>
/// Every module writes a FluentValidation <c>AbstractValidator&lt;TRequest&gt;</c> per request
/// type (coding_standards.md), but nothing was ever resolving them at request time — a
/// malformed request skipped straight past validation into a Service method, surfacing (at
/// best) as an unhandled exception instead of a clean 400 ValidationFailed. Found while wiring
/// Workflow's own request validators, but the same gap silently existed for every prior module's
/// validators too, back to Phase 2 — fixed here once, globally, rather than adding a per-module
/// workaround. Runs before model binding's action arguments reach any Controller action: for
/// each bound argument, resolves <c>IValidator&lt;TArgument&gt;</c> from DI (registered via
/// <c>AddValidatorsFromAssembly</c> in <see cref="CoreModuleExtensions"/>) if one exists, and
/// short-circuits with the same <see cref="ServiceResult{T}.ValidationFailed"/>→400 mapping a
/// Service method's own validation failures already use, so the response shape is identical
/// either way.
/// </summary>
public sealed class ValidationActionFilter(IServiceProvider serviceProvider) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (serviceProvider.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationResult = await validator.ValidateAsync(new ValidationContext<object>(argument));
            if (!validationResult.IsValid)
            {
                var details = validationResult.Errors
                    .Select(e => new ValidationDetail(e.PropertyName, e.ErrorMessage))
                    .ToList();

                context.Result = ServiceResult<object>.ValidationFailed(details).ToActionResult(context.HttpContext);
                return;
            }
        }

        await next();
    }
}
