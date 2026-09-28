using FluentAssertions;
using Trellis.Api.Modules.Tenant.Domain.ValueObjects;
using Xunit;

namespace Trellis.Tests.Tenant;

public sealed class DomainPolicyTests
{
    private static readonly string[] PlatformHosts = ["app.trellis-platform.com", "localhost", "127.0.0.1"];

    [Theory]
    [InlineData("Listings.Acme.com", "listings.acme.com")]
    [InlineData("  acme.com.  ", "acme.com")]
    [InlineData("ACME.CO.ZA", "acme.co.za")]
    public void Normalize_LowercasesTrimsAndDropsTheTrailingDot(string input, string expected) => DomainPolicy.Normalize(input).Should().Be(expected);

    [Fact]
    public void Normalize_ConvertsInternationalisedNamesToPunycode() => DomainPolicy.Normalize("bücher.example").Should().Be("xn--bcher-kva.example");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://acme.com")]
    [InlineData("acme.com/path")]
    [InlineData("acme.com:8443")]
    [InlineData("acme .com")]
    public void Normalize_RejectsSchemesPortsPathsAndBlanks(string? input) => DomainPolicy.Normalize(input).Should().BeNull();

    [Theory]
    [InlineData("acme.com")]
    [InlineData("listings.acme.com")]
    [InlineData("a-b.acme.co.za")]
    [InlineData("xn--bcher-kva.example")]
    public void IsValid_AcceptsRegistrableLookingHostnames(string host) => DomainPolicy.IsValid(host, PlatformHosts).Should().BeTrue();

    [Theory]
    [InlineData("localhost")]
    [InlineData("acme")]
    [InlineData("192.168.0.1")]
    [InlineData("127.0.0.1")]
    [InlineData("-acme.com")]
    [InlineData("acme-.com")]
    [InlineData("acme..com")]
    [InlineData("acme.c")]
    [InlineData("acme.123")]
    public void IsValid_RejectsSingleLabelIpsAndMalformedNames(string host) => DomainPolicy.IsValid(host, PlatformHosts).Should().BeFalse();

    [Theory]
    [InlineData("app.trellis-platform.com")]
    [InlineData("login.app.trellis-platform.com")]
    public void IsValid_RejectsThePlatformsOwnHostAndItsSubdomains_SoATenantCannotImpersonateIt(string host) =>
        DomainPolicy.IsValid(host, PlatformHosts).Should().BeFalse();

    [Fact]
    public void IsValid_DoesNotTreatALookalikeSuffixAsAPlatformSubdomain() =>
        DomainPolicy.IsValid("notapp.trellis-platform.com.evil.io", PlatformHosts).Should().BeTrue();
}
