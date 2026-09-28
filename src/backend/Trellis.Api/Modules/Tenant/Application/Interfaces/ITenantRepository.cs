using TenantEntity = Trellis.Api.Modules.Tenant.Domain.Entities.Tenant;

namespace Trellis.Api.Modules.Tenant.Application.Interfaces;

/// <summary>
/// Aliased as <c>TenantEntity</c> throughout this module — <c>Tenant</c> the class and
/// <c>Modules.Tenant</c> the namespace segment collide, since this is the one module whose name
/// matches its own aggregate root's name.
/// </summary>
public interface ITenantRepository
{
    Task AddAsync(TenantEntity tenant);

    Task<TenantEntity?> GetByIdAsync(Guid tenantId);

    Task<TenantEntity?> GetBySlugAsync(string slug);

    Task<bool> SlugExistsAsync(string slug);

    /// <summary>Keyset pagination ordered by (created_at, tenant_id) — fetches one extra row past <paramref name="limit"/> to determine <c>has_more</c> without a second query.</summary>
    Task<(IReadOnlyList<TenantEntity> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    Task SaveChangesAsync();
}
