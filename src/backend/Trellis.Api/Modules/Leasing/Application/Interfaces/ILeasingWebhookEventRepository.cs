using Trellis.Api.Modules.Leasing.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Application.Interfaces;

/// <summary>Not TenantAwareRepository-based — LeasingWebhookEvent isn't ITenantOwned. Every method here is only ever called by ESignatureWebhookController, itself wrapped in SystemContext.Enter() — the only context "system_only" RLS allows.</summary>
public interface ILeasingWebhookEventRepository
{
    Task<bool> ExistsAsync(string providerEventId);

    Task RecordAsync(LeasingWebhookEvent webhookEvent);

    Task MarkProcessedAsync(string providerEventId);

    Task SaveChangesAsync();
}
