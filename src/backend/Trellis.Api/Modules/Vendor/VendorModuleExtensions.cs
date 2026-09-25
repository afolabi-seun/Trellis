using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Vendor.Application.Interfaces;
using Trellis.Api.Modules.Vendor.Application.Services;
using Trellis.Api.Modules.Vendor.Infrastructure.Data;
using Trellis.Api.Modules.Vendor.Infrastructure.Repositories;

namespace Trellis.Api.Modules.Vendor;

public static class VendorModuleExtensions
{
    public static IServiceCollection AddVendorModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<VendorDbContext>((sp, opt) => opt.UseNpgsql(
            config.GetConnectionString("Trellis"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trellis_vendor"))
            .AddInterceptors(sp.GetRequiredService<TenantScopeConnectionInterceptor>()));

        services.AddScoped<IVendorRepository, VendorRepository>();
        services.AddScoped<ITenantVendorRelationshipRepository, TenantVendorRelationshipRepository>();

        // One VendorAssignmentRepository instance serves three roles: the module's own
        // IVendorAssignmentRepository, and Core's cross-module IVendorAssignmentReader — a single
        // registration keeps both interfaces resolving the same underlying DbContext instance
        // within a request/scope, same reasoning DocumentVersionRepository's own dual-interface
        // shape doesn't need here since there's only ever one concrete type.
        services.AddScoped<VendorAssignmentRepository>();
        services.AddScoped<IVendorAssignmentRepository>(sp => sp.GetRequiredService<VendorAssignmentRepository>());
        services.AddScoped<IVendorAssignmentReader>(sp => sp.GetRequiredService<VendorAssignmentRepository>());

        services.AddScoped<IVendorService, VendorService>();
        services.AddScoped<IVendorAssignmentService, VendorAssignmentService>();
        services.AddScoped<ITenantVendorRelationshipService, TenantVendorRelationshipService>();

        return services;
    }
}
