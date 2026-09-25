using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Workflow.Application.Dtos;
using Trellis.Api.Modules.Workflow.Application.Interfaces;

namespace Trellis.Api.Modules.Workflow.Presentation.Controllers;

/// <summary>BDR-WF-001 — a Tenant Admin's configurable approval-chain builder; DEPARTMENT_STAFF can read (needed to pick a template when creating an instance) but never edit.</summary>
[ApiController]
[Route("api/v1/workflow-templates")]
[ApiExplorerSettings(GroupName = "workflow")]
public sealed class WorkflowTemplatesController(IWorkflowTemplateService templateService) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<WorkflowTemplateResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await templateService.ListAsync(cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
    [ProducesResponseType(typeof(ApiResponse<WorkflowTemplateResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id) =>
        (await templateService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpPost]
    [Authorize(Roles = "TENANT_ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<WorkflowTemplateResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] WorkflowTemplateCreateRequest request) =>
        (await templateService.CreateAsync(request)).ToActionResult(HttpContext);

    [HttpPatch("{id:guid}")]
    [Authorize(Roles = "TENANT_ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<WorkflowTemplateResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] WorkflowTemplateUpdateRequest request) =>
        (await templateService.UpdateAsync(id, request)).ToActionResult(HttpContext);

    /// <summary>Archives, never a hard delete — WorkflowTemplate.Archive()'s own doc comment explains why.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "TENANT_ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<WorkflowTemplateResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Archive(Guid id) =>
        (await templateService.ArchiveAsync(id)).ToActionResult(HttpContext);
}
