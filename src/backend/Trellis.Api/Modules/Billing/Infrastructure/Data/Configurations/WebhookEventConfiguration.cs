using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Infrastructure.Data.Configurations;

public sealed class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.ToTable("webhook_event", schema: "trellis_billing");
        builder.HasKey(w => w.WebhookEventId);
        // No xmin — MarkProcessed() is the only mutation, and no caller ever races on it (a
        // single webhook controller request handles one event start to finish).

        builder.Property(w => w.WebhookEventId).HasColumnName("webhook_event_id");
        builder.Property(w => w.ProviderEventId).HasColumnName("provider_event_id").HasMaxLength(255);
        builder.Property(w => w.EventType).HasColumnName("event_type").HasMaxLength(50);
        builder.Property(w => w.PayloadJson).HasColumnName("payload_json");
        builder.Property(w => w.ReceivedAt).HasColumnName("received_at");
        builder.Property(w => w.ProcessedAt).HasColumnName("processed_at");

        builder.HasIndex(w => w.ProviderEventId).IsUnique();
    }
}
