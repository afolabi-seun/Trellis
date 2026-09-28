using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Leasing.Infrastructure.Data;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Repositories;

/// <summary>Not TenantAwareRepository-based — LeasingWebhookEvent isn't ITenantOwned. Only ever called by ESignatureWebhookController, itself wrapped in SystemContext.Enter().</summary>
public sealed class LeasingWebhookEventRepository(LeasingDbContext dbContext) : ILeasingWebhookEventRepository
{
    /// <summary>True only once an event has been fully PROCESSED — deliberately stricter than Billing's WebhookEventRepository (any recorded row). A delivery whose processing threw (500, the provider retries) leaves an unprocessed row behind; treating that as "already handled" would make the retry a silent no-op and lose the lease completion.</summary>
    public Task<bool> ExistsAsync(string providerEventId) =>
        dbContext.LeasingWebhookEvents.AnyAsync(w => w.ProviderEventId == providerEventId && w.ProcessedAt != null);

    /// <summary>Idempotent on an unprocessed retry — the unique index on provider_event_id would otherwise reject the second insert.</summary>
    public async Task RecordAsync(LeasingWebhookEvent webhookEvent)
    {
        if (await dbContext.LeasingWebhookEvents.AnyAsync(w => w.ProviderEventId == webhookEvent.ProviderEventId))
        {
            return;
        }

        await dbContext.LeasingWebhookEvents.AddAsync(webhookEvent);
    }

    public async Task MarkProcessedAsync(string providerEventId)
    {
        var webhookEvent = await dbContext.LeasingWebhookEvents.FirstOrDefaultAsync(w => w.ProviderEventId == providerEventId);
        webhookEvent?.MarkProcessed();
    }

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
