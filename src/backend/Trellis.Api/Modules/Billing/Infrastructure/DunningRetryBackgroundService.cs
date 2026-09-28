using Trellis.Api.Core.Middleware;
using Trellis.Api.Modules.Billing.Application.Interfaces;

namespace Trellis.Api.Modules.Billing.Infrastructure;

/// <summary>BDR-BIL-004 — the day-2/day-5 dunning retry schedule's own poll loop. Same structural mirror as UsageSnapshotBackgroundService; an hourly interval is fine for a schedule measured in days.</summary>
public sealed class DunningRetryBackgroundService(
    IServiceScopeFactory scopeFactory, ILogger<DunningRetryBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var systemScope = SystemContext.Enter();
                using var scope = scopeFactory.CreateScope();
                var dunningService = scope.ServiceProvider.GetRequiredService<IDunningService>();
                await dunningService.RetryDueSubscriptionsAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Dunning retry poll failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
