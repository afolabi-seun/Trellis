namespace Trellis.Api.Modules.Billing.Domain.Entities;

/// <summary>
/// BDR-BIL-008's idempotency ledger. Deliberately NOT ITenantOwned — a Paystack webhook payload
/// carries no Trellis tenant identity at receipt time (the request has no JWT at all,
/// [AllowAnonymous]; tenant resolution happens inside ProcessWebhookEventAsync, from the
/// payload's own customer/transaction reference). This table has no tenant-scoped read path —
/// only the webhook controller and the background job ever touch it, both under
/// SystemContext.Enter() — so its RLS policy is "system-only," not the plain tenant_isolation
/// every other table in this schema uses (see the migration's own comment).
/// </summary>
public sealed class WebhookEvent
{
    public Guid WebhookEventId { get; private set; }
    public string ProviderEventId { get; private set; } = null!;
    public string EventType { get; private set; } = null!;
    public string PayloadJson { get; private set; } = null!;
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    private WebhookEvent() { }

    public static WebhookEvent Create(string providerEventId, string eventType, string payloadJson) => new()
    {
        WebhookEventId = Guid.NewGuid(),
        ProviderEventId = providerEventId,
        EventType = eventType,
        PayloadJson = payloadJson,
        ReceivedAt = DateTimeOffset.UtcNow,
    };

    public void MarkProcessed() => ProcessedAt = DateTimeOffset.UtcNow;
}
