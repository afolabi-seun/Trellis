using FluentAssertions;
using Trellis.Api.Modules.Tenant.Domain.ValueObjects;
using Xunit;

namespace Trellis.Tests.Tenant;

public sealed class SlugPolicyTests
{
    [Theory]
    [InlineData("acme")]
    [InlineData("acme-pm")]
    [InlineData("a1b")]
    [InlineData("t-9f9f9f9f")]
    [InlineData("acme-property-management-2")]
    public void IsValid_AcceptsLowercaseLettersDigitsAndInnerHyphens(string slug) => SlugPolicy.IsValid(slug).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("-acme")]
    [InlineData("acme-")]
    [InlineData("Acme")]
    [InlineData("acme pm")]
    [InlineData("acme_pm")]
    [InlineData("acme.pm")]
    [InlineData("ac/me")]
    public void IsValid_RejectsAnythingThatIsNotADnsSafeLowercaseLabel(string? slug) => SlugPolicy.IsValid(slug).Should().BeFalse();

    [Fact]
    public void IsValid_RejectsTheSixtyFourthCharacter()
    {
        SlugPolicy.IsValid(new string('a', 63)).Should().BeTrue();
        SlugPolicy.IsValid(new string('a', 64)).Should().BeFalse();
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("api")]
    [InlineData("platform")]
    [InlineData("login")]
    [InlineData("vacancies")]
    [InlineData("listings")]
    [InlineData("apply")]
    [InlineData("forgot-password")]
    public void IsValid_RejectsReservedWords_SoASlugNeverCollidesWithARouteOrImpersonatesThePlatform(string slug) =>
        SlugPolicy.IsValid(slug).Should().BeFalse();

    [Theory]
    [InlineData("Acme Property Management", "acme-property-management")]
    [InlineData("  Acme & Sons, Ltd.  ", "acme-sons-ltd")]
    [InlineData("Café Résidences", "cafe-residences")]
    [InlineData("ACME", "acme")]
    public void FromName_SlugifiesAccentsPunctuationAndCase(string name, string expected) => SlugPolicy.FromName(name).Should().Be(expected);

    [Theory]
    [InlineData("!!")]
    [InlineData("é")]
    [InlineData("")]
    public void FromName_AlwaysReturnsAValidSlug_EvenForANameWithNothingUsable(string name) =>
        SlugPolicy.IsValid(SlugPolicy.FromName(name)).Should().BeTrue();

    [Fact]
    public void FromName_NeverReturnsAReservedWord() => SlugPolicy.IsValid(SlugPolicy.FromName("Admin")).Should().BeTrue();

    [Fact]
    public void FromName_CapsLengthSoAUniquenessSuffixStillFits()
    {
        var slug = SlugPolicy.FromName(new string('x', 200));

        slug.Length.Should().BeLessThanOrEqualTo(50);
        SlugPolicy.IsValid(SlugPolicy.WithSuffix(slug, 12)).Should().BeTrue();
    }

    [Theory]
    [InlineData(1, "acme")]
    [InlineData(2, "acme-2")]
    [InlineData(10, "acme-10")]
    public void WithSuffix_FirstAttemptIsTheBareSlug(int attempt, string expected) => SlugPolicy.WithSuffix("acme", attempt).Should().Be(expected);
}
