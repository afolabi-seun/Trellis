using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.Notification.Domain.Entities;
using Trellis.Api.Modules.Notification.Infrastructure.Data;

namespace Trellis.Api.Modules.Notification.Infrastructure.Repositories;

public sealed class NotificationTemplateRepository(NotificationDbContext dbContext) : INotificationTemplateRepository
{
    public async Task<NotificationTemplate?> ResolveAsync(Guid? tenantId, string templateKey, string locale = "en")
    {
        // Tenant-specific row (TenantId != null) is ordered first, so it wins over the platform
        // default (TenantId == null) when both exist — notifications_design.md's resolution order.
        var candidates = await dbContext.Templates
            .Where(t => (t.TenantId == tenantId || t.TenantId == null) && t.TemplateKey == templateKey && t.Locale == locale)
            .ToListAsync();

        return candidates.OrderByDescending(t => t.TenantId != null).FirstOrDefault();
    }

    public async Task AddAsync(NotificationTemplate notificationTemplate) => await dbContext.Templates.AddAsync(notificationTemplate);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
