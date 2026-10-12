namespace Trellis.Api.Modules.RentLedger.Domain.ValueObjects;

/// <summary>
/// BDR-RENT-007 — how promptly rent was recorded as received. Counts, not percentages, so that
/// schedules, properties, and Owners roll up by adding (BDR-RPT-005: weighted by the underlying
/// count, never an average of averages). A period that isn't assessed yet (still inside its grace
/// window) is in none of the counts.
/// </summary>
public sealed record PaymentTimeliness(int PeriodsAssessed, int PeriodsOnTime, int TotalDaysLate)
{
    public static PaymentTimeliness None { get; } = new(0, 0, 0);

    public int PeriodsLate => PeriodsAssessed - PeriodsOnTime;

    /// <summary>Null when nothing has been assessed — "no data", never a measured 0% or 100%.</summary>
    public decimal? OnTimeRate => PeriodsAssessed == 0 ? null : Math.Round((decimal)PeriodsOnTime / PeriodsAssessed, 4);

    /// <summary>Across late periods only; null when none were late.</summary>
    public decimal? AverageDaysLate => PeriodsLate == 0 ? null : Math.Round((decimal)TotalDaysLate / PeriodsLate, 1);

    public PaymentTimeliness Add(PaymentTimeliness other) =>
        new(PeriodsAssessed + other.PeriodsAssessed, PeriodsOnTime + other.PeriodsOnTime, TotalDaysLate + other.TotalDaysLate);
}

/// <summary>
/// The same cumulative model as <see cref="RentStatusCalculator"/>: payments cover periods oldest
/// first, so a period counts as paid on the date the running total of recorded payments first
/// covered it. It is on time if that date is no later than its due date plus the grace period, and
/// is assessed only once that deadline has passed. A late period's days late run from its due date
/// to the date it was covered — or to <c>asOf</c> while it is still uncovered, so an unpaid month
/// counts as late now rather than disappearing until someone pays it.
/// </summary>
public static class PaymentTimelinessCalculator
{
    public static PaymentTimeliness Calculate(
        DateOnly startDate, int dueDayOfMonth, int gracePeriodDays, Func<DateOnly, decimal> rentOn,
        IReadOnlyList<(DateOnly DateReceived, decimal Amount)> payments, DateOnly asOf,
        DateOnly? dueFrom = null, DateOnly? dueTo = null)
    {
        var dueDates = RentStatusCalculator.DueDatesThrough(startDate, dueDayOfMonth, asOf);
        var received = payments.Where(p => p.DateReceived <= asOf).OrderBy(p => p.DateReceived).ToList();

        int assessed = 0, onTime = 0, daysLate = 0;
        var cumulativeDue = 0m;
        var paymentIndex = 0;
        var cumulativeReceived = 0m;

        foreach (var dueDate in dueDates)
        {
            cumulativeDue += rentOn(dueDate);

            // Advance through payments until this period is covered (or they run out).
            while (cumulativeReceived < cumulativeDue && paymentIndex < received.Count)
            {
                cumulativeReceived += received[paymentIndex].Amount;
                paymentIndex++;
            }

            DateOnly? coveredOn = cumulativeReceived >= cumulativeDue && paymentIndex > 0
                ? received[paymentIndex - 1].DateReceived
                : cumulativeReceived >= cumulativeDue ? dueDate : null;

            var deadline = dueDate.AddDays(gracePeriodDays);
            var inWindow = (dueFrom is null || dueDate >= dueFrom) && (dueTo is null || dueDate <= dueTo);
            if (deadline >= asOf || !inWindow)
            {
                continue;
            }

            assessed++;
            if (coveredOn is { } covered && covered <= deadline)
            {
                onTime++;
            }
            else
            {
                daysLate += (coveredOn ?? asOf).DayNumber - dueDate.DayNumber;
            }
        }

        return new PaymentTimeliness(assessed, onTime, daysLate);
    }
}
