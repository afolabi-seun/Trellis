using FluentAssertions;
using Trellis.Api.Modules.RentLedger.Application.Dtos;
using Trellis.Api.Modules.Reporting.Application.Dtos;
using Trellis.Api.Modules.Reporting.Infrastructure.Pdf;
using Xunit;

namespace Trellis.Tests.Reporting;

/// <summary>
/// A deliberate asymmetry: PDF assertions stay loose (non-empty, starts with the %PDF magic
/// number) — QuestPDF's internal byte layout isn't a contract worth pinning — while CSV
/// assertions are exact (header row, escaping, row count), since CSV is plain text and cheap to
/// assert precisely.
/// </summary>
public sealed class ReportExportServiceTests
{
    private static readonly OwnerReportContent SampleContent = new(
        "Acme Property Management", "Jane Owner", "MONTHLY", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
        3, 450.00m, "USD", 2, 8, 1);

    private static readonly DashboardSummaryResponse SampleSummary = new(4, 2, 3, 1);

    private readonly QuestPdfOwnerReportRenderer _sut = new();

    [Fact]
    public void GenerateOwnerReportPdf_ProducesNonEmptyWellFormedPdfBytes()
    {
        var bytes = _sut.GenerateOwnerReportPdf(SampleContent);

        bytes.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public void GenerateOwnerReportCsv_ProducesExactHeaderAndRows()
    {
        var bytes = _sut.GenerateOwnerReportCsv(SampleContent);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        lines[0].Should().Be("Metric,Value");
        lines[1].Should().Be("Owner,Jane Owner");
        lines[2].Should().Be("Period,2026-01-01 to 2026-01-31");
        lines[3].Should().Be("Properties,3");
        lines[4].Should().Be("Total maintenance cost (USD),450.00");
        lines[5].Should().Be("Open maintenance requests,2");
        lines[6].Should().Be("Compliant documents,8");
        lines[7].Should().Be("Documents expiring soon,1");
        lines.Should().HaveCount(8);
    }

    [Fact]
    public void GenerateOwnerReportCsv_WithARentSection_AppendsTheExactRentRows()
    {
        var content = SampleContent with
        {
            Rent = new OwnerReportRentSection(5, 2, 1, [new CurrencyAmount("USD", 6300.5m)], new DateOnly(2026, 2, 3)),
        };

        var lines = System.Text.Encoding.UTF8.GetString(_sut.GenerateOwnerReportCsv(content)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        lines[8].Should().Be("Rent schedules current,5");
        lines[9].Should().Be("Rent schedules overdue,2");
        lines[10].Should().Be("Rent schedules in arrears,1");
        lines[11].Should().Be("Rent recorded as received in period (USD),6300.50");
        lines[12].Should().Be("Rent status as of,2026-02-03");
        lines.Should().HaveCount(13);
    }

    [Fact]
    public void GenerateOwnerReportCsv_WithMultipleCurrencies_EmitsOneRowPerCurrency_NeverSummed()
    {
        var content = SampleContent with
        {
            Rent = new OwnerReportRentSection(
                2, 0, 0, [new CurrencyAmount("USD", 1000m), new CurrencyAmount("ZAR", 15000m)], new DateOnly(2026, 2, 3)),
        };

        var lines = System.Text.Encoding.UTF8.GetString(_sut.GenerateOwnerReportCsv(content)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        lines.Should().Contain("Rent recorded as received in period (USD),1000.00");
        lines.Should().Contain("Rent recorded as received in period (ZAR),15000.00");
    }

    [Fact]
    public void GenerateOwnerReportCsv_WithoutARentSection_HasNoRentRows()
    {
        var lines = System.Text.Encoding.UTF8.GetString(_sut.GenerateOwnerReportCsv(SampleContent)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        lines.Should().NotContain(l => l.StartsWith("Rent", StringComparison.Ordinal));
    }

    [Fact]
    public void GenerateOwnerReportPdf_WithARentSection_StillProducesAWellFormedPdf()
    {
        var content = SampleContent with
        {
            Rent = new OwnerReportRentSection(5, 2, 1, [new CurrencyAmount("USD", 6300.5m)], new DateOnly(2026, 2, 3)),
        };

        var bytes = _sut.GenerateOwnerReportPdf(content);

        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public void GenerateDashboardCsv_ProducesExactHeaderAndRows()
    {
        var bytes = _sut.GenerateDashboardCsv(SampleSummary);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        lines[0].Should().Be("Metric,Value");
        lines[1].Should().Be("Open work orders,4");
        lines[2].Should().Be("Expiring documents,2");
        lines[3].Should().Be("Upcoming inspections,3");
        lines[4].Should().Be("Bottlenecked steps,1");
        lines.Should().HaveCount(5);
    }
}
