using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Reporting.Application.Dtos;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.Reporting.Presentation.Controllers;

/// <summary>
/// Reporting is Core tier (project_architecture.md's Module Catalogue) — bundled for every
/// tenant, never behind [RequiresModuleEntitlement]. Mixed role requirements per action rather
/// than one class-level [Authorize]: the dashboard (BDR-RPT-001) is TENANT_ADMIN+DEPARTMENT_STAFF,
/// but every Owner-report action is TENANT_ADMIN only — the same financial-sensitivity class
/// Billing's own TENANT_ADMIN-only controller already treats billing data as.
/// </summary>
[ApiController]
[Route("api/v1/reporting")]
[ApiExplorerSettings(GroupName = "reporting")]
public sealed class ReportingController(
    IDashboardService dashboardService, IReportExportService exportService, IOwnerReportService ownerReportService, IReportShareLinkService shareLinkService)
    : ControllerBase
{
    [HttpGet("dashboard")]
    [Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
    [ProducesResponseType(typeof(ApiResponse<DashboardSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard() =>
        (await dashboardService.GetSummaryAsync()).ToActionResult(HttpContext);

    [HttpGet("dashboard/export.csv")]
    [Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
    public async Task<IActionResult> ExportDashboardCsv()
    {
        var result = await dashboardService.GetSummaryAsync();
        return result.Outcome == ServiceResultOutcome.Success
            ? File(exportService.GenerateDashboardCsv(result.Data!), "text/csv", "dashboard-summary.csv")
            : result.ToActionResult(HttpContext);
    }

    [HttpPost("owner-reports")]
    [Authorize(Roles = "TENANT_ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<OwnerReportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateOwnerReport([FromBody] OwnerReportCreateRequest request) =>
        (await ownerReportService.GenerateAsync(
            request.OwnerId, ParsePeriodType(request.PeriodType), request.PeriodStart, request.PeriodEnd, CurrentUserId()))
        .ToActionResult(HttpContext);

    [HttpGet("owner-reports")]
    [Authorize(Roles = "TENANT_ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<OwnerReportResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListOwnerReports([FromQuery] Guid? ownerId, [FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await ownerReportService.ListByOwnerAsync(ownerId, cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpGet("owner-reports/{id:guid}")]
    [Authorize(Roles = "TENANT_ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<OwnerReportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOwnerReport(Guid id) =>
        (await ownerReportService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpGet("owner-reports/{id:guid}/download.pdf")]
    [Authorize(Roles = "TENANT_ADMIN")]
    public async Task<IActionResult> DownloadOwnerReportPdf(Guid id)
    {
        var result = await ownerReportService.DownloadPdfAsync(id);
        return result.Outcome == ServiceResultOutcome.Success
            ? File(result.Data!, "application/pdf", $"owner-report-{id}.pdf")
            : result.ToActionResult(HttpContext);
    }

    [HttpGet("owner-reports/{id:guid}/download.csv")]
    [Authorize(Roles = "TENANT_ADMIN")]
    public async Task<IActionResult> DownloadOwnerReportCsv(Guid id)
    {
        var result = await ownerReportService.DownloadCsvAsync(id);
        return result.Outcome == ServiceResultOutcome.Success
            ? File(result.Data!, "text/csv", $"owner-report-{id}.csv")
            : result.ToActionResult(HttpContext);
    }

    [HttpPost("owner-reports/{id:guid}/deliver")]
    [Authorize(Roles = "TENANT_ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeliverOwnerReport(Guid id) =>
        (await ownerReportService.DeliverByEmailAsync(id, CurrentUserId())).ToActionResult(HttpContext);

    [HttpPost("owner-reports/{id:guid}/share-links")]
    [Authorize(Roles = "TENANT_ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<ReportShareLinkResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateShareLink(Guid id) =>
        (await shareLinkService.CreateAsync(id, CurrentUserId())).ToActionResult(HttpContext);

    [HttpDelete("share-links/{id:guid}")]
    [Authorize(Roles = "TENANT_ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeShareLink(Guid id) =>
        (await shareLinkService.RevokeAsync(id)).ToActionResult(HttpContext);

    private static OwnerReportPeriodType ParsePeriodType(string value) => value.ToUpperInvariant() switch
    {
        "QUARTERLY" => OwnerReportPeriodType.Quarterly,
        _ => OwnerReportPeriodType.Monthly,
    };

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue("sub")!);
}
