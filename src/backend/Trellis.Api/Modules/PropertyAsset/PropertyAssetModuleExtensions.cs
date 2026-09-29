using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Privacy;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.PropertyAsset.Application.Services;
using Trellis.Api.Modules.PropertyAsset.Infrastructure.Data;
using Trellis.Api.Modules.PropertyAsset.Infrastructure.Repositories;

namespace Trellis.Api.Modules.PropertyAsset;

public static class PropertyAssetModuleExtensions
{
    public static IServiceCollection AddPropertyAssetModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<PropertyDbContext>((sp, opt) => opt.UseNpgsql(
            config.GetConnectionString("Trellis"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trellis_property"))
            .AddInterceptors(sp.GetRequiredService<TenantScopeConnectionInterceptor>()));

        services.AddScoped<IOwnerRepository, OwnerRepository>();
        services.AddScoped<IPropertyRepository, PropertyRepository>();
        services.AddScoped<IUnitAssetRepository, UnitAssetRepository>();

        services.AddScoped<IOwnerService, OwnerService>();
        services.AddScoped<IPropertyService, PropertyService>();
        services.AddScoped<IUnitAssetService, UnitAssetService>();
        services.AddScoped<IPersonalDataHandler, PropertyAssetPersonalDataHandler>();

        return services;
    }
}
