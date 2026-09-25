using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.Vendor.Domain.Entities;

namespace Trellis.Api.Modules.Vendor.Infrastructure.Data.Configurations;

public sealed class TenantVendorRelationshipConfiguration : IEntityTypeConfiguration<TenantVendorRelationship>
{
    public void Configure(EntityTypeBuilder<TenantVendorRelationship> builder)
    {
        builder.ToTable("tenant_vendor_relationship", schema: "trellis_vendor");
        builder.HasKey(r => r.TenantVendorRelationshipId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(r => r.TenantVendorRelationshipId).HasColumnName("tenant_vendor_relationship_id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.VendorId).HasColumnName("vendor_id");

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToString().ToUpperInvariant(), s => (TenantVendorRelationshipStatus)Enum.Parse(typeof(TenantVendorRelationshipStatus), s, true))
            .HasMaxLength(20)
            .HasDefaultValue(TenantVendorRelationshipStatus.Active);

        builder.Property(r => r.TerminatedAt).HasColumnName("terminated_at");
        builder.Property(r => r.TerminatedByUserId).HasColumnName("terminated_by_user_id");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => new { r.TenantId, r.VendorId }).IsUnique();
    }
}
