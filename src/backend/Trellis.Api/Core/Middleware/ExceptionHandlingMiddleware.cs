using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Helpers;

namespace Trellis.Api.Core.Middleware;

/// <summary>
/// Catches whatever a Service method didn't (error_handling.md, "Global Exception Handling
/// Middleware") — a genuine bug, not an expected business outcome (those return a
/// <see cref="Results.ServiceResult{T}"/> and never reach here). The client only ever sees a
/// generic message and a correlation ID, never the exception's message, type, or stack trace.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.TraceIdentifier;
            logger.LogError(ex, "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);

            var response = ApiResponse<object>.Failure(
                new ApiError
                {
                    Code = ErrorCodeRegistry.Platform.UnhandledFault,
                    Message = "An unexpected error occurred. Reference this ID if reporting the issue.",
                    RequestId = correlationId,
                },
                ApiMeta.Now(correlationId));

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
