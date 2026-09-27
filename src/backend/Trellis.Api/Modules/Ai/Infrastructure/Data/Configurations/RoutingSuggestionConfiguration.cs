using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Ai.Domain.Entities;

namespace Trellis.Api.Modules.Ai.Infrastructure.Data.Configurations;

public sealed class RoutingSuggestionConfiguration : IEntityTypeConfiguration<RoutingSuggestion>
{
    public void Configure(EntityTypeBuilder<RoutingSuggestion> builder)
    {
        builder.ToTable("routing_suggestion", schema: "trellis_ai");
        builder.HasKey(r => r.RoutingSuggestionId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(r => r.RoutingSuggestionId).HasColumnName("routing_suggestion_id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(r => r.PropertyId).HasColumnName("property_id");
        builder.Property(r => r.SuggestedDepartmentId).HasColumnName("suggested_department_id");
        builder.Property(r => r.SuggestedVendorCategory).HasColumnName("suggested_vendor_category").HasMaxLength(30);
        builder.Property(r => r.Confidence).HasColumnName("confidence").HasColumnType("numeric(5,4)");

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<RoutingSuggestionStatus>(s))
            .HasMaxLength(20);

        builder.Property(r => r.FinalDepartmentId).HasColumnName("final_department_id");
        builder.Property(r => r.WorkflowInstanceId).HasColumnName("workflow_instance_id");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => r.WorkflowInstanceId);
        builder.HasIndex(r => new { r.TenantId, r.Status });
    }
}
