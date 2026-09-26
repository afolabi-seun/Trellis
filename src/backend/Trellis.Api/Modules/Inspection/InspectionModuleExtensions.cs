using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Inspection.Application.Interfaces;
using Trellis.Api.Modules.Inspection.Application.Services;
using Trellis.Api.Modules.Inspection.Infrastructure.Data;
using Trellis.Api.Modules.Inspection.Infrastructure.Repositories;

namespace Trellis.Api.Modules.Inspection;

public static class InspectionModuleExtensions
{
    public static IServiceCollection AddInspectionModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<InspectionDbContext>((sp, opt) => opt.UseNpgsql(
            config.GetConnectionString("Trellis"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trellis_inspection"))
            .AddInterceptors(sp.GetRequiredService<TenantScopeConnectionInterceptor>()));

        services.AddScoped<IInspectionRepository, InspectionRepository>();
        services.AddScoped<IInspectionItemRepository, InspectionItemRepository>();
        services.AddScoped<IInspectionPhotoRepository, InspectionPhotoRepository>();
        services.AddScoped<IInspectionService, InspectionService>();

        // IFileStorage/IFileScanner resolve from Core (hoisted there once Inspection became a
        // second real consumer alongside Document) — no registration needed here.

        return services;
    }
}
