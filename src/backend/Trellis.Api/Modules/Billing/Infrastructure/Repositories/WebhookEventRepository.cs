using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Trellis.Api.Modules.Billing.Infrastructure.Data;

namespace Trellis.Api.Modules.Billing.Infrastructure.Repositories;

/// <summary>Not TenantAwareRepository-based — WebhookEvent isn't ITenantOwned (see its own doc comment). Every method here is only ever called by PaystackWebhookController, itself wrapped in SystemContext.Enter() — the only context "system_only" RLS allows.</summary>
public sealed class WebhookEventRepository(BillingDbContext dbContext) : IWebhookEventRepository
{
    public Task<bool> ExistsAsync(string providerEventId) =>
        dbContext.WebhookEvents.AnyAsync(w => w.ProviderEventId == providerEventId);

    public async Task RecordAsync(WebhookEvent webhookEvent) => await dbContext.WebhookEvents.AddAsync(webhookEvent);

    public async Task MarkProcessedAsync(string providerEventId)
    {
        var webhookEvent = await dbContext.WebhookEvents.FirstOrDefaultAsync(w => w.ProviderEventId == providerEventId);
        webhookEvent?.MarkProcessed();
    }

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
