using Trellis.Api.Core.Middleware;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.Tenant.Application.Interfaces;

namespace Trellis.Api.Modules.Billing.Infrastructure;

/// <summary>
/// BDR-BIL-001/002 — captures each tenant's active unit count once per billing cycle and
/// immediately generates + attempts to charge that period's invoice, in the same poll iteration
/// (generating an invoice without that period's usage figure is meaningless, and doing both
/// together avoids a cross-job race with DunningRetryBackgroundService). Same structural mirror as
/// DocumentExpiryAlertBackgroundService: own DI scope per poll, SystemContext.Enter() since it
/// reads/writes every tenant's due rows in one batch. A daily interval is right for cycle-boundary
/// work (unlike Notification's/Document's minute-scale dispatch polls).
/// </summary>
public sealed class UsageSnapshotBackgroundService(
    IServiceScopeFactory scopeFactory, ILogger<UsageSnapshotBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Usage snapshot poll failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var systemScope = SystemContext.Enter();
        using var scope = scopeFactory.CreateScope();

        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
        var unitAssetService = scope.ServiceProvider.GetRequiredService<IUnitAssetService>();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        var invoiceRepository = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
        var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
        var usageSnapshotRepository = scope.ServiceProvider.GetRequiredService<IUsageSnapshotRepository>();
        var dunningService = scope.ServiceProvider.GetRequiredService<IDunningService>();
        var moduleEntitlementService = scope.ServiceProvider.GetRequiredService<IModuleEntitlementService>();

        string? cursor = null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = await tenantService.ListAsync(cursor, 100);
            if (page.Data is null)
            {
                break;
            }

            foreach (var tenant in page.Data.Items)
            {
                // Backfills any tenant provisioned after InitialBilling's own one-time migration
                // seed — without this, a new tenant gets zero entitlement rows and silently fails
                // RequiresModuleEntitlement forever, since "no row" reads as "not entitled."
                await moduleEntitlementService.EnsureDefaultEntitlementsAsync(tenant.TenantId);

                var subscription = await subscriptionService.GetOrCreateForTenantAsync(tenant.TenantId, tenant.CountryCode, tenant.CreatedAt);
                if (subscription.Status is SubscriptionStatus.Suspended or SubscriptionStatus.Canceled)
                {
                    continue;
                }

                var effectiveAnchor = Math.Min(subscription.BillingCycleAnchorDay, DateTime.DaysInMonth(today.Year, today.Month));
                if (today.Day != effectiveAnchor)
                {
                    continue;
                }

                var periodStart = new DateOnly(today.Year, today.Month, effectiveAnchor);
                if (await invoiceRepository.ExistsForPeriodAsync(tenant.TenantId, periodStart))
                {
                    continue;
                }

                var activeUnitCount = await unitAssetService.CountActiveForTenantAsync(tenant.TenantId);
                var snapshot = UsageSnapshot.Create(tenant.TenantId, periodStart, periodStart.AddMonths(1).AddDays(-1), activeUnitCount);
                await usageSnapshotRepository.AddAsync(snapshot);
                await usageSnapshotRepository.SaveChangesAsync();

                var invoice = await invoiceService.GenerateForPeriodAsync(
                    tenant.TenantId, subscription.SubscriptionId, snapshot.BillingPeriodStart, snapshot.BillingPeriodEnd, subscription.Currency, activeUnitCount);

                await dunningService.AttemptChargeAsync(invoice);
            }

            cursor = page.Data.NextCursor;
        }
        while (cursor is not null);
    }
}
