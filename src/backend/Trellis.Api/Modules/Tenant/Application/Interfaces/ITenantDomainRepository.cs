using Trellis.Api.Modules.Tenant.Domain.Entities;

namespace Trellis.Api.Modules.Tenant.Application.Interfaces;

public interface ITenantDomainRepository
{
    Task AddAsync(TenantDomain domain);

    Task<TenantDomain?> GetByIdAsync(Guid tenantDomainId);

    Task<IReadOnlyList<TenantDomain>> ListForTenantAsync();

    /// <summary>Anonymous host resolution — cross-tenant by nature, so unscoped (the caller wraps it in SystemContext).</summary>
    Task<TenantDomain?> GetVerifiedByHostAsync(string normalizedHost);

    /// <summary>Explicit tenantId, unscoped — used when building an emailed link on an anonymous path.</summary>
    Task<TenantDomain?> GetPrimaryVerifiedForTenantAsync(Guid tenantId);

    /// <summary>Every VERIFIED domain across tenants — the CORS origin snapshot.</summary>
    Task<IReadOnlyList<string>> ListAllVerifiedHostsAsync();

    void Remove(TenantDomain domain);

    Task SaveChangesAsync();
}
