namespace Trellis.Api.Modules.Billing.Application.Interfaces;

/// <summary>
/// billing_design.md's thin cross-module façade — the one Billing interface
/// RequiresModuleEntitlementFilter and PaystackWebhookController depend on, rather than each
/// reaching into 5 narrower services directly.
/// </summary>
public interface IBillingService
{
    Task<bool> IsModuleEntitledAsync(Guid tenantId, string moduleKey);

    /// <summary>Routes a verified, deduplicated Paystack webhook payload — charge.success to IInvoiceService/IDunningService.ReactivateOnSuccessAsync, charge.failed to IDunningService's own failure path via AttemptChargeAsync's recorded outcome.</summary>
    Task ProcessWebhookEventAsync(string payloadJson);
}
