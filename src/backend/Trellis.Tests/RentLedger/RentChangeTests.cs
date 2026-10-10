using FluentAssertions;
using Trellis.Api.Modules.RentLedger.Domain.Entities;
using Trellis.Api.Modules.RentLedger.Domain.ValueObjects;
using Xunit;

namespace Trellis.Tests.RentLedger;

/// <summary>
/// BDR-LSE-010 — a renewal at a new rent changes the rent only from its date; periods already due
/// keep the amount that applied to them. Rent 1000 due on the 5th with 5 days' grace from
/// 2026-01-01, rising to 1200 from 2026-04-01, unless stated.
/// </summary>
public sealed class RentChangeTests
{
    private static readonly DateOnly RaiseFrom = new(2026, 4, 1);

    private static RentSchedule Schedule() =>
        RentSchedule.Create(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), 1000m, "USD", new DateOnly(2026, 1, 1), 5, 5, null);

    private static RentSchedule ScheduleWithRaise()
    {
        var schedule = Schedule();
        schedule.ScheduleRentChange(RaiseFrom, 1200m, null, Guid.NewGuid());
        return schedule;
    }

    [Fact]
    public void RentAmountOn_UsesTheBaseAmountBeforeTheChange_AndTheNewOneFromIt()
    {
        var schedule = ScheduleWithRaise();

        schedule.RentAmountOn(new DateOnly(2026, 3, 5)).Should().Be(1000m);
        schedule.RentAmountOn(RaiseFrom).Should().Be(1200m);
        schedule.RentAmountOn(new DateOnly(2026, 6, 5)).Should().Be(1200m);
    }

    [Fact]
    public void Evaluate_ChargesEachPeriodTheRentThatAppliedOnItsDueDate()
    {
        // Jan–Mar at 1000 and Apr–May at 1200 = 5400 due by 2026-05-20.
        var schedule = ScheduleWithRaise();

        schedule.Evaluate(5400m, new DateOnly(2026, 5, 20)).Status.Should().Be(RentStatus.Current);

        var short1200 = schedule.Evaluate(4200m, new DateOnly(2026, 5, 20));
        short1200.Status.Should().Be(RentStatus.InArrears);
        short1200.AmountShort.Should().Be(1200m);
        short1200.PeriodsInArrears.Should().Be(1);
        short1200.OldestUnpaidDueDate.Should().Be(new DateOnly(2026, 5, 5));
    }

    [Fact]
    public void Evaluate_APartialPaymentAcrossTheChange_CoversOldestPeriodsFirst()
    {
        // 3000 covers Jan–Mar exactly; April's 1200 is the oldest unpaid.
        var result = ScheduleWithRaise().Evaluate(3500m, new DateOnly(2026, 4, 20));

        result.OldestUnpaidDueDate.Should().Be(new DateOnly(2026, 4, 5));
        result.AmountShort.Should().Be(700m);
        result.PeriodsInArrears.Should().Be(1);
    }

    [Fact]
    public void TheVariableAmountCalculator_MatchesTheFixedOne_WhenThereIsNoChange()
    {
        var asOf = new DateOnly(2026, 6, 20);
        foreach (var received in new[] { 0m, 999m, 2500m, 6000m })
        {
            var fixedAmount = RentStatusCalculator.Evaluate(new DateOnly(2026, 1, 1), 5, 5, 1000m, received, asOf);
            Schedule().Evaluate(received, asOf).Should().Be(fixedAmount);
        }
    }

    [Fact]
    public void ScheduleRentChange_OnTheSameDateAgain_ReplacesTheAmount_NeverStacksASecondChange()
    {
        var schedule = ScheduleWithRaise();

        schedule.ScheduleRentChange(RaiseFrom, 1300m, null, Guid.NewGuid());

        schedule.RentChanges.Should().ContainSingle().Which.RentAmount.Should().Be(1300m);
    }

    [Theory]
    [InlineData("2026-01-01", 1200)]
    [InlineData("2025-12-01", 1200)]
    [InlineData("2026-04-01", 0)]
    public void ScheduleRentChange_OnOrBeforeTheStart_OrNotPositive_Throws(string from, int amount)
    {
        var act = () => Schedule().ScheduleRentChange(
            DateOnly.Parse(from, System.Globalization.CultureInfo.InvariantCulture), amount, null, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ScheduleRentChange_OnAnEndedSchedule_Throws()
    {
        var schedule = Schedule();
        schedule.End();

        var act = () => schedule.ScheduleRentChange(RaiseFrom, 1200m, null, Guid.NewGuid());

        act.Should().Throw<Api.Modules.RentLedger.Domain.Exceptions.InvalidStatusTransitionException>();
    }

    [Fact]
    public void Update_CorrectsTheAmountInEffectToday_NotTheHistoricalBase()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var schedule = RentSchedule.Create(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), 1000m, "USD", today.AddMonths(-6), 5, 5, null);
        schedule.ScheduleRentChange(today.AddMonths(-1), 1200m, null, Guid.NewGuid());

        schedule.Update(1250m, 5, 5);

        schedule.RentAmount.Should().Be(1000m);
        schedule.RentAmountOn(today).Should().Be(1250m);
    }

    [Fact]
    public void NextRentChangeAfter_ReturnsOnlyAFutureChange()
    {
        var schedule = ScheduleWithRaise();

        schedule.NextRentChangeAfter(new DateOnly(2026, 3, 1))!.EffectiveFrom.Should().Be(RaiseFrom);
        schedule.NextRentChangeAfter(RaiseFrom).Should().BeNull();
    }
}
