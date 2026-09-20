using Trellis.Api.Modules.Notification.Application.Dtos;
using Trellis.Api.Modules.Notification.Application.Interfaces;

namespace Trellis.Api.Modules.Notification.Infrastructure;

/// <summary>
/// notifications_design.md: no real email/SMS provider is fixed for Phase 1. Writes the
/// would-be send to the log instead of calling a real API, so password reset and MFA flows are
/// fully testable without real provider credentials. A real provider (SendGrid, Twilio, or
/// whatever the market decision ends up being) is a pre-Production requirement, tracked, not
/// Phase 1 scope — swapping it in later is a new <see cref="INotificationProvider"/>
/// implementation and one DI registration change, nothing else.
/// </summary>
public sealed class LoggingNotificationProvider(ILogger<LoggingNotificationProvider> logger) : INotificationProvider
{
    public Task<DeliveryResult> SendEmailAsync(string recipient, string subject, string htmlBody)
    {
        logger.LogInformation(
            "LoggingNotificationProvider: would send EMAIL to {Recipient}, subject {Subject}", recipient, subject);
        return Task.FromResult(new DeliveryResult(true, $"logged-{Guid.NewGuid()}", null));
    }

    public Task<DeliveryResult> SendSmsAsync(string recipient, string message)
    {
        logger.LogInformation("LoggingNotificationProvider: would send SMS to {Recipient}", recipient);
        return Task.FromResult(new DeliveryResult(true, $"logged-{Guid.NewGuid()}", null));
    }
}
