using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Inspection.Domain.Entities;

namespace Trellis.Api.Modules.Inspection.Infrastructure.Data.Configurations;

public sealed class InspectionConfiguration : IEntityTypeConfiguration<InspectionRecord>
{
    public void Configure(EntityTypeBuilder<InspectionRecord> builder)
    {
        builder.ToTable("inspection", schema: "trellis_inspection");
        builder.HasKey(i => i.InspectionId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(i => i.InspectionId).HasColumnName("inspection_id");
        builder.Property(i => i.TenantId).HasColumnName("tenant_id");
        builder.Property(i => i.PropertyId).HasColumnName("property_id");
        builder.Property(i => i.UnitAssetId).HasColumnName("unit_asset_id");
        builder.Property(i => i.AssignedToUserId).HasColumnName("assigned_to_user_id");
        builder.Property(i => i.ScheduledByUserId).HasColumnName("scheduled_by_user_id");

        // InspectionStatus.InProgress is a compound name — a bare ToUpperInvariant() would
        // produce "INPROGRESS", not "IN_PROGRESS" (same reasoning DocumentConfiguration's own
        // DocumentType conversion comment gives).
        builder.Property(i => i.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<InspectionStatus>(s))
            .HasMaxLength(20);

        builder.Property(i => i.ScheduledAt).HasColumnName("scheduled_at");
        builder.Property(i => i.StartedAt).HasColumnName("started_at");
        builder.Property(i => i.CompletedAt).HasColumnName("completed_at");
        builder.Property(i => i.ClosedAt).HasColumnName("closed_at");
        builder.Property(i => i.Latitude).HasColumnName("latitude").HasColumnType("numeric(9,6)");
        builder.Property(i => i.Longitude).HasColumnName("longitude").HasColumnType("numeric(9,6)");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(i => i.TenantId);
        builder.HasIndex(i => i.PropertyId);
        builder.HasIndex(i => i.UnitAssetId);
    }
}
