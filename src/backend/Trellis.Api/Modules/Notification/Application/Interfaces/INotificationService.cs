using Trellis.Api.Modules.Notification.Application.Dtos;

namespace Trellis.Api.Modules.Notification.Application.Interfaces;

/// <summary>
/// The one door into this module every other module calls through — never `trellis_notification`
/// directly (notifications_design.md, framed identically to `IPaymentGateway`). Queues a
/// <c>notification_delivery</c> row and returns immediately; the actual send happens on
/// <c>NotificationDispatchBackgroundService</c>'s own schedule
/// (BDR-NOTIF-006: never block the triggering operation on delivery succeeding).
/// </summary>
public interface INotificationService
{
    Task SendAsync(
        Guid? tenantId, string templateKey, string recipientAddress, object templateData,
        RelatedEntityRef? relatedEntity = null);
}
