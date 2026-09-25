using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Workflow.Application.Dtos;
using Trellis.Api.Modules.Workflow.Application.Interfaces;

namespace Trellis.Api.Modules.Workflow.Presentation.Controllers;

/// <summary>BDR-DEPT-001 — a Tenant Admin's configurable department catalogue; only they may edit it, DEPARTMENT_STAFF can only read it (needed to see which department a workflow step routed to).</summary>
[ApiController]
[Route("api/v1/departments")]
[ApiExplorerSettings(GroupName = "workflow")]
public sealed class DepartmentsController(IDepartmentService departmentService) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<DepartmentResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await departmentService.ListAsync(cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id) =>
        (await departmentService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpPost]
    [Authorize(Roles = "TENANT_ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] DepartmentCreateRequest request) =>
        (await departmentService.CreateAsync(request)).ToActionResult(HttpContext);

    [HttpPatch("{id:guid}")]
    [Authorize(Roles = "TENANT_ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] DepartmentUpdateRequest request) =>
        (await departmentService.UpdateAsync(id, request)).ToActionResult(HttpContext);

    /// <summary>Archives, never a hard delete — Department.Archive()'s own doc comment explains why.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "TENANT_ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Archive(Guid id) =>
        (await departmentService.ArchiveAsync(id)).ToActionResult(HttpContext);
}
