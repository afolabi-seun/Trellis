using Trellis.Api.Modules.Notification.Domain.Entities;

namespace Trellis.Api.Modules.Notification.Application.Interfaces;

public interface INotificationTemplateRepository
{
    /// <summary>notifications_design.md's resolution order: tenant-specific row wins, else the platform default (`tenant_id IS NULL`).</summary>
    Task<NotificationTemplate?> ResolveAsync(Guid? tenantId, string templateKey, string locale = "en");

    Task AddAsync(NotificationTemplate notificationTemplate);

    Task SaveChangesAsync();
}

public interface INotificationDeliveryRepository
{
    Task AddAsync(NotificationDelivery delivery);

    Task<IReadOnlyList<NotificationDelivery>> GetQueuedAsync(int batchSize);

    /// <summary>POPIA erasure's own lookup — explicit tenantId, same reasoning as AppUserRepository's own methods (NotificationDelivery isn't ITenantOwned; TenantId is nullable for platform-level rows, which this deliberately excludes).</summary>
    Task<IReadOnlyList<NotificationDelivery>> ListByRecipientAddressAsync(Guid tenantId, string recipientAddress);

    Task SaveChangesAsync();
}
