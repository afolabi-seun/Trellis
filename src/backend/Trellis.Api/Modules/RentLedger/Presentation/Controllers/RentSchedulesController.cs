using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Presentation.Filters;
using Trellis.Api.Modules.RentLedger.Application.Dtos;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;

namespace Trellis.Api.Modules.RentLedger.Presentation.Controllers;

/// <summary>
/// Staff-side rent tracking. TENANT_ADMIN + DEPARTMENT_STAFF — recording what was received is
/// operational, matching Leasing. Add-on tier, no default entitlement. Every payment route records
/// a payment received OUTSIDE the platform; nothing here processes, holds, or moves funds
/// (BDR-RENT-006), which is also why the route is "payment-records", not a payment action.
/// </summary>
[ApiController]
[Route("api/v1/rent-schedules")]
[ApiExplorerSettings(GroupName = "rent-ledger")]
[Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
[RequiresModuleEntitlement("RENT_COLLECTION")]
public sealed class RentSchedulesController(IRentScheduleService scheduleService, IRentPaymentService paymentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<RentScheduleResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await scheduleService.ListAsync(cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpGet("arrears")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RentScheduleResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListArrears() =>
        (await scheduleService.ListArrearsAsync()).ToActionResult(HttpContext);

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RentScheduleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] RentScheduleCreateRequest request) =>
        (await scheduleService.CreateAsync(request, CurrentUserId())).ToActionResult(HttpContext);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RentScheduleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id) =>
        (await scheduleService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RentScheduleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] RentScheduleUpdateRequest request) =>
        (await scheduleService.UpdateAsync(id, request, CurrentUserId())).ToActionResult(HttpContext);

    /// <summary>The body is optional: required only when the schedule's lease was started manually, since ending that schedule ends the tenancy (BDR-LSE-011).</summary>
    [HttpPost("{id:guid}/end")]
    [ProducesResponseType(typeof(ApiResponse<RentScheduleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> End(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RentScheduleEndRequest? request) =>
        (await scheduleService.EndAsync(id, request, CurrentUserId())).ToActionResult(HttpContext);

    [HttpGet("{id:guid}/payment-records")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RentPaymentRecordResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPaymentRecords(Guid id) =>
        (await paymentService.ListAsync(id)).ToActionResult(HttpContext);

    [HttpPost("{id:guid}/payment-records")]
    [ProducesResponseType(typeof(ApiResponse<RentPaymentRecordResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordPaymentReceived(Guid id, [FromBody] RentPaymentRecordCreateRequest request) =>
        (await paymentService.RecordAsync(id, request, CurrentUserId())).ToActionResult(HttpContext);

    [HttpPost("{id:guid}/payment-records/{recordId:guid}/void")]
    [ProducesResponseType(typeof(ApiResponse<RentPaymentRecordResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VoidPaymentRecord(Guid id, Guid recordId, [FromBody] RentPaymentRecordVoidRequest request) =>
        (await paymentService.VoidAsync(id, recordId, request, CurrentUserId())).ToActionResult(HttpContext);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue("sub")!);
}
