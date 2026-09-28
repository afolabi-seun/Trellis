using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.Reporting.Infrastructure.Data.Configurations;

public sealed class ReportShareLinkConfiguration : IEntityTypeConfiguration<ReportShareLink>
{
    public void Configure(EntityTypeBuilder<ReportShareLink> builder)
    {
        builder.ToTable("report_share_link", schema: "trellis_reporting");
        builder.HasKey(l => l.ReportShareLinkId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(l => l.ReportShareLinkId).HasColumnName("report_share_link_id");
        builder.Property(l => l.TenantId).HasColumnName("tenant_id");
        builder.Property(l => l.OwnerReportId).HasColumnName("owner_report_id");
        builder.Property(l => l.TokenHash).HasColumnName("token_hash").HasMaxLength(64);

        builder.Property(l => l.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<ReportShareLinkStatus>(s))
            .HasMaxLength(20);

        builder.Property(l => l.ExpiresAt).HasColumnName("expires_at");
        builder.Property(l => l.LastAccessedAt).HasColumnName("last_accessed_at");
        builder.Property(l => l.CreatedBy).HasColumnName("created_by");
        builder.Property(l => l.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(l => l.TokenHash).IsUnique();
        builder.HasIndex(l => l.TenantId);
    }
}
