using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.Workflow.Domain.Entities;

namespace Trellis.Api.Modules.Workflow.Infrastructure.Data.Configurations;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("department", schema: "trellis_workflow");
        builder.HasKey(d => d.DepartmentId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(d => d.DepartmentId).HasColumnName("department_id");
        builder.Property(d => d.TenantId).HasColumnName("tenant_id");
        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(200).IsRequired();

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToString().ToUpperInvariant(), s => (DepartmentStatus)Enum.Parse(typeof(DepartmentStatus), s, true))
            .HasMaxLength(20);

        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(d => d.TenantId);
    }
}
