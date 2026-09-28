using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Infrastructure.Data.Configurations;

public sealed class ModuleEntitlementConfiguration : IEntityTypeConfiguration<ModuleEntitlement>
{
    public void Configure(EntityTypeBuilder<ModuleEntitlement> builder)
    {
        builder.ToTable("module_entitlement", schema: "trellis_billing");
        builder.HasKey(e => e.EntitlementId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(e => e.EntitlementId).HasColumnName("entitlement_id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.ModuleKey).HasColumnName("module_key").HasMaxLength(30);

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<EntitlementStatus>(s))
            .HasMaxLength(20);

        builder.Property(e => e.MonthlyPrice).HasColumnName("monthly_price").HasColumnType("numeric(10,2)");
        builder.Property(e => e.EnabledAt).HasColumnName("enabled_at");
        builder.Property(e => e.DisabledAt).HasColumnName("disabled_at");

        builder.HasIndex(e => new { e.TenantId, e.ModuleKey }).IsUnique();
    }
}
