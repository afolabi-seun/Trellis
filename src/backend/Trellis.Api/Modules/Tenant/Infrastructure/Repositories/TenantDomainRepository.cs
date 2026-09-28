using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Trellis.Api.Modules.Tenant.Domain.Entities;
using Trellis.Api.Modules.Tenant.Infrastructure.Data;

namespace Trellis.Api.Modules.Tenant.Infrastructure.Repositories;

public sealed class TenantDomainRepository(TenantDbContext dbContext) : TenantAwareRepository<TenantDomain>, ITenantDomainRepository
{
    public async Task AddAsync(TenantDomain domain) => await dbContext.TenantDomains.AddAsync(domain);

    public Task<TenantDomain?> GetByIdAsync(Guid tenantDomainId) =>
        Scoped(dbContext.TenantDomains).FirstOrDefaultAsync(d => d.TenantDomainId == tenantDomainId);

    public async Task<IReadOnlyList<TenantDomain>> ListForTenantAsync() =>
        await Scoped(dbContext.TenantDomains).OrderBy(d => d.CreatedAt).ToListAsync();

    public Task<TenantDomain?> GetVerifiedByHostAsync(string normalizedHost) =>
        dbContext.TenantDomains.FirstOrDefaultAsync(d => d.Domain == normalizedHost && d.Status == TenantDomainStatus.Verified);

    public Task<TenantDomain?> GetPrimaryVerifiedForTenantAsync(Guid tenantId) =>
        dbContext.TenantDomains
            .Where(d => d.TenantId == tenantId && d.Status == TenantDomainStatus.Verified)
            .OrderBy(d => d.VerifiedAt)
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<string>> ListAllVerifiedHostsAsync() =>
        await dbContext.TenantDomains.Where(d => d.Status == TenantDomainStatus.Verified).Select(d => d.Domain).ToListAsync();

    public void Remove(TenantDomain domain) => dbContext.TenantDomains.Remove(domain);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
