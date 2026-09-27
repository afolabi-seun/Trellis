namespace Trellis.Api.Modules.Ai.Application.Interfaces;

public sealed record ExtractedField(string FieldName, string? Value);

public sealed record ExtractionResult(IReadOnlyList<ExtractedField> Fields, decimal Confidence);

public sealed record DepartmentCandidate(Guid DepartmentId, string Name);

public sealed record RoutingClassification(Guid? SuggestedDepartmentId, string? SuggestedVendorCategory, decimal Confidence);

/// <summary>
/// security_guide.md/deployment_topologies.md's named-but-unspecified external AI provider seam —
/// mirrors Core/Files/IFileScanner.cs's exact shape (a small, provider-agnostic interface, a stub
/// implementation until a real vendor is chosen). Kept inside the Ai module, not Core, since only
/// Ai needs it today — IFileScanner/IFileStorage only moved to Core once a second module needed
/// them.
/// </summary>
public interface IAiProvider
{
    Task<ExtractionResult> ExtractAsync(Stream content, string fileName, string documentType);

    Task<RoutingClassification> ClassifyRoutingAsync(string description, IReadOnlyList<DepartmentCandidate> departments);
}
