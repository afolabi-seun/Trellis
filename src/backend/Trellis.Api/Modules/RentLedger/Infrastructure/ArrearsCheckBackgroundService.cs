using Trellis.Api.Core.Middleware;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;

namespace Trellis.Api.Modules.RentLedger.Infrastructure;

/// <summary>
/// BDR-RENT-003/004's scheduled check. A thin shell over IRentArrearsService (exact
/// DunningRetryBackgroundService precedent) so the logic itself is unit-testable with an explicit
/// as-of date. Hourly is cheap because alerting is idempotent — LastArrearsAlertedPeriod dedupes.
/// </summary>
public sealed class ArrearsCheckBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ArrearsCheckBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Rent arrears check failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task RunOnceAsync()
    {
        using var systemScope = SystemContext.Enter();
        using var scope = scopeFactory.CreateScope();

        var raised = await scope.ServiceProvider.GetRequiredService<IRentArrearsService>()
            .RaiseArrearsAlertsAsync(DateOnly.FromDateTime(DateTime.UtcNow));

        if (raised > 0)
        {
            logger.LogInformation("Raised {Count} rent arrears alert(s)", raised);
        }
    }
}
