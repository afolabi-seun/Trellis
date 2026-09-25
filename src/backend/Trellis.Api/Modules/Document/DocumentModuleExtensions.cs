using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Files;
using Trellis.Api.Modules.Document.Application.Interfaces;
using Trellis.Api.Modules.Document.Application.Services;
using Trellis.Api.Modules.Document.Configuration;
using Trellis.Api.Modules.Document.Infrastructure;
using Trellis.Api.Modules.Document.Infrastructure.Data;
using Trellis.Api.Modules.Document.Infrastructure.Repositories;

namespace Trellis.Api.Modules.Document;

public static class DocumentModuleExtensions
{
    public static IServiceCollection AddDocumentModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<DocumentOptions>()
            .Bind(config.GetSection("Modules:Document"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<FileStorageOptions>()
            .Bind(config.GetSection("FileStorage"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<DocumentDbContext>((sp, opt) => opt.UseNpgsql(
            config.GetConnectionString("Trellis"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trellis_document"))
            .AddInterceptors(sp.GetRequiredService<TenantScopeConnectionInterceptor>()));

        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentVersionRepository, DocumentVersionRepository>();
        services.AddScoped<IDocumentService, DocumentService>();

        // Core-level seams (security_guide.md frames them alongside IPaymentGateway) — Document
        // is their first caller, registered here rather than Core.AddTrellisCore() since nothing
        // else needs them yet (Inspection, Phase 5, will add its own registration when it does).
        services.AddScoped<IFileStorage, LocalDiskFileStorage>();
        services.AddScoped<IFileScanner, NoOpFileScanner>();

        services.AddHostedService<DocumentExpiryAlertBackgroundService>();

        return services;
    }
}
