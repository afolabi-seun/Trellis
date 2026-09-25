using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Document.Domain.Entities;

/// <summary>security_guide.md's File Upload Security — PENDING until both the type check (before this row even exists) and the malware scan clear.</summary>
public enum DocumentScanStatus
{
    Pending,
    Clean,
    Infected,
    ScanFailed,
}

/// <summary>
/// Immutable once created — a new upload is a new version, never an edit (the plan's own stated
/// reasoning). <see cref="Document.CurrentVersionId"/> is only ever repointed to a new row here,
/// never has this one mutated underneath it.
/// </summary>
public sealed class DocumentVersion : ITenantOwned
{
    public Guid DocumentVersionId { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid TenantId { get; private set; }
    public int VersionNumber { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long FileSizeBytes { get; private set; }
    public string StorageKey { get; private set; } = null!;
    public DocumentScanStatus ScanStatus { get; private set; }
    public Guid UploadedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private DocumentVersion() { }

    public static DocumentVersion Create(
        Guid tenantId, Guid documentId, int versionNumber, string fileName, string contentType,
        long fileSizeBytes, string storageKey, Guid uploadedBy) => new()
        {
            DocumentVersionId = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentId = documentId,
            VersionNumber = versionNumber,
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            StorageKey = storageKey,
            ScanStatus = DocumentScanStatus.Pending,
            UploadedBy = uploadedBy,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    /// <summary>error_handling.md's resilience pattern: SCAN_FAILED is retried on the same backoff as other external dependencies, so this can be called more than once for the same row.</summary>
    public void MarkScanResult(DocumentScanStatus status) => ScanStatus = status;
}
