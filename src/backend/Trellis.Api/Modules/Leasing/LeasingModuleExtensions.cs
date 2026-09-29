using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Privacy;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Services;
using Trellis.Api.Modules.Leasing.Configuration;
using Trellis.Api.Modules.Leasing.Infrastructure.Data;
using Trellis.Api.Modules.Leasing.Infrastructure.Providers;
using Trellis.Api.Modules.Leasing.Infrastructure.Repositories;

namespace Trellis.Api.Modules.Leasing;

public static class LeasingModuleExtensions
{
    public static IServiceCollection AddLeasingModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<LeasingOptions>()
            .Bind(config.GetSection("Modules:Leasing"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<LeasingDbContext>((sp, opt) => opt.UseNpgsql(
            config.GetConnectionString("Trellis"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trellis_leasing"))
            .AddInterceptors(sp.GetRequiredService<TenantScopeConnectionInterceptor>()));

        services.AddScoped<IVacancyListingRepository, VacancyListingRepository>();
        services.AddScoped<IVacancyListingPhotoRepository, VacancyListingPhotoRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IScreeningResultRepository, ScreeningResultRepository>();
        services.AddScoped<IApplicationEventRepository, ApplicationEventRepository>();
        services.AddScoped<IApplicationLinkTokenRepository, ApplicationLinkTokenRepository>();
        services.AddScoped<ILeasingWebhookEventRepository, LeasingWebhookEventRepository>();

        services.AddScoped<IVacancyListingService, VacancyListingService>();
        services.AddScoped<IApplicationLinkService, ApplicationLinkService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<ILeasingWebhookService, LeasingWebhookService>();
        services.AddScoped<IScreeningReportService, ScreeningReportService>();
        services.AddScoped<IPersonalDataHandler, LeasingPersonalDataHandler>();

        // No real screening bureau or e-signature vendor is fixed for this phase (leasing_design.md
        // deliberately doesn't commit to one — a business/legal decision). Swap each of these two
        // provider registrations for a real implementation (resolving credentials via
        // ISecretProvider per call, not at construction) once real credentials and provider
        // contracts exist — same convention as BillingModuleExtensions's NoOpPaymentGateway
        // registration. The signature validator is the one piece built for real regardless: pure
        // cryptography needs no vendor account to be correct, exact PaystackSignatureValidator
        // precedent.
        services.AddScoped<IScreeningProvider, NoOpScreeningProvider>();
        services.AddScoped<IESignatureProvider, NoOpESignatureProvider>();
        services.AddScoped<IESignatureSignatureValidator, HmacESignatureSignatureValidator>();

        return services;
    }
}
