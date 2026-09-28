using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Leasing.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Data.Configurations;

/// <summary>UnitAssetId's own "unique while LISTED or UNDER_APPLICATION" constraint is a Postgres partial unique index — not expressible via EF Core's fluent API — so it lives in the owning migration's raw SQL only, not here.</summary>
public sealed class VacancyListingConfiguration : IEntityTypeConfiguration<VacancyListing>
{
    public void Configure(EntityTypeBuilder<VacancyListing> builder)
    {
        builder.ToTable("vacancy_listing", schema: "trellis_leasing");
        builder.HasKey(l => l.VacancyListingId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(l => l.VacancyListingId).HasColumnName("vacancy_listing_id");
        builder.Property(l => l.TenantId).HasColumnName("tenant_id");
        builder.Property(l => l.UnitAssetId).HasColumnName("unit_asset_id");
        builder.Property(l => l.RentAmount).HasColumnName("rent_amount").HasColumnType("numeric(12,2)");
        builder.Property(l => l.Currency).HasColumnName("currency").HasMaxLength(3);
        builder.Property(l => l.AvailableFrom).HasColumnName("available_from");
        builder.Property(l => l.Description).HasColumnName("description");

        builder.Property(l => l.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<VacancyListingStatus>(s))
            .HasMaxLength(20);

        builder.Property(l => l.CreatedBy).HasColumnName("created_by");
        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(l => l.TenantId);
        builder.HasIndex(l => l.UnitAssetId);
    }
}
