using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Inspection.Domain.Entities;

/// <summary>security_guide.md's File Upload Security — PENDING until both the type check and the malware scan clear, own copy since InspectionPhoto is never attached to Document's own DocumentVersion.</summary>
public enum InspectionPhotoScanStatus
{
    Pending,
    Clean,
    Infected,
    ScanFailed,
}

/// <summary>
/// A photo captured during an inspection — its own first-class entity, not a Document, since
/// Document has no concept of the per-photo geolocation/capturedAt/checklist-item link this needs.
/// Reuses the same generic IFileStorage/IFileScanner pipeline Document uses, with its own narrower
/// (JPG/PNG/HEIC) type allow-list.
/// </summary>
public sealed class InspectionPhoto : ITenantOwned
{
    public Guid InspectionPhotoId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InspectionId { get; private set; }
    public Guid? InspectionItemId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long FileSizeBytes { get; private set; }
    public string StorageKey { get; private set; } = null!;
    public InspectionPhotoScanStatus ScanStatus { get; private set; } = InspectionPhotoScanStatus.Pending;
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public DateTimeOffset CapturedAt { get; private set; }
    public Guid UploadedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private InspectionPhoto() { }

    public static InspectionPhoto Create(
        Guid tenantId, Guid inspectionId, Guid? inspectionItemId, string fileName, string contentType,
        long fileSizeBytes, string storageKey, decimal? latitude, decimal? longitude, DateTimeOffset capturedAt, Guid uploadedBy) => new()
        {
            InspectionPhotoId = Guid.NewGuid(),
            TenantId = tenantId,
            InspectionId = inspectionId,
            InspectionItemId = inspectionItemId,
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            StorageKey = storageKey,
            Latitude = latitude,
            Longitude = longitude,
            CapturedAt = capturedAt,
            UploadedBy = uploadedBy,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    /// <summary>error_handling.md's resilience pattern: SCAN_FAILED is retried on the same backoff as other external dependencies, so this can be called more than once for the same row.</summary>
    public void MarkScanResult(InspectionPhotoScanStatus status) => ScanStatus = status;
}
