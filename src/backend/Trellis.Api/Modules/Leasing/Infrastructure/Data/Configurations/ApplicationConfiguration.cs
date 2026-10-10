using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using LeasingApplication = Trellis.Api.Modules.Leasing.Domain.Entities.Application;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Data.Configurations;

public sealed class ApplicationConfiguration : IEntityTypeConfiguration<LeasingApplication>
{
    public void Configure(EntityTypeBuilder<LeasingApplication> builder)
    {
        builder.ToTable("application", schema: "trellis_leasing");
        builder.HasKey(a => a.ApplicationId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(a => a.ApplicationId).HasColumnName("application_id");
        builder.Property(a => a.TenantId).HasColumnName("tenant_id");
        builder.Property(a => a.VacancyListingId).HasColumnName("vacancy_listing_id");
        builder.Property(a => a.ApplicantEmail).HasColumnName("applicant_email").HasMaxLength(255);
        builder.Property(a => a.ApplicantFullName).HasColumnName("applicant_full_name").HasMaxLength(255);
        builder.Property(a => a.ApplicantPhone).HasColumnName("applicant_phone").HasMaxLength(30);
        builder.Property(a => a.ApplicantDateOfBirth).HasColumnName("applicant_date_of_birth");
        builder.Property(a => a.ApplicantCurrentAddress).HasColumnName("applicant_current_address").HasMaxLength(500);
        builder.Property(a => a.ApplicantEmployerName).HasColumnName("applicant_employer_name").HasMaxLength(255);
        builder.Property(a => a.ApplicantMonthlyIncome).HasColumnName("applicant_monthly_income").HasColumnType("numeric(12,2)");
        builder.Property(a => a.ConsentToScreening).HasColumnName("consent_to_screening");
        builder.Property(a => a.EnvelopeId).HasColumnName("envelope_id").HasMaxLength(255);
        builder.Property(a => a.SentForSignatureBy).HasColumnName("sent_for_signature_by");
        builder.Property(a => a.DraftDocumentId).HasColumnName("draft_document_id");
        builder.Property(a => a.SignedLeaseVersionId).HasColumnName("signed_lease_version_id");
        builder.Property(a => a.LeaseEndedDate).HasColumnName("lease_ended_date");
        builder.Property(a => a.LeaseStartDate).HasColumnName("lease_start_date");
        builder.Property(a => a.LeaseEndDate).HasColumnName("lease_end_date");

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<Domain.Entities.ApplicationStatus>(s))
            .HasMaxLength(20);

        builder.Property(a => a.SubmittedAt).HasColumnName("submitted_at");

        builder.HasIndex(a => a.TenantId);
        builder.HasIndex(a => a.VacancyListingId);
        builder.HasIndex(a => a.EnvelopeId);
    }
}
