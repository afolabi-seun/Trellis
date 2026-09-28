using FluentAssertions;
using Trellis.Api.Modules.RentLedger.Domain.ValueObjects;
using Xunit;

namespace Trellis.Tests.RentLedger;

/// <summary>
/// BDR-RENT-003 — the arrears model is cumulative (total received vs total rent due so far), so the
/// cases that matter are the boundaries: the grace window's edges, and partial/early/multiple payments.
/// Every test uses rent 1000, due on the 5th, 5 days' grace, starting 2026-01-01 unless stated.
/// </summary>
public sealed class RentStatusCalculatorTests
{
    private static RentEvaluation Evaluate(decimal received, DateOnly asOf, int dueDay = 5, int grace = 5, DateOnly? start = null, decimal rent = 1000m) =>
        RentStatusCalculator.Evaluate(start ?? new DateOnly(2026, 1, 1), dueDay, grace, rent, received, asOf);

    [Fact]
    public void BeforeTheFirstDueDate_IsCurrent()
    {
        var result = Evaluate(0m, new DateOnly(2026, 1, 4));

        result.Status.Should().Be(RentStatus.Current);
        result.AmountShort.Should().Be(0m);
        result.OldestUnpaidDueDate.Should().BeNull();
    }

    [Fact]
    public void OnTheDueDate_WithNothingRecorded_IsOverdue_NotYetInArrears()
    {
        var result = Evaluate(0m, new DateOnly(2026, 1, 5));

        result.Status.Should().Be(RentStatus.Overdue);
        result.AmountShort.Should().Be(1000m);
        result.OldestUnpaidDueDate.Should().Be(new DateOnly(2026, 1, 5));
        result.PeriodsInArrears.Should().Be(0);
    }

    [Fact]
    public void OnTheLastDayOfTheGraceWindow_IsStillOverdue_NotInArrears()
    {
        // due 5th + 5 days' grace = the 10th is the last day inside grace.
        Evaluate(0m, new DateOnly(2026, 1, 10)).Status.Should().Be(RentStatus.Overdue);
    }

    [Fact]
    public void TheDayAfterTheGraceWindow_IsInArrears()
    {
        var result = Evaluate(0m, new DateOnly(2026, 1, 11));

        result.Status.Should().Be(RentStatus.InArrears);
        result.PeriodsInArrears.Should().Be(1);
        result.LatestPastGraceDue.Should().Be(new DateOnly(2026, 1, 5));
        result.OldestUnpaidDueDate.Should().Be(new DateOnly(2026, 1, 5));
    }

    [Fact]
    public void PaymentInFull_BeforeTheGraceWindowCloses_KeepsTheScheduleCurrent()
    {
        Evaluate(1000m, new DateOnly(2026, 1, 8)).Status.Should().Be(RentStatus.Current);
    }

    [Fact]
    public void ALatePayment_ClearsArrears_WithNoStoredFlagToDrift()
    {
        Evaluate(0m, new DateOnly(2026, 1, 20)).Status.Should().Be(RentStatus.InArrears);
        Evaluate(1000m, new DateOnly(2026, 1, 20)).Status.Should().Be(RentStatus.Current);
    }

    [Fact]
    public void APartialPayment_LeavesTheShortfallInArrears_AndReportsTheAmountShort()
    {
        var result = Evaluate(400m, new DateOnly(2026, 1, 20));

        result.Status.Should().Be(RentStatus.InArrears);
        result.PeriodsInArrears.Should().Be(1);
        result.AmountShort.Should().Be(600m);
        result.OldestUnpaidDueDate.Should().Be(new DateOnly(2026, 1, 5));
    }

    [Fact]
    public void MultiplePartialPayments_AreSummed_NotMatchedPeriodByPeriod()
    {
        // 400 + 600 recorded as two payments is exactly January's rent.
        Evaluate(400m + 600m, new DateOnly(2026, 1, 20)).Status.Should().Be(RentStatus.Current);
    }

