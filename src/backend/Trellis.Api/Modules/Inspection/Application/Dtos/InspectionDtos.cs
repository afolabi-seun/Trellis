namespace Trellis.Api.Modules.Inspection.Application.Dtos;

/// <summary>BDR-INSP-001 — exactly one of PropertyId/UnitAssetId; AssignedToUserId is optional and, if set, mutually exclusive with a later Vendor assignment (enforced in VendorAssignmentService).</summary>
public sealed record InspectionCreateRequest(Guid? PropertyId, Guid? UnitAssetId, Guid? AssignedToUserId, DateTimeOffset ScheduledAt);

public sealed record InspectionItemAddRequest(string Label);

public sealed record InspectionItemUpdateRequest(string Status, string? Notes);

/// <summary>Internal staff's own direct Complete — an empty body ({}) is the common case (no geolocation captured for an in-person completion).</summary>
public sealed record InspectionCompleteStaffRequest(decimal? Latitude, decimal? Longitude);

public sealed record InspectionItemResponse(Guid InspectionItemId, string Label, string Status, string? Notes, int SortOrder);

public sealed record InspectionPhotoResponse(
    Guid InspectionPhotoId, Guid? InspectionItemId, string FileName, string ContentType, long FileSizeBytes,
    string ScanStatus, decimal? Latitude, decimal? Longitude, DateTimeOffset CapturedAt, DateTimeOffset CreatedAt);

/// <summary>BDR-INSP-004's "structured inspection report" — this response DTO IS the report; no separate PDF/export route (decision 10, PDF/document export is Phase 8 Reporting territory).</summary>
public sealed record InspectionReportResponse(
    Guid InspectionId,
    Guid? PropertyId,
    Guid? UnitAssetId,
    Guid? AssignedToUserId,
    string Status,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    decimal? Latitude,
    decimal? Longitude,
    IReadOnlyList<InspectionItemResponse> Items,
    IReadOnlyList<InspectionPhotoResponse> Photos,
    string RowVersion,
    DateTimeOffset CreatedAt);

/// <summary>The Vendor-safe read of the same inspection — omits internal staff/scheduler ids and RowVersion (the vendor never PATCHes directly; completion is the one idempotent POST).</summary>
public sealed record VendorInspectionView(
    Guid InspectionId,
    Guid? PropertyId,
    Guid? UnitAssetId,
    string Status,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? StartedAt,
    decimal? Latitude,
    decimal? Longitude,
    IReadOnlyList<InspectionItemResponse> Items,
    IReadOnlyList<InspectionPhotoResponse> Photos,
    DateTimeOffset CreatedAt);

/// <summary>One checklist item's final state, submitted as part of the vendor's single completion payload (decision 12 — one JSON blob, not a multipart array).</summary>
public sealed record InspectionCompleteItem(Guid InspectionItemId, string Status, string? Notes);

public sealed record InspectionCompleteLocation(decimal Latitude, decimal Longitude);

/// <summary>Matched to an uploaded IFormFile by FileName in InspectionCompleteForm's PhotoMetaJson.</summary>
public sealed record InspectionPhotoMeta(string FileName, Guid? InspectionItemId, decimal? Latitude, decimal? Longitude, DateTimeOffset CapturedAt);

/// <summary>The parsed, framework-agnostic shape of a vendor's completion submission — the Controller deserializes InspectionCompleteForm's JSON fields and matches Photos to PhotoMetaJson before calling the Service.</summary>
public sealed record InspectionCompleteRequest(
    IReadOnlyList<InspectionCompleteItem> Items,
    InspectionCompleteLocation? Location,
    IReadOnlyList<InspectionPhotoUpload> Photos);

public sealed record InspectionPhotoUpload(
    string FileName, string ContentType, long Length, Stream Content, Guid? InspectionItemId, decimal? Latitude, decimal? Longitude, DateTimeOffset CapturedAt);
