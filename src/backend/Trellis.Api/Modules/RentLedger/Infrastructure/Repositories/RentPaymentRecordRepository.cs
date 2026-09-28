using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Domain.Entities;
using Trellis.Api.Modules.RentLedger.Infrastructure.Data;

namespace Trellis.Api.Modules.RentLedger.Infrastructure.Repositories;

public sealed class RentPaymentRecordRepository(RentLedgerDbContext dbContext) : TenantAwareRepository<RentPaymentRecord>, IRentPaymentRecordRepository
{
    public async Task AddAsync(RentPaymentRecord record) => await dbContext.RentPaymentRecords.AddAsync(record);

    public Task<RentPaymentRecord?> GetByIdAsync(Guid rentPaymentRecordId) =>
        Scoped(dbContext.RentPaymentRecords).FirstOrDefaultAsync(r => r.RentPaymentRecordId == rentPaymentRecordId);

    public async Task<IReadOnlyList<RentPaymentRecord>> ListForScheduleAsync(Guid rentScheduleId) =>
        await Scoped(dbContext.RentPaymentRecords)
            .Where(r => r.RentScheduleId == rentScheduleId)
            .OrderByDescending(r => r.DateReceived).ThenByDescending(r => r.RecordedAt)
            .ToListAsync();

    // Explicit schedule ids and no Scoped(): the arrears job calls this under SystemContext with no ambient tenant, and the ids were already tenant-resolved by whoever fetched the schedules (RLS still applies underneath).
    public async Task<IReadOnlyDictionary<Guid, decimal>> GetReceivedTotalsAsync(IReadOnlyCollection<Guid> rentScheduleIds) =>
        await dbContext.RentPaymentRecords
            .Where(r => rentScheduleIds.Contains(r.RentScheduleId) && r.VoidedAt == null)
            .GroupBy(r => r.RentScheduleId)
            .Select(g => new { RentScheduleId = g.Key, Total = g.Sum(r => r.AmountReceived) })
            .ToDictionaryAsync(x => x.RentScheduleId, x => x.Total);

    public async Task<decimal> GetReceivedInPeriodAsync(IReadOnlyCollection<Guid> rentScheduleIds, DateOnly periodStart, DateOnly periodEnd) =>
        await dbContext.RentPaymentRecords
            .Where(r => rentScheduleIds.Contains(r.RentScheduleId) && r.VoidedAt == null
                && r.DateReceived >= periodStart && r.DateReceived <= periodEnd)
            .SumAsync(r => r.AmountReceived);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
