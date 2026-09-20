using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Audit;
using Trellis.Api.Core.Data;

namespace Trellis.Api.Core;

/// <summary>
/// Registers Core/'s own cross-cutting services — not a module, but follows the same
/// <c>Add{Name}()</c> extension pattern every module uses (coding_standards.md), called first
/// in <c>Program.cs</c> since every module depends on <see cref="IAuditService"/> and RLS being
/// wired before any module's own <c>DbContext</c> runs a query.
/// </summary>
public static class CoreModuleExtensions
{
    public static IServiceCollection AddTrellisCore(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<TenantScopeConnectionInterceptor>();

        services.AddDbContext<CoreDbContext>((sp, opt) => opt.UseNpgsql(
            config.GetConnectionString("Trellis"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trellis_audit"))
            .AddInterceptors(sp.GetRequiredService<TenantScopeConnectionInterceptor>()));

        services.AddScoped<IAuditService, AuditService>();

        return services;
    }
}
