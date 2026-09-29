namespace Trellis.Api.Modules.Notification.Domain.Entities;

public enum NotificationRecipientType
{
    AppUser,
    VendorUser,
    Owner,
    ProspectiveTenant,
    External,
}

public enum NotificationDeliveryStatus
{
    Queued,
    Sent,
    Failed,
    Bounced,
}

/// <summary>
/// notifications_design.md's `notification_delivery` — one row per attempted send, audit and
/// retry both driven off this table. <see cref="TenantId"/> is nullable for a platform-level
/// notification with no tenant context (e.g. a Super Admin alert).
/// </summary>
public sealed class NotificationDelivery
{
    public Guid DeliveryId { get; private set; }
    public Guid? TenantId { get; private set; }
    public NotificationRecipientType RecipientType { get; private set; }
    public string RecipientAddress { get; private set; } = null!;
    public string TemplateKey { get; private set; } = null!;
    public NotificationChannel Channel { get; private set; }
    public NotificationDeliveryStatus Status { get; private set; } = NotificationDeliveryStatus.Queued;
    public string? ProviderReference { get; private set; }
    public string? RelatedEntityType { get; private set; }
    public Guid? RelatedEntityId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }

    private NotificationDelivery() { }

    public static NotificationDelivery Create(
        Guid? tenantId, NotificationRecipientType recipientType, string recipientAddress,
        string templateKey, NotificationChannel channel, string? relatedEntityType, Guid? relatedEntityId)
    {
        if (string.IsNullOrWhiteSpace(recipientAddress))
        {
            throw new ArgumentException("Recipient address is required.", nameof(recipientAddress));
        }

        return new NotificationDelivery
        {
            DeliveryId = Guid.NewGuid(),
            TenantId = tenantId,
            RecipientType = recipientType,
            RecipientAddress = recipientAddress,
            TemplateKey = templateKey,
            Channel = channel,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void MarkSent(string? providerReference)
    {
        Status = NotificationDeliveryStatus.Sent;
        ProviderReference = providerReference;
        SentAt = DateTimeOffset.UtcNow;
    }

    /// <summary>BDR-NOTIF-004: never silently dropped — a final failure still leaves an auditable, queryable row.</summary>
    public void MarkFailed() => Status = NotificationDeliveryStatus.Failed;

    /// <summary>POPIA erasure — the row (TemplateKey, Status, timestamps) is kept for delivery-audit purposes; only the address itself is cleared.</summary>
    public void RedactRecipientAddress() => RecipientAddress = "[erased]";
}
