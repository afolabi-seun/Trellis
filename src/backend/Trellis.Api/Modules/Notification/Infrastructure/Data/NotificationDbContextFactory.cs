using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Notification.Infrastructure.Data;

public sealed class NotificationDbContextFactory : DesignTimeDbContextFactoryBase<NotificationDbContext>
{
    protected override NotificationDbContext CreateContext(DbContextOptions<NotificationDbContext> options) => new(options);
}
