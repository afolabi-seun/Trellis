using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Dtos;
using Trellis.Api.Modules.Billing.Application.Interfaces;

namespace Trellis.Api.Modules.Billing.Presentation.Controllers;

/// <summary>
/// api_standards.md's "Tenant Management Endpoints (Platform)" section — the /platform/ prefix,
/// not /api/v1/: this is genuinely cross-tenant by nature (BDR-BIL-007's revenue view and
/// BDR-BIL-009's per-tenant entitlement management), SUPER_ADMIN only, exact shape of
/// TenantsController.
/// </summary>
[ApiController]
[Route("platform/billing")]
[ApiExplorerSettings(GroupName = "billing")]
[Authorize(Roles = "SUPER_ADMIN")]
public sealed class PlatformBillingController(
    ISubscriptionService subscriptionService, IInvoiceService invoiceService, IModuleEntitlementService moduleEntitlementService)
    : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<PlatformRevenueSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary() =>
        (await subscriptionService.GetPlatformRevenueSummaryAsync()).ToActionResult(HttpContext);

    [HttpGet("subscriptions")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<SubscriptionResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSubscriptions([FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await subscriptionService.ListAllAsync(cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpGet("invoices")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<InvoiceResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListInvoices([FromQuery] Guid? tenantId, [FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await invoiceService.ListAllAsync(tenantId, cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpGet("module-entitlements/{tenantId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ModuleEntitlementResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListModuleEntitlements(Guid tenantId) =>
        (await moduleEntitlementService.ListForTenantAsync(tenantId)).ToActionResult(HttpContext);

    [HttpPut("module-entitlements/{tenantId:guid}/{moduleKey}")]
    [ProducesResponseType(typeof(ApiResponse<ModuleEntitlementResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetModuleEntitlement(Guid tenantId, string moduleKey, [FromBody] ModuleEntitlementSetRequest request) =>
        (await moduleEntitlementService.SetAsync(tenantId, moduleKey.ToUpperInvariant(), request)).ToActionResult(HttpContext);
}
