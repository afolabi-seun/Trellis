using System.Text.Json;
using Trellis.Api.Modules.Billing.Application.Interfaces;

namespace Trellis.Api.Modules.Billing.Application.Services;

/// <summary>billing_design.md's thin cross-module façade — the entry point RequiresModuleEntitlementFilter and PaystackWebhookController depend on.</summary>
public sealed class BillingService(IModuleEntitlementService moduleEntitlementService, IDunningService dunningService) : IBillingService
{
    public Task<bool> IsModuleEntitledAsync(Guid tenantId, string moduleKey) =>
        moduleEntitlementService.IsEntitledAsync(tenantId, moduleKey);

    /// <summary>
    /// Only charge.success is handled for real this phase — reversing a non-payment suspension is
    /// the one dunning behavior billing_design.md quotes as webhook-driven verbatim
    /// ("reactivation on a subsequent successful charge..."). Our own background jobs already own
    /// the failure-handling state machine end to end via their own synchronous NoOp gateway
    /// calls (see DunningService.AttemptChargeAsync), so a webhook-driven charge.failed path has
    /// no real Paystack payload shape to validate against yet under the NoOp-stub scope this
    /// phase confirmed — any other event type is recorded (for idempotency) but otherwise a no-op.
    /// </summary>
    public async Task ProcessWebhookEventAsync(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        var root = document.RootElement;
        var eventType = root.GetProperty("event").GetString();

        if (eventType != "charge.success")
        {
            return;
        }

        var data = root.GetProperty("data");
        var subscriptionId = Guid.Parse(data.GetProperty("subscriptionId").GetString()!);
        var reference = data.GetProperty("reference").GetString()!;

        await dunningService.ReactivateOnSuccessAsync(subscriptionId, reference);
    }
}
