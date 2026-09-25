using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Document.Domain.Exceptions;

namespace Trellis.Api.Modules.Document.Domain.Entities;

/// <summary>data_model.md's `trellis_document.document.document_type`.</summary>
public enum DocumentType
{
    Lease,
    InsuranceCert,
    Licence,
    Invoice,
    Other,
}

public enum DocumentStatus
{
    Active,
    Archived,
}

/// <summary>
/// BDR-DOC-001 — links to at most one of {Property, UnitAsset, Owner} (Vendor is Phase 4, per
/// the Phase 3 plan's decision #6), or none at all (a tenant-level document, e.g. a company
/// policy). That at-most-one rule is enforced at the request layer (DocumentService), not here —
/// no clean partial-exclusivity constraint in Postgres worth the complexity, same reasoning the
/// plan gives. Status/UpdatedAt/RowVersion are gap-fills, same as every other entity
/// data_model.md originally documented without them (Owner's own precedent).
/// </summary>
public sealed class DocumentRecord : ITenantOwned
{
    public Guid DocumentId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? PropertyId { get; private set; }
    public Guid? UnitAssetId { get; private set; }
    public Guid? OwnerId { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public Guid? CurrentVersionId { get; private set; }
    public DateOnly? ExpiresAt { get; private set; }

    /// <summary>BDR-DOC-003's own-fire-once guard — set when the expiry alert background service dispatches for this document, so the same expiry never re-fires every poll.</summary>
    public DateTimeOffset? LastAlertedAt { get; private set; }

    public DocumentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private DocumentRecord() { }

    public static DocumentRecord Create(
        Guid tenantId, Guid? propertyId, Guid? unitAssetId, Guid? ownerId, DocumentType documentType, DateOnly? expiresAt)
    {
        var now = DateTimeOffset.UtcNow;
        return new DocumentRecord
        {
            DocumentId = Guid.NewGuid(),
            TenantId = tenantId,
            PropertyId = propertyId,
            UnitAssetId = unitAssetId,
            OwnerId = ownerId,
            DocumentType = documentType,
            ExpiresAt = expiresAt,
            Status = DocumentStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Update(DocumentType documentType, DateOnly? expiresAt)
    {
        DocumentType = documentType;
        ExpiresAt = expiresAt;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Called once the version's scan clears (or immediately for the very first upload) — a document is never left pointing at a version that's still PENDING/INFECTED.</summary>
    public void SetCurrentVersion(Guid versionId)
    {
        CurrentVersionId = versionId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAlerted() => LastAlertedAt = DateTimeOffset.UtcNow;

    /// <summary>Never a hard delete — a document's own audit/version history must keep resolving.</summary>
    public void Archive()
    {
        if (Status != DocumentStatus.Active)
        {
            throw new InvalidStatusTransitionException("Document", Status.ToString(), DocumentStatus.Archived.ToString());
        }

        Status = DocumentStatus.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
