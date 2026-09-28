using FluentAssertions;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Leasing.Domain.Exceptions;
using Xunit;

namespace Trellis.Tests.Leasing;

public sealed class VacancyListingTests
{
    private static VacancyListing CreateListed() =>
        VacancyListing.Create(Guid.NewGuid(), Guid.NewGuid(), 1500m, "usd", new DateOnly(2026, 11, 1), "Sunny 2-bed", Guid.NewGuid());

    [Fact]
    public void Create_SetsListedStatus_AndUpperCasesCurrency()
    {
        var listing = CreateListed();

        listing.Status.Should().Be(VacancyListingStatus.Listed);
        listing.Currency.Should().Be("USD");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_WithNonPositiveRent_Throws(int rent)
    {
        var act = () => VacancyListing.Create(Guid.NewGuid(), Guid.NewGuid(), rent, "USD", new DateOnly(2026, 11, 1), null, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNonThreeLetterCurrency_Throws()
    {
        var act = () => VacancyListing.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, "US", new DateOnly(2026, 11, 1), null, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_WhileListed_ChangesTerms()
    {
        var listing = CreateListed();

        listing.Update(1800m, "EUR", new DateOnly(2026, 12, 1), "Renovated");

        listing.RentAmount.Should().Be(1800m);
        listing.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Update_OnceUnderApplication_IsRejected_SoAMidReviewRentChangeCantInvalidateAScreening()
    {
        var listing = CreateListed();
        listing.MarkUnderApplication();

        var act = () => listing.Update(2000m, "USD", new DateOnly(2026, 12, 1), null);

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void MarkUnderApplication_IsIdempotent_ForASecondApplicant()
    {
        var listing = CreateListed();
        listing.MarkUnderApplication();

        listing.MarkUnderApplication();

        listing.Status.Should().Be(VacancyListingStatus.UnderApplication);
    }

    [Theory]
    [InlineData(VacancyListingStatus.Listed)]
    [InlineData(VacancyListingStatus.UnderApplication)]
    public void Withdraw_FromAnActiveStatus_Succeeds(VacancyListingStatus from)
    {
        var listing = CreateListed();
        if (from == VacancyListingStatus.UnderApplication)
        {
            listing.MarkUnderApplication();
        }

        listing.Withdraw();

        listing.Status.Should().Be(VacancyListingStatus.Withdrawn);
    }

    [Fact]
    public void Withdraw_AfterLeased_Throws()
    {
        var listing = CreateListed();
        listing.MarkLeased();

        var act = listing.Withdraw;

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void MarkLeased_AfterWithdrawn_Throws()
    {
        var listing = CreateListed();
        listing.Withdraw();

        var act = listing.MarkLeased;

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void AcceptsApplications_OnlyWhileListedOrUnderApplication()
    {
        var listing = CreateListed();
        listing.AcceptsApplications().Should().BeTrue();

        listing.MarkUnderApplication();
        listing.AcceptsApplications().Should().BeTrue();

        listing.MarkLeased();
        listing.AcceptsApplications().Should().BeFalse();
    }
}
