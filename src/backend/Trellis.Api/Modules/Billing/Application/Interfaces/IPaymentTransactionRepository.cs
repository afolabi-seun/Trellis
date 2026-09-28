using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Application.Interfaces;

public interface IPaymentTransactionRepository
{
    Task AddAsync(PaymentTransaction transaction);

    /// <summary>DunningRetryBackgroundService's own attempt counter — how many times has this invoice been charged, and did any succeed.</summary>
    Task<IReadOnlyList<PaymentTransaction>> ListForInvoiceAsync(Guid invoiceId);

    Task SaveChangesAsync();
}
