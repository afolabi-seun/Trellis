namespace Trellis.Api.Modules.Inspection.Presentation;

/// <summary>
/// The Vendor's single offline-sync completion payload. Decision 12: one JSON blob per non-file
/// concern rather than arrays of complex objects — ASP.NET Core's multipart binding for
/// <c>List&lt;ComplexType&gt;</c> fields needs fragile <c>Field[0].Prop</c>-style naming that a
/// PWA's own FormData-building code would have to replicate exactly; a plain JSON string plus
/// <see cref="System.Text.Json.JsonSerializer"/> in the Controller is simpler and less brittle.
/// </summary>
public sealed class InspectionCompleteForm
{
    /// <summary>Serialized <c>InspectionCompleteItem[]</c>.</summary>
    public string ItemsJson { get; set; } = "[]";

    /// <summary>Serialized <c>InspectionCompleteLocation?</c> — null/omitted if geolocation wasn't available.</summary>
    public string? LocationJson { get; set; }

    /// <summary>Serialized <c>InspectionPhotoMeta[]</c>, matched to <see cref="Photos"/> by FileName.</summary>
    public string PhotoMetaJson { get; set; } = "[]";

    public List<IFormFile> Photos { get; set; } = [];
}
