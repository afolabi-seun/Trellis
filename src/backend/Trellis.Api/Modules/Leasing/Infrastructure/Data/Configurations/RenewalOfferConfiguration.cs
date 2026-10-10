using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Leasing.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Data.Configurations;

/// <summary>"One OFFERED offer per lease" is a Postgres partial unique index in the owning migration. lease_id and unit_asset_id are plain columns, not cross-schema foreign keys.</summary>
public sealed class RenewalOfferConfiguration : IEntityTypeConfiguration<RenewalOffer>
{
    public void Configure(EntityTypeBuilder<RenewalOffer> builder)
    {
        builder.ToTable("renewal_offer", schema: "trellis_leasing");
        builder.HasKey(o => o.RenewalOfferId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(o => o.RenewalOfferId).HasColumnName("renewal_offer_id");
        builder.Property(o => o.TenantId).HasColumnName("tenant_id");
        builder.Property(o => o.LeaseId).HasColumnName("lease_id");
        builder.Property(o => o.UnitAssetId).HasColumnName("unit_asset_id");

        builder.Property(o => o.ProposedTerm)
            .HasColumnName("proposed_term")
            .HasConversion(t => t.ToUpperSnakeCase(), t => EnumStringConverter.ParseUpperSnakeCase<RenewalTerm>(t))
            .HasMaxLength(20);

        builder.Property(o => o.ProposedEndDate).HasColumnName("proposed_end_date");
        builder.Property(o => o.ProposedRentAmount).HasColumnName("proposed_rent_amount").HasColumnType("numeric(12,2)");
        builder.Property(o => o.ProposedRentFrom).HasColumnName("proposed_rent_from");
        builder.Property(o => o.RespondBy).HasColumnName("respond_by");

        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<RenewalOfferStatus>(s))
            .HasMaxLength(20);

        builder.Property(o => o.OutcomeOn).HasColumnName("outcome_on");
        builder.Property(o => o.DeclineReason).HasColumnName("decline_reason").HasMaxLength(RenewalOffer.MaxDeclineReasonLength);
        builder.Property(o => o.DeclineNote).HasColumnName("decline_note").HasMaxLength(RenewalOffer.MaxNoteLength);
        builder.Property(o => o.RentChangeApplied).HasColumnName("rent_change_applied");
        builder.Property(o => o.OfferedBy).HasColumnName("offered_by");
        builder.Property(o => o.OutcomeRecordedBy).HasColumnName("outcome_recorded_by");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(o => o.TenantId);
        builder.HasIndex(o => o.LeaseId);
    }
}
