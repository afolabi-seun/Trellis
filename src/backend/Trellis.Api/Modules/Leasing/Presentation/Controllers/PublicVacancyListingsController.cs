using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Leasing.Application.Dtos;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Tenant.Application.Interfaces;

namespace Trellis.Api.Modules.Leasing.Presentation.Controllers;

/// <summary>
/// The public vacancy catalog — a genuine browse/search surface, not a single-token lookup, so it
/// lives under the normal versioned /api/v1/ contract (standard ApiResponse envelope, pagination)
/// rather than SharedReportController's bare-route shape. The tenant is identified by its public
/// slug (never its GUID, BDR-SEC-004): the controller resolves slug → tenant once, in front of the
/// existing tenantId-shaped services, which stay unchanged. Every failure mode — unknown or
/// inactive slug, a tenant that isn't entitled to Leasing — is the same plain 404.
/// </summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(GroupName = "leasing")]
[Route("api/v1/public/tenants/{tenantSlug}/vacancy-listings")]
public sealed class PublicVacancyListingsController(
    IVacancyListingService listingService, IApplicationLinkService linkService, ITenantService tenantService) : ControllerBase
{
    private async Task<Guid?> ResolveTenantAsync(string tenantSlug)
    {
        var tenant = await tenantService.ResolveSlugAsync(tenantSlug);
        return tenant.Outcome == ServiceResultOutcome.Success ? tenant.Data!.TenantId : null;
    }

    private IActionResult UnknownTenant() => ServiceResult<bool>.NotFound().ToActionResult(HttpContext);

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<PublicVacancyListingResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Browse(string tenantSlug, [FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        await ResolveTenantAsync(tenantSlug) is { } tenantId
            ? (await listingService.BrowsePublicAsync(tenantId, cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext)
            : UnknownTenant();

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PublicVacancyListingDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(string tenantSlug, Guid id) =>
        await ResolveTenantAsync(tenantSlug) is { } tenantId
            ? (await listingService.GetPublicByIdAsync(tenantId, id)).ToActionResult(HttpContext)
            : UnknownTenant();

    /// <summary>Clean-scanned photos only — an unscanned/infected photo is a plain 404 to an anonymous caller.</summary>
    [HttpGet("{id:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> GetPhoto(string tenantSlug, Guid id, Guid photoId)
    {
        if (await ResolveTenantAsync(tenantSlug) is not { } tenantId)
        {
            return UnknownTenant();
        }

        var result = await listingService.GetPublicPhotoAsync(tenantId, photoId);
        return result.Outcome == ServiceResultOutcome.Success
            ? File(result.Data!.Content, result.Data.ContentType)
            : result.ToActionResult(HttpContext);
    }

    /// <summary>BDR-LSE-002/008 — emails a single-use link to the given address. The raw token is delivered only by email and never returned here, so this response can't be used to obtain a link for an address the caller doesn't control.</summary>
    [HttpPost("{id:guid}/application-link")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> IssueApplicationLink(string tenantSlug, Guid id, [FromBody] ApplicationLinkIssueRequest request)
    {
        if (await ResolveTenantAsync(tenantSlug) is not { } tenantId)
        {
            return UnknownTenant();
        }

        var result = await linkService.IssueApplicationLinkAsync(tenantId, id, request.ApplicantEmail);
        return result.Outcome == ServiceResultOutcome.Success
            ? ServiceResult<bool>.Success(true).ToActionResult(HttpContext)
            : result.ToActionResult(HttpContext);
    }
}
