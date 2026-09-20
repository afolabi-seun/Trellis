using Trellis.Api.Modules.Notification.Application.Dtos;

namespace Trellis.Api.Modules.Notification.Application.Interfaces;

/// <summary>
/// notifications_design.md: "provider choice deliberately not fixed here" — same interface-first
/// pattern as <c>IScreeningProvider</c>/<c>IFileScanner</c>. Phase 1 implements this with
/// <c>LoggingNotificationProvider</c> only; a real email/SMS provider is a pre-Production
/// requirement, not Phase 1 scope.
/// </summary>
public interface INotificationProvider
{
    Task<DeliveryResult> SendEmailAsync(string recipient, string subject, string htmlBody);

    Task<DeliveryResult> SendSmsAsync(string recipient, string message);
}
