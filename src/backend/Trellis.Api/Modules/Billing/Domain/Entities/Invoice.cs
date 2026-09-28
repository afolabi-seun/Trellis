using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Billing.Domain.Exceptions;

namespace Trellis.Api.Modules.Billing.Domain.Entities;

/// <summary>BDR-BIL-002 — Draft is a working state while line items are still being assembled; Open is charge-attempted but not yet resolved.</summary>
public enum InvoiceStatus
{
    Draft,
    Open,
    Paid,
    Failed,
    Void,
}

/// <summary>One per tenant per billing cycle (BDR-BIL-002). Its LineItems are added directly by the owning service, not through a domain method — matching InvoiceLineItem's own insert-only, invoice-owned shape.</summary>
public sealed class Invoice : ITenantOwned
{
    public Guid InvoiceId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public DateOnly BillingPeriodStart { get; private set; }
    public DateOnly BillingPeriodEnd { get; private set; }
    public string Currency { get; private set; } = null!;
    public decimal SubtotalAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public DateTimeOffset? DueAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Invoice() { }

    public static Invoice Create(Guid tenantId, Guid subscriptionId, DateOnly billingPeriodStart, DateOnly billingPeriodEnd, string currency) => new()
    {
        InvoiceId = Guid.NewGuid(),
        TenantId = tenantId,
        SubscriptionId = subscriptionId,
        BillingPeriodStart = billingPeriodStart,
        BillingPeriodEnd = billingPeriodEnd,
        Currency = currency,
        Status = InvoiceStatus.Draft,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    public void Finalize(decimal subtotal, decimal tax, decimal total, DateTimeOffset dueAt)
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidStatusTransitionException(nameof(Invoice), Status.ToString(), InvoiceStatus.Open.ToString());
        }

        Status = InvoiceStatus.Open;
        SubtotalAmount = subtotal;
        TaxAmount = tax;
        TotalAmount = total;
        DueAt = dueAt;
    }

    public void MarkPaid(DateTimeOffset paidAt)
    {
        if (Status != InvoiceStatus.Open)
        {
            throw new InvalidStatusTransitionException(nameof(Invoice), Status.ToString(), InvoiceStatus.Paid.ToString());
        }

        Status = InvoiceStatus.Paid;
        PaidAt = paidAt;
    }

    /// <summary>Only reached at dunning exhaustion (billing_design.md) — a single failed attempt leaves the invoice Open, retried by DunningRetryBackgroundService.</summary>
    public void MarkFailed()
    {
        if (Status != InvoiceStatus.Open)
        {
            throw new InvalidStatusTransitionException(nameof(Invoice), Status.ToString(), InvoiceStatus.Failed.ToString());
        }

        Status = InvoiceStatus.Failed;
    }

    public void Void()
    {
        if (Status is not (InvoiceStatus.Draft or InvoiceStatus.Open))
        {
            throw new InvalidStatusTransitionException(nameof(Invoice), Status.ToString(), InvoiceStatus.Void.ToString());
        }

        Status = InvoiceStatus.Void;
    }
}
