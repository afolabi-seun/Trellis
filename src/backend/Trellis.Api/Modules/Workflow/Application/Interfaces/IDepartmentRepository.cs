using Trellis.Api.Modules.Workflow.Domain.Entities;

namespace Trellis.Api.Modules.Workflow.Application.Interfaces;

public interface IDepartmentRepository
{
    Task AddAsync(Department department);

    Task<Department?> GetByIdAsync(Guid departmentId);

    Task<(IReadOnlyList<Department> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    string GetRowVersion(Department department);

    void SetExpectedRowVersion(Department department, string rowVersion);

    Task SaveChangesAsync();
}
