using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Helpers;

namespace Trellis.Api.Core.Results;

/// <summary>
/// Implements error_handling.md's "ServiceResult&lt;T&gt; Outcome → HTTP Mapping" table — the
/// single place a <see cref="ServiceResult{T}"/> becomes an HTTP response. A Controller calls
/// this and nothing else; it never re-derives a status code from an outcome itself
/// (project_architecture.md, "Thin Controllers").
/// </summary>
public static class ServiceResultExtensions
{
    public static IActionResult ToActionResult<T>(this ServiceResult<T> result, HttpContext context)
    {
        var meta = ApiMeta.Now(context.TraceIdentifier);

        return result.Outcome switch
        {
            ServiceResultOutcome.Success => new ObjectResult(ApiResponse<T>.Success(result.Data!, meta))
            {
                StatusCode = StatusCodes.Status200OK,
            },

            ServiceResultOutcome.NotFound => new ObjectResult(ApiResponse<object>.Failure(
                new ApiError { Code = ErrorCodeRegistry.Platform.NotFound, Message = "The requested resource was not found." },
                meta))
            {
                StatusCode = StatusCodes.Status404NotFound,
            },

            ServiceResultOutcome.ValidationFailed => new ObjectResult(ApiResponse<object>.Failure(
                new ApiError
                {
                    Code = ErrorCodeRegistry.Platform.ValidationFailed,
                    Message = "The request could not be processed as submitted.",
                    Details = result.ValidationDetails?
                        .Select(d => new ApiErrorDetail(d.Field, d.Issue))
                        .ToList(),
                },
                meta))
            {
                StatusCode = StatusCodes.Status400BadRequest,
            },

            ServiceResultOutcome.BusinessRuleViolation => new ObjectResult(ApiResponse<object>.Failure(
                new ApiError { Code = result.ErrorCode!, Message = result.Message ?? "The request violates a business rule." },
                meta))
            {
                StatusCode = StatusCodes.Status422UnprocessableEntity,
            },

            ServiceResultOutcome.Conflict => new ObjectResult(ApiResponse<object>.Failure(
                new ApiError { Code = result.ErrorCode!, Message = result.Message ?? "The request conflicts with the current state." },
                meta))
            {
                StatusCode = StatusCodes.Status409Conflict,
            },

            // Same HTTP status as Conflict, but a distinct error code, so the client can tell
            // "already exists" apart from "changed under you" — error_handling.md.
            ServiceResultOutcome.ConcurrencyConflict => new ObjectResult(ApiResponse<object>.Failure(
                new ApiError { Code = ErrorCodeRegistry.Platform.ConcurrencyConflict, Message = "This record was changed by someone else. Refresh and try again." },
                meta))
            {
                StatusCode = StatusCodes.Status409Conflict,
            },

            ServiceResultOutcome.Forbidden => new ObjectResult(ApiResponse<object>.Failure(
                new ApiError { Code = result.ErrorCode!, Message = result.Message ?? "You do not have permission to perform this action." },
                meta))
            {
                StatusCode = StatusCodes.Status403Forbidden,
            },

            ServiceResultOutcome.Failure => new ObjectResult(ApiResponse<object>.Failure(
                new ApiError { Code = result.ErrorCode!, Message = result.Message ?? "An unexpected error occurred." },
                meta))
            {
                StatusCode = StatusCodes.Status500InternalServerError,
            },

            _ => throw new InvalidOperationException($"Unmapped {nameof(ServiceResultOutcome)}: {result.Outcome}"),
        };
    }
}
