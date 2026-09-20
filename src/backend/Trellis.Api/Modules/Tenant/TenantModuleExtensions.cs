using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Trellis.Api.Modules.Tenant.Application.Services;
using Trellis.Api.Modules.Tenant.Infrastructure.Data;
using Trellis.Api.Modules.Tenant.Infrastructure.Repositories;

namespace Trellis.Api.Modules.Tenant;

public static class TenantModuleExtensions
{
    public static IServiceCollection AddTenantModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<TenantDbContext>((sp, opt) => opt.UseNpgsql(
            config.GetConnectionString("Trellis"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trellis_tenant"))
            .AddInterceptors(sp.GetRequiredService<TenantScopeConnectionInterceptor>()));

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantService, TenantService>();

        return services;
    }
}
