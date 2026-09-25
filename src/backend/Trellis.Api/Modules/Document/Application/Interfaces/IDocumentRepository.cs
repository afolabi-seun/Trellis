using Trellis.Api.Modules.Document.Domain.Entities;

namespace Trellis.Api.Modules.Document.Application.Interfaces;

public interface IDocumentRepository
{
    Task AddAsync(DocumentRecord document);

    Task<DocumentRecord?> GetByIdAsync(Guid documentId);

    Task<(IReadOnlyList<DocumentRecord> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    Task<(IReadOnlyList<DocumentRecord> Items, bool HasMore, int TotalCount)> ListByPropertyAsync(
        Guid propertyId, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    Task<(IReadOnlyList<DocumentRecord> Items, bool HasMore, int TotalCount)> ListByUnitAssetAsync(
        Guid unitAssetId, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    Task<(IReadOnlyList<DocumentRecord> Items, bool HasMore, int TotalCount)> ListByOwnerAsync(
        Guid ownerId, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    /// <summary>BDR-DOC-003's own poll — documents whose ExpiresAt falls within the lookahead window and haven't already been alerted, across every tenant (called under SystemContext.Enter()).</summary>
    Task<IReadOnlyList<DocumentRecord>> GetDueForExpiryAlertAsync(DateOnly lookaheadDate, int batchSize);

    string GetRowVersion(DocumentRecord document);

    void SetExpectedRowVersion(DocumentRecord document, string rowVersion);

    Task SaveChangesAsync();
}
