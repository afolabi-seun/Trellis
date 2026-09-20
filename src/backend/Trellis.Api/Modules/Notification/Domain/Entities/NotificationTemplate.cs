namespace Trellis.Api.Modules.Notification.Domain.Entities;

public enum NotificationChannel
{
    Email,
    Sms,
}

/// <summary>
/// notifications_design.md's `notification_template` — a `TenantId` of `null` is the platform
/// default every tenant falls back to; a tenant-specific row (BDR-NOTIF-002) overrides it.
/// </summary>
public sealed class NotificationTemplate
{
    public Guid TemplateId { get; private set; }
    public Guid? TenantId { get; private set; }
    public string TemplateKey { get; private set; } = null!;
    public NotificationChannel Channel { get; private set; }
    public string? SubjectTemplate { get; private set; }
    public string BodyTemplate { get; private set; } = null!;
    public string Locale { get; private set; } = "en";

    private NotificationTemplate() { }

    public static NotificationTemplate Create(
        Guid? tenantId, string templateKey, NotificationChannel channel, string bodyTemplate,
        string? subjectTemplate = null, string locale = "en")
    {
        if (string.IsNullOrWhiteSpace(templateKey))
        {
            throw new ArgumentException("Template key is required.", nameof(templateKey));
        }

        if (string.IsNullOrWhiteSpace(bodyTemplate))
        {
            throw new ArgumentException("Body template is required.", nameof(bodyTemplate));
        }

        return new NotificationTemplate
        {
            TemplateId = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateKey = templateKey,
            Channel = channel,
            SubjectTemplate = subjectTemplate,
            BodyTemplate = bodyTemplate,
            Locale = locale,
        };
    }
}
