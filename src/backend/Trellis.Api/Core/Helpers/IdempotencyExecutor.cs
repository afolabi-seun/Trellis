using Trellis.Api.Core.Results;

namespace Trellis.Api.Core.Helpers;

/// <summary>
/// A plain, publicly-constructible mirror of <see cref="ServiceResult{T}"/>'s fields — the
/// sealed <c>ServiceResult&lt;T&gt;</c> itself has no public constructor and get-only properties,
/// so it isn't something System.Text.Json can round-trip directly. This is the JSON shape
/// actually stored in <c>idempotency_key.response_json</c>; <see cref="ServiceResult{T}.FromCache"/>
/// rebuilds the real type from it on a cache hit.
/// </summary>
internal sealed record IdempotencyCachedResult<T>(
    ServiceResultOutcome Outcome, T? Data, string? ErrorCode, string? Message, IReadOnlyList<ValidationDetail>? ValidationDetails);

/// <summary>
/// api_standards.md's Idempotency section, verbatim in spirit. A client-supplied
/// <c>X-Idempotency-Key</c> makes a retry of the same POST — a flaky mobile connection during
/// Phase 5's offline sync being the concrete case that finally required building this — return the
/// cached first result instead of re-running <paramref name="operation"/> a second time.
/// </summary>
public sealed class IdempotencyExecutor(IIdempotencyStore idempotencyStore)
{
    public async Task<ServiceResult<T>> ExecuteIdempotentAsync<T>(
        Guid tenantId, string idempotencyKey, Func<Task<ServiceResult<T>>> operation)
    {
        var cached = await idempotencyStore.GetAsync<IdempotencyCachedResult<T>>(tenantId, idempotencyKey);
        if (cached is not null)
        {
            return ServiceResult<T>.FromCache(cached.Outcome, cached.Data, cached.ErrorCode, cached.Message, cached.ValidationDetails);
        }

        var result = await operation();
        await idempotencyStore.SetAsync(
            tenantId, idempotencyKey,
            new IdempotencyCachedResult<T>(result.Outcome, result.Data, result.ErrorCode, result.Message, result.ValidationDetails),
            TimeSpan.FromHours(24));
        return result;
    }
}
