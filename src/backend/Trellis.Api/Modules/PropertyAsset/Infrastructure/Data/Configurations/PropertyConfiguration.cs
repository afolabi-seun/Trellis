using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.PropertyAsset.Domain.Entities;

namespace Trellis.Api.Modules.PropertyAsset.Infrastructure.Data.Configurations;

public sealed class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("property", schema: "trellis_property");
        builder.HasKey(p => p.PropertyId);
        // coding_standards.md's Optimistic Concurrency example, verbatim: xmin as a shadow
        // row-version property, not the obsolete UseXminAsConcurrencyToken().
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(p => p.PropertyId).HasColumnName("property_id");
        builder.Property(p => p.TenantId).HasColumnName("tenant_id");
        builder.Property(p => p.OwnerId).HasColumnName("owner_id");
        builder.Property(p => p.Address).HasColumnName("address").IsRequired();

        // data_model.md stores property_type as RESIDENTIAL|COMMERCIAL|MIXED_USE — the
        // underscore in MIXED_USE means a bare PascalCase-to-upper conversion doesn't round-trip
        // (same reasoning as PropertyService's ParsePropertyType/PropertyTypeToString, which
        // this mirrors so the DB and the API surface agree on the exact same string).
        builder.Property(p => p.PropertyType)
            .HasColumnName("property_type")
            .HasConversion(
                t => t == PropertyType.MixedUse ? "MIXED_USE" : t.ToString().ToUpperInvariant(),
                s => s == "MIXED_USE" ? PropertyType.MixedUse : (PropertyType)Enum.Parse(typeof(PropertyType), s, true))
            .HasMaxLength(30);

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToString().ToUpperInvariant(), s => (PropertyStatus)Enum.Parse(typeof(PropertyStatus), s, true))
            .HasMaxLength(20);

        // BDR-AST-002 gap-fill columns (this phase) — all nullable, optional at entry.
        builder.Property(p => p.SizeSqft).HasColumnName("size_sqft").HasColumnType("numeric");
        builder.Property(p => p.AcquisitionDate).HasColumnName("acquisition_date");
        builder.Property(p => p.AcquisitionValue).HasColumnName("acquisition_value").HasColumnType("numeric(14,2)");
        builder.Property(p => p.CurrentValuation).HasColumnName("current_valuation").HasColumnType("numeric(14,2)");

        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => p.OwnerId);
    }
}
