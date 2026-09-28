using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Leasing.Domain.Entities;

/// <summary>security_guide.md's File Upload Security — PENDING until both the type check and the malware scan clear, own copy since VacancyListingPhoto is never attached to Document's own DocumentVersion.</summary>
public enum VacancyListingPhotoScanStatus
{
    Pending,
    Clean,
    Infected,
    ScanFailed,
}

/// <summary>
/// BDR-LSE-001's listing photos — its own first-class entity, not a Document (marketing media, not
/// a compliance record), mirroring InspectionPhoto's own direct IFileStorage/IFileScanner use.
/// <see cref="DisplayOrder"/> is new relative to InspectionPhoto — a listing is a public gallery,
/// needs stable ordering; inspection photos have no analogous gallery concept.
/// </summary>
public sealed class VacancyListingPhoto : ITenantOwned
{
    public Guid VacancyListingPhotoId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid VacancyListingId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long FileSizeBytes { get; private set; }
    public string StorageKey { get; private set; } = null!;
    public VacancyListingPhotoScanStatus ScanStatus { get; private set; } = VacancyListingPhotoScanStatus.Pending;
    public int DisplayOrder { get; private set; }
    public Guid UploadedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private VacancyListingPhoto() { }

    public static VacancyListingPhoto Create(
        Guid tenantId, Guid vacancyListingId, string fileName, string contentType, long fileSizeBytes,
        string storageKey, int displayOrder, Guid uploadedBy) => new()
        {
            VacancyListingPhotoId = Guid.NewGuid(),
            TenantId = tenantId,
            VacancyListingId = vacancyListingId,
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            StorageKey = storageKey,
            DisplayOrder = displayOrder,
            UploadedBy = uploadedBy,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    public void MarkScanResult(VacancyListingPhotoScanStatus status) => ScanStatus = status;
}
