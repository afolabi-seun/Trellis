using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Trellis.Api.Modules.Billing.Infrastructure.Data;

namespace Trellis.Api.Modules.Billing.Infrastructure.Repositories;

public sealed class PaymentTransactionRepository(BillingDbContext dbContext) : TenantAwareRepository<PaymentTransaction>, IPaymentTransactionRepository
{
    public async Task AddAsync(PaymentTransaction transaction) => await dbContext.PaymentTransactions.AddAsync(transaction);

    public async Task<IReadOnlyList<PaymentTransaction>> ListForInvoiceAsync(Guid invoiceId) =>
        await dbContext.PaymentTransactions.Where(t => t.InvoiceId == invoiceId).ToListAsync();

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
