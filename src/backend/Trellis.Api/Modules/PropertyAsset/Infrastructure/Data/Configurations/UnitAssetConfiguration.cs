using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.PropertyAsset.Domain.Entities;

namespace Trellis.Api.Modules.PropertyAsset.Infrastructure.Data.Configurations;

public sealed class UnitAssetConfiguration : IEntityTypeConfiguration<UnitAsset>
{
    public void Configure(EntityTypeBuilder<UnitAsset> builder)
    {
        builder.ToTable("unit_asset", schema: "trellis_property");
        builder.HasKey(u => u.UnitAssetId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(u => u.UnitAssetId).HasColumnName("unit_asset_id");
        // Denormalised (data_model.md) — always matches the parent property's tenant_id, purely
        // so RLS can filter this table directly without a join back to property.
        builder.Property(u => u.TenantId).HasColumnName("tenant_id");
        builder.Property(u => u.PropertyId).HasColumnName("property_id");
        builder.Property(u => u.Label).HasColumnName("label").HasMaxLength(100).IsRequired();

        builder.Property(u => u.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToString().ToUpperInvariant(), s => (UnitAssetStatus)Enum.Parse(typeof(UnitAssetStatus), s, true))
            .HasMaxLength(20);

        builder.Property(u => u.SizeSqft).HasColumnName("size_sqft").HasColumnType("numeric");
        builder.Property(u => u.AcquisitionValue).HasColumnName("acquisition_value").HasColumnType("numeric(14,2)");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(u => u.TenantId);
        builder.HasIndex(u => u.PropertyId);
    }
}
