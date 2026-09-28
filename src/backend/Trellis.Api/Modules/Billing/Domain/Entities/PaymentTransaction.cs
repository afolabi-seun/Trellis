using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Billing.Domain.Entities;

public enum PaymentTransactionStatus
{
    Success,
    Failed,
}

/// <summary>Every charge attempt against IPaymentGateway, success or failure (billing_design.md) — insert-only, this is DunningRetryBackgroundService's own source of truth for how many attempts an invoice has had.</summary>
public sealed class PaymentTransaction : ITenantOwned
{
    public Guid TransactionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public string ProviderReference { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public PaymentTransactionStatus Status { get; private set; }
    public string? ProviderResponseJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private PaymentTransaction() { }

    public static PaymentTransaction Create(
        Guid tenantId, Guid invoiceId, string providerReference, decimal amount, PaymentTransactionStatus status, string? providerResponseJson) => new()
        {
            TransactionId = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceId = invoiceId,
            ProviderReference = providerReference,
            Amount = amount,
            Status = status,
            ProviderResponseJson = providerResponseJson,
            CreatedAt = DateTimeOffset.UtcNow,
        };
}
