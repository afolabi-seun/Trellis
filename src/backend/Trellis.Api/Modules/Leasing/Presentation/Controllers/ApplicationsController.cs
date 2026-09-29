using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth;
using Trellis.Api.Modules.Billing.Presentation.Filters;
using Trellis.Api.Modules.Leasing.Application.Dtos;
using Trellis.Api.Modules.Leasing.Application.Interfaces;

namespace Trellis.Api.Modules.Leasing.Presentation.Controllers;

/// <summary>
/// BDR-LSE-004 — staff review of submitted applications. TENANT_ADMIN + DEPARTMENT_STAFF
/// (user-confirmed). Only the coarse screening outcome summary ever appears in any ordinary
/// response here; the one exception is the password-gated screening-report endpoint below,
/// TENANT_ADMIN only.
/// </summary>
[ApiController]
[Route("api/v1/applications")]
[ApiExplorerSettings(GroupName = "leasing")]
[Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
[RequiresModuleEntitlement("LEASING")]
public sealed class ApplicationsController(
    IApplicationService applicationService, IScreeningReportService screeningReportService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ApplicationResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByListing([FromQuery] Guid vacancyListingId, [FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await applicationService.ListByListingAsync(vacancyListingId, cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id) =>
        (await applicationService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpGet("{id:guid}/events")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ApplicationEventResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents(Guid id) =>
        (await applicationService.GetEventsAsync(id)).ToActionResult(HttpContext);

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id) =>
        (await applicationService.ApproveAsync(id, CurrentUserId())).ToActionResult(HttpContext);

    [HttpPost("{id:guid}/decline")]
    [ProducesResponseType(typeof(ApiResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Decline(Guid id) =>
        (await applicationService.DeclineAsync(id, CurrentUserId())).ToActionResult(HttpContext);

    [HttpPost("{id:guid}/send-for-signature")]
    [ProducesResponseType(typeof(ApiResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendForSignature(Guid id, [FromBody] SendForSignatureRequest request) =>
        (await applicationService.SendForSignatureAsync(id, request.DraftDocumentId, CurrentUserId())).ToActionResult(HttpContext);

    /// <summary>A staff-initiated move-out on a Leased application — ends its rent schedule (if any) and frees the unit to be re-listed.</summary>
    [HttpPost("{id:guid}/end-lease")]
    [ProducesResponseType(typeof(ApiResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> EndLease(Guid id, [FromBody] EndLeaseRequest request) =>
        (await applicationService.EndLeaseAsync(id, request, CurrentUserId())).ToActionResult(HttpContext);

    /// <summary>BDR-LSE-004's one exception — TENANT_ADMIN only, password re-entry, rate-limited the same as any other authentication-adjacent endpoint (security_guide.md's checklist). Never cached: the response body is the single most sensitive payload this API returns.</summary>
    [HttpPost("{id:guid}/screening-report")]
    [Authorize(Roles = "TENANT_ADMIN")]
    [EnableRateLimiting(AuthModuleExtensions.AuthSensitiveRateLimitPolicy)]
    [ProducesResponseType(typeof(ApiResponse<ScreeningReportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ViewScreeningReport(Guid id, [FromBody] ViewScreeningReportRequest request)
    {
        Response.Headers.CacheControl = "no-store";
        return (await screeningReportService.ViewAsync(id, CurrentUserId(), request.CurrentPassword)).ToActionResult(HttpContext);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue("sub")!);
}
