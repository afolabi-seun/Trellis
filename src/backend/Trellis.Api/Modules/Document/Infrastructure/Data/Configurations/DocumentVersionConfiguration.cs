using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Document.Domain.Entities;

namespace Trellis.Api.Modules.Document.Infrastructure.Data.Configurations;

public sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("document_version", schema: "trellis_document");
        builder.HasKey(v => v.DocumentVersionId);

        builder.Property(v => v.DocumentVersionId).HasColumnName("document_version_id");
        builder.Property(v => v.DocumentId).HasColumnName("document_id");
        builder.Property(v => v.TenantId).HasColumnName("tenant_id");
        builder.Property(v => v.VersionNumber).HasColumnName("version_number");
        builder.Property(v => v.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
        builder.Property(v => v.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        builder.Property(v => v.FileSizeBytes).HasColumnName("file_size_bytes");
        builder.Property(v => v.StorageKey).HasColumnName("storage_key").HasMaxLength(500).IsRequired();

        // DocumentScanStatus.ScanFailed is a compound name — see WorkflowInstanceConfiguration's
        // identical comment for why a bare ToUpperInvariant() would be wrong here.
        builder.Property(v => v.ScanStatus)
            .HasColumnName("scan_status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<DocumentScanStatus>(s))
            .HasMaxLength(20)
            .HasDefaultValue(DocumentScanStatus.Pending);

        builder.Property(v => v.UploadedBy).HasColumnName("uploaded_by");
        builder.Property(v => v.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(v => v.TenantId);
        builder.HasIndex(v => v.DocumentId);
    }
}
