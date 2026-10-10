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
/// BDR-LSE-010 — lease renewal offers and their outcomes, recorded by staff (lessees have no
/// platform access). Part of the Leasing add-on, like the rest of BRD Section 5.13, but an offer
/// can be made on any active lease, however it was started.
/// </summary>
[ApiController]
[Route("api/v1/renewal-offers")]
[ApiExplorerSettings(GroupName = "leasing")]
[Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
[RequiresModuleEntitlement("LEASING")]
public sealed class RenewalOffersController(IRenewalOfferService renewalOfferService) : ControllerBase
{
    /// <summary>Newest first. status: OFFERED, ACCEPTED, DECLINED, or EXPIRED.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RenewalOfferResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? leaseId, [FromQuery] string? status) =>
        (await renewalOfferService.ListAsync(leaseId, status)).ToActionResult(HttpContext);

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RenewalOfferResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] RenewalOfferCreateRequest request) =>
        (await renewalOfferService.CreateAsync(request, CurrentUserId())).ToActionResult(HttpContext);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RenewalOfferResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id) =>
        (await renewalOfferService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpPost("{id:guid}/accept")]
    [ProducesResponseType(typeof(ApiResponse<RenewalOfferResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Accept(Guid id, [FromBody] RenewalOfferAcceptRequest request) =>
        (await renewalOfferService.AcceptAsync(id, request, CurrentUserId())).ToActionResult(HttpContext);

    [HttpPost("{id:guid}/decline")]
    [ProducesResponseType(typeof(ApiResponse<RenewalOfferResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Decline(Guid id, [FromBody] RenewalOfferDeclineRequest request) =>
        (await renewalOfferService.DeclineAsync(id, request, CurrentUserId())).ToActionResult(HttpContext);

    /// <summary>Only after the respond-by date has passed with no answer.</summary>
    [HttpPost("{id:guid}/expire")]
    [ProducesResponseType(typeof(ApiResponse<RenewalOfferResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Expire(Guid id) =>
        (await renewalOfferService.ExpireAsync(id, CurrentUserId())).ToActionResult(HttpContext);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue("sub")!);
}
