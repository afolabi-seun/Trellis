using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Presentation.Controllers;

/// <summary>
/// The e-signature provider's completion webhook — the provider can't authenticate as a Trellis
/// user, so this is [AllowAnonymous], verified by signature (the check IS its authentication).
/// Bare HTTP status codes, not ApiResponse&lt;T&gt;: the same documented gateway-to-gateway
/// exception PaystackWebhookController relies on. Wrapped in SystemContext.Enter() — no JWT, so
/// no TenantContext at entry; leasing_webhook_event's own "system_only" RLS requires it.
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class ESignatureWebhookController(
    ILeasingWebhookEventRepository webhookEventRepository, ILeasingWebhookService leasingWebhookService, IESignatureSignatureValidator signatureValidator)
    : ControllerBase
{
    [HttpPost("webhooks/esignature")]
    public async Task<IActionResult> HandleESignatureWebhook()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        var signature = Request.Headers["x-esignature-signature"].ToString();

        if (!await signatureValidator.IsValidAsync(body, signature))
        {
            return Unauthorized();
        }

        using var document = JsonDocument.Parse(body);
        var envelopeId = document.RootElement.GetProperty("envelopeId").GetString()!;
        var eventType = document.RootElement.GetProperty("event").GetString()!;

        using var systemScope = SystemContext.Enter();

        // A fully-processed duplicate delivery is an idempotent no-op — see
        // LeasingWebhookEventRepository.ExistsAsync for why "processed", not merely "recorded".
        if (await webhookEventRepository.ExistsAsync(envelopeId))
        {
            return Ok();
        }

        await webhookEventRepository.RecordAsync(LeasingWebhookEvent.Create(envelopeId, eventType, body));
        await webhookEventRepository.SaveChangesAsync();

        if (eventType == "envelope.completed")
        {
            await leasingWebhookService.ProcessSignatureCompletedAsync(envelopeId, body);
        }

        await webhookEventRepository.MarkProcessedAsync(envelopeId);
        await webhookEventRepository.SaveChangesAsync();

        return Ok();
    }
}
