using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Inspection.Domain.Entities;

namespace Trellis.Api.Modules.Inspection.Infrastructure.Data.Configurations;

public sealed class InspectionItemConfiguration : IEntityTypeConfiguration<InspectionItem>
{
    public void Configure(EntityTypeBuilder<InspectionItem> builder)
    {
        builder.ToTable("inspection_item", schema: "trellis_inspection");
        builder.HasKey(i => i.InspectionItemId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(i => i.InspectionItemId).HasColumnName("inspection_item_id");
        builder.Property(i => i.TenantId).HasColumnName("tenant_id");
        builder.Property(i => i.InspectionId).HasColumnName("inspection_id");
        builder.Property(i => i.Label).HasColumnName("label").HasMaxLength(100);

        // NotApplicable is a compound name — needs the snake-case converter, not a bare
        // ToUpperInvariant() (which would produce "NOTAPPLICABLE").
        builder.Property(i => i.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<InspectionItemStatus>(s))
            .HasMaxLength(20);

        builder.Property(i => i.Notes).HasColumnName("notes");
        builder.Property(i => i.SortOrder).HasColumnName("sort_order");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(i => i.TenantId);
        builder.HasIndex(i => i.InspectionId);
    }
}
