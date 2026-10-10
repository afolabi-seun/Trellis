using FluentAssertions;
using Trellis.Api.Modules.PropertyAsset.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Domain.Exceptions;
using Xunit;

namespace Trellis.Tests.PropertyAsset;

public sealed class LeaseTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);

    private static Lease Fixed(Guid? applicationId = null) =>
        Lease.Start(Guid.NewGuid(), Guid.NewGuid(), applicationId, Start, LeaseTermType.Fixed, new DateOnly(2026, 12, 31), null);

    [Fact]
    public void Start_IsLeasingSourcedExactlyWhenItHasAnApplication()
    {
        Fixed(Guid.NewGuid()).Source.Should().Be(LeaseSource.Leasing);
        Fixed().Source.Should().Be(LeaseSource.Manual);
        Fixed().Status.Should().Be(LeaseStatus.Active);
    }

    [Theory]
    [InlineData(LeaseTermType.Fixed, null)]
    [InlineData(LeaseTermType.Fixed, "2026-01-01")]
    [InlineData(LeaseTermType.Fixed, "2025-12-31")]
    [InlineData(LeaseTermType.MonthToMonth, "2026-12-31")]
    [InlineData(LeaseTermType.NotRecorded, "2026-12-31")]
    public void Start_WithATermThatDoesntHoldTogether_Throws(LeaseTermType termType, string? endDate)
    {
        var act = () => Lease.Start(
            Guid.NewGuid(), Guid.NewGuid(), null, Start, termType,
            endDate is null ? null : DateOnly.Parse(endDate, System.Globalization.CultureInfo.InvariantCulture), null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DaysUntilEnd_CountsDownToAFixedEnd_GoesNegativeAfterIt_AndIsNullWithoutOne()
    {
        var lease = Fixed();

        lease.DaysUntilEnd(new DateOnly(2026, 12, 1)).Should().Be(30);
        lease.DaysUntilEnd(new DateOnly(2027, 1, 2)).Should().Be(-2);
        Lease.Start(Guid.NewGuid(), Guid.NewGuid(), null, Start, LeaseTermType.MonthToMonth, null, null)
            .DaysUntilEnd(new DateOnly(2026, 12, 1)).Should().BeNull();
    }

    [Fact]
    public void RecordTerm_GivesANotRecordedLeaseItsRealTerm()
    {
        var lease = Lease.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Start, LeaseTermType.NotRecorded, null, null);

        lease.RecordTerm(LeaseTermType.Fixed, new DateOnly(2026, 6, 30));

        lease.TermType.Should().Be(LeaseTermType.Fixed);
        lease.EndDate.Should().Be(new DateOnly(2026, 6, 30));
    }

    [Fact]
    public void RecordTerm_CantRecordNotRecorded_OrChangeAnEndedLease()
    {
        var lease = Fixed();
        var notRecorded = () => lease.RecordTerm(LeaseTermType.NotRecorded, null);
        notRecorded.Should().Throw<ArgumentException>();

        lease.End(new DateOnly(2026, 6, 30), LeaseEndReason.NonRenewal, LeaseEndedBy.Lessee, null);
        var afterEnd = () => lease.RecordTerm(LeaseTermType.MonthToMonth, null);
        afterEnd.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void End_RecordsTheReasonPartyAndTrimmedNote()
    {
        var lease = Fixed();

        lease.End(new DateOnly(2026, 6, 30), LeaseEndReason.Other, LeaseEndedBy.PmCompany, "  Owner moving in  ");

        lease.Status.Should().Be(LeaseStatus.Ended);
        lease.EndedOn.Should().Be(new DateOnly(2026, 6, 30));
        lease.EndReason.Should().Be(LeaseEndReason.Other);
        lease.EndedBy.Should().Be(LeaseEndedBy.PmCompany);
        lease.EndNote.Should().Be("Owner moving in");
    }

    [Fact]
    public void End_WithReasonOther_NeedsANote()
    {
        var act = () => Fixed().End(new DateOnly(2026, 6, 30), LeaseEndReason.Other, LeaseEndedBy.PmCompany, "  ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void End_CantUseNotRecorded_OrEndBeforeTheStart_OrEndTwice()
    {
        var notRecorded = () => Fixed().End(new DateOnly(2026, 6, 30), LeaseEndReason.NotRecorded, LeaseEndedBy.Lessee, null);
        notRecorded.Should().Throw<ArgumentException>();

        var beforeStart = () => Fixed().End(Start.AddDays(-1), LeaseEndReason.NonRenewal, LeaseEndedBy.Lessee, null);
        beforeStart.Should().Throw<ArgumentException>();

        var lease = Fixed();
        lease.End(new DateOnly(2026, 6, 30), LeaseEndReason.NonRenewal, LeaseEndedBy.Lessee, null);
        var twice = () => lease.End(new DateOnly(2026, 7, 1), LeaseEndReason.NonRenewal, LeaseEndedBy.Lessee, null);
        twice.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void UnitAsset_IsVacantSinceItsCreation_ClearsItWhenOccupied_AndTakesTheLeaseEndWhenVacated()
    {
        var unit = UnitAsset.Create(Guid.NewGuid(), Guid.NewGuid(), "4B", null, null);
        unit.VacantSince.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));

        unit.MarkOccupied();
        unit.VacantSince.Should().BeNull();

        unit.MarkVacant(new DateOnly(2026, 6, 30));
        unit.VacantSince.Should().Be(new DateOnly(2026, 6, 30));
    }
}
