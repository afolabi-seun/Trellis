using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Ai.Domain.Entities;

namespace Trellis.Api.Modules.Ai.Infrastructure.Data.Configurations;

public sealed class ExtractionJobConfiguration : IEntityTypeConfiguration<ExtractionJob>
{
    public void Configure(EntityTypeBuilder<ExtractionJob> builder)
    {
        builder.ToTable("extraction_job", schema: "trellis_ai");
        builder.HasKey(j => j.ExtractionJobId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(j => j.ExtractionJobId).HasColumnName("extraction_job_id");
        builder.Property(j => j.TenantId).HasColumnName("tenant_id");
        builder.Property(j => j.DocumentId).HasColumnName("document_id");
        builder.Property(j => j.DocumentVersionId).HasColumnName("document_version_id");
        builder.Property(j => j.DocumentType).HasColumnName("document_type").HasMaxLength(20);

        // ManualReviewRequired is a compound name — needs the snake-case converter, not a bare
        // ToUpperInvariant() (which would produce "MANUALREVIEWREQUIRED"), the same Phase 5 bug
        // class this pattern exists to avoid.
        builder.Property(j => j.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<ExtractionStatus>(s))
            .HasMaxLength(30);

        builder.Property(j => j.ExtractedFieldsJson).HasColumnName("extracted_fields_json");
        builder.Property(j => j.Confidence).HasColumnName("confidence").HasColumnType("numeric(5,4)");
        builder.Property(j => j.ConfirmedFieldsJson).HasColumnName("confirmed_fields_json");
        builder.Property(j => j.FailureReason).HasColumnName("failure_reason").HasMaxLength(500);
        builder.Property(j => j.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(j => j.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(j => j.CreatedAt).HasColumnName("created_at");
        builder.Property(j => j.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(j => j.TenantId);
        builder.HasIndex(j => j.DocumentVersionId);
        builder.HasIndex(j => new { j.TenantId, j.Status });
    }
}
