using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Trellis.Api.Modules.Billing.Infrastructure.Data;

namespace Trellis.Api.Modules.Billing.Infrastructure.Repositories;

public sealed class InvoiceRepository(BillingDbContext dbContext) : TenantAwareRepository<Invoice>, IInvoiceRepository
{
    public async Task AddAsync(Invoice invoice) => await dbContext.Invoices.AddAsync(invoice);

    public Task<Invoice?> GetByIdAsync(Guid invoiceId) =>
        Scoped(dbContext.Invoices).FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

    public async Task<IReadOnlyList<InvoiceLineItem>> GetLineItemsAsync(Guid invoiceId) =>
        await dbContext.InvoiceLineItems.Where(l => l.InvoiceId == invoiceId).ToListAsync();

    public async Task AddLineItemAsync(InvoiceLineItem lineItem) => await dbContext.InvoiceLineItems.AddAsync(lineItem);

    public async Task<(IReadOnlyList<Invoice> Items, bool HasMore, int TotalCount)> ListForTenantAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        var baseQuery = Scoped(dbContext.Invoices);
        var totalCount = await baseQuery.CountAsync();

        var query = baseQuery;
        if (cursor is { } c)
        {
            query = query.Where(i => i.CreatedAt > c.CreatedAt || (i.CreatedAt == c.CreatedAt && i.InvoiceId > c.Id));
        }

        var page = await query.OrderBy(i => i.CreatedAt).ThenBy(i => i.InvoiceId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public async Task<(IReadOnlyList<Invoice> Items, bool HasMore, int TotalCount)> ListAllAsync(
        Guid? tenantId, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        var baseQuery = dbContext.Invoices.AsQueryable();
        if (tenantId is { } t)
        {
            baseQuery = baseQuery.Where(i => i.TenantId == t);
        }

        var totalCount = await baseQuery.CountAsync();

        var query = baseQuery;
        if (cursor is { } c)
        {
            query = query.Where(i => i.CreatedAt > c.CreatedAt || (i.CreatedAt == c.CreatedAt && i.InvoiceId > c.Id));
        }

        var page = await query.OrderBy(i => i.CreatedAt).ThenBy(i => i.InvoiceId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public Task<bool> ExistsForPeriodAsync(Guid tenantId, DateOnly billingPeriodStart) =>
        dbContext.Invoices.AnyAsync(i => i.TenantId == tenantId && i.BillingPeriodStart == billingPeriodStart);

    public Task<Invoice?> GetOpenForTenantAsync(Guid tenantId) =>
        dbContext.Invoices.Where(i => i.TenantId == tenantId && i.Status == InvoiceStatus.Open)
            .OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync();

    public Task<decimal> SumPaidTotalSinceAsync(DateOnly periodStart) =>
        dbContext.Invoices.Where(i => i.Status == InvoiceStatus.Paid && i.BillingPeriodStart >= periodStart).SumAsync(i => i.TotalAmount);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
