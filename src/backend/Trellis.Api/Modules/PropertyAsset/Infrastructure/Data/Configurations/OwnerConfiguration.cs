using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.PropertyAsset.Domain.Entities;

namespace Trellis.Api.Modules.PropertyAsset.Infrastructure.Data.Configurations;

public sealed class OwnerConfiguration : IEntityTypeConfiguration<Owner>
{
    public void Configure(EntityTypeBuilder<Owner> builder)
    {
        builder.ToTable("owner", schema: "trellis_property");
        builder.HasKey(o => o.OwnerId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(o => o.OwnerId).HasColumnName("owner_id");
        builder.Property(o => o.TenantId).HasColumnName("tenant_id");
        builder.Property(o => o.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(o => o.Email).HasColumnName("email").HasMaxLength(255);
        builder.Property(o => o.Phone).HasColumnName("phone").HasMaxLength(30);

        // data_model.md never defined owner's own schema (a gap closed this phase) — status
        // follows the same ACTIVE/ARCHIVED convention as property/unit_asset for consistency.
        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToString().ToUpperInvariant(), s => (OwnerStatus)Enum.Parse(typeof(OwnerStatus), s, true))
            .HasMaxLength(20);

        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(o => o.TenantId);
    }
}
