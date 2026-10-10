using FluentAssertions;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Leasing.Domain.Exceptions;
using Xunit;

namespace Trellis.Tests.Leasing;

public sealed class RenewalOfferTests
{
    private static readonly DateOnly Today = new(2026, 10, 10);
    private static readonly string[] Reasons = ["Rent too high", "Buying a home"];

    private static RenewalOffer Offer(decimal? rent = 1200m, DateOnly? rentFrom = null, DateOnly? respondBy = null) =>
        RenewalOffer.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), RenewalTerm.Fixed, new DateOnly(2027, 12, 31),
            rent, rent is null ? null : rentFrom ?? new DateOnly(2027, 1, 1), respondBy ?? new DateOnly(2026, 11, 1), Today, Guid.NewGuid());

    [Fact]
    public void Create_StartsOffered()
    {
        Offer().Status.Should().Be(RenewalOfferStatus.Offered);
    }

    [Fact]
    public void Create_WithATermOrRentThatDoesntHoldTogether_Throws()
    {
        var fixedNoEnd = () => RenewalOffer.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), RenewalTerm.Fixed, null, null, null, Today, Today, Guid.NewGuid());
        var monthlyWithEnd = () => RenewalOffer.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), RenewalTerm.MonthToMonth, Today.AddYears(1), null, null, Today, Today, Guid.NewGuid());
        var rentNoDate = () => RenewalOffer.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), RenewalTerm.MonthToMonth, null, 1200m, null, Today, Today, Guid.NewGuid());
        var pastRespondBy = () => Offer(respondBy: Today.AddDays(-1));

        fixedNoEnd.Should().Throw<ArgumentException>();
        monthlyWithEnd.Should().Throw<ArgumentException>();
        rentNoDate.Should().Throw<ArgumentException>();
        pastRespondBy.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Accept_RecordsWhenAndWhetherTheRentChangeLanded_AndCanOnlyHappenOnce()
    {
        var offer = Offer();
        offer.Accept(Today, Today, rentChangeApplied: true, Guid.NewGuid());

        offer.Status.Should().Be(RenewalOfferStatus.Accepted);
        offer.OutcomeOn.Should().Be(Today);
        offer.RentChangeApplied.Should().BeTrue();

        var again = () => offer.Accept(Today, Today, true, Guid.NewGuid());
        again.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Accept_WithNoNewRent_LeavesRentChangeAppliedUnset()
    {
        var offer = Offer(rent: null);

        offer.Accept(Today, Today, rentChangeApplied: false, Guid.NewGuid());

        offer.RentChangeApplied.Should().BeNull();
    }

    [Fact]
    public void Accept_DatedInTheFuture_Throws()
    {
        var act = () => Offer().Accept(Today.AddDays(1), Today, null, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Decline_TakesAListedReason_InTheListsOwnSpelling()
    {
        var offer = Offer();

        offer.Decline(Today, Today, "rent too high", null, Reasons, Guid.NewGuid());

        offer.Status.Should().Be(RenewalOfferStatus.Declined);
        offer.DeclineReason.Should().Be("Rent too high");
    }

    [Fact]
    public void Decline_WithOther_NeedsANote_AndAnUnlistedReasonIsRejected()
    {
        var noNote = () => Offer().Decline(Today, Today, "Other", " ", Reasons, Guid.NewGuid());
        var unlisted = () => Offer().Decline(Today, Today, "Hates the paint", null, Reasons, Guid.NewGuid());

        noNote.Should().Throw<ArgumentException>();
        unlisted.Should().Throw<ArgumentException>();

        var offer = Offer();
        offer.Decline(Today, Today, "other", "Moving abroad", Reasons, Guid.NewGuid());
        offer.DeclineReason.Should().Be("Other");
        offer.DeclineNote.Should().Be("Moving abroad");
    }

    [Fact]
    public void Expire_OnlyAfterTheRespondByDate()
    {
        var offer = Offer(respondBy: new DateOnly(2026, 11, 1));

        var tooSoon = () => offer.Expire(new DateOnly(2026, 11, 1), Guid.NewGuid());
        tooSoon.Should().Throw<ArgumentException>();

        offer.Expire(new DateOnly(2026, 11, 2), Guid.NewGuid());
        offer.Status.Should().Be(RenewalOfferStatus.Expired);
        offer.OutcomeOn.Should().Be(new DateOnly(2026, 11, 2));
    }
}
