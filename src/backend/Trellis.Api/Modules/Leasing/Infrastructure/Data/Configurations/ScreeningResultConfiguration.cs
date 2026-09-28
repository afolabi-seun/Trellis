using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Leasing.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Data.Configurations;

public sealed class ScreeningResultConfiguration : IEntityTypeConfiguration<ScreeningResult>
{
    public void Configure(EntityTypeBuilder<ScreeningResult> builder)
    {
        builder.ToTable("screening_result", schema: "trellis_leasing");
        builder.HasKey(r => r.ScreeningResultId);

        builder.Property(r => r.ScreeningResultId).HasColumnName("screening_result_id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.ApplicationId).HasColumnName("application_id");
        builder.Property(r => r.Provider).HasColumnName("provider").HasMaxLength(30);

        builder.Property(r => r.OutcomeSummary)
            .HasColumnName("outcome_summary")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<ScreeningOutcomeSummary>(s))
            .HasMaxLength(20);

        builder.Property(r => r.FullReportCiphertext).HasColumnName("full_report_ciphertext");
        builder.Property(r => r.ReceivedAt).HasColumnName("received_at");

        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => r.ApplicationId);
    }
}
