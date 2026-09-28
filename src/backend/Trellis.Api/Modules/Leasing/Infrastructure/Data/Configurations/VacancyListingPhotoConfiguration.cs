using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Leasing.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Data.Configurations;

public sealed class VacancyListingPhotoConfiguration : IEntityTypeConfiguration<VacancyListingPhoto>
{
    public void Configure(EntityTypeBuilder<VacancyListingPhoto> builder)
    {
        builder.ToTable("vacancy_listing_photo", schema: "trellis_leasing");
        builder.HasKey(p => p.VacancyListingPhotoId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(p => p.VacancyListingPhotoId).HasColumnName("vacancy_listing_photo_id");
        builder.Property(p => p.TenantId).HasColumnName("tenant_id");
        builder.Property(p => p.VacancyListingId).HasColumnName("vacancy_listing_id");
        builder.Property(p => p.FileName).HasColumnName("file_name").HasMaxLength(260);
        builder.Property(p => p.ContentType).HasColumnName("content_type").HasMaxLength(100);
        builder.Property(p => p.FileSizeBytes).HasColumnName("file_size_bytes");
        builder.Property(p => p.StorageKey).HasColumnName("storage_key").HasMaxLength(500);

        builder.Property(p => p.ScanStatus)
            .HasColumnName("scan_status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<VacancyListingPhotoScanStatus>(s))
            .HasMaxLength(20);

        builder.Property(p => p.DisplayOrder).HasColumnName("display_order");
        builder.Property(p => p.UploadedBy).HasColumnName("uploaded_by");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => p.VacancyListingId);
    }
}
