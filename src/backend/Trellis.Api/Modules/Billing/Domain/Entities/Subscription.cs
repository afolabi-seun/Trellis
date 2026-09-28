using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Billing.Domain.Exceptions;

namespace Trellis.Api.Modules.Billing.Domain.Entities;

/// <summary>billing_design.md's dunning state machine drives Active/PastDue/Suspended; Trialing is the bootstrap state a new tenant's first-ever snapshot creates it in.</summary>
public enum SubscriptionStatus
{
    Trialing,
    Active,
    PastDue,
    Suspended,
    Canceled,
}

/// <summary>One row per tenant (BDR-BIL-002/003) — the cycle anchor and payment-gateway relationship UsageSnapshotBackgroundService/DunningRetryBackgroundService both key off.</summary>
public sealed class Subscription : ITenantOwned
{
    public Guid SubscriptionId { get; private set; }
    public Guid TenantId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public string Currency { get; private set; } = null!;
    public int BillingCycleAnchorDay { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Subscription() { }

    public static Subscription Create(Guid tenantId, string currency, int billingCycleAnchorDay) => new()
    {
        SubscriptionId = Guid.NewGuid(),
        TenantId = tenantId,
        Status = SubscriptionStatus.Trialing,
        Currency = currency,
        BillingCycleAnchorDay = billingCycleAnchorDay,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    /// <summary>Idempotent — UsageSnapshotBackgroundService calls this after every successful charge, including one against a subscription that's already Active.</summary>
    public void Activate() =>
        TransitionTo(SubscriptionStatus.Active, SubscriptionStatus.Trialing, SubscriptionStatus.Active, SubscriptionStatus.PastDue, SubscriptionStatus.Suspended);

    /// <summary>Also allowed from Trialing — a brand-new subscription's very first charge attempt happens before it's ever been Active, and a failure there must still enter dunning, not silently stay Trialing forever.</summary>
    public void MarkPastDue() => TransitionTo(SubscriptionStatus.PastDue, SubscriptionStatus.Trialing, SubscriptionStatus.Active);

    public void Suspend() => TransitionTo(SubscriptionStatus.Suspended, SubscriptionStatus.PastDue);

    public void Reactivate() => TransitionTo(SubscriptionStatus.Active, SubscriptionStatus.PastDue, SubscriptionStatus.Suspended);

    public void Cancel() =>
        TransitionTo(SubscriptionStatus.Canceled, SubscriptionStatus.Trialing, SubscriptionStatus.Active, SubscriptionStatus.PastDue, SubscriptionStatus.Suspended);

    private void TransitionTo(SubscriptionStatus target, params SubscriptionStatus[] allowedFrom)
    {
        if (!allowedFrom.Contains(Status))
        {
            throw new InvalidStatusTransitionException(nameof(Subscription), Status.ToString(), target.ToString());
        }

        Status = target;
    }
}
