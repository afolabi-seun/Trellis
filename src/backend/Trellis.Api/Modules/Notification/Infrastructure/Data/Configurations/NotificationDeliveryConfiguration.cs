using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.Notification.Domain.Entities;

namespace Trellis.Api.Modules.Notification.Infrastructure.Data.Configurations;

public sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    // A switch expression / throw expression isn't allowed inside a HasConversion lambda — same
    // "no expression tree can contain this construct" rule that ruled out named arguments
    // elsewhere in this module. A dictionary lookup is a plain method call, which is allowed.
    private static readonly Dictionary<NotificationRecipientType, string> RecipientTypeToColumn = new()
    {
        [NotificationRecipientType.AppUser] = "APP_USER",
        [NotificationRecipientType.VendorUser] = "VENDOR_USER",
        [NotificationRecipientType.Owner] = "OWNER",
        [NotificationRecipientType.ProspectiveTenant] = "PROSPECTIVE_TENANT",
        [NotificationRecipientType.External] = "EXTERNAL",
    };

    private static readonly Dictionary<string, NotificationRecipientType> ColumnToRecipientType =
        RecipientTypeToColumn.ToDictionary(kv => kv.Value, kv => kv.Key);

    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("notification_delivery", schema: "trellis_notification");
        builder.HasKey(d => d.DeliveryId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(d => d.DeliveryId).HasColumnName("delivery_id");
        builder.Property(d => d.TenantId).HasColumnName("tenant_id");

        // notifications_design.md's schema uses snake_case uppercase (APP_USER,
        // PROSPECTIVE_TENANT) — a bare HasConversion<string>() would store the C# member name
        // (AppUser, ProspectiveTenant) instead.
        builder.Property(d => d.RecipientType)
            .HasColumnName("recipient_type")
            .HasConversion(r => RecipientTypeToColumn[r], r => ColumnToRecipientType[r])
            .HasMaxLength(20);

        builder.Property(d => d.RecipientAddress).HasColumnName("recipient_address").HasMaxLength(255).IsRequired();
        builder.Property(d => d.TemplateKey).HasColumnName("template_key").HasMaxLength(50).IsRequired();
        builder.Property(d => d.Channel)
            .HasColumnName("channel")
            .HasConversion(c => c.ToString().ToUpperInvariant(), c => (NotificationChannel)Enum.Parse(typeof(NotificationChannel), c, true))
            .HasMaxLength(10);
        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToString().ToUpperInvariant(), s => (NotificationDeliveryStatus)Enum.Parse(typeof(NotificationDeliveryStatus), s, true))
            .HasMaxLength(20)
            .HasDefaultValue(NotificationDeliveryStatus.Queued);
        builder.Property(d => d.ProviderReference).HasColumnName("provider_reference").HasMaxLength(255);
        builder.Property(d => d.RelatedEntityType).HasColumnName("related_entity_type").HasMaxLength(30);
        builder.Property(d => d.RelatedEntityId).HasColumnName("related_entity_id");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.SentAt).HasColumnName("sent_at");

        builder.HasIndex(d => d.Status);
    }
}
