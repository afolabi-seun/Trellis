using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Billing.Domain.Entities;

/// <summary>One itemised charge on an Invoice (BDR-BIL-002/009) — one usage line plus one per ACTIVE ModuleEntitlement with a non-zero price. Insert-only, owned entirely by the Invoice that creates it.</summary>
public sealed class InvoiceLineItem : ITenantOwned
{
    public Guid LineItemId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public string Description { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal UnitAmount { get; private set; }
    public decimal Amount { get; private set; }

    private InvoiceLineItem() { }

    public static InvoiceLineItem Create(Guid tenantId, Guid invoiceId, string description, decimal quantity, decimal unitAmount) => new()
    {
        LineItemId = Guid.NewGuid(),
        TenantId = tenantId,
        InvoiceId = invoiceId,
        Description = description,
        Quantity = quantity,
        UnitAmount = unitAmount,
        Amount = quantity * unitAmount,
    };
}
