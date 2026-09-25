using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Workflow.Application.Interfaces;
using Trellis.Api.Modules.Workflow.Domain.Entities;
using Trellis.Api.Modules.Workflow.Infrastructure.Data;

namespace Trellis.Api.Modules.Workflow.Infrastructure.Repositories;

public sealed class WorkflowTemplateRepository(WorkflowDbContext dbContext)
    : TenantAwareRepository<WorkflowTemplate>, IWorkflowTemplateRepository
{
    public async Task AddAsync(WorkflowTemplate template) => await dbContext.WorkflowTemplates.AddAsync(template);

    public Task<WorkflowTemplate?> GetByIdAsync(Guid workflowTemplateId) =>
        Scoped(dbContext.WorkflowTemplates)
            .Include(t => t.Steps).ThenInclude(s => s.Rules)
            .FirstOrDefaultAsync(t => t.WorkflowTemplateId == workflowTemplateId);

    public async Task<(IReadOnlyList<WorkflowTemplate> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        IQueryable<WorkflowTemplate> query = Scoped(dbContext.WorkflowTemplates).Include(t => t.Steps).ThenInclude(s => s.Rules);

        if (cursor is { } c)
        {
            query = query.Where(t => t.CreatedAt > c.CreatedAt || (t.CreatedAt == c.CreatedAt && t.WorkflowTemplateId > c.Id));
        }

        var totalCount = await Scoped(dbContext.WorkflowTemplates).CountAsync();

        var page = await query.OrderBy(t => t.CreatedAt).ThenBy(t => t.WorkflowTemplateId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public string GetRowVersion(WorkflowTemplate template) => RowVersionHelper.Get(dbContext, template);

    public void SetExpectedRowVersion(WorkflowTemplate template, string rowVersion) => RowVersionHelper.SetExpected(dbContext, template, rowVersion);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
