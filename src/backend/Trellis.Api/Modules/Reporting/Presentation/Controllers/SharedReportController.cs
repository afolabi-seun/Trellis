using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Reporting.Application.Interfaces;

namespace Trellis.Api.Modules.Reporting.Presentation.Controllers;

/// <summary>
/// BDR-SEC-003's public share-link resolution — bare route, no /api/v1/ prefix, [AllowAnonymous],
/// mirrors PaystackWebhookController's own bare-route shape. Response is raw PDF bytes, not an
/// HTML page or JSON envelope — the same artifact an Owner would get by email attachment, so no
/// new frontend route is needed; the browser's native PDF viewer is the "report viewer." Wrapped
/// in SystemContext.Enter() for the cross-tenant token lookup — IReportShareLinkService.ResolveAsync
/// itself never depends on TenantContext.Current (see its own doc comment), so this scope exists
/// only to satisfy report_share_link's standard tenant_isolation RLS policy for the SELECT, not to
/// enable any Scoped() call.
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class SharedReportController(IReportShareLinkService shareLinkService) : ControllerBase
{
    [HttpGet("reports/shared/{token}")]
    public async Task<IActionResult> GetShared(string token)
    {
        using var systemScope = SystemContext.Enter();
        var result = await shareLinkService.ResolveAsync(token);

        if (result.Outcome != ServiceResultOutcome.Success)
        {
            return NotFound();
        }

        var (pdfContent, periodStart) = result.Data;
        return File(pdfContent, "application/pdf", $"owner-report-{periodStart:yyyy-MM-dd}.pdf");
    }
}
