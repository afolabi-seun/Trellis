using FluentAssertions;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Reporting.Domain.Exceptions;
using Xunit;

namespace Trellis.Tests.Reporting;

public sealed class OwnerReportTests
{
    private static OwnerReport CreateGenerating() =>
        OwnerReport.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Jane Owner", OwnerReportPeriodType.Monthly,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid());

    [Fact]
    public void Create_SetsStatusToGenerating()
    {
        var report = CreateGenerating();

        report.Status.Should().Be(OwnerReportStatus.Generating);
        report.ReadyAt.Should().BeNull();
    }

    [Fact]
    public void MarkReady_WhenGenerating_TransitionsToReadyAndStoresFigures()
    {
        var report = CreateGenerating();

        report.MarkReady(3, 450.00m, 2, 8, 1, [0x25, 0x50, 0x44, 0x46]);

        report.Status.Should().Be(OwnerReportStatus.Ready);
        report.PropertyCount.Should().Be(3);
        report.TotalMaintenanceCost.Should().Be(450.00m);
        report.OpenMaintenanceCount.Should().Be(2);
        report.CompliantDocumentCount.Should().Be(8);
        report.ExpiringDocumentCount.Should().Be(1);
        report.PdfContent.Should().NotBeNull();
        report.ReadyAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkReady_WithRentFigures_StoresTheSnapshot_AndWithoutThemLeavesThemNull()
    {
        var withRent = CreateGenerating();
        withRent.MarkReady(1, 0, 0, 0, 0, [0x25], 3, 1, 2, 900.25m, new DateOnly(2026, 2, 3));
        var withoutRent = CreateGenerating();
        withoutRent.MarkReady(1, 0, 0, 0, 0, [0x25]);

        withRent.RentCurrentCount.Should().Be(3);
        withRent.RentOverdueCount.Should().Be(1);
        withRent.RentArrearsCount.Should().Be(2);
        withRent.RentReceivedAmount.Should().Be(900.25m);
        withRent.RentAsOf.Should().Be(new DateOnly(2026, 2, 3));
        withoutRent.RentCurrentCount.Should().BeNull();
        withoutRent.RentReceivedAmount.Should().BeNull();
    }

    [Fact]
    public void MarkReady_CalledTwice_ThrowsOnTheSecondCall()
    {
        var report = CreateGenerating();
        report.MarkReady(1, 0, 0, 0, 0, []);

        var act = () => report.MarkReady(1, 0, 0, 0, 0, []);

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void MarkFailed_WhenGenerating_TransitionsToFailed()
    {
        var report = CreateGenerating();

        report.MarkFailed();

        report.Status.Should().Be(OwnerReportStatus.Failed);
    }

    [Fact]
    public void MarkFailed_AfterAlreadyReady_ThrowsInvalidStatusTransitionException()
    {
        var report = CreateGenerating();
        report.MarkReady(1, 0, 0, 0, 0, []);

        var act = report.MarkFailed;

        act.Should().Throw<InvalidStatusTransitionException>();
    }
}
