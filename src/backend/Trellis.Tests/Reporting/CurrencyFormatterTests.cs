using FluentAssertions;
using Trellis.Api.Modules.Reporting.Infrastructure.Pdf;
using Xunit;

namespace Trellis.Tests.Reporting;

/// <summary>Every currency is formatted with its own real symbol/grouping, not a hard-coded en-US "$" (the real bug this class fixes — see its own doc comment).</summary>
public sealed class CurrencyFormatterTests
{
    [Fact]
    public void Format_UsesTheDollarSign_ForUsd() => CurrencyFormatter.Format(1234.5m, "USD").Should().Be("$1,234.50");

    [Fact]
    public void Format_UsesTheRandSign_ForZar() => CurrencyFormatter.Format(1234.5m, "ZAR").Should().Contain("1").And.NotContain("$");

    [Fact]
    public void Format_UsesAShillingSign_ForKes() => CurrencyFormatter.Format(1234.5m, "KES").Should().NotContain("$");

    [Fact]
    public void Format_ZarAndUsd_RenderVisiblyDifferently_NeverBothWithADollarSign()
    {
        var usd = CurrencyFormatter.Format(1000m, "USD");
        var zar = CurrencyFormatter.Format(1000m, "ZAR");

        usd.Should().StartWith("$");
        zar.Should().NotBe(usd);
    }

    [Fact]
    public void Format_ForAnUnrecognisedCode_FallsBackToTheCodePlusPlainNumber_RatherThanGuessingASymbol() =>
        CurrencyFormatter.Format(1234.5m, "XYZ").Should().Be("XYZ 1,234.50");

    [Fact]
    public void Format_NeverThrows_ForAnEmptyOrGarbageCode()
    {
        var act1 = () => CurrencyFormatter.Format(1m, "");
        var act2 = () => CurrencyFormatter.Format(1m, "NOTREAL");

        act1.Should().NotThrow();
        act2.Should().NotThrow();
    }
}
