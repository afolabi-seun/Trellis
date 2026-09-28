using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Cors;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Trellis.Api.Modules.Tenant.Application.Services;
using Trellis.Api.Modules.Tenant.Infrastructure;
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
        services.AddScoped<ITenantSettingsService, TenantSettingsService>();

        // Custom domains (ENTERPRISE): DNS TXT proof of control, plus the CORS origin snapshot Core's
        // policy provider consults through the ICustomDomainOriginProvider interface (Core never
        // depends on this module).
        services.AddScoped<ITenantDomainRepository, TenantDomainRepository>();
        services.AddScoped<ITenantDomainService, TenantDomainService>();
        services.AddSingleton<IDomainOwnershipVerifier, DnsDomainOwnershipVerifier>();
        services.AddScoped<ICustomDomainOriginProvider, TenantDomainOriginProvider>();

        return services;
    }
}
