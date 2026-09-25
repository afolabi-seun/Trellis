using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Vendor.Domain.Entities;

namespace Trellis.Api.Modules.Vendor.Infrastructure.Data.Configurations;

public sealed class VendorConfiguration : IEntityTypeConfiguration<VendorRecord>
{
    public void Configure(EntityTypeBuilder<VendorRecord> builder)
    {
        builder.ToTable("vendor", schema: "trellis_vendor");
        builder.HasKey(v => v.VendorId);

        // No xmin/RowVersion — VendorRecord has no Update method (see its own doc comment).

        builder.Property(v => v.VendorId).HasColumnName("vendor_id");
        builder.Property(v => v.VendorName).HasColumnName("vendor_name").HasMaxLength(200).IsRequired();

        builder.Property(v => v.Category)
            .HasColumnName("category")
            .HasConversion(c => c.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<VendorCategory>(s))
            .HasMaxLength(50);

        builder.Property(v => v.CreatedAt).HasColumnName("created_at");
    }
}
