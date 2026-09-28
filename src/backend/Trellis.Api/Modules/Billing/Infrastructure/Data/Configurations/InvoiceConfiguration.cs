using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Infrastructure.Data.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoice", schema: "trellis_billing");
        builder.HasKey(i => i.InvoiceId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(i => i.InvoiceId).HasColumnName("invoice_id");
        builder.Property(i => i.TenantId).HasColumnName("tenant_id");
        builder.Property(i => i.SubscriptionId).HasColumnName("subscription_id");
        builder.Property(i => i.BillingPeriodStart).HasColumnName("billing_period_start");
        builder.Property(i => i.BillingPeriodEnd).HasColumnName("billing_period_end");
        builder.Property(i => i.Currency).HasColumnName("currency").HasMaxLength(3);
        builder.Property(i => i.SubtotalAmount).HasColumnName("subtotal_amount").HasColumnType("numeric(12,2)");
        builder.Property(i => i.TaxAmount).HasColumnName("tax_amount").HasColumnType("numeric(12,2)");
        builder.Property(i => i.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(12,2)");

        builder.Property(i => i.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<InvoiceStatus>(s))
            .HasMaxLength(20);

        builder.Property(i => i.DueAt).HasColumnName("due_at");
        builder.Property(i => i.PaidAt).HasColumnName("paid_at");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(i => new { i.TenantId, i.BillingPeriodStart });
        builder.HasIndex(i => new { i.TenantId, i.Status });
    }
}
