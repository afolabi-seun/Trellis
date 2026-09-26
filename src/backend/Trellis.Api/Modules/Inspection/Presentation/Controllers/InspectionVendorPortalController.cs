using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Attributes;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Inspection.Application.Dtos;
using Trellis.Api.Modules.Inspection.Application.Interfaces;

namespace Trellis.Api.Modules.Inspection.Presentation.Controllers;

/// <summary>
/// Vendor-side — mirrors VendorPortalAssignmentsController.cs's exact shape/reasoning (a separate
/// controller from InspectionsController since [Authorize(Roles=...)] ANDs across stacked
/// attributes, not ORs). <see cref="Complete"/> is the one idempotency-protected route in the
/// whole phase — the offline-sync completion a flaky mobile connection may retry verbatim.
/// </summary>
[ApiController]
[Route("api/v1/vendor-portal/inspections")]
[ApiExplorerSettings(GroupName = "inspection")]
[Authorize(Roles = "VENDOR_USER")]
public sealed class InspectionVendorPortalController(
    IInspectionService inspectionService, IVendorAssignmentReader vendorAssignmentReader, IdempotencyExecutor idempotencyExecutor)
    : ControllerBase
{
    [HttpGet("{workItemId:guid}")]
    [VendorAssignedToWorkItem(WorkItemTypes.Inspection)]
    [ProducesResponseType(typeof(ApiResponse<VendorInspectionView>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid workItemId) =>
        (await inspectionService.GetForVendorAsync(CurrentVendorId(), workItemId)).ToActionResult(HttpContext);

    [HttpPost("{workItemId:guid}/start")]
    [VendorAssignedToWorkItem(WorkItemTypes.Inspection)]
    [ProducesResponseType(typeof(ApiResponse<VendorInspectionView>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Start(Guid workItemId) =>
        (await inspectionService.StartForVendorAsync(CurrentVendorId(), workItemId)).ToActionResult(HttpContext);

    [HttpGet("{workItemId:guid}/photos/{photoId:guid}/download")]
    [VendorAssignedToWorkItem(WorkItemTypes.Inspection)]
    public async Task<IActionResult> DownloadPhoto(Guid workItemId, Guid photoId)
    {
        var result = await inspectionService.DownloadPhotoAsync(photoId, CurrentUserId());
        return result.Outcome == ServiceResultOutcome.Success
            ? File(result.Data!.Content, result.Data.ContentType, result.Data.FileName)
            : result.ToActionResult(HttpContext);
    }

    [HttpPost("{workItemId:guid}/complete")]
    [VendorAssignedToWorkItem(WorkItemTypes.Inspection)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<VendorInspectionView>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(Guid workItemId, [FromForm] InspectionCompleteForm form)
    {
        if (!Request.Headers.TryGetValue("X-Idempotency-Key", out var idempotencyKeyValues) || string.IsNullOrWhiteSpace(idempotencyKeyValues.ToString()))
        {
            return ServiceResult<object>.ValidationFailed(
                [new ValidationDetail("X-Idempotency-Key", "The X-Idempotency-Key header is required.")]).ToActionResult(HttpContext);
        }

        var vendorId = CurrentVendorId();
        var tenantId = await vendorAssignmentReader.GetAssignmentTenantIdAsync(vendorId, WorkItemTypes.Inspection, workItemId);
        if (tenantId is null)
        {
            return ServiceResult<object>.NotFound().ToActionResult(HttpContext);
        }

        var items = JsonSerializer.Deserialize<List<InspectionCompleteItem>>(form.ItemsJson) ?? [];
        var location = string.IsNullOrWhiteSpace(form.LocationJson)
            ? null
            : JsonSerializer.Deserialize<InspectionCompleteLocation>(form.LocationJson);
        var photoMetaByFileName = (JsonSerializer.Deserialize<List<InspectionPhotoMeta>>(form.PhotoMetaJson) ?? [])
            .ToDictionary(m => m.FileName, m => m);

        var openStreams = new List<Stream>();
        try
        {
            var photos = new List<InspectionPhotoUpload>();
            foreach (var file in form.Photos)
            {
                var meta = photoMetaByFileName.GetValueOrDefault(file.FileName);
                var stream = file.OpenReadStream();
                openStreams.Add(stream);
                photos.Add(new InspectionPhotoUpload(
                    file.FileName, file.ContentType, file.Length, stream,
                    meta?.InspectionItemId, meta?.Latitude, meta?.Longitude, meta?.CapturedAt ?? DateTimeOffset.UtcNow));
            }

            var idempotencyKey = idempotencyKeyValues.ToString();
            var completeRequest = new InspectionCompleteRequest(items, location, photos);

            var result = await idempotencyExecutor.ExecuteIdempotentAsync(
                tenantId.Value, idempotencyKey, () => inspectionService.CompleteFromVendorAsync(vendorId, workItemId, completeRequest, CurrentUserId()));

            return result.ToActionResult(HttpContext);
        }
        finally
        {
            foreach (var stream in openStreams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    private Guid CurrentVendorId() => Guid.Parse(User.FindFirstValue("vendor_id")!);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue("sub")!);
}
