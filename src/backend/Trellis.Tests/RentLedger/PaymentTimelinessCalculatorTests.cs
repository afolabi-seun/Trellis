using FluentAssertions;
using Trellis.Api.Modules.RentLedger.Domain.Entities;
using Trellis.Api.Modules.RentLedger.Domain.ValueObjects;
using Xunit;

namespace Trellis.Tests.RentLedger;

/// <summary>
/// BDR-RENT-007. Rent 1000 due on the 5th with 5 days' grace from 2026-01-01, so each period's
/// deadline is the 10th. "Paid" means the running total of recorded payments covered the period —
/// the same oldest-first model as arrears.
/// </summary>
public sealed class PaymentTimelinessCalculatorTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);

    private static PaymentTimeliness Calc(DateOnly asOf, params (string Date, decimal Amount)[] payments) =>
        Calc(asOf, null, null, payments);

    private static PaymentTimeliness Calc(DateOnly asOf, DateOnly? from, DateOnly? to, params (string Date, decimal Amount)[] payments) =>
        PaymentTimelinessCalculator.Calculate(
            Start, 5, 5, _ => 1000m,
            payments.Select(p => (DateOnly.Parse(p.Date, System.Globalization.CultureInfo.InvariantCulture), p.Amount)).ToList(),
            asOf, from, to);

    [Fact]
    public void APeriodPaidByItsGraceDeadline_IsOnTime_EvenOnTheLastDay()
    {
        var result = Calc(new DateOnly(2026, 1, 31), ("2026-01-10", 1000m));

        result.Should().Be(new PaymentTimeliness(1, 1, 0));
        result.OnTimeRate.Should().Be(1m);
        result.AverageDaysLate.Should().BeNull();
    }

    [Fact]
    public void APeriodPaidAfterItsDeadline_IsLate_ByDaysFromItsDueDate()
    {
        var result = Calc(new DateOnly(2026, 1, 31), ("2026-01-12", 1000m));

        result.Should().Be(new PaymentTimeliness(1, 0, 7));
        result.AverageDaysLate.Should().Be(7m);
    }

    [Fact]
    public void AnUnpaidPeriod_IsLate_ByDaysSoFar_NotLeftOutUntilSomeonePays()
    {
        var result = Calc(new DateOnly(2026, 1, 25));

        result.Should().Be(new PaymentTimeliness(1, 0, 20));
    }

    [Fact]
    public void APeriodStillInsideItsGraceWindow_IsNotAssessedYet()
    {
        Calc(new DateOnly(2026, 1, 10)).Should().Be(PaymentTimeliness.None);
        Calc(new DateOnly(2026, 1, 10)).OnTimeRate.Should().BeNull();
    }

    [Fact]
    public void OnePaymentCoveringSeveralPeriods_PaysEachOnTheDayItArrived()
    {
        // 3000 on 2026-02-08: January (due 01-05) is late by 34 days; February and March are covered early.
        var result = Calc(new DateOnly(2026, 3, 31), ("2026-02-08", 3000m));

        result.Should().Be(new PaymentTimeliness(3, 2, 34));
    }

    [Fact]
    public void APartPayment_DoesNotCoverThePeriod_UntilTheRestArrives()
    {
        var result = Calc(new DateOnly(2026, 1, 31), ("2026-01-05", 600m), ("2026-01-15", 400m));

        result.Should().Be(new PaymentTimeliness(1, 0, 10));
    }

    [Fact]
    public void TheWindow_JudgesOnlyPeriodsDueWithinIt_ButEarlierPaymentsStillCountOldestFirst()
    {
        // January paid on time, February late; only February is due in the window.
        var result = Calc(
            new DateOnly(2026, 2, 28), new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28),
            ("2026-01-08", 1000m), ("2026-02-20", 1000m));

        result.Should().Be(new PaymentTimeliness(1, 0, 15));
    }

    [Fact]
    public void ARentChange_RaisesWhatAPeriodNeedsFromItsDate()
    {
        // From March the rent is 1200, so a 1000 payment no longer covers March.
        var schedule = RentSchedule.Create(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), 1000m, "USD", Start, 5, 5, null);
        schedule.ScheduleRentChange(new DateOnly(2026, 3, 1), 1200m, null, Guid.NewGuid());
        List<(DateOnly, decimal)> payments =
        [
            (new DateOnly(2026, 1, 5), 1000m),
            (new DateOnly(2026, 2, 5), 1000m),
            (new DateOnly(2026, 3, 5), 1000m),
        ];

        var result = schedule.Timeliness(payments, new DateOnly(2026, 3, 20));

        result.Should().Be(new PaymentTimeliness(3, 2, 15));
    }

    [Fact]
    public void Add_SumsCounts_SoRollUpsAreWeightedByPeriods()
    {
        var small = new PaymentTimeliness(1, 0, 10);
        var large = new PaymentTimeliness(9, 9, 0);

        var total = small.Add(large);

        // 90%, not the 50% an average of the two schedules' rates (0% and 100%) would give.
        total.OnTimeRate.Should().Be(0.9m);
        total.AverageDaysLate.Should().Be(10m);
    }
}
