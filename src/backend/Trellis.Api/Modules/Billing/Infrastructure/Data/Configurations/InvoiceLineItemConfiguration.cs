using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Infrastructure.Data.Configurations;

public sealed class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable("invoice_line_item", schema: "trellis_billing");
        builder.HasKey(l => l.LineItemId);
        // No xmin — insert-only, owned entirely by the Invoice that creates it.

        builder.Property(l => l.LineItemId).HasColumnName("line_item_id");
        builder.Property(l => l.TenantId).HasColumnName("tenant_id");
        builder.Property(l => l.InvoiceId).HasColumnName("invoice_id");
        builder.Property(l => l.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(l => l.Quantity).HasColumnName("quantity").HasColumnType("numeric(12,2)");
        builder.Property(l => l.UnitAmount).HasColumnName("unit_amount").HasColumnType("numeric(12,2)");
        builder.Property(l => l.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)");

        builder.HasIndex(l => l.TenantId);
        builder.HasIndex(l => l.InvoiceId);
    }
}
