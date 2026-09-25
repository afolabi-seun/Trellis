using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Document.Application.Dtos;
using Trellis.Api.Modules.Document.Application.Interfaces;

namespace Trellis.Api.Modules.Document.Presentation.Controllers;

/// <summary>
/// No class-level [Route] — a document is always created/listed within its linked parent
/// (property/unit/owner), or standalone via the flat route, but read/updated/archived/
/// downloaded by its own flat id, same no-class-level-[Route] shape as UnitsController.
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = "document")]
[Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
public sealed class DocumentsController(IDocumentService documentService) : ControllerBase
{
    [HttpGet("api/v1/documents")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<DocumentResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await documentService.ListAsync(cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpPost("api/v1/documents")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<DocumentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromForm] DocumentUploadForm form) =>
        (await UploadAsync(
            new DocumentUploadRequest(form.DocumentType, form.ExpiresAt, form.PropertyId, form.UnitAssetId, form.OwnerId, form.WorkflowInstanceId), form.File))
        .ToActionResult(HttpContext);

    [HttpGet("api/v1/properties/{propertyId:guid}/documents")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<DocumentResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByProperty(Guid propertyId, [FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await documentService.ListByPropertyAsync(propertyId, cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpPost("api/v1/properties/{propertyId:guid}/documents")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<DocumentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateForProperty(Guid propertyId, [FromForm] DocumentUploadForm form) =>
        (await UploadAsync(new DocumentUploadRequest(form.DocumentType, form.ExpiresAt, propertyId, null, null, null), form.File))
        .ToActionResult(HttpContext);

    [HttpGet("api/v1/units/{unitAssetId:guid}/documents")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<DocumentResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByUnit(Guid unitAssetId, [FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await documentService.ListByUnitAssetAsync(unitAssetId, cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpPost("api/v1/units/{unitAssetId:guid}/documents")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<DocumentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateForUnit(Guid unitAssetId, [FromForm] DocumentUploadForm form) =>
        (await UploadAsync(new DocumentUploadRequest(form.DocumentType, form.ExpiresAt, null, unitAssetId, null, null), form.File))
        .ToActionResult(HttpContext);

    [HttpGet("api/v1/owners/{ownerId:guid}/documents")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<DocumentResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByOwner(Guid ownerId, [FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await documentService.ListByOwnerAsync(ownerId, cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpPost("api/v1/owners/{ownerId:guid}/documents")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<DocumentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateForOwner(Guid ownerId, [FromForm] DocumentUploadForm form) =>
        (await UploadAsync(new DocumentUploadRequest(form.DocumentType, form.ExpiresAt, null, null, ownerId, null), form.File))
        .ToActionResult(HttpContext);

    [HttpGet("api/v1/documents/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id) =>
        (await documentService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpPatch("api/v1/documents/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] DocumentUpdateRequest request) =>
        (await documentService.UpdateAsync(id, request)).ToActionResult(HttpContext);

    /// <summary>Archives, never a hard delete — see DocumentRecord.Archive().</summary>
    [HttpDelete("api/v1/documents/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Archive(Guid id) =>
        (await documentService.ArchiveAsync(id)).ToActionResult(HttpContext);

    [HttpGet("api/v1/documents/{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var result = await documentService.DownloadAsync(id, CurrentUserId());
        return result.Outcome == ServiceResultOutcome.Success
            ? File(result.Data!.Content, result.Data.ContentType, result.Data.FileName)
            : result.ToActionResult(HttpContext);
    }

    private async Task<ServiceResult<DocumentResponse>> UploadAsync(DocumentUploadRequest request, IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        return await documentService.CreateAsync(request, stream, file.FileName, file.ContentType, file.Length, CurrentUserId());
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue("sub")!);
}
