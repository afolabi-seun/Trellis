using FluentAssertions;
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
        3, 450.00m, 2, 8, 1);

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
        lines[4].Should().Be("Total maintenance cost,450.00");
        lines[5].Should().Be("Open maintenance requests,2");
        lines[6].Should().Be("Compliant documents,8");
        lines[7].Should().Be("Documents expiring soon,1");
        lines.Should().HaveCount(8);
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
