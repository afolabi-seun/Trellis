namespace Trellis.Api.Modules.Leasing.Domain.Entities;

/// <summary>
/// The e-signature completion webhook's idempotency ledger. Deliberately NOT ITenantOwned — the
/// provider's payload carries no Trellis tenant identity at receipt time (no JWT at all,
/// [AllowAnonymous]; tenant resolution happens inside LeasingWebhookService, from the resolved
/// Application). Exact WebhookEvent (Billing) shape — "system_only" RLS, not tenant_isolation.
/// </summary>
public sealed class LeasingWebhookEvent
{
    public Guid LeasingWebhookEventId { get; private set; }
    public string ProviderEventId { get; private set; } = null!;
    public string EventType { get; private set; } = null!;
    public string PayloadJson { get; private set; } = null!;
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    private LeasingWebhookEvent() { }

    public static LeasingWebhookEvent Create(string providerEventId, string eventType, string payloadJson) => new()
    {
        LeasingWebhookEventId = Guid.NewGuid(),
        ProviderEventId = providerEventId,
        EventType = eventType,
        PayloadJson = payloadJson,
        ReceivedAt = DateTimeOffset.UtcNow,
    };

    public void MarkProcessed() => ProcessedAt = DateTimeOffset.UtcNow;

    /// <summary>POPIA erasure — the provider's raw payload can embed applicant PII; the event row itself (ProviderEventId, EventType, timestamps) is kept for the idempotency ledger's own sake, only its body is cleared.</summary>
    public void MarkPayloadRedacted() => PayloadJson = "{\"redacted\":true}";
}
