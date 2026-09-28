using Microsoft.Extensions.Options;
using Trellis.Api.Core.Audit;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Dtos;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Configuration;
using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Application.Services;

public sealed class InvoiceService(
    IInvoiceRepository repository,
    IModuleEntitlementRepository moduleEntitlementRepository,
    IAuditService auditService,
    IOptions<BillingOptions> options) : IInvoiceService
{
    public async Task<ServiceResult<PagedResponse<InvoiceResponse>>> ListForTenantAsync(string? cursor, int limit)
    {
        var decoded = CursorCodec.Decode(cursor);
        var (items, hasMore, totalCount) = await repository.ListForTenantAsync(decoded, limit);

        var nextCursor = hasMore && items.Count > 0
            ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].InvoiceId)
            : null;

        return ServiceResult<PagedResponse<InvoiceResponse>>.Success(
            new PagedResponse<InvoiceResponse>(items.Select(i => ToResponse(i, null)).ToList(), nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<InvoiceResponse>> GetByIdAsync(Guid invoiceId)
    {
        var invoice = await repository.GetByIdAsync(invoiceId);
        if (invoice is null)
        {
            return ServiceResult<InvoiceResponse>.NotFound();
        }

        var lineItems = await repository.GetLineItemsAsync(invoiceId);
        return ServiceResult<InvoiceResponse>.Success(ToResponse(invoice, lineItems));
    }

    public async Task<ServiceResult<PagedResponse<InvoiceResponse>>> ListAllAsync(Guid? tenantId, string? cursor, int limit)
    {
        var decoded = CursorCodec.Decode(cursor);
        var (items, hasMore, totalCount) = await repository.ListAllAsync(tenantId, decoded, limit);

        var nextCursor = hasMore && items.Count > 0
            ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].InvoiceId)
            : null;

        return ServiceResult<PagedResponse<InvoiceResponse>>.Success(
            new PagedResponse<InvoiceResponse>(items.Select(i => ToResponse(i, null)).ToList(), nextCursor, hasMore, totalCount));
    }

    public async Task<Invoice> GenerateForPeriodAsync(
        Guid tenantId, Guid subscriptionId, DateOnly periodStart, DateOnly periodEnd, string currency, int activeUnitCount)
    {
        var invoice = Invoice.Create(tenantId, subscriptionId, periodStart, periodEnd, currency);
        await repository.AddAsync(invoice);

        var rate = options.Value.PerUnitRate;
        var usageLine = InvoiceLineItem.Create(
            tenantId, invoice.InvoiceId, $"{activeUnitCount} active units × {rate:0.00}", activeUnitCount, rate);
        await repository.AddLineItemAsync(usageLine);
        var subtotal = usageLine.Amount;

        var entitlements = await moduleEntitlementRepository.ListForTenantAsync(tenantId);
        foreach (var entitlement in entitlements.Where(e => e.Status == EntitlementStatus.Active && e.MonthlyPrice > 0))
        {
            var line = InvoiceLineItem.Create(
                tenantId, invoice.InvoiceId, $"{entitlement.ModuleKey} module — {entitlement.MonthlyPrice:0.00}/mo", 1, entitlement.MonthlyPrice);
            await repository.AddLineItemAsync(line);
            subtotal += line.Amount;
        }

        invoice.Finalize(subtotal, 0, subtotal, DateTimeOffset.UtcNow);
        await repository.SaveChangesAsync();

        await auditService.RecordAsync(tenantId, tenantId, AuditActorType.System, AuditTypes.Billing.InvoiceGenerated);

        return invoice;
    }

    private static InvoiceResponse ToResponse(Invoice invoice, IReadOnlyList<InvoiceLineItem>? lineItems) => new(
        invoice.InvoiceId,
        invoice.TenantId,
        invoice.BillingPeriodStart,
        invoice.BillingPeriodEnd,
        invoice.Currency,
        invoice.SubtotalAmount,
        invoice.TaxAmount,
        invoice.TotalAmount,
        invoice.Status.ToUpperSnakeCase(),
        invoice.DueAt,
        invoice.PaidAt,
        invoice.CreatedAt,
        lineItems?.Select(l => new InvoiceLineItemResponse(l.Description, l.Quantity, l.UnitAmount, l.Amount)).ToList());
}
