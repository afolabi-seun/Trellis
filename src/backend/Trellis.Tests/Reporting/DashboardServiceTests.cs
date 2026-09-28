using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Document.Application.Interfaces;
using Trellis.Api.Modules.Inspection.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Services;
using Trellis.Api.Modules.Reporting.Configuration;
using Trellis.Api.Modules.Workflow.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.Reporting;

/// <summary>Composes 4 mocked cross-module calls correctly — the same "own-service-exposes-what-it-owns" pattern UsageSnapshotBackgroundService's own tests exercise.</summary>
public sealed class DashboardServiceTests
{
    private readonly IWorkflowInstanceService _workflowInstanceService = Substitute.For<IWorkflowInstanceService>();
    private readonly IDocumentService _documentService = Substitute.For<IDocumentService>();
    private readonly IInspectionService _inspectionService = Substitute.For<IInspectionService>();

    private DashboardService CreateSut(int bottleneckThresholdDays = 5) =>
        new(_workflowInstanceService, _documentService, _inspectionService, Options.Create(new ReportingOptions { WorkflowBottleneckThresholdDays = bottleneckThresholdDays }));

    [Fact]
    public async Task GetSummaryAsync_ComposesAllFourCrossModuleCounts()
    {
        _workflowInstanceService.CountOpenAsync().Returns(4);
        _documentService.CountExpiringAsync(30).Returns(2);
        _inspectionService.CountUpcomingAsync(30).Returns(3);
        _workflowInstanceService.CountBottlenecksAsync(5).Returns(1);
        var sut = CreateSut();

        var result = await sut.GetSummaryAsync();

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.OpenWorkOrders.Should().Be(4);
        result.Data.ExpiringDocuments.Should().Be(2);
        result.Data.UpcomingInspections.Should().Be(3);
        result.Data.BottleneckedSteps.Should().Be(1);
    }

    [Fact]
    public async Task GetSummaryAsync_PassesConfiguredThreshold_ToBottleneckCount()
    {
        var sut = CreateSut(bottleneckThresholdDays: 10);

        await sut.GetSummaryAsync();

        await _workflowInstanceService.Received(1).CountBottlenecksAsync(10);
    }
}
