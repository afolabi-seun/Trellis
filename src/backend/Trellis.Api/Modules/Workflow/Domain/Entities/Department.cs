using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Workflow.Domain.Exceptions;

namespace Trellis.Api.Modules.Workflow.Domain.Entities;

/// <summary>data_model.md's `trellis_workflow.department` — filled in this phase (only ever named in the schema catalogue and referenced via app_user.department_ids before now).</summary>
public enum DepartmentStatus
{
    Active,
    Archived,
}

/// <summary>BDR-DEPT-001 — a Tenant Admin's configurable department (Maintenance, Legal, Finance, ...), never hardcoded categories.</summary>
public sealed class Department : ITenantOwned
{
    public Guid DepartmentId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public DepartmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Department() { }

    public static Department Create(Guid tenantId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Department name is required.", nameof(name));
        }

        var now = DateTimeOffset.UtcNow;
        return new Department
        {
            DepartmentId = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Status = DepartmentStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Update(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Department name is required.", nameof(name));
        }

        Name = name;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Never a hard delete — workflow_step_template.default_department_id and app_user.department_ids must keep resolving.</summary>
    public void Archive()
    {
        if (Status != DepartmentStatus.Active)
        {
            throw new InvalidStatusTransitionException("Department", Status.ToString(), DepartmentStatus.Archived.ToString());
        }

        Status = DepartmentStatus.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
