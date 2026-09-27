using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Ai.Application.Interfaces;
using Trellis.Api.Modules.Ai.Application.Services;
using Trellis.Api.Modules.Ai.Infrastructure.Data;
using Trellis.Api.Modules.Ai.Infrastructure.Providers;
using Trellis.Api.Modules.Ai.Infrastructure.Repositories;

namespace Trellis.Api.Modules.Ai;

public static class AiModuleExtensions
{
    public static IServiceCollection AddAiModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AiDbContext>((sp, opt) => opt.UseNpgsql(
            config.GetConnectionString("Trellis"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trellis_ai"))
            .AddInterceptors(sp.GetRequiredService<TenantScopeConnectionInterceptor>()));

        services.AddScoped<IExtractionJobRepository, ExtractionJobRepository>();
        services.AddScoped<IRoutingSuggestionRepository, RoutingSuggestionRepository>();
        services.AddScoped<IOverrideLogRepository, OverrideLogRepository>();

        services.AddScoped<IAiTierGate, AiTierGate>();
        services.AddScoped<IExtractionService, ExtractionService>();
        services.AddScoped<IRoutingSuggestionService, RoutingSuggestionService>();
        services.AddScoped<IOverrideLogService, OverrideLogService>();

        // No real AI vendor is chosen yet — swap this for a real implementation (resolving its API
        // key via ISecretProvider.GetSecretAsync("AI_PROVIDER_API_KEY")) when one is. Registered
        // here, not Core — only Ai itself needs IAiProvider today (IFileScanner/IFileStorage only
        // moved to Core once a second module needed them).
        services.AddScoped<IAiProvider, NoOpAiProvider>();

        return services;
    }
}
