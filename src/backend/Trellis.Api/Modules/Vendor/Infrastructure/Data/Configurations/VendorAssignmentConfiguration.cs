using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Vendor.Domain.Entities;

namespace Trellis.Api.Modules.Vendor.Infrastructure.Data.Configurations;

public sealed class VendorAssignmentConfiguration : IEntityTypeConfiguration<VendorAssignment>
{
    public void Configure(EntityTypeBuilder<VendorAssignment> builder)
    {
        builder.ToTable("vendor_assignment", schema: "trellis_vendor");
        builder.HasKey(a => a.AssignmentId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(a => a.AssignmentId).HasColumnName("assignment_id");
        builder.Property(a => a.VendorId).HasColumnName("vendor_id");
        builder.Property(a => a.TenantId).HasColumnName("tenant_id");
        builder.Property(a => a.WorkItemType).HasColumnName("work_item_type").HasMaxLength(30).IsRequired();
        builder.Property(a => a.WorkItemId).HasColumnName("work_item_id");

        // VendorAssignmentStatus.InProgress is a compound name — see WorkflowInstanceConfiguration's identical comment for why a bare ToUpperInvariant() would be wrong here.
        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<VendorAssignmentStatus>(s))
            .HasMaxLength(20)
            .HasDefaultValue(VendorAssignmentStatus.Assigned);

        builder.Property(a => a.AssignedAt).HasColumnName("assigned_at");
        builder.Property(a => a.ClosedAt).HasColumnName("closed_at");

        builder.HasIndex(a => a.TenantId);
        builder.HasIndex(a => a.VendorId);
        builder.HasIndex(a => new { a.WorkItemType, a.WorkItemId });
    }
}
