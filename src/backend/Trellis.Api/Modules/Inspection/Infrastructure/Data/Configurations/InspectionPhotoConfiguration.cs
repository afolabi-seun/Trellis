using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Inspection.Domain.Entities;

namespace Trellis.Api.Modules.Inspection.Infrastructure.Data.Configurations;

public sealed class InspectionPhotoConfiguration : IEntityTypeConfiguration<InspectionPhoto>
{
    public void Configure(EntityTypeBuilder<InspectionPhoto> builder)
    {
        builder.ToTable("inspection_photo", schema: "trellis_inspection");
        builder.HasKey(p => p.InspectionPhotoId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(p => p.InspectionPhotoId).HasColumnName("inspection_photo_id");
        builder.Property(p => p.TenantId).HasColumnName("tenant_id");
        builder.Property(p => p.InspectionId).HasColumnName("inspection_id");
        builder.Property(p => p.InspectionItemId).HasColumnName("inspection_item_id");
        builder.Property(p => p.FileName).HasColumnName("file_name").HasMaxLength(260);
        builder.Property(p => p.ContentType).HasColumnName("content_type").HasMaxLength(100);
        builder.Property(p => p.FileSizeBytes).HasColumnName("file_size_bytes");
        builder.Property(p => p.StorageKey).HasColumnName("storage_key").HasMaxLength(500);

        // ScanFailed is a compound name — needs the snake-case converter, not a bare
        // ToUpperInvariant() (which would produce "SCANFAILED").
        builder.Property(p => p.ScanStatus)
            .HasColumnName("scan_status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<InspectionPhotoScanStatus>(s))
            .HasMaxLength(20);

        builder.Property(p => p.Latitude).HasColumnName("latitude").HasColumnType("numeric(9,6)");
        builder.Property(p => p.Longitude).HasColumnName("longitude").HasColumnType("numeric(9,6)");
        builder.Property(p => p.CapturedAt).HasColumnName("captured_at");
        builder.Property(p => p.UploadedBy).HasColumnName("uploaded_by");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => p.InspectionId);
        builder.HasIndex(p => p.InspectionItemId);
    }
}
