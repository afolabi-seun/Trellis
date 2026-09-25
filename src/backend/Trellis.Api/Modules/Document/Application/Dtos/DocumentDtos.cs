namespace Trellis.Api.Modules.Document.Application.Dtos;

/// <summary>
/// PropertyId/UnitAssetId/OwnerId are all optional here — a nested create route
/// (e.g. POST /api/v1/properties/{id}/documents) pre-fills exactly one from its route param,
/// while the flat POST /api/v1/documents route lets the caller set none (a tenant-level
/// document) or, if they set more than one, DocumentService rejects it as
/// MultipleAttachmentTargets. IFormFile itself never appears here — Application stays
/// framework-agnostic; the Controller extracts the stream/fileName/contentType/length and passes
/// those as plain parameters to the Service.
/// </summary>
public sealed record DocumentUploadRequest(
    string DocumentType, DateOnly? ExpiresAt, Guid? PropertyId, Guid? UnitAssetId, Guid? OwnerId);

public sealed record DocumentUpdateRequest(string DocumentType, DateOnly? ExpiresAt, string RowVersion);

public sealed record DocumentVersionResponse(
    Guid DocumentVersionId, int VersionNumber, string FileName, string ContentType, long FileSizeBytes, string ScanStatus, DateTimeOffset CreatedAt);

public sealed record DocumentResponse(
    Guid DocumentId,
    Guid? PropertyId,
    Guid? UnitAssetId,
    Guid? OwnerId,
    string DocumentType,
    DateOnly? ExpiresAt,
    string Status,
    DocumentVersionResponse? CurrentVersion,
    string RowVersion,
    DateTimeOffset CreatedAt);
