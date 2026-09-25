using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Workflow.Domain.Entities;

namespace Trellis.Api.Modules.Workflow.Infrastructure.Data.Configurations;

public sealed class WorkflowRoutingRuleConfiguration : IEntityTypeConfiguration<WorkflowRoutingRule>
{
    public void Configure(EntityTypeBuilder<WorkflowRoutingRule> builder)
    {
        builder.ToTable("workflow_routing_rule", schema: "trellis_workflow");
        builder.HasKey(r => r.WorkflowRoutingRuleId);

        builder.Property(r => r.WorkflowRoutingRuleId).HasColumnName("workflow_routing_rule_id");
        builder.Property(r => r.WorkflowStepTemplateId).HasColumnName("workflow_step_template_id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.RuleOrder).HasColumnName("rule_order");

        builder.Property(r => r.Criterion)
            .HasColumnName("criterion")
            .HasConversion(c => c.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<RoutingCriterion>(s))
            .HasMaxLength(30);

        builder.Property(r => r.Operator)
            .HasColumnName("operator")
            .HasConversion(o => o.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<RoutingOperator>(s))
            .HasMaxLength(30);

        builder.Property(r => r.ComparisonValue).HasColumnName("comparison_value").HasMaxLength(200).IsRequired();
        builder.Property(r => r.TargetDepartmentId).HasColumnName("target_department_id");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => r.WorkflowStepTemplateId);
    }
}
