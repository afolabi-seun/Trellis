using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Leasing.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Data.Configurations;

public sealed class ApplicationLinkTokenConfiguration : IEntityTypeConfiguration<ApplicationLinkToken>
{
    public void Configure(EntityTypeBuilder<ApplicationLinkToken> builder)
    {
        builder.ToTable("application_link_token", schema: "trellis_leasing");
        builder.HasKey(t => t.ApplicationLinkTokenId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(t => t.ApplicationLinkTokenId).HasColumnName("application_link_token_id");
        builder.Property(t => t.TenantId).HasColumnName("tenant_id");
        builder.Property(t => t.VacancyListingId).HasColumnName("vacancy_listing_id");
        builder.Property(t => t.ApplicantEmail).HasColumnName("applicant_email").HasMaxLength(255);
        builder.Property(t => t.TokenHash).HasColumnName("token_hash").HasMaxLength(64);

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<ApplicationLinkTokenStatus>(s))
            .HasMaxLength(20);

        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at");
        builder.Property(t => t.ConsumedApplicationId).HasColumnName("consumed_application_id");
        builder.Property(t => t.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.TenantId);
    }
}
