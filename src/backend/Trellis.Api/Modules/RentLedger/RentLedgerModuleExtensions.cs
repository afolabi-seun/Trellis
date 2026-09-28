using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Application.Services;
using Trellis.Api.Modules.RentLedger.Configuration;
using Trellis.Api.Modules.RentLedger.Infrastructure;
using Trellis.Api.Modules.RentLedger.Infrastructure.Data;
using Trellis.Api.Modules.RentLedger.Infrastructure.Repositories;

namespace Trellis.Api.Modules.RentLedger;

public static class RentLedgerModuleExtensions
{
    public static IServiceCollection AddRentLedgerModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<RentLedgerOptions>()
            .Bind(config.GetSection("Modules:RentLedger"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<RentLedgerDbContext>((sp, opt) => opt.UseNpgsql(
            config.GetConnectionString("Trellis"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trellis_rentledger"))
            .AddInterceptors(sp.GetRequiredService<TenantScopeConnectionInterceptor>()));

        services.AddScoped<IRentScheduleRepository, RentScheduleRepository>();
        services.AddScoped<IRentPaymentRecordRepository, RentPaymentRecordRepository>();

        services.AddScoped<IRentScheduleService, RentScheduleService>();
        services.AddScoped<IRentPaymentService, RentPaymentService>();
        services.AddScoped<IRentArrearsService, RentArrearsService>();

        // Deliberately no IPaymentGateway, and nothing from Billing beyond IModuleEntitlementService
        // (BDR-RENT-006): what a tenant owes a PM company and what a PM company owes Trellis are
        // different questions, and this module never processes, holds, or moves funds.
        services.AddHostedService<ArrearsCheckBackgroundService>();

        return services;
    }
}
