using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Document.Application.Interfaces;
using Trellis.Api.Modules.Document.Domain.Entities;
using Trellis.Api.Modules.Document.Infrastructure.Data;

namespace Trellis.Api.Modules.Document.Infrastructure.Repositories;

public sealed class DocumentRepository(DocumentDbContext dbContext) : TenantAwareRepository<DocumentRecord>, IDocumentRepository
{
    public async Task AddAsync(DocumentRecord document) => await dbContext.Documents.AddAsync(document);

    public Task<DocumentRecord?> GetByIdAsync(Guid documentId) =>
        Scoped(dbContext.Documents).FirstOrDefaultAsync(d => d.DocumentId == documentId);

    public Task<(IReadOnlyList<DocumentRecord> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit) =>
        PageAsync(Scoped(dbContext.Documents), cursor, limit);

    public Task<(IReadOnlyList<DocumentRecord> Items, bool HasMore, int TotalCount)> ListByPropertyAsync(
        Guid propertyId, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit) =>
        PageAsync(Scoped(dbContext.Documents).Where(d => d.PropertyId == propertyId), cursor, limit);

    public Task<(IReadOnlyList<DocumentRecord> Items, bool HasMore, int TotalCount)> ListByUnitAssetAsync(
        Guid unitAssetId, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit) =>
        PageAsync(Scoped(dbContext.Documents).Where(d => d.UnitAssetId == unitAssetId), cursor, limit);

    public Task<(IReadOnlyList<DocumentRecord> Items, bool HasMore, int TotalCount)> ListByOwnerAsync(
        Guid ownerId, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit) =>
        PageAsync(Scoped(dbContext.Documents).Where(d => d.OwnerId == ownerId), cursor, limit);

    public Task<(IReadOnlyList<DocumentRecord> Items, bool HasMore, int TotalCount)> ListByWorkflowInstanceAsync(
        Guid workflowInstanceId, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit) =>
        PageAsync(dbContext.Documents.Where(d => d.WorkflowInstanceId == workflowInstanceId), cursor, limit);

    /// <summary>Runs under SystemContext.Enter() (the caller's responsibility, same as NotificationDispatchBackgroundService's own poll) — deliberately queries dbContext.Documents directly, not Scoped(), since it must see every tenant's due documents in one batch.</summary>
    public async Task<IReadOnlyList<DocumentRecord>> GetDueForExpiryAlertAsync(DateOnly lookaheadDate, int batchSize) =>
        await dbContext.Documents
            .Where(d => d.Status == DocumentStatus.Active
                && d.ExpiresAt != null && d.ExpiresAt <= lookaheadDate
                && d.LastAlertedAt == null)
            .OrderBy(d => d.ExpiresAt)
            .Take(batchSize)
            .ToListAsync();

    public string GetRowVersion(DocumentRecord document) => RowVersionHelper.Get(dbContext, document);

    public void SetExpectedRowVersion(DocumentRecord document, string rowVersion) => RowVersionHelper.SetExpected(dbContext, document, rowVersion);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();

    private static async Task<(IReadOnlyList<DocumentRecord> Items, bool HasMore, int TotalCount)> PageAsync(
        IQueryable<DocumentRecord> baseQuery, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        var totalCount = await baseQuery.CountAsync();

        var query = baseQuery;
        if (cursor is { } c)
        {
            query = query.Where(d => d.CreatedAt > c.CreatedAt || (d.CreatedAt == c.CreatedAt && d.DocumentId > c.Id));
        }

        var page = await query.OrderBy(d => d.CreatedAt).ThenBy(d => d.DocumentId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }
}
