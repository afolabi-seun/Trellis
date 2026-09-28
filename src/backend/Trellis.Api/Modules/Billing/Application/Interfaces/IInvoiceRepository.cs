using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Application.Interfaces;

public interface IInvoiceRepository
{
    Task AddAsync(Invoice invoice);

    Task<Invoice?> GetByIdAsync(Guid invoiceId);

    Task<IReadOnlyList<InvoiceLineItem>> GetLineItemsAsync(Guid invoiceId);

    Task AddLineItemAsync(InvoiceLineItem lineItem);

    /// <summary>Own-tenant history (GET /api/v1/billing/invoices) — scoped via TenantContext.Current.</summary>
    Task<(IReadOnlyList<Invoice> Items, bool HasMore, int TotalCount)> ListForTenantAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    /// <summary>Platform-wide list (GET /platform/billing/invoices), optionally filtered to one tenant — no ambient TenantContext, SUPER_ADMIN only.</summary>
    Task<(IReadOnlyList<Invoice> Items, bool HasMore, int TotalCount)> ListAllAsync(
        Guid? tenantId, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    /// <summary>UsageSnapshotBackgroundService's idempotency guard — has this tenant already been invoiced for this period?</summary>
    Task<bool> ExistsForPeriodAsync(Guid tenantId, DateOnly billingPeriodStart);

    /// <summary>The tenant's current OPEN invoice, if any — DunningRetryBackgroundService's retry target.</summary>
    Task<Invoice?> GetOpenForTenantAsync(Guid tenantId);

    /// <summary>BDR-BIL-007's platform revenue summary — trailing-30-day recognized (PAID) revenue as this phase's MRR proxy; a precise subscription-level MRR calculation is a tracked follow-up once real billing history exists to validate it against.</summary>
    Task<decimal> SumPaidTotalSinceAsync(DateOnly periodStart);

    Task SaveChangesAsync();
}
