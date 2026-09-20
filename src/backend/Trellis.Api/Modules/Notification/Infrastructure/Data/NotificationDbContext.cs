using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Notification.Domain.Entities;

namespace Trellis.Api.Modules.Notification.Infrastructure.Data;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<NotificationTemplate> Templates => Set<NotificationTemplate>();
    public DbSet<NotificationDelivery> Deliveries => Set<NotificationDelivery>();
    public DbSet<TenantNotificationChannelPref> ChannelPrefs => Set<TenantNotificationChannelPref>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(NotificationDbContext).Assembly,
            t => t.Namespace?.StartsWith("Trellis.Api.Modules.Notification.", StringComparison.Ordinal) == true);
}
