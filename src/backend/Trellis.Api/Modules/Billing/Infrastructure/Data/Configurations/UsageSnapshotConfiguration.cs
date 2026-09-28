using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Infrastructure.Data.Configurations;

public sealed class UsageSnapshotConfiguration : IEntityTypeConfiguration<UsageSnapshot>
{
    public void Configure(EntityTypeBuilder<UsageSnapshot> builder)
    {
        builder.ToTable("usage_snapshot", schema: "trellis_billing");
        builder.HasKey(u => u.SnapshotId);
        // No xmin — insert-only, immutable, matching override_log's own append-only shape.

        builder.Property(u => u.SnapshotId).HasColumnName("snapshot_id");
        builder.Property(u => u.TenantId).HasColumnName("tenant_id");
        builder.Property(u => u.BillingPeriodStart).HasColumnName("billing_period_start");
        builder.Property(u => u.BillingPeriodEnd).HasColumnName("billing_period_end");
        builder.Property(u => u.ActiveUnitCount).HasColumnName("active_unit_count");
        builder.Property(u => u.CapturedAt).HasColumnName("captured_at");

        builder.HasIndex(u => u.TenantId);
        builder.HasIndex(u => new { u.TenantId, u.CapturedAt });
    }
}
