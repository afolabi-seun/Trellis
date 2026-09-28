using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Infrastructure.Data.Configurations;

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscription", schema: "trellis_billing");
        builder.HasKey(s => s.SubscriptionId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(s => s.SubscriptionId).HasColumnName("subscription_id");
        builder.Property(s => s.TenantId).HasColumnName("tenant_id");

        // PastDue is a compound name — needs the snake-case converter, not a bare
        // ToUpperInvariant() (which would produce "PASTDUE"), the same Phase 5 bug class this
        // pattern exists to avoid.
        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<SubscriptionStatus>(s))
            .HasMaxLength(20);

        builder.Property(s => s.Currency).HasColumnName("currency").HasMaxLength(3);
        builder.Property(s => s.BillingCycleAnchorDay).HasColumnName("billing_cycle_anchor_day");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(s => s.TenantId).IsUnique();
        builder.HasIndex(s => s.Status);
    }
}
