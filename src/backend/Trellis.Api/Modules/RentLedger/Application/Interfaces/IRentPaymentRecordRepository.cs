using Trellis.Api.Modules.RentLedger.Domain.Entities;

namespace Trellis.Api.Modules.RentLedger.Application.Interfaces;

public interface IRentPaymentRecordRepository
{
    Task AddAsync(RentPaymentRecord record);

    Task<RentPaymentRecord?> GetByIdAsync(Guid rentPaymentRecordId);

    Task<IReadOnlyList<RentPaymentRecord>> ListForScheduleAsync(Guid rentScheduleId);

    /// <summary>Sum of non-voided amounts per schedule id. Schedules with no records are simply absent from the result.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetReceivedTotalsAsync(IReadOnlyCollection<Guid> rentScheduleIds);

    /// <summary>Non-voided amounts with DateReceived inside [periodStart, periodEnd] across the given schedules, grouped by currency — the Owner report's "received in period". A schedule with no payments in the period is simply absent, never a zero entry.</summary>
    Task<IReadOnlyDictionary<string, decimal>> GetReceivedInPeriodByCurrencyAsync(
        IReadOnlyDictionary<Guid, string> scheduleCurrencies, DateOnly periodStart, DateOnly periodEnd);

    /// <summary>BDR-RENT-007 — every non-voided payment's date and amount, per schedule. Explicit ids, like GetReceivedTotalsAsync.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<(DateOnly DateReceived, decimal Amount)>>> GetPaymentsAsync(IReadOnlyCollection<Guid> rentScheduleIds);

    Task SaveChangesAsync();
}
