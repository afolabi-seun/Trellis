using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Application.Interfaces;

/// <summary>Not TenantAwareRepository-based — WebhookEvent has no TenantId (see its own doc comment). Every method here runs under SystemContext.Enter(), the "system-only" RLS policy's only legitimate caller.</summary>
public interface IWebhookEventRepository
{
    Task<bool> ExistsAsync(string providerEventId);

    Task RecordAsync(WebhookEvent webhookEvent);

    Task MarkProcessedAsync(string providerEventId);

    Task SaveChangesAsync();
}
