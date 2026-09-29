using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Document.Application.Interfaces;
using Trellis.Api.Modules.Inspection.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Services;
using Trellis.Api.Modules.Reporting.Configuration;
using Trellis.Api.Modules.Workflow.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.Reporting;

/// <summary>Composes 5 mocked cross-module calls correctly — the same "own-service-exposes-what-it-owns" pattern UsageSnapshotBackgroundService's own tests exercise.</summary>
public sealed class DashboardServiceTests
{
    private readonly IWorkflowInstanceService _workflowInstanceService = Substitute.For<IWorkflowInstanceService>();
    private readonly IDocumentService _documentService = Substitute.For<IDocumentService>();
    private readonly IInspectionService _inspectionService = Substitute.For<IInspectionService>();
    private readonly IRentScheduleService _rentScheduleService = Substitute.For<IRentScheduleService>();

    private DashboardService CreateSut(int bottleneckThresholdDays = 5) => new(
        _workflowInstanceService, _documentService, _inspectionService, _rentScheduleService,
        Options.Create(new ReportingOptions { WorkflowBottleneckThresholdDays = bottleneckThresholdDays }));

    [Fact]
    public async Task GetSummaryAsync_ComposesAllFiveCrossModuleCounts()
    {
        _workflowInstanceService.CountOpenAsync().Returns(4);
        _documentService.CountExpiringAsync(30).Returns(2);
        _inspectionService.CountUpcomingAsync(30).Returns(3);
        _workflowInstanceService.CountBottlenecksAsync(5).Returns(1);
        _rentScheduleService.CountInArrearsAsync().Returns(6);
        var sut = CreateSut();

        var result = await sut.GetSummaryAsync();

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.OpenWorkOrders.Should().Be(4);
        result.Data.ExpiringDocuments.Should().Be(2);
        result.Data.UpcomingInspections.Should().Be(3);
        result.Data.BottleneckedSteps.Should().Be(1);
        result.Data.ArrearsSchedules.Should().Be(6);
    }

    [Fact]
    public async Task GetSummaryAsync_WhenNotEntitledToRentCollection_LeavesArrearsSchedulesNull()
    {
        _rentScheduleService.CountInArrearsAsync().Returns((int?)null);
        var sut = CreateSut();

        var result = await sut.GetSummaryAsync();

        result.Data!.ArrearsSchedules.Should().BeNull();
    }

    [Fact]
    public async Task GetSummaryAsync_PassesConfiguredThreshold_ToBottleneckCount()
    {
        var sut = CreateSut(bottleneckThresholdDays: 10);

        await sut.GetSummaryAsync();

        await _workflowInstanceService.Received(1).CountBottlenecksAsync(10);
    }
}
