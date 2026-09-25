using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Workflow.Application.Interfaces;
using Trellis.Api.Modules.Workflow.Domain.Entities;
using Trellis.Api.Modules.Workflow.Infrastructure.Data;

namespace Trellis.Api.Modules.Workflow.Infrastructure.Repositories;

public sealed class DepartmentRepository(WorkflowDbContext dbContext) : TenantAwareRepository<Department>, IDepartmentRepository
{
    public async Task AddAsync(Department department) => await dbContext.Departments.AddAsync(department);

    public Task<Department?> GetByIdAsync(Guid departmentId) =>
        Scoped(dbContext.Departments).FirstOrDefaultAsync(d => d.DepartmentId == departmentId);

    public async Task<(IReadOnlyList<Department> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        var query = Scoped(dbContext.Departments);

        if (cursor is { } c)
        {
            query = query.Where(d => d.CreatedAt > c.CreatedAt || (d.CreatedAt == c.CreatedAt && d.DepartmentId > c.Id));
        }

        var totalCount = await Scoped(dbContext.Departments).CountAsync();

        var page = await query.OrderBy(d => d.CreatedAt).ThenBy(d => d.DepartmentId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public string GetRowVersion(Department department) => RowVersionHelper.Get(dbContext, department);

    public void SetExpectedRowVersion(Department department, string rowVersion) => RowVersionHelper.SetExpected(dbContext, department, rowVersion);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
