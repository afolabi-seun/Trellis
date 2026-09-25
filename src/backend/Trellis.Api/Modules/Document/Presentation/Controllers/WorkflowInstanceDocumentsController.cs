using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Attributes;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Document.Application.Dtos;
using Trellis.Api.Modules.Document.Application.Interfaces;

namespace Trellis.Api.Modules.Document.Presentation.Controllers;

/// <summary>
/// BDR-VEN-003's evidence upload — split from DocumentsController since it's the one route both
/// TENANT_ADMIN/DEPARTMENT_STAFF *and* VENDOR_USER can reach; DocumentsController's own
/// class-level [Authorize] can't be widened per-action (ASP.NET Core ANDs stacked [Authorize]
/// role lists together, it doesn't OR them), so this needs its own controller rather than another
/// action on that one. VendorAssignedToWorkItem no-ops for non-VENDOR_USER callers (its own
/// documented behavior) — a TENANT_ADMIN/DEPARTMENT_STAFF caller always passes through untouched.
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = "document")]
[Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF,VENDOR_USER")]
public sealed class WorkflowInstanceDocumentsController(IDocumentService documentService, IVendorAssignmentReader vendorAssignmentReader)
    : ControllerBase
{
    [HttpGet("api/v1/workflow-instances/{workItemId:guid}/documents")]
    [VendorAssignedToWorkItem(WorkItemTypes.WorkflowInstance)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<DocumentResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid workItemId, [FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await documentService.ListByWorkflowInstanceAsync(workItemId, cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpPost("api/v1/workflow-instances/{workItemId:guid}/documents")]
    [VendorAssignedToWorkItem(WorkItemTypes.WorkflowInstance)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<DocumentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(Guid workItemId, [FromForm] DocumentUploadForm form)
    {
        await using var stream = form.File.OpenReadStream();

        ServiceResult<DocumentResponse> result;
        if (User.FindFirstValue("role") == "VENDOR_USER")
        {
            var vendorId = Guid.Parse(User.FindFirstValue("vendor_id")!);
            // Resolved from the assignment itself (IVendorAssignmentReader), never from a
            // request field — VendorAssignedToWorkItem already confirmed this vendor has a real
            // assignment for workItemId, so the tenant to attribute the upload to is a fact about
            // that assignment, not something the caller gets to assert.
            var tenantId = await vendorAssignmentReader.GetAssignmentTenantIdAsync(vendorId, WorkItemTypes.WorkflowInstance, workItemId);
            if (tenantId is null)
            {
                return ServiceResult<DocumentResponse>.NotFound().ToActionResult(HttpContext);
            }

            result = await documentService.CreateVendorEvidenceAsync(
                tenantId.Value, workItemId, form.DocumentType, stream, form.File.FileName, form.File.ContentType, form.File.Length, CurrentUserId());
        }
        else
        {
            result = await documentService.CreateAsync(
                new DocumentUploadRequest(form.DocumentType, form.ExpiresAt, null, null, null, workItemId),
                stream, form.File.FileName, form.File.ContentType, form.File.Length, CurrentUserId());
        }

        return result.ToActionResult(HttpContext);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue("sub")!);
}
