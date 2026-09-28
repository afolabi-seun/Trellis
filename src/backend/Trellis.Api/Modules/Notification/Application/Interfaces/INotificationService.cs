using Trellis.Api.Modules.Notification.Application.Dtos;
using Trellis.Api.Modules.Notification.Domain.Entities;

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
    /// <summary>
    /// <paramref name="recipientType"/> defaults to the original derivation (External when there's
    /// no tenant, AppUser otherwise). Leasing (Phase 9) passes ProspectiveTenant explicitly —
    /// that enum value existed in the schema since Phase 1 but no code path ever produced it, so
    /// every applicant-facing email would otherwise have been silently mis-tagged AppUser.
    /// </summary>
    Task SendAsync(
        Guid? tenantId, string templateKey, string recipientAddress, object templateData,
        RelatedEntityRef? relatedEntity = null, NotificationRecipientType? recipientType = null);
}
