using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Dtos;
using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Application.Interfaces;

public interface IInvoiceService
{
    Task<ServiceResult<PagedResponse<InvoiceResponse>>> ListForTenantAsync(string? cursor, int limit);

    Task<ServiceResult<InvoiceResponse>> GetByIdAsync(Guid invoiceId);

    Task<ServiceResult<PagedResponse<InvoiceResponse>>> ListAllAsync(Guid? tenantId, string? cursor, int limit);

    /// <summary>
    /// UsageSnapshotBackgroundService's own generation step — one usage-based line item plus one
    /// per ACTIVE ModuleEntitlement with a non-zero price, Finalize()d to Open. Returns the domain
    /// entity, not a Response — the caller immediately attempts a charge against it in the same
    /// poll iteration.
    /// </summary>
    Task<Invoice> GenerateForPeriodAsync(Guid tenantId, Guid subscriptionId, DateOnly periodStart, DateOnly periodEnd, string currency, int activeUnitCount);
}
