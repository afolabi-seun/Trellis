using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Infrastructure.Data.Configurations;

public sealed class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("payment_method", schema: "trellis_billing");
        builder.HasKey(p => p.PaymentMethodId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(p => p.PaymentMethodId).HasColumnName("payment_method_id");
        builder.Property(p => p.TenantId).HasColumnName("tenant_id");
        builder.Property(p => p.Provider).HasColumnName("provider").HasMaxLength(20);
        builder.Property(p => p.ProviderAuthorizationCode).HasColumnName("provider_authorization_code").HasMaxLength(255);
        builder.Property(p => p.CardLast4).HasColumnName("card_last4").HasMaxLength(4);
        builder.Property(p => p.CardBrand).HasColumnName("card_brand").HasMaxLength(20);
        builder.Property(p => p.IsDefault).HasColumnName("is_default");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => new { p.TenantId, p.IsDefault });
    }
}
