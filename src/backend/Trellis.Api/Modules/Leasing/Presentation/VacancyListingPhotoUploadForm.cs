namespace Trellis.Api.Modules.Leasing.Presentation;

/// <summary>Presentation-layer only — mirrors InspectionPhotoUploadForm.cs's own reasoning (IFormFile never reaches Application).</summary>
public sealed class VacancyListingPhotoUploadForm
{
    public IFormFile File { get; set; } = null!;
}
