using Trellis.Api.Modules.RentLedger.Domain.Entities;

namespace Trellis.Api.Modules.RentLedger.Application.Interfaces;

public interface IRentPaymentRecordRepository
{
    Task AddAsync(RentPaymentRecord record);

    Task<RentPaymentRecord?> GetByIdAsync(Guid rentPaymentRecordId);

    Task<IReadOnlyList<RentPaymentRecord>> ListForScheduleAsync(Guid rentScheduleId);

    /// <summary>Sum of non-voided amounts per schedule id. Schedules with no records are simply absent from the result.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetReceivedTotalsAsync(IReadOnlyCollection<Guid> rentScheduleIds);

    /// <summary>Non-voided amounts with DateReceived inside [periodStart, periodEnd] across the given schedules — the Owner report's "received in period".</summary>
    Task<decimal> GetReceivedInPeriodAsync(IReadOnlyCollection<Guid> rentScheduleIds, DateOnly periodStart, DateOnly periodEnd);

    Task SaveChangesAsync();
}
