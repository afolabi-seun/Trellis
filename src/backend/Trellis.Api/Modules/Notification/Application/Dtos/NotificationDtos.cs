namespace Trellis.Api.Modules.Notification.Application.Dtos;

/// <summary>notifications_design.md's `INotificationService.SendAsync` — the audit-trail link back to whatever triggered the notification.</summary>
public sealed record RelatedEntityRef(string EntityType, Guid EntityId);

/// <summary>What <see cref="Interfaces.INotificationProvider"/> hands back to the dispatcher — never thrown as an exception for an ordinary provider failure.</summary>
public sealed record DeliveryResult(bool Success, string? ProviderReference, string? ErrorMessage);
