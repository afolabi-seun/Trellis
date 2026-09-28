using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Presentation.Filters;
using Trellis.Api.Modules.Leasing.Application.Dtos;
using Trellis.Api.Modules.Leasing.Application.Interfaces;

namespace Trellis.Api.Modules.Leasing.Presentation.Controllers;

/// <summary>
/// Staff-side listing management. TENANT_ADMIN + DEPARTMENT_STAFF (user-confirmed — no formal
/// "Leasing Agent" role exists). Leasing is Add-on tier, gated from day one with no default
/// entitlement (a new, integration-heavy capability is not silently free for existing tenants).
/// </summary>
[ApiController]
[Route("api/v1/vacancy-listings")]
[ApiExplorerSettings(GroupName = "leasing")]
[Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
[RequiresModuleEntitlement("LEASING")]
public sealed class VacancyListingsController(IVacancyListingService listingService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<VacancyListingResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await listingService.ListForTenantAsync(cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<VacancyListingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] VacancyListingCreateRequest request) =>
        (await listingService.CreateAsync(request, CurrentUserId())).ToActionResult(HttpContext);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<VacancyListingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id) =>
        (await listingService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<VacancyListingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] VacancyListingUpdateRequest request) =>
        (await listingService.UpdateAsync(id, request)).ToActionResult(HttpContext);

    [HttpPost("{id:guid}/withdraw")]
    [ProducesResponseType(typeof(ApiResponse<VacancyListingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Withdraw(Guid id) =>
        (await listingService.WithdrawAsync(id)).ToActionResult(HttpContext);

    [HttpPost("{id:guid}/photos")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<VacancyListingPhotoResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadPhoto(Guid id, [FromForm] VacancyListingPhotoUploadForm form)
    {
        await using var stream = form.File.OpenReadStream();
        var result = await listingService.UploadPhotoAsync(
            id, stream, form.File.FileName, form.File.ContentType, form.File.Length, CurrentUserId());

        return result.ToActionResult(HttpContext);
    }

    [HttpGet("{id:guid}/photos/{photoId:guid}/download")]
    public async Task<IActionResult> DownloadPhoto(Guid id, Guid photoId)
    {
        var result = await listingService.DownloadPhotoAsync(photoId);
        return result.Outcome == ServiceResultOutcome.Success
            ? File(result.Data!.Content, result.Data.ContentType, result.Data.FileName)
            : result.ToActionResult(HttpContext);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue("sub")!);
}
