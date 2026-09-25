using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Attributes;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Vendor.Application.Dtos;
using Trellis.Api.Modules.Vendor.Application.Interfaces;

namespace Trellis.Api.Modules.Vendor.Presentation.Controllers;

/// <summary>
/// BDR-VEN-002/003 — a Vendor's own view: assignments across every tenant that has engaged them
/// (roadmap.md's exit criterion, made real), keyed by work item id rather than the assignment's
/// own PK (matching VendorAssignedToWorkItemAttribute's own route param) so the same id a Vendor
/// sees in a work item reference is what every action here takes.
/// </summary>
[ApiController]
[Route("api/v1/vendor-portal/assignments")]
[ApiExplorerSettings(GroupName = "vendor")]
[Authorize(Roles = "VENDOR_USER")]
public sealed class VendorPortalAssignmentsController(IVendorAssignmentService assignmentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<VendorAssignmentVendorView>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await assignmentService.ListForVendorAsync(CurrentVendorId(), cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpGet("{workItemId:guid}")]
    [VendorAssignedToWorkItem(WorkItemTypes.WorkflowInstance)]
    [ProducesResponseType(typeof(ApiResponse<VendorAssignmentDetailView>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid workItemId) =>
        (await assignmentService.GetForVendorAsync(CurrentVendorId(), WorkItemTypes.WorkflowInstance, workItemId)).ToActionResult(HttpContext);

    [HttpPost("{workItemId:guid}/start")]
    [VendorAssignedToWorkItem(WorkItemTypes.WorkflowInstance)]
    [ProducesResponseType(typeof(ApiResponse<VendorAssignmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Start(Guid workItemId) =>
        (await assignmentService.StartAsync(CurrentVendorId(), WorkItemTypes.WorkflowInstance, workItemId)).ToActionResult(HttpContext);

    /// <summary>BDR-VEN-005's approval/sign-off gate is deferred — this closes the assignment immediately (VendorAssignment.Complete()'s own doc comment).</summary>
    [HttpPost("{workItemId:guid}/complete")]
    [VendorAssignedToWorkItem(WorkItemTypes.WorkflowInstance)]
    [ProducesResponseType(typeof(ApiResponse<VendorAssignmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(Guid workItemId) =>
        (await assignmentService.CompleteAsync(CurrentVendorId(), WorkItemTypes.WorkflowInstance, workItemId)).ToActionResult(HttpContext);

    private Guid CurrentVendorId() => Guid.Parse(User.FindFirstValue("vendor_id")!);
}
