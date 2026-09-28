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

    /// <summary>
    /// Reachable from both a tenant-staff context and a vendor context (BDR-VEN-003's evidence
    /// list) — deliberately not Scoped() (which would throw for a vendor caller with no
    /// TenantContext.Current). Correctness for the vendor path comes from the
    /// tenant_or_vendor_isolation RLS policy on document/document_version (layer 1) plus
    /// VendorAssignedToWorkItemAttribute already having verified the caller's assignment before
    /// the controller ever reaches this call (layer 2, at the route rather than the repository
    /// this one time, since one method serves both caller shapes).
    /// </summary>
    Task<(IReadOnlyList<DocumentRecord> Items, bool HasMore, int TotalCount)> ListByWorkflowInstanceAsync(
        Guid workflowInstanceId, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    /// <summary>BDR-DOC-003's own poll — documents whose ExpiresAt falls within the lookahead window and haven't already been alerted, across every tenant (called under SystemContext.Enter()).</summary>
    Task<IReadOnlyList<DocumentRecord>> GetDueForExpiryAlertAsync(DateOnly lookaheadDate, int batchSize);

    /// <summary>Reporting's dashboard (BDR-RPT-001) — the tenant-scoped (Scoped()) opposite of GetDueForExpiryAlertAsync: one tenant's own count, not a cross-tenant batch, and doesn't consult LastAlertedAt (a document due for its 3rd reminder is still "expiring soon" for this count).</summary>
    Task<int> CountDueForExpiryAsync(DateOnly lookaheadDate);

    string GetRowVersion(DocumentRecord document);

    void SetExpectedRowVersion(DocumentRecord document, string rowVersion);

    Task SaveChangesAsync();
}
