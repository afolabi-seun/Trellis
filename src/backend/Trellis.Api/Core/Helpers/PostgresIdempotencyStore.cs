using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Middleware;

namespace Trellis.Api.Core.Helpers;

/// <summary>
/// Backed by a plain Postgres table, no Redis (api_standards.md's own stated trade-off). Runs
/// under <see cref="SystemContext.Enter"/> with an explicitly-passed <paramref name="tenantId"/>
/// rather than reading <see cref="TenantContext.Current"/> — the one caller this phase has
/// (a Vendor's offline-sync completion) never has a TenantContext at all, only a VendorContext,
/// and the tenant to scope the cache row to is already a resolved fact by the time this is
/// called (IVendorAssignmentReader.GetAssignmentTenantIdAsync), not something re-derived here.
/// </summary>
public sealed class PostgresIdempotencyStore(CoreDbContext dbContext) : IIdempotencyStore
{
    public async Task<T?> GetAsync<T>(Guid tenantId, string idempotencyKey)
    {
        using var systemScope = SystemContext.Enter();

        var row = await dbContext.IdempotencyKeys
            .FirstOrDefaultAsync(k => k.TenantId == tenantId && k.Key == idempotencyKey && k.ExpiresAt > DateTimeOffset.UtcNow);

        return row is null ? default : JsonSerializer.Deserialize<T>(row.ResponseJson);
    }

    public async Task SetAsync<T>(Guid tenantId, string idempotencyKey, T response, TimeSpan ttl)
    {
        using var systemScope = SystemContext.Enter();

        var responseJson = JsonSerializer.Serialize(response);
        await dbContext.IdempotencyKeys.AddAsync(IdempotencyKey.Create(tenantId, idempotencyKey, responseJson, ttl));
        await dbContext.SaveChangesAsync();
    }
}
