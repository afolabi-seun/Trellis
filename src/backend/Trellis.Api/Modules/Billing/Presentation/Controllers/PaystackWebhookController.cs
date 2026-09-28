using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Trellis.Api.Modules.Billing.Infrastructure.Security;

namespace Trellis.Api.Modules.Billing.Presentation.Controllers;

/// <summary>
/// billing_design.md's inbound Paystack webhook receiver — Paystack can't authenticate as a
/// Trellis user, so this is [AllowAnonymous], verified by HMAC-SHA512 signature instead (the
/// signature check IS its authentication, not an actual gap). Bare HTTP status codes, not the
/// standard ApiResponse&lt;T&gt; envelope: api_standards.md's Related Documentation footer names this
/// as the one confirmed exception, since this is a gateway-to-gateway contract with Paystack's own
/// retry semantics, never a client-facing response. Wrapped in SystemContext.Enter() — there's no
/// JWT on this request at all, so no TenantContext is ever set; webhook_event's own "system_only"
/// RLS policy requires it.
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class PaystackWebhookController(
    IWebhookEventRepository webhookEventRepository, IBillingService billingService, IPaystackSignatureValidator signatureValidator)
    : ControllerBase
{
    [HttpPost("webhooks/paystack")]
    public async Task<IActionResult> HandlePaystackWebhook()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        var signature = Request.Headers["x-paystack-signature"].ToString();

        if (!await signatureValidator.IsValidAsync(body, signature))
        {
            return Unauthorized();
        }

        using var document = JsonDocument.Parse(body);
        var eventType = document.RootElement.GetProperty("event").GetString()!;
        // Paystack's real webhook payloads don't carry a stable top-level event id — data.reference
        // (the transaction reference Trellis itself generated when initiating the charge) is the
        // one field present on every payload shape this controller's own callers construct.
        var eventId = document.RootElement.GetProperty("data").GetProperty("reference").GetString()!;

        using var systemScope = SystemContext.Enter();

        // Paystack retries webhook deliveries — a duplicate provider_event_id is an idempotent
        // no-op, never processed twice (BDR-BIL-008).
        if (await webhookEventRepository.ExistsAsync(eventId))
        {
            return Ok();
        }

        await webhookEventRepository.RecordAsync(WebhookEvent.Create(eventId, eventType, body));
        await webhookEventRepository.SaveChangesAsync();

        await billingService.ProcessWebhookEventAsync(body);
        await webhookEventRepository.MarkProcessedAsync(eventId);
        await webhookEventRepository.SaveChangesAsync();

        return Ok();
    }
}
