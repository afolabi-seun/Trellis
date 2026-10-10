using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.PropertyAsset.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Infrastructure.Data;

namespace Trellis.Api.Modules.PropertyAsset.Infrastructure.Repositories;

public sealed class LeaseRepository(PropertyDbContext dbContext) : TenantAwareRepository<Lease>, ILeaseRepository
{
    public async Task AddAsync(Lease lease) => await dbContext.Leases.AddAsync(lease);

    public Task<Lease?> GetByIdAsync(Guid leaseId) =>
        Scoped(dbContext.Leases).FirstOrDefaultAsync(l => l.LeaseId == leaseId);

    public Task<Lease?> GetByLeaseApplicationIdAsync(Guid leaseApplicationId) =>
        Scoped(dbContext.Leases).FirstOrDefaultAsync(l => l.LeaseApplicationId == leaseApplicationId);

    public Task<Lease?> GetActiveForUnitAsync(Guid unitAssetId) =>
        Scoped(dbContext.Leases).FirstOrDefaultAsync(l => l.UnitAssetId == unitAssetId && l.Status == LeaseStatus.Active);

    public async Task<IReadOnlyList<Lease>> ListActiveFixedEndingOnOrBeforeAsync(DateOnly endingOnOrBefore) =>
        await Scoped(dbContext.Leases)
            .Where(l => l.Status == LeaseStatus.Active && l.TermType == LeaseTermType.Fixed && l.EndDate <= endingOnOrBefore)
            .OrderBy(l => l.EndDate).ThenBy(l => l.LeaseId)
            .ToListAsync();

    public async Task<IReadOnlyList<Lease>> ListActiveWithTermNotRecordedAsync() =>
        await Scoped(dbContext.Leases)
            .Where(l => l.Status == LeaseStatus.Active && l.TermType == LeaseTermType.NotRecorded)
            .OrderBy(l => l.StartDate).ThenBy(l => l.LeaseId)
            .ToListAsync();

    public string GetRowVersion(Lease lease) => RowVersionHelper.Get(dbContext, lease);

    public void SetExpectedRowVersion(Lease lease, string rowVersion) => RowVersionHelper.SetExpected(dbContext, lease, rowVersion);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
