using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Dtos;
using Trellis.Api.Modules.Billing.Application.Interfaces;

namespace Trellis.Api.Modules.Billing.Presentation.Controllers;

/// <summary>BDR-BIL-006 — a Tenant Admin's own view of their tenant's billing state. Deliberately TENANT_ADMIN only, not DEPARTMENT_STAFF, unlike most /admin routes — billing is never a staff concern.</summary>
[ApiController]
[Route("api/v1/billing")]
[ApiExplorerSettings(GroupName = "billing")]
[Authorize(Roles = "TENANT_ADMIN")]
public sealed class BillingController(
    ISubscriptionService subscriptionService, IInvoiceService invoiceService, IPaymentMethodService paymentMethodService, IModuleEntitlementService moduleEntitlementService)
    : ControllerBase
{
    [HttpGet("subscription")]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubscription() =>
        (await subscriptionService.GetSummaryForTenantAsync(CurrentTenantId())).ToActionResult(HttpContext);

    [HttpGet("invoices")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<InvoiceResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListInvoices([FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await invoiceService.ListForTenantAsync(cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpGet("invoices/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<InvoiceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoice(Guid id) =>
        (await invoiceService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpGet("payment-method")]
    [ProducesResponseType(typeof(ApiResponse<PaymentMethodResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaymentMethod() =>
        (await paymentMethodService.GetDefaultAsync(CurrentTenantId())).ToActionResult(HttpContext);

    [HttpPost("payment-method/initialize")]
    [ProducesResponseType(typeof(ApiResponse<InitializeCardCaptureResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> InitializePaymentMethod() =>
        (await paymentMethodService.InitializeCardCaptureAsync(CurrentTenantId(), CurrentEmail())).ToActionResult(HttpContext);

    [HttpPost("payment-method/confirm")]
    [ProducesResponseType(typeof(ApiResponse<PaymentMethodResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmPaymentMethod([FromBody] ConfirmCardCaptureRequest request) =>
        (await paymentMethodService.ConfirmCardCaptureAsync(CurrentTenantId(), request)).ToActionResult(HttpContext);

    [HttpGet("module-entitlements")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ModuleEntitlementResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListModuleEntitlements() =>
        (await moduleEntitlementService.ListForTenantAsync(CurrentTenantId())).ToActionResult(HttpContext);

    private static Guid CurrentTenantId() => TenantContext.Current!.TenantId;

    private string CurrentEmail() => User.FindFirstValue("email")!;
}
