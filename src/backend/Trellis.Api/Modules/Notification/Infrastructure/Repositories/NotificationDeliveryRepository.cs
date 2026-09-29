using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.Notification.Domain.Entities;
using Trellis.Api.Modules.Notification.Infrastructure.Data;

namespace Trellis.Api.Modules.Notification.Infrastructure.Repositories;

public sealed class NotificationDeliveryRepository(NotificationDbContext dbContext) : INotificationDeliveryRepository
{
    public async Task AddAsync(NotificationDelivery delivery) => await dbContext.Deliveries.AddAsync(delivery);

    public async Task<IReadOnlyList<NotificationDelivery>> GetQueuedAsync(int batchSize) =>
        await dbContext.Deliveries
            .Where(d => d.Status == NotificationDeliveryStatus.Queued)
            .OrderBy(d => d.CreatedAt)
            .Take(batchSize)
            .ToListAsync();

    public async Task<IReadOnlyList<NotificationDelivery>> ListByRecipientAddressAsync(Guid tenantId, string recipientAddress) =>
        await dbContext.Deliveries.Where(d => d.TenantId == tenantId && d.RecipientAddress == recipientAddress).ToListAsync();

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
