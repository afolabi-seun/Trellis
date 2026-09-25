using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Document.Domain.Entities;

namespace Trellis.Api.Modules.Document.Infrastructure.Data.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<DocumentRecord>
{
    public void Configure(EntityTypeBuilder<DocumentRecord> builder)
    {
        builder.ToTable("document", schema: "trellis_document");
        builder.HasKey(d => d.DocumentId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(d => d.DocumentId).HasColumnName("document_id");
        builder.Property(d => d.TenantId).HasColumnName("tenant_id");
        builder.Property(d => d.PropertyId).HasColumnName("property_id");
        builder.Property(d => d.UnitAssetId).HasColumnName("unit_asset_id");
        builder.Property(d => d.OwnerId).HasColumnName("owner_id");
        builder.Property(d => d.WorkflowInstanceId).HasColumnName("workflow_instance_id");

        // DocumentType.InsuranceCert is a compound name — see WorkflowInstanceConfiguration's
        // identical comment for why a bare ToUpperInvariant() would be wrong here.
        builder.Property(d => d.DocumentType)
            .HasColumnName("document_type")
            .HasConversion(t => t.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<DocumentType>(s))
            .HasMaxLength(50);

        builder.Property(d => d.CurrentVersionId).HasColumnName("current_version_id");
        builder.Property(d => d.ExpiresAt).HasColumnName("expires_at");
        builder.Property(d => d.LastAlertedAt).HasColumnName("last_alerted_at");

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToString().ToUpperInvariant(), s => (DocumentStatus)Enum.Parse(typeof(DocumentStatus), s, true))
            .HasMaxLength(20);

        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(d => d.TenantId);
        builder.HasIndex(d => d.PropertyId);
        builder.HasIndex(d => d.UnitAssetId);
        builder.HasIndex(d => d.OwnerId);
        builder.HasIndex(d => d.WorkflowInstanceId);
        builder.HasIndex(d => new { d.ExpiresAt, d.LastAlertedAt });
    }
}
