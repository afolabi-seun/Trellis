using Microsoft.Extensions.Options;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Document.Application.Interfaces;
using Trellis.Api.Modules.Inspection.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Dtos;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Configuration;
using Trellis.Api.Modules.Workflow.Application.Interfaces;

namespace Trellis.Api.Modules.Reporting.Application.Services;

/// <summary>
/// BDR-RPT-001 — composes 3 cross-module Application Services, the same "own service exposes
/// what it owns" convention every cross-module read in this codebase uses (confirmed: zero
/// exceptions anywhere to "cross-module reads go through the other module's own Application
/// Service via plain constructor DI"). Live-computed on every request, no caching/materialization
/// layer — matching Billing's own GetPlatformRevenueSummaryAsync precedent, the only other
/// "aggregate report" in this codebase.
/// </summary>
public sealed class DashboardService(
    IWorkflowInstanceService workflowInstanceService,
    IDocumentService documentService,
    IInspectionService inspectionService,
    IOptions<ReportingOptions> options) : IDashboardService
{
    /// <summary>Not a ReportingOptions field — this is a dashboard-tile lookahead, not a business-rule threshold; Document's own ExpiryAlertLookaheadDays (30) is the closest existing precedent value.</summary>
    private const int DashboardLookaheadDays = 30;

    public async Task<ServiceResult<DashboardSummaryResponse>> GetSummaryAsync()
    {
        var openWorkOrders = await workflowInstanceService.CountOpenAsync();
        var expiringDocuments = await documentService.CountExpiringAsync(DashboardLookaheadDays);
        var upcomingInspections = await inspectionService.CountUpcomingAsync(DashboardLookaheadDays);
        var bottleneckedSteps = await workflowInstanceService.CountBottlenecksAsync(options.Value.WorkflowBottleneckThresholdDays);

        return ServiceResult<DashboardSummaryResponse>.Success(
            new DashboardSummaryResponse(openWorkOrders, expiringDocuments, upcomingInspections, bottleneckedSteps));
    }
}
