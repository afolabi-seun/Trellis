namespace Trellis.Api.Modules.Notification.Domain.Entities;

/// <summary>
/// notifications_design.md's `tenant_notification_channel_pref` — BDR-NOTIF-005 (channel choice
/// per notification type), a "Could" not in Phase 1's BDR set (roadmap.md). Table exists from
/// this module's first migration since it's part of the module's core schema, but nothing reads
/// it yet — <c>NotificationService</c> doesn't consult it until BDR-NOTIF-005 is actually built.
/// </summary>
public sealed class TenantNotificationChannelPref
{
    public Guid TenantId { get; private set; }
    public string TemplateKey { get; private set; } = null!;
    public NotificationChannel Channel { get; private set; }
    public bool Enabled { get; private set; } = true;

    private TenantNotificationChannelPref() { }

    public static TenantNotificationChannelPref Create(Guid tenantId, string templateKey, NotificationChannel channel) =>
        new() { TenantId = tenantId, TemplateKey = templateKey, Channel = channel, Enabled = true };
}
