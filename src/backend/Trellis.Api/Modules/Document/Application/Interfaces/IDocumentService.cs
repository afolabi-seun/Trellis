using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Document.Application.Dtos;

namespace Trellis.Api.Modules.Document.Application.Interfaces;

public sealed record DocumentDownloadResult(Stream Content, string FileName, string ContentType);

public interface IDocumentService
{
    /// <summary>Runs magic-number type validation, saves via IFileStorage, creates the first DocumentVersion at PENDING, kicks off IFileScanner, and sets Document.CurrentVersionId — see the Phase 3 plan's UploadAsync description.</summary>
    Task<ServiceResult<DocumentResponse>> CreateAsync(
        DocumentUploadRequest request, Stream fileContent, string fileName, string declaredContentType, long fileSizeBytes, Guid uploadedBy);

    Task<ServiceResult<DocumentResponse>> GetByIdAsync(Guid documentId);

    Task<ServiceResult<PagedResponse<DocumentResponse>>> ListAsync(string? cursor, int limit);

    Task<ServiceResult<PagedResponse<DocumentResponse>>> ListByPropertyAsync(Guid propertyId, string? cursor, int limit);

    Task<ServiceResult<PagedResponse<DocumentResponse>>> ListByUnitAssetAsync(Guid unitAssetId, string? cursor, int limit);

    Task<ServiceResult<PagedResponse<DocumentResponse>>> ListByOwnerAsync(Guid ownerId, string? cursor, int limit);

    Task<ServiceResult<PagedResponse<DocumentResponse>>> ListByWorkflowInstanceAsync(Guid workflowInstanceId, string? cursor, int limit);

    /// <summary>BDR-VEN-003 — a Vendor's evidence upload, reached only after VendorAssignedToWorkItemAttribute has verified the caller's assignment. tenantId is resolved by the caller from the vendor_assignment row itself, never from TenantContext.Current (a vendor-context request has none) or trusted from the request body.</summary>
    Task<ServiceResult<DocumentResponse>> CreateVendorEvidenceAsync(
        Guid tenantId, Guid workflowInstanceId, string documentType, Stream fileContent, string fileName,
        string declaredContentType, long fileSizeBytes, Guid uploadedBy);

    Task<ServiceResult<DocumentResponse>> UpdateAsync(Guid documentId, DocumentUpdateRequest request);

    Task<ServiceResult<DocumentResponse>> ArchiveAsync(Guid documentId);

    /// <summary>security_guide.md's one exception: a stream only if scan_status = CLEAN, or regardless of status if the caller is the version's own uploader.</summary>
    Task<ServiceResult<DocumentDownloadResult>> DownloadAsync(Guid documentId, Guid callerUserId);

    /// <summary>Reporting's dashboard (BDR-RPT-001) — count of the caller's own tenant's documents expiring within lookaheadDays.</summary>
    Task<int> CountExpiringAsync(int lookaheadDays);
}
