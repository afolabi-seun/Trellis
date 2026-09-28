using System.Text.RegularExpressions;
using Trellis.Api.Modules.Notification.Application.Dtos;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.Notification.Domain.Entities;

namespace Trellis.Api.Modules.Notification.Application.Services;

public sealed partial class NotificationService(
    INotificationTemplateRepository templateRepository, INotificationDeliveryRepository deliveryRepository)
    : INotificationService
{
    public async Task SendAsync(
        Guid? tenantId, string templateKey, string recipientAddress, object templateData,
        RelatedEntityRef? relatedEntity = null, NotificationRecipientType? recipientType = null)
    {
        var template = await templateRepository.ResolveAsync(tenantId, templateKey);
        if (template is null)
        {
            // BDR-NOTIF-006: a missing template is a configuration problem to log, never an
            // exception that could fail the caller's own operation.
            return;
        }

        var resolvedRecipientType = recipientType
            ?? (tenantId is null ? NotificationRecipientType.External : NotificationRecipientType.AppUser);

        var delivery = NotificationDelivery.Create(
            tenantId, resolvedRecipientType, recipientAddress, templateKey, template.Channel,
            relatedEntity?.EntityType, relatedEntity?.EntityId);

        await deliveryRepository.AddAsync(delivery);
        await deliveryRepository.SaveChangesAsync();
        // Dispatch itself happens on NotificationDispatchBackgroundService's own schedule —
        // SendAsync never calls the provider inline, so the caller's transaction is never
        // blocked on (or rolled back by) a delivery failure.
    }

    /// <summary>notifications_design.md's `{{variableName}}` placeholder syntax, rendered against <paramref name="data"/>'s public properties.</summary>
    public static string Render(string template, object data)
    {
        var properties = data.GetType().GetProperties();
        return PlaceholderPattern().Replace(template, match =>
        {
            var name = match.Groups[1].Value;
            var property = properties.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return property?.GetValue(data)?.ToString() ?? match.Value;
        });
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex PlaceholderPattern();
}
