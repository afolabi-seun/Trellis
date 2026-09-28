using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Infrastructure.Data.Configurations;

public sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transaction", schema: "trellis_billing");
        builder.HasKey(t => t.TransactionId);
        // No xmin — insert-only, every attempt is its own immutable record.

        builder.Property(t => t.TransactionId).HasColumnName("transaction_id");
        builder.Property(t => t.TenantId).HasColumnName("tenant_id");
        builder.Property(t => t.InvoiceId).HasColumnName("invoice_id");
        builder.Property(t => t.ProviderReference).HasColumnName("provider_reference").HasMaxLength(255);
        builder.Property(t => t.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)");

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<PaymentTransactionStatus>(s))
            .HasMaxLength(20);

        builder.Property(t => t.ProviderResponseJson).HasColumnName("provider_response_json");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(t => t.TenantId);
        builder.HasIndex(t => t.InvoiceId);
    }
}
