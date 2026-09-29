using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Privacy;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.Notification.Application.Services;
using Trellis.Api.Modules.Notification.Infrastructure;
using Trellis.Api.Modules.Notification.Infrastructure.Data;
using Trellis.Api.Modules.Notification.Infrastructure.Repositories;

namespace Trellis.Api.Modules.Notification;

public static class NotificationModuleExtensions
{
    public static IServiceCollection AddNotificationModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<NotificationDbContext>((sp, opt) => opt.UseNpgsql(
            config.GetConnectionString("Trellis"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trellis_notification"))
            .AddInterceptors(sp.GetRequiredService<TenantScopeConnectionInterceptor>()));

        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<INotificationDeliveryRepository, NotificationDeliveryRepository>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationProvider, LoggingNotificationProvider>();
        services.AddScoped<IPersonalDataHandler, NotificationPersonalDataHandler>();

        services.AddHostedService<NotificationDispatchBackgroundService>();

        return services;
    }
}