    [Fact]
    public void TwoMissedPeriods_CountsBoth_AndPointsAtTheOldestUnpaidOne()
    {
        var result = Evaluate(0m, new DateOnly(2026, 2, 20));

        result.Status.Should().Be(RentStatus.InArrears);
        result.PeriodsInArrears.Should().Be(2);
        result.AmountShort.Should().Be(2000m);
        result.OldestUnpaidDueDate.Should().Be(new DateOnly(2026, 1, 5));
        result.LatestPastGraceDue.Should().Be(new DateOnly(2026, 2, 5));
    }

    [Fact]
    public void AnAdvancePayment_CoversTheNextPeriodsToo()
    {
        // 2000 recorded in early January covers January and February.
        Evaluate(2000m, new DateOnly(2026, 2, 20)).Status.Should().Be(RentStatus.Current);
        Evaluate(2000m, new DateOnly(2026, 3, 20)).Status.Should().Be(RentStatus.InArrears);
    }

    [Fact]
    public void JanuaryPaid_FebruaryMissed_ReportsOnlyFebruaryInArrears()
    {
        var result = Evaluate(1000m, new DateOnly(2026, 2, 20));

        result.Status.Should().Be(RentStatus.InArrears);
        result.PeriodsInArrears.Should().Be(1);
        result.OldestUnpaidDueDate.Should().Be(new DateOnly(2026, 2, 5));
        result.LatestPastGraceDue.Should().Be(new DateOnly(2026, 2, 5));
    }

    [Fact]
    public void AnAlertKey_AdvancesEachTimeAnotherPeriodSlipsPastGrace()
    {
        var february = Evaluate(0m, new DateOnly(2026, 2, 20)).LatestPastGraceDue;
        var march = Evaluate(0m, new DateOnly(2026, 3, 20)).LatestPastGraceDue;

        march.Should().BeAfter(february!.Value);
    }

    [Fact]
    public void AStartDateAfterThisMonthsDueDay_StartsBillingFromNextMonth()
    {
        // Lease starts Jan 20; Jan 5 has already passed, so the first due date is Feb 5.
        var result = Evaluate(0m, new DateOnly(2026, 1, 31), start: new DateOnly(2026, 1, 20));

        result.Status.Should().Be(RentStatus.Current);
        Evaluate(0m, new DateOnly(2026, 2, 5), start: new DateOnly(2026, 1, 20)).OldestUnpaidDueDate.Should().Be(new DateOnly(2026, 2, 5));
    }

    [Fact]
    public void ADueDayOf31_ClampsToTheMonthsLastDay_InAShortMonth()
    {
        // Feb 2026 has 28 days: a due day of 31 falls on Feb 28.
        var result = Evaluate(0m, new DateOnly(2026, 2, 28), dueDay: 31, grace: 0, start: new DateOnly(2026, 2, 1));

        result.Status.Should().Be(RentStatus.Overdue);
        result.OldestUnpaidDueDate.Should().Be(new DateOnly(2026, 2, 28));
    }

    [Fact]
    public void ADueDayOf31_ClampsToFeb29_InALeapYear()
    {
        var result = Evaluate(0m, new DateOnly(2028, 2, 29), dueDay: 31, grace: 0, start: new DateOnly(2028, 2, 1));

        result.OldestUnpaidDueDate.Should().Be(new DateOnly(2028, 2, 29));
    }

    [Fact]
    public void AZeroDayGracePeriod_MakesTheDayAfterTheDueDateArrears()
    {
        Evaluate(0m, new DateOnly(2026, 1, 5), grace: 0).Status.Should().Be(RentStatus.Overdue);
        Evaluate(0m, new DateOnly(2026, 1, 6), grace: 0).Status.Should().Be(RentStatus.InArrears);
    }

    [Fact]
    public void ARentAmountThatDoesNotDivideThePayments_RoundsPeriodsInArrearsUp()
    {
        // 1500 received against 2 past-grace periods of 1000: 500 short → 1 period in arrears.
        var result = Evaluate(1500m, new DateOnly(2026, 2, 20));

        result.PeriodsInArrears.Should().Be(1);
        result.AmountShort.Should().Be(500m);
        result.OldestUnpaidDueDate.Should().Be(new DateOnly(2026, 2, 5));
    }

    [Fact]
    public void AnEvaluationBeforeTheStartDate_IsCurrent()
    {
        Evaluate(0m, new DateOnly(2025, 12, 1)).Status.Should().Be(RentStatus.Current);
    }
}
