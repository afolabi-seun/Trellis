using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.RentLedger.Domain.Entities;

namespace Trellis.Api.Modules.RentLedger.Infrastructure.Data.Configurations;

public sealed class RentPaymentRecordConfiguration : IEntityTypeConfiguration<RentPaymentRecord>
{
    public void Configure(EntityTypeBuilder<RentPaymentRecord> builder)
    {
        builder.ToTable("rent_payment_record", schema: "trellis_rentledger");
        builder.HasKey(r => r.RentPaymentRecordId);
        builder.Ignore(r => r.IsVoided);

        builder.Property(r => r.RentPaymentRecordId).HasColumnName("rent_payment_record_id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.RentScheduleId).HasColumnName("rent_schedule_id");
        builder.Property(r => r.AmountReceived).HasColumnName("amount_received").HasColumnType("numeric(12,2)");
        builder.Property(r => r.DateReceived).HasColumnName("date_received");
        builder.Property(r => r.MethodReference).HasColumnName("method_reference").HasMaxLength(255);
        builder.Property(r => r.RecordedByUserId).HasColumnName("recorded_by_user_id");
        builder.Property(r => r.RecordedAt).HasColumnName("recorded_at");
        builder.Property(r => r.VoidedAt).HasColumnName("voided_at");
        builder.Property(r => r.VoidedBy).HasColumnName("voided_by");
        builder.Property(r => r.VoidReason).HasColumnName("void_reason").HasMaxLength(500);

        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => r.RentScheduleId);
    }
}
