using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Application.Services;
using Trellis.Api.Modules.Billing.Configuration;
using Trellis.Api.Modules.Billing.Infrastructure;
using Trellis.Api.Modules.Billing.Infrastructure.Data;
using Trellis.Api.Modules.Billing.Infrastructure.Providers;
using Trellis.Api.Modules.Billing.Infrastructure.Repositories;
using Trellis.Api.Modules.Billing.Infrastructure.Security;

namespace Trellis.Api.Modules.Billing;

public static class BillingModuleExtensions
{
    public static IServiceCollection AddBillingModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<BillingOptions>()
            .Bind(config.GetSection("Modules:Billing"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<BillingDbContext>((sp, opt) => opt.UseNpgsql(
            config.GetConnectionString("Trellis"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trellis_billing"))
            .AddInterceptors(sp.GetRequiredService<TenantScopeConnectionInterceptor>()));

        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();
        services.AddScoped<IModuleEntitlementRepository, ModuleEntitlementRepository>();
        services.AddScoped<IUsageSnapshotRepository, UsageSnapshotRepository>();
        services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
        services.AddScoped<IWebhookEventRepository, WebhookEventRepository>();

        services.AddScoped<IModuleEntitlementService, ModuleEntitlementService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IDunningService, DunningService>();
        services.AddScoped<IBillingService, BillingService>();

        services.AddScoped<IPaystackSignatureValidator, PaystackSignatureValidator>();

        // No real Paystack sandbox charge has been made yet (user-confirmed scope for this
        // phase) — swap this for PaystackGateway (resolving its secret key via
        // ISecretProvider.GetSecretAsync("PaystackSecretKey") per call, not at construction) once
        // real test credentials exist. Registered here, not Core — only Billing needs
        // IPaymentGateway today (IFileScanner/IFileStorage only moved to Core once a second
        // module needed them).
        services.AddScoped<IPaymentGateway, NoOpPaymentGateway>();

        services.AddHostedService<UsageSnapshotBackgroundService>();
        services.AddHostedService<DunningRetryBackgroundService>();

        return services;
    }
}
