using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.Reporting.Infrastructure.Data.Configurations;

public sealed class OwnerReportConfiguration : IEntityTypeConfiguration<OwnerReport>
{
    public void Configure(EntityTypeBuilder<OwnerReport> builder)
    {
        builder.ToTable("owner_report", schema: "trellis_reporting");
        builder.HasKey(r => r.OwnerReportId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(r => r.OwnerReportId).HasColumnName("owner_report_id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.OwnerId).HasColumnName("owner_id");
        builder.Property(r => r.OwnerName).HasColumnName("owner_name").HasMaxLength(255);

        builder.Property(r => r.PeriodType)
            .HasColumnName("period_type")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<OwnerReportPeriodType>(s))
            .HasMaxLength(20);

        builder.Property(r => r.PeriodStart).HasColumnName("period_start");
        builder.Property(r => r.PeriodEnd).HasColumnName("period_end");

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<OwnerReportStatus>(s))
            .HasMaxLength(20);

        builder.Property(r => r.PropertyCount).HasColumnName("property_count");
        builder.Property(r => r.TotalMaintenanceCost).HasColumnName("total_maintenance_cost").HasColumnType("numeric(12,2)");
        builder.Property(r => r.MaintenanceCurrency).HasColumnName("maintenance_currency").HasMaxLength(3);
        builder.Property(r => r.OpenMaintenanceCount).HasColumnName("open_maintenance_count");
        builder.Property(r => r.CompliantDocumentCount).HasColumnName("compliant_document_count");
        builder.Property(r => r.ExpiringDocumentCount).HasColumnName("expiring_document_count");
        builder.Property(r => r.RentCurrentCount).HasColumnName("rent_current_count");
        builder.Property(r => r.RentOverdueCount).HasColumnName("rent_overdue_count");
        builder.Property(r => r.RentArrearsCount).HasColumnName("rent_arrears_count");
        builder.Property(r => r.RentReceivedByCurrencyJson).HasColumnName("rent_received_by_currency").HasColumnType("jsonb");
        builder.Property(r => r.RentAsOf).HasColumnName("rent_as_of");
        builder.Property(r => r.RentPaymentTimelinessJson).HasColumnName("rent_payment_timeliness").HasColumnType("jsonb");
        builder.Property(r => r.PdfContent).HasColumnName("pdf_content").HasColumnType("bytea");
        builder.Property(r => r.GeneratedBy).HasColumnName("generated_by");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.ReadyAt).HasColumnName("ready_at");

        builder.HasIndex(r => new { r.TenantId, r.OwnerId });
        builder.HasIndex(r => new { r.TenantId, r.CreatedAt });
    }
}
