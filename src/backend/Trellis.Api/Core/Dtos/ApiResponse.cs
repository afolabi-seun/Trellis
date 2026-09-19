namespace Trellis.Api.Core.Dtos;

/// <summary>One field-level problem inside an error response — api_standards.md's Response Envelope.</summary>
public sealed record ApiErrorDetail(string Field, string Issue);

/// <summary>api_standards.md's Response Envelope error shape: <c>{ code, message, details }</c>.</summary>
public sealed class ApiError
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public IReadOnlyList<ApiErrorDetail>? Details { get; init; }

    /// <summary>
    /// Present only on an unhandled-fault (500) response, so a user reporting the issue can
    /// hand over one ID that ties the client error to the server log line — error_handling.md's
    /// ExceptionHandlingMiddleware.
    /// </summary>
    public string? RequestId { get; init; }
}

/// <summary>api_standards.md's Response Envelope <c>meta</c> block.</summary>
public sealed class ApiMeta
{
    public required string RequestId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    public static ApiMeta Now(string requestId) => new() { RequestId = requestId, Timestamp = DateTimeOffset.UtcNow };
}

/// <summary>
/// The one response shape every Trellis endpoint returns, success or error
/// (api_standards.md, "Response Envelope"). Built from a <see cref="Results.ServiceResult{T}"/>
/// at the controller boundary via <c>ToActionResult()</c> — a Controller never constructs this
/// directly.
/// </summary>
public sealed class ApiResponse<T>
{
    public string Status { get; private init; } = "success";
    public T? Data { get; private init; }
    public ApiError? Error { get; private init; }
    public required ApiMeta Meta { get; init; }

    public static ApiResponse<T> Success(T data, ApiMeta meta) =>
        new() { Status = "success", Data = data, Meta = meta };

    public static ApiResponse<T> Failure(ApiError error, ApiMeta meta) =>
        new() { Status = "error", Error = error, Meta = meta };
}
