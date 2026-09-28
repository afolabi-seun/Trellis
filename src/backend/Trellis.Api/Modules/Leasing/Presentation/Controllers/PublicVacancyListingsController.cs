using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Leasing.Application.Dtos;
using Trellis.Api.Modules.Leasing.Application.Interfaces;

namespace Trellis.Api.Modules.Leasing.Presentation.Controllers;

/// <summary>
/// The public vacancy catalog — a genuine browse/search surface, not a single-token lookup, so it
/// lives under the normal versioned /api/v1/ contract (standard ApiResponse envelope, pagination)
/// rather than SharedReportController's bare-route shape. Carries an explicit tenantId path
/// segment: no tenant-resolution-by-domain/slug mechanism exists anywhere in this codebase yet
/// (a known, deliberately-deferred gap — a shared listing URL embeds the raw tenant GUID).
/// Every failure mode, including a tenant that isn't entitled to Leasing, is a plain 404.
/// </summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(GroupName = "leasing")]
[Route("api/v1/public/tenants/{tenantId:guid}/vacancy-listings")]
public sealed class PublicVacancyListingsController(IVacancyListingService listingService, IApplicationLinkService linkService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<PublicVacancyListingResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Browse(Guid tenantId, [FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await listingService.BrowsePublicAsync(tenantId, cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PublicVacancyListingDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid tenantId, Guid id) =>
        (await listingService.GetPublicByIdAsync(tenantId, id)).ToActionResult(HttpContext);

    /// <summary>Clean-scanned photos only — an unscanned/infected photo is a plain 404 to an anonymous caller.</summary>
    [HttpGet("{id:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> GetPhoto(Guid tenantId, Guid id, Guid photoId)
    {
        var result = await listingService.GetPublicPhotoAsync(tenantId, photoId);
        return result.Outcome == ServiceResultOutcome.Success
            ? File(result.Data!.Content, result.Data.ContentType)
            : result.ToActionResult(HttpContext);
    }

    /// <summary>BDR-LSE-002/008 — emails a single-use link to the given address. The raw token is delivered only by email and never returned here, so this response can't be used to obtain a link for an address the caller doesn't control.</summary>
    [HttpPost("{id:guid}/application-link")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> IssueApplicationLink(Guid tenantId, Guid id, [FromBody] ApplicationLinkIssueRequest request)
    {
        var result = await linkService.IssueApplicationLinkAsync(tenantId, id, request.ApplicantEmail);
        return result.Outcome == ServiceResultOutcome.Success
            ? ServiceResult<bool>.Success(true).ToActionResult(HttpContext)
            : result.ToActionResult(HttpContext);
    }
}
