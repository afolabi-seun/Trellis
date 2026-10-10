using FluentAssertions;
using Trellis.Api.Modules.RentLedger.Domain.Entities;
using Trellis.Api.Modules.RentLedger.Domain.Exceptions;
using Xunit;

namespace Trellis.Tests.RentLedger;

public sealed class RentScheduleTests
{
    private static RentSchedule CreateActive() =>
        RentSchedule.Create(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), 1000m, "usd", new DateOnly(2026, 1, 1), 5, 5, Guid.NewGuid());

    [Fact]
    public void Create_StartsActive_UppercasesTheCurrency_AndDefaultsTheAlertKeyToNull()
    {
        var schedule = CreateActive();

        schedule.Status.Should().Be(RentScheduleStatus.Active);
        schedule.Currency.Should().Be("USD");
        schedule.LastArrearsAlertedPeriod.Should().BeNull();
        schedule.EndedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 5, 5)]
    [InlineData(-10, 5, 5)]
    [InlineData(1000, 0, 5)]
    [InlineData(1000, 32, 5)]
    [InlineData(1000, 5, -1)]
    [InlineData(1000, 5, 61)]
    public void Create_RejectsInvalidTerms(decimal amount, int dueDay, int grace)
    {
        var act = () => RentSchedule.Create(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), amount, "USD", new DateOnly(2026, 1, 1), dueDay, grace, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RejectsANonThreeLetterCurrency()
    {
        var act = () => RentSchedule.Create(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), 1000m, "US", new DateOnly(2026, 1, 1), 5, 5, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_ChangesTheTerms_WhileActive()
    {
        var schedule = CreateActive();

        schedule.Update(1200m, 1, 3);

        schedule.RentAmount.Should().Be(1200m);
        schedule.DueDayOfMonth.Should().Be(1);
        schedule.GracePeriodDays.Should().Be(3);
    }

    [Fact]
    public void Update_OnAnEndedSchedule_Throws()
    {
        var schedule = CreateActive();
        schedule.End();

        var act = () => schedule.Update(1200m, 1, 3);

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void End_TransitionsToEnded_AndStampsTheTime()
    {
        var schedule = CreateActive();

        schedule.End();

        schedule.Status.Should().Be(RentScheduleStatus.Ended);
        schedule.EndedAt.Should().NotBeNull();
    }

    [Fact]
    public void End_CalledTwice_Throws()
    {
        var schedule = CreateActive();
        schedule.End();

        var act = schedule.End;

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Evaluate_DelegatesToTheCalculator_WithTheSchedulesOwnTerms()
    {
        var schedule = CreateActive();

        schedule.Evaluate(0m, new DateOnly(2026, 1, 20)).Status.Should().Be(Api.Modules.RentLedger.Domain.ValueObjects.RentStatus.InArrears);
        schedule.Evaluate(1000m, new DateOnly(2026, 1, 20)).Status.Should().Be(Api.Modules.RentLedger.Domain.ValueObjects.RentStatus.Current);
    }

    [Fact]
    public void MarkArrearsAlerted_RecordsThePeriodKey()
    {
        var schedule = CreateActive();

        schedule.MarkArrearsAlerted(new DateOnly(2026, 2, 5));

        schedule.LastArrearsAlertedPeriod.Should().Be(new DateOnly(2026, 2, 5));
    }
}
