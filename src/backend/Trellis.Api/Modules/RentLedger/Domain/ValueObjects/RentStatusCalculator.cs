namespace Trellis.Api.Modules.RentLedger.Domain.ValueObjects;

/// <summary>
/// BDR-RENT-003 — arrears is computed, never stored. A cumulative model rather than per-period
/// payment matching: total recorded as received is compared with total rent due so far, so late,
/// partial, early, and multiple payments all just work with no allocation rules to get wrong.
/// A due date is the schedule's due day within a month, clamped to that month's length (a due day
/// of 31 falls on the 28th/29th/30th where the month is shorter); the first due date is the first
/// one on or after StartDate.
/// </summary>
public static class RentStatusCalculator
{
    /// <summary>One rent amount for every period — a schedule with no rent changes.</summary>
    public static RentEvaluation Evaluate(
        DateOnly startDate, int dueDayOfMonth, int gracePeriodDays, decimal rentAmount, decimal totalReceived, DateOnly asOf) =>
        Evaluate(startDate, dueDayOfMonth, gracePeriodDays, _ => rentAmount, totalReceived, asOf);

    /// <summary>
    /// Each period's rent is <paramref name="rentOn"/> its due date, so a rent change (BDR-LSE-010)
    /// applies only to periods due on or after it. Payments still cover periods oldest first.
    /// </summary>
    public static RentEvaluation Evaluate(
        DateOnly startDate, int dueDayOfMonth, int gracePeriodDays, Func<DateOnly, decimal> rentOn, decimal totalReceived, DateOnly asOf)
    {
        var dueDates = DueDatesThrough(startDate, dueDayOfMonth, asOf);
        var amounts = dueDates.Select(rentOn).ToList();
        var pastGrace = dueDates.Count(d => d.AddDays(gracePeriodDays) < asOf);

        var rentDue = amounts.Sum();
        var amountShort = Math.Max(0m, rentDue - totalReceived);

        if (totalReceived >= rentDue)
        {
            return new RentEvaluation(RentStatus.Current, 0, 0m, null, null);
        }

        // Periods fully covered, oldest first: the first period the running total can't cover is the oldest unpaid.
        var fullyCovered = 0;
        var covered = 0m;
        while (fullyCovered < amounts.Count && covered + amounts[fullyCovered] <= totalReceived)
        {
            covered += amounts[fullyCovered];
            fullyCovered++;
        }

        var oldestUnpaid = dueDates[fullyCovered];

        var rentPastGrace = amounts.Take(pastGrace).Sum();
        if (totalReceived >= rentPastGrace)
        {
            return new RentEvaluation(RentStatus.Overdue, 0, amountShort, oldestUnpaid, null);
        }

        var periodsInArrears = pastGrace - fullyCovered;
        return new RentEvaluation(RentStatus.InArrears, periodsInArrears, amountShort, oldestUnpaid, dueDates[pastGrace - 1]);
    }

    /// <summary>Every due date from the first one on/after startDate up to and including asOf, in order.</summary>
    private static List<DateOnly> DueDatesThrough(DateOnly startDate, int dueDayOfMonth, DateOnly asOf)
    {
        var dueDates = new List<DateOnly>();
        var monthStart = new DateOnly(startDate.Year, startDate.Month, 1);

        while (true)
        {
            var due = DueDateIn(monthStart, dueDayOfMonth);
            if (due > asOf)
            {
                break;
            }

            if (due >= startDate)
            {
                dueDates.Add(due);
            }

            monthStart = monthStart.AddMonths(1);
        }

        return dueDates;
    }

    private static DateOnly DueDateIn(DateOnly monthStart, int dueDayOfMonth) =>
        new(monthStart.Year, monthStart.Month, Math.Min(dueDayOfMonth, DateTime.DaysInMonth(monthStart.Year, monthStart.Month)));
}
