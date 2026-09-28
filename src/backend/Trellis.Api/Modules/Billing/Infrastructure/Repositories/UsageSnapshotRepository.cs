using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Trellis.Api.Modules.Billing.Infrastructure.Data;

namespace Trellis.Api.Modules.Billing.Infrastructure.Repositories;

public sealed class UsageSnapshotRepository(BillingDbContext dbContext) : TenantAwareRepository<UsageSnapshot>, IUsageSnapshotRepository
{
    public async Task AddAsync(UsageSnapshot snapshot) => await dbContext.UsageSnapshots.AddAsync(snapshot);

    public Task<UsageSnapshot?> GetLatestForTenantAsync(Guid tenantId) =>
        dbContext.UsageSnapshots.Where(u => u.TenantId == tenantId).OrderByDescending(u => u.CapturedAt).FirstOrDefaultAsync();

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
