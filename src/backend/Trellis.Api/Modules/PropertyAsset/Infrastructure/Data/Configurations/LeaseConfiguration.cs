using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.PropertyAsset.Domain.Entities;

namespace Trellis.Api.Modules.PropertyAsset.Infrastructure.Data.Configurations;

/// <summary>
/// "One ACTIVE lease per unit" and "one lease per Leasing application" are Postgres partial unique
/// indexes in the owning migration, as are the CHECK constraints tying end_date to term_type —
/// none expressible via EF Core's fluent API.
/// </summary>
public sealed class LeaseConfiguration : IEntityTypeConfiguration<Lease>
{
    public void Configure(EntityTypeBuilder<Lease> builder)
    {
        builder.ToTable("lease", schema: "trellis_property");
        builder.HasKey(l => l.LeaseId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(l => l.LeaseId).HasColumnName("lease_id");
        builder.Property(l => l.TenantId).HasColumnName("tenant_id");
        builder.Property(l => l.UnitAssetId).HasColumnName("unit_asset_id");

        builder.Property(l => l.Source)
            .HasColumnName("source")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<LeaseSource>(s))
            .HasMaxLength(20);

        builder.Property(l => l.LeaseApplicationId).HasColumnName("lease_application_id");
        builder.Property(l => l.StartDate).HasColumnName("start_date");

        builder.Property(l => l.TermType)
            .HasColumnName("term_type")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<LeaseTermType>(s))
            .HasMaxLength(20);

        builder.Property(l => l.EndDate).HasColumnName("end_date");

        builder.Property(l => l.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<LeaseStatus>(s))
            .HasMaxLength(20);

        builder.Property(l => l.EndedOn).HasColumnName("ended_on");

        // EF Core never passes a null to a value converter, so .Value is safe on the two nullable enums.
        builder.Property(l => l.EndReason)
            .HasColumnName("end_reason")
            .HasConversion(s => s!.Value.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<LeaseEndReason>(s))
            .HasMaxLength(30);

        builder.Property(l => l.EndedBy)
            .HasColumnName("ended_by")
            .HasConversion(s => s!.Value.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<LeaseEndedBy>(s))
            .HasMaxLength(20);

        builder.Property(l => l.EndNote).HasColumnName("end_note").HasMaxLength(Lease.MaxEndNoteLength);
        builder.Property(l => l.CreatedBy).HasColumnName("created_by");
        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(l => l.TenantId);
        builder.HasIndex(l => l.UnitAssetId);
    }
}
