namespace Trellis.Api.Modules.Document.Presentation;

/// <summary>
/// Presentation-layer only — IFormFile never reaches Application (DocumentUploadRequest stays
/// framework-agnostic). PropertyId/UnitAssetId/OwnerId/WorkflowInstanceId only matter on the flat
/// POST /api/v1/documents route; the nested routes ignore them and use their own route param
/// instead, so a confused/malicious client can't set one in the URL and a different one in the
/// body.
/// </summary>
public sealed class DocumentUploadForm
{
    public string DocumentType { get; set; } = string.Empty;
    public DateOnly? ExpiresAt { get; set; }
    public Guid? PropertyId { get; set; }
    public Guid? UnitAssetId { get; set; }
    public Guid? OwnerId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public IFormFile File { get; set; } = null!;
}
