using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Audit;
using Trellis.Api.Core.Configuration;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Secrets;

namespace Trellis.Api.Core;

/// <summary>
/// Registers Core/'s own cross-cutting services — not a module, but follows the same
/// <c>Add{Name}()</c> extension pattern every module uses (coding_standards.md), called first
/// in <c>Program.cs</c> since every module depends on <see cref="IAuditService"/> and RLS being
/// wired before any module's own <c>DbContext</c> runs a query.
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
        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

        services.AddSingleton<TenantScopeConnectionInterceptor>();

        services.AddDbContext<CoreDbContext>((sp, opt) => opt.UseNpgsql(
            config.GetConnectionString("Trellis"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trellis_audit"))
            .AddInterceptors(sp.GetRequiredService<TenantScopeConnectionInterceptor>()));

        services.AddScoped<IAuditService, AuditService>();
        services.AddSingleton<ISecretProvider, EnvironmentSecretProvider>();

        return services;
    }
}
