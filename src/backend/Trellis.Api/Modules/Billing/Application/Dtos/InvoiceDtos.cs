namespace Trellis.Api.Modules.Billing.Application.Dtos;

public sealed record InvoiceLineItemResponse(string Description, decimal Quantity, decimal UnitAmount, decimal Amount);

public sealed record InvoiceResponse(
    Guid InvoiceId,
    Guid TenantId,
    DateOnly BillingPeriodStart,
    DateOnly BillingPeriodEnd,
    string Currency,
    decimal SubtotalAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    string Status,
    DateTimeOffset? DueAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<InvoiceLineItemResponse>? LineItems);
