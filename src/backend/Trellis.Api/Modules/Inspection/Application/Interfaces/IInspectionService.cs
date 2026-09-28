using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Inspection.Application.Dtos;

namespace Trellis.Api.Modules.Inspection.Application.Interfaces;

public sealed record InspectionPhotoDownloadResult(Stream Content, string FileName, string ContentType);

public interface IInspectionService
{
    /// <summary>operational_flows.md step 1, SCHEDULE — also auto-populates the fixed baseline checklist items (InspectionChecklistDefaults).</summary>
    Task<ServiceResult<InspectionReportResponse>> ScheduleAsync(InspectionCreateRequest request, Guid scheduledBy);

    /// <summary>Also VendorAssignmentService.CreateAsync's work-item-existence check for WorkItemTypes.Inspection.</summary>
    Task<ServiceResult<InspectionReportResponse>> GetByIdAsync(Guid inspectionId);

    Task<ServiceResult<PagedResponse<InspectionReportResponse>>> ListForTenantAsync(string? cursor, int limit);

    Task<ServiceResult<InspectionItemResponse>> AddItemAsync(Guid inspectionId, InspectionItemAddRequest request);

    Task<ServiceResult<InspectionItemResponse>> UpdateItemAsync(Guid inspectionId, Guid inspectionItemId, InspectionItemUpdateRequest request);

    Task<ServiceResult<bool>> RemoveItemAsync(Guid inspectionId, Guid inspectionItemId);

    /// <summary>Internal staff's own online upload path — mirrors DocumentService's create+scan shape, narrowed to JPG/PNG/HEIC.</summary>
    Task<ServiceResult<InspectionPhotoResponse>> UploadPhotoAsync(
        Guid inspectionId, Guid? inspectionItemId, Stream content, string fileName, string contentType, long fileSizeBytes,
        decimal? latitude, decimal? longitude, DateTimeOffset capturedAt, Guid uploadedBy);

    /// <summary>security_guide.md's one exception: invisible to everyone but the uploader until scan_status = CLEAN. Shared by both the staff and vendor-portal controllers.</summary>
    Task<ServiceResult<InspectionPhotoDownloadResult>> DownloadPhotoAsync(Guid inspectionPhotoId, Guid callerUserId);

    Task<ServiceResult<InspectionReportResponse>> StartAsync(Guid inspectionId, Guid actorId);

    Task<ServiceResult<InspectionReportResponse>> CompleteAsync(Guid inspectionId, Guid actorId, decimal? latitude, decimal? longitude);

    /// <summary>Layer 2 check (IVendorAssignmentReader.IsAssignedAsync) before an unscoped query — mirrors WorkflowInstanceService.GetForVendorAsync exactly.</summary>
    Task<ServiceResult<VendorInspectionView>> GetForVendorAsync(Guid vendorId, Guid inspectionId);

    Task<ServiceResult<VendorInspectionView>> StartForVendorAsync(Guid vendorId, Guid inspectionId);

    /// <summary>
    /// The one idempotency-protected operation in this phase — called from inside
    /// IdempotencyExecutor.ExecuteIdempotentAsync by InspectionVendorPortalController, never
    /// directly. Applies the final checklist state, saves+scans each photo, transitions to Closed.
    /// </summary>
    Task<ServiceResult<VendorInspectionView>> CompleteFromVendorAsync(
        Guid vendorId, Guid inspectionId, InspectionCompleteRequest request, Guid uploadedBy);

    /// <summary>Reporting's dashboard (BDR-RPT-001) — count of the caller's own tenant's Scheduled inspections within the next lookaheadDays.</summary>
    Task<int> CountUpcomingAsync(int lookaheadDays);
}
