using Trellis.Api.Core.Resilience;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.Notification.Domain.Entities;

namespace Trellis.Api.Modules.Notification.Infrastructure;

/// <summary>
/// notifications_design.md: `notification_delivery` starts `QUEUED`; this picks up queued rows
/// and actually calls <see cref="INotificationProvider"/> — the same "no job-queue library in
/// this codebase yet" decision the Phase 1 plan made, a plain <see cref="BackgroundService"/>
/// rather than Hangfire/Quartz. Each poll runs in its own DI scope
/// (<see cref="IServiceScopeFactory"/>), since a <c>BackgroundService</c> lives for the whole
/// app's lifetime and can't hold a single scoped <c>DbContext</c> that long.
/// </summary>
public sealed class NotificationDispatchBackgroundService(
    IServiceScopeFactory scopeFactory, ILogger<NotificationDispatchBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A single failed poll cycle never takes the dispatcher down — it retries next
                // interval. Individual delivery failures are handled per-item below, not here.
                logger.LogError(ex, "Notification dispatch poll failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task DispatchOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var deliveryRepository = scope.ServiceProvider.GetRequiredService<INotificationDeliveryRepository>();
        var templateRepository = scope.ServiceProvider.GetRequiredService<INotificationTemplateRepository>();
        var provider = scope.ServiceProvider.GetRequiredService<INotificationProvider>();

        var queued = await deliveryRepository.GetQueuedAsync(batchSize: 20);

        foreach (var delivery in queued)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var template = await templateRepository.ResolveAsync(delivery.TenantId, delivery.TemplateKey);
            if (template is null)
            {
                delivery.MarkFailed();
                continue;
            }

            try
            {
                var result = await ResiliencePolicies.ExternalProviderPolicy.ExecuteAsync(() =>
                    delivery.Channel == NotificationChannel.Email
                        ? provider.SendEmailAsync(delivery.RecipientAddress, template.SubjectTemplate ?? string.Empty, template.BodyTemplate)
                        : provider.SendSmsAsync(delivery.RecipientAddress, template.BodyTemplate));

                if (result.Success)
                {
                    delivery.MarkSent(result.ProviderReference);
                }
                else
                {
                    delivery.MarkFailed();
                    logger.LogWarning(
                        "Notification delivery {DeliveryId} failed: {Error}", delivery.DeliveryId, result.ErrorMessage);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // error_handling.md: final-retry exhaustion is marked FAILED, not lost — queued
                // for a scheduled retry pass rather than surfaced to whoever triggered the send.
                delivery.MarkFailed();
                logger.LogError(ex, "Notification delivery {DeliveryId} exhausted retries", delivery.DeliveryId);
            }
        }

        if (queued.Count > 0)
        {
            await deliveryRepository.SaveChangesAsync();
        }
    }
}
