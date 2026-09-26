using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Inspection.Application.Dtos;
using Trellis.Api.Modules.Inspection.Application.Interfaces;

namespace Trellis.Api.Modules.Inspection.Presentation.Controllers;

/// <summary>Staff-side: schedule/read/manage an inspection. None of these routes are idempotency-protected — every one is a single, online, synchronous staff action, unlike the vendor's offline-sync completion.</summary>
[ApiController]
[Route("api/v1/inspections")]
[ApiExplorerSettings(GroupName = "inspection")]
[Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
public sealed class InspectionsController(IInspectionService inspectionService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<InspectionReportResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await inspectionService.ListForTenantAsync(cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<InspectionReportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] InspectionCreateRequest request) =>
        (await inspectionService.ScheduleAsync(request, CurrentUserId())).ToActionResult(HttpContext);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<InspectionReportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id) =>
        (await inspectionService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpPost("{id:guid}/items")]
    [ProducesResponseType(typeof(ApiResponse<InspectionItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] InspectionItemAddRequest request) =>
        (await inspectionService.AddItemAsync(id, request)).ToActionResult(HttpContext);

    [HttpPatch("{id:guid}/items/{itemId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<InspectionItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateItem(Guid id, Guid itemId, [FromBody] InspectionItemUpdateRequest request) =>
        (await inspectionService.UpdateItemAsync(id, itemId, request)).ToActionResult(HttpContext);

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveItem(Guid id, Guid itemId) =>
        (await inspectionService.RemoveItemAsync(id, itemId)).ToActionResult(HttpContext);

    [HttpPost("{id:guid}/photos")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<InspectionPhotoResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadPhoto(Guid id, [FromForm] InspectionPhotoUploadForm form)
    {
        await using var stream = form.File.OpenReadStream();
        var result = await inspectionService.UploadPhotoAsync(
            id, form.InspectionItemId, stream, form.File.FileName, form.File.ContentType, form.File.Length,
            form.Latitude, form.Longitude, form.CapturedAt ?? DateTimeOffset.UtcNow, CurrentUserId());

        return result.ToActionResult(HttpContext);
    }

    [HttpGet("{id:guid}/photos/{photoId:guid}/download")]
    public async Task<IActionResult> DownloadPhoto(Guid id, Guid photoId)
    {
        var result = await inspectionService.DownloadPhotoAsync(photoId, CurrentUserId());
        return result.Outcome == ServiceResultOutcome.Success
            ? File(result.Data!.Content, result.Data.ContentType, result.Data.FileName)
            : result.ToActionResult(HttpContext);
    }

    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(typeof(ApiResponse<InspectionReportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Start(Guid id) =>
        (await inspectionService.StartAsync(id, CurrentUserId())).ToActionResult(HttpContext);

    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(ApiResponse<InspectionReportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(Guid id, [FromBody] InspectionCompleteStaffRequest request) =>
        (await inspectionService.CompleteAsync(id, CurrentUserId(), request.Latitude, request.Longitude)).ToActionResult(HttpContext);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue("sub")!);
}
