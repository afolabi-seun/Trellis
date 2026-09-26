namespace Trellis.Api.Core.Helpers;

/// <summary>api_standards.md's Idempotency section — the storage seam <see cref="IdempotencyExecutor"/> is built against.</summary>
public interface IIdempotencyStore
{
    Task<T?> GetAsync<T>(Guid tenantId, string idempotencyKey);

    Task SetAsync<T>(Guid tenantId, string idempotencyKey, T response, TimeSpan ttl);
}
