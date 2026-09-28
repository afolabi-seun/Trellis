using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.RentLedger.Domain.Entities;

namespace Trellis.Api.Modules.RentLedger.Infrastructure.Data.Configurations;

/// <summary>
/// "One ACTIVE schedule per unit" and "one schedule per lease application" are Postgres partial
/// unique indexes — not expressible via EF Core's fluent API — so they live in the owning
/// migration's raw SQL only, not here. Like every other module, unit_asset_id and
/// lease_application_id are plain columns, not cross-schema foreign keys.
/// </summary>
public sealed class RentScheduleConfiguration : IEntityTypeConfiguration<RentSchedule>
{
    public void Configure(EntityTypeBuilder<RentSchedule> builder)
    {
        builder.ToTable("rent_schedule", schema: "trellis_rentledger");
        builder.HasKey(s => s.RentScheduleId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(s => s.RentScheduleId).HasColumnName("rent_schedule_id");
        builder.Property(s => s.TenantId).HasColumnName("tenant_id");
        builder.Property(s => s.UnitAssetId).HasColumnName("unit_asset_id");
        builder.Property(s => s.LeaseApplicationId).HasColumnName("lease_application_id");
        builder.Property(s => s.RentAmount).HasColumnName("rent_amount").HasColumnType("numeric(12,2)");
        builder.Property(s => s.Currency).HasColumnName("currency").HasMaxLength(3);
        builder.Property(s => s.StartDate).HasColumnName("start_date");
        builder.Property(s => s.DueDayOfMonth).HasColumnName("due_day_of_month");
        builder.Property(s => s.GracePeriodDays).HasColumnName("grace_period_days");

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<RentScheduleStatus>(s))
            .HasMaxLength(20);

        builder.Property(s => s.LastArrearsAlertedPeriod).HasColumnName("last_arrears_alerted_period");
        builder.Property(s => s.EndedAt).HasColumnName("ended_at");
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(s => s.TenantId);
        builder.HasIndex(s => s.UnitAssetId);
    }
}
