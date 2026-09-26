namespace Trellis.Api.Core.Data;

/// <summary>
/// api_standards.md's Idempotency section — backs <see cref="Trellis.Api.Core.Helpers.IdempotencyExecutor"/>.
/// A cached response for a given tenant + client-supplied key, replayed verbatim for 24h so a
/// flaky-connection retry of the same request never re-executes the underlying operation. Phase 5
/// is its first real caller (the vendor's offline inspection-sync completion), but the mechanism
/// itself is generic — any future POST that needs retry-safety can reuse it.
/// </summary>
public sealed class IdempotencyKey
{
    public Guid TenantId { get; private init; }
    public string Key { get; private init; } = null!;
    public string ResponseJson { get; private init; } = null!;
    public DateTimeOffset ExpiresAt { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    private IdempotencyKey() { }

    public static IdempotencyKey Create(Guid tenantId, string key, string responseJson, TimeSpan ttl) => new()
    {
        TenantId = tenantId,
        Key = key,
        ResponseJson = responseJson,
        ExpiresAt = DateTimeOffset.UtcNow.Add(ttl),
        CreatedAt = DateTimeOffset.UtcNow,
    };
}
