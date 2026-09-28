using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Application.Interfaces;

public interface IDunningService
{
    /// <summary>
    /// The one shared charge-attempt path both UsageSnapshotBackgroundService's initial attempt
    /// and DunningRetryBackgroundService's day-2/day-5 retries go through — a first attempt and a
    /// retry are structurally identical: charge, record the PaymentTransaction, decide the next
    /// Invoice/Subscription state. Resolves the tenant's default PaymentMethod internally. On the
    /// 3rd total failed attempt (dunning exhausted): Invoice.MarkFailed(), Subscription.Suspend(),
    /// ITenantService.SuspendAsync(tenantId, "non-payment"). Returns whether the charge succeeded.
    /// </summary>
    Task<bool> AttemptChargeAsync(Invoice invoice);

    /// <summary>DunningRetryBackgroundService's hourly entry point — finds every PAST_DUE subscription whose day-2/day-5 retry is due (derived from invoice.due_at and existing PaymentTransaction counts, no separate NextRetryAt column) and calls AttemptChargeAsync.</summary>
    Task RetryDueSubscriptionsAsync();

    /// <summary>Paystack's charge.success webhook path — reverses both Subscription and Tenant status automatically, no manual intervention (billing_design.md).</summary>
    Task ReactivateOnSuccessAsync(Guid subscriptionId, string providerReference);
}
