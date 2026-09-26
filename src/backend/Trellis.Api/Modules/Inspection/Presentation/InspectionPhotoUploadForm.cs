namespace Trellis.Api.Modules.Inspection.Presentation;

/// <summary>Presentation-layer only — mirrors DocumentUploadForm.cs's own reasoning (IFormFile never reaches Application). Internal staff's own single, online photo upload.</summary>
public sealed class InspectionPhotoUploadForm
{
    public Guid? InspectionItemId { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTimeOffset? CapturedAt { get; set; }
    public IFormFile File { get; set; } = null!;
}
