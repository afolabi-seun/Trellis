using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Application.Interfaces;

public interface IModuleEntitlementRepository
{
    Task AddAsync(ModuleEntitlement entitlement);

    /// <summary>The RequiresModuleEntitlementAttribute's own hot path — an explicit tenantId param since it's called with only TenantContext.Current's Guid, not an ambient-scoped query.</summary>
    Task<ModuleEntitlement?> GetAsync(Guid tenantId, string moduleKey);

    Task<IReadOnlyList<ModuleEntitlement>> ListForTenantAsync(Guid tenantId);

    Task SaveChangesAsync();
}
