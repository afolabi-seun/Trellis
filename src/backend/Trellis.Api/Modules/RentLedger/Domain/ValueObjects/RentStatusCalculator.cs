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
    public static RentEvaluation Evaluate(
        DateOnly startDate, int dueDayOfMonth, int gracePeriodDays, decimal rentAmount, decimal totalReceived, DateOnly asOf)
    {
        var dueDates = DueDatesThrough(startDate, dueDayOfMonth, asOf);
        var pastGrace = dueDates.Count(d => d.AddDays(gracePeriodDays) < asOf);

        var rentDue = rentAmount * dueDates.Count;
        var amountShort = Math.Max(0m, rentDue - totalReceived);

        if (totalReceived >= rentDue)
        {
            return new RentEvaluation(RentStatus.Current, 0, 0m, null, null);
        }

        var fullyCovered = (int)Math.Floor(totalReceived / rentAmount);
        var oldestUnpaid = dueDates[fullyCovered];

        var rentPastGrace = rentAmount * pastGrace;
        if (totalReceived >= rentPastGrace)
        {
            return new RentEvaluation(RentStatus.Overdue, 0, amountShort, oldestUnpaid, null);
        }

        var periodsInArrears = (int)Math.Ceiling((rentPastGrace - totalReceived) / rentAmount);
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
