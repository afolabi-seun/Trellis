namespace Trellis.Api.Core.Results;

/// <summary>
/// Every outcome a Service method can return. See error_handling.md's
/// "ServiceResult&lt;T&gt; Outcome → HTTP Mapping" table for the HTTP status each maps to —
/// that mapping lives in <see cref="ServiceResultExtensions.ToActionResult{T}"/>, not here.
/// </summary>
public enum ServiceResultOutcome
{
    Success,
    NotFound,
    ValidationFailed,
    BusinessRuleViolation,
    Conflict,
    ConcurrencyConflict,
    Forbidden,
    Failure,
}

/// <summary>A single field-level validation problem, carried by <see cref="ServiceResult{T}.ValidationFailed"/>.</summary>
public sealed record ValidationDetail(string Field, string Issue);

/// <summary>
/// The return type of every Application-layer Service method (coding_standards.md,
/// project_architecture.md's Thin Controllers section). A Controller never throws for an
/// expected business outcome — it inspects this and calls <c>ToActionResult()</c>.
/// </summary>
public sealed class ServiceResult<T>
{
    public ServiceResultOutcome Outcome { get; }
    public T? Data { get; }
    public string? ErrorCode { get; }
    public string? Message { get; }
    public IReadOnlyList<ValidationDetail>? ValidationDetails { get; }

    private ServiceResult(
        ServiceResultOutcome outcome,
        T? data = default,
        string? errorCode = null,
        string? message = null,
        IReadOnlyList<ValidationDetail>? validationDetails = null)
    {
        Outcome = outcome;
        Data = data;
        ErrorCode = errorCode;
        Message = message;
        ValidationDetails = validationDetails;
    }

    public static ServiceResult<T> Success(T data) =>
        new(ServiceResultOutcome.Success, data: data);

    public static ServiceResult<T> NotFound() =>
        new(ServiceResultOutcome.NotFound);

    public static ServiceResult<T> ValidationFailed(IReadOnlyList<ValidationDetail> details) =>
        new(ServiceResultOutcome.ValidationFailed, validationDetails: details);

    public static ServiceResult<T> BusinessRuleViolation(string errorCode) =>
        new(ServiceResultOutcome.BusinessRuleViolation, errorCode: errorCode);

    public static ServiceResult<T> Conflict(string errorCode, string message) =>
        new(ServiceResultOutcome.Conflict, errorCode: errorCode, message: message);

    /// <summary>
    /// A stale RowVersion was presented on an update (coding_standards.md, "Optimistic
    /// Concurrency", BDR-NFR-006). Distinct from <see cref="Conflict"/> so the frontend can
    /// tell "already exists" apart from "changed under you" — see error_handling.md.
    /// </summary>
    public static ServiceResult<T> ConcurrencyConflict() =>
        new(ServiceResultOutcome.ConcurrencyConflict);

    public static ServiceResult<T> Forbidden(string errorCode) =>
        new(ServiceResultOutcome.Forbidden, errorCode: errorCode);

    public static ServiceResult<T> Failure(string errorCode, string message) =>
        new(ServiceResultOutcome.Failure, errorCode: errorCode, message: message);
}
