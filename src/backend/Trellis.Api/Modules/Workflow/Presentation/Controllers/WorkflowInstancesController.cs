using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Workflow.Application.Dtos;
using Trellis.Api.Modules.Workflow.Application.Interfaces;

namespace Trellis.Api.Modules.Workflow.Presentation.Controllers;

/// <summary>Both TENANT_ADMIN and DEPARTMENT_STAFF can create/view instances; Approve/Reject's finer TenantAdminOrDepartmentLead check (department membership on the active step) lives in the Service — see IWorkflowInstanceService's doc comment.</summary>
[ApiController]
[Route("api/v1/workflow-instances")]
[ApiExplorerSettings(GroupName = "workflow")]
[Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
public sealed class WorkflowInstancesController(IWorkflowInstanceService instanceService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<WorkflowInstanceResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await instanceService.ListAsync(cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<WorkflowInstanceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] WorkflowInstanceCreateRequest request) =>
        (await instanceService.CreateAsync(request, CurrentUserId())).ToActionResult(HttpContext);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<WorkflowInstanceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id) =>
        (await instanceService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<WorkflowInstanceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] WorkflowActionRequest request) =>
        (await instanceService.ApproveAsync(id, CurrentUserId(), CurrentRole(), CurrentDepartmentIds(), request)).ToActionResult(HttpContext);

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<WorkflowInstanceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] WorkflowActionRequest request) =>
        (await instanceService.RejectAsync(id, CurrentUserId(), CurrentRole(), CurrentDepartmentIds(), request)).ToActionResult(HttpContext);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue("sub")!);

    private string CurrentRole() => User.FindFirstValue("role")!;

    /// <summary>security_guide.md's no-per-request-revalidation-against-Auth principle — read straight off the token, same as JwtTokenService.IssueAccessToken adds them.</summary>
    private List<Guid> CurrentDepartmentIds() =>
        User.FindAll("department_id").Select(c => Guid.Parse(c.Value)).ToList();
}
