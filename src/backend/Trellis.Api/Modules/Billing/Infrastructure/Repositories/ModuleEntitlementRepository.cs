using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Trellis.Api.Modules.Billing.Infrastructure.Data;

namespace Trellis.Api.Modules.Billing.Infrastructure.Repositories;

public sealed class ModuleEntitlementRepository(BillingDbContext dbContext) : TenantAwareRepository<ModuleEntitlement>, IModuleEntitlementRepository
{
    public async Task AddAsync(ModuleEntitlement entitlement) => await dbContext.ModuleEntitlements.AddAsync(entitlement);

    // Not Scoped() — RequiresModuleEntitlementFilter calls this with the request's own
    // TenantContext.Current.TenantId as an explicit param, and PlatformBillingController calls
    // it cross-tenant as SUPER_ADMIN; neither goes through the ambient Scoped() filter.
    public Task<ModuleEntitlement?> GetAsync(Guid tenantId, string moduleKey) =>
        dbContext.ModuleEntitlements.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.ModuleKey == moduleKey);

    public async Task<IReadOnlyList<ModuleEntitlement>> ListForTenantAsync(Guid tenantId) =>
        await dbContext.ModuleEntitlements.Where(e => e.TenantId == tenantId).ToListAsync();

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
