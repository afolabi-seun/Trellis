using FluentValidation;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Configuration;
using Trellis.Api.Core.Cors;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Files;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Secrets;
using Trellis.Api.Core.Security;

namespace Trellis.Api.Core;

/// <summary>
/// Registers Core/'s own cross-cutting services — not a module, but follows the same
/// <c>Add{Name}()</c> extension pattern every module uses (coding_standards.md), called first
/// in <c>Program.cs</c> since every module depends on RLS being wired before any module's own
/// <c>DbContext</c> runs a query. IAuditService itself moved to the Reporting module (Phase 8) —
/// see that module's own registration for why registering it dead last is still safe.
/// </summary>
public static class CoreModuleExtensions
{
    public const string CorsPolicyName = "trellis-frontend";

    public static IServiceCollection AddTrellisCore(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<AppOptions>()
            .Bind(config)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // ALLOWED_ORIGINS is a flat, comma-separated env var, not a nested Modules__ key — a
        // deployment concern (which frontend origin is allowed to call this API), not a module's.
        var allowedOrigins = (config["AllowedOrigins"] ?? "http://localhost:5173")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // AddCors still registers the CORS services and the named policy, but the policy actually used
        // is built per request by TrellisCorsPolicyProvider: the same static origins plus every
        // VERIFIED tenant custom domain (Tenant module, via ICustomDomainOriginProvider).
        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        services.AddSingleton<ICorsPolicyProvider>(new TrellisCorsPolicyProvider(allowedOrigins));
        services.AddMemoryCache();

        services.AddSingleton<TenantScopeConnectionInterceptor>();

        services.AddDbContext<CoreDbContext>((sp, opt) => opt.UseNpgsql(
            config.GetConnectionString("Trellis"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trellis_audit"))
            .AddInterceptors(sp.GetRequiredService<TenantScopeConnectionInterceptor>()));

        services.AddSingleton<ISecretProvider, EnvironmentSecretProvider>();

        services.AddOptions<EncryptionOptions>()
            .Bind(config.GetSection("Encryption"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IEncryptionService, AesGcmEncryptionService>();

        services.AddScoped<IIdempotencyStore, PostgresIdempotencyStore>();
        services.AddScoped<IdempotencyExecutor>();

        // Hoisted here from DocumentModuleExtensions once Inspection (Phase 5) became a second
        // real consumer — a generic, entity-agnostic seam (security_guide.md frames it alongside
        // IPaymentGateway), not something either module should own.
        services.AddOptions<FileStorageOptions>()
            .Bind(config.GetSection("FileStorage"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IFileStorage, LocalDiskFileStorage>();
        services.AddScoped<IFileScanner, NoOpFileScanner>();

        // Every module's AbstractValidator<TRequest> subclasses, from every module in this
        // single-project assembly (data_model.md's EF Core Conventions note applies here too) —
        // see ValidationActionFilter's doc comment for why this call needed adding at all.
        services.AddValidatorsFromAssembly(typeof(CoreModuleExtensions).Assembly);

        return services;
    }
}
