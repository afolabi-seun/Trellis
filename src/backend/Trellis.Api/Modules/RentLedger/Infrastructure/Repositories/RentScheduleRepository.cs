using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Domain.Entities;
using Trellis.Api.Modules.RentLedger.Infrastructure.Data;

namespace Trellis.Api.Modules.RentLedger.Infrastructure.Repositories;

public sealed class RentScheduleRepository(RentLedgerDbContext dbContext) : TenantAwareRepository<RentSchedule>, IRentScheduleRepository
{
    public async Task AddAsync(RentSchedule schedule) => await dbContext.RentSchedules.AddAsync(schedule);

    public Task<RentSchedule?> GetByIdAsync(Guid rentScheduleId) =>
        Scoped(dbContext.RentSchedules).FirstOrDefaultAsync(s => s.RentScheduleId == rentScheduleId);

    public async Task<(IReadOnlyList<RentSchedule> Items, bool HasMore, int TotalCount)> ListForTenantAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        var baseQuery = Scoped(dbContext.RentSchedules);
        var totalCount = await baseQuery.CountAsync();

        var query = baseQuery;
        if (cursor is { } c)
        {
            query = query.Where(s => s.CreatedAt > c.CreatedAt || (s.CreatedAt == c.CreatedAt && s.RentScheduleId > c.Id));
        }

        var page = await query.OrderBy(s => s.CreatedAt).ThenBy(s => s.RentScheduleId).Take(limit + 1).ToListAsync();
        return (page.Take(limit).ToList(), page.Count > limit, totalCount);
    }

    public async Task<IReadOnlyList<RentSchedule>> ListActiveForTenantAsync() =>
        await Scoped(dbContext.RentSchedules).Where(s => s.Status == RentScheduleStatus.Active).ToListAsync();

    public async Task<IReadOnlyList<RentSchedule>> ListForUnitsAsync(IReadOnlyCollection<Guid> unitAssetIds) =>
        await Scoped(dbContext.RentSchedules).Where(s => unitAssetIds.Contains(s.UnitAssetId)).ToListAsync();

    public Task<RentSchedule?> GetByLeaseApplicationIdAsync(Guid tenantId, Guid leaseApplicationId) =>
        dbContext.RentSchedules.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.LeaseApplicationId == leaseApplicationId);

    public Task<RentSchedule?> GetActiveForLeaseAsync(Guid tenantId, Guid leaseId) =>
        dbContext.RentSchedules.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.LeaseId == leaseId && s.Status == RentScheduleStatus.Active);

    public async Task AddRentChangeAsync(RentChange change) => await dbContext.RentChanges.AddAsync(change);

    public Task<RentSchedule?> GetActiveForUnitAsync(Guid tenantId, Guid unitAssetId) =>
        dbContext.RentSchedules.FirstOrDefaultAsync(s =>
            s.TenantId == tenantId && s.UnitAssetId == unitAssetId && s.Status == RentScheduleStatus.Active);

    public async Task<IReadOnlyList<RentSchedule>> ListActiveAcrossTenantsAsync(Guid? afterScheduleId, int batchSize)
    {
        var query = dbContext.RentSchedules.Where(s => s.Status == RentScheduleStatus.Active);
        if (afterScheduleId is { } after)
        {
            query = query.Where(s => s.RentScheduleId.CompareTo(after) > 0);
        }

        return await query.OrderBy(s => s.RentScheduleId).Take(batchSize).ToListAsync();
    }

    public void DiscardChanges(RentSchedule schedule) => dbContext.Entry(schedule).State = EntityState.Detached;

    public string GetRowVersion(RentSchedule schedule) => RowVersionHelper.Get(dbContext, schedule);

    public void SetExpectedRowVersion(RentSchedule schedule, string rowVersion) => RowVersionHelper.SetExpected(dbContext, schedule, rowVersion);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
