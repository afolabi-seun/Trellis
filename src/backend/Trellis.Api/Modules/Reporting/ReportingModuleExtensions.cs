using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Privacy;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Services;
using Trellis.Api.Modules.Reporting.Configuration;
using Trellis.Api.Modules.Reporting.Infrastructure.Data;
using Trellis.Api.Modules.Reporting.Infrastructure.Pdf;
using Trellis.Api.Modules.Reporting.Infrastructure.Repositories;

namespace Trellis.Api.Modules.Reporting;

public static class ReportingModuleExtensions
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<ReportingOptions>()
            .Bind(config.GetSection("Modules:Reporting"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<ReportingDbContext>((sp, opt) => opt.UseNpgsql(
            config.GetConnectionString("Trellis"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trellis_reporting"))
            .AddInterceptors(sp.GetRequiredService<TenantScopeConnectionInterceptor>()));

        services.AddScoped<IOwnerReportRepository, OwnerReportRepository>();
        services.AddScoped<IReportShareLinkRepository, ReportShareLinkRepository>();

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IOwnerReportService, OwnerReportService>();
        services.AddScoped<IReportShareLinkService, ReportShareLinkService>();
        services.AddScoped<IPersonalDataHandler, ReportingPersonalDataHandler>();
        // QuestPdfOwnerReportRenderer's own static constructor sets QuestPDF.Settings.License —
        // guaranteed to run before the first render regardless of DI, so no explicit call is
        // needed here. QuestPDF's Community license is free under $1M USD annual revenue — a
        // real threshold, not a placeholder; see that class's own doc comment.
        services.AddScoped<IReportExportService, QuestPdfOwnerReportRenderer>();

        return services;
    }
}
