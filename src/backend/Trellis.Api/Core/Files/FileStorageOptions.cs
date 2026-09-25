using System.ComponentModel.DataAnnotations;

namespace Trellis.Api.Core.Files;

/// <summary>
/// Core-level, not module-specific — <see cref="IFileStorage"/> and <see cref="IFileScanner"/>
/// are shared cross-module seams (security_guide.md frames them alongside IPaymentGateway), not
/// owned by Document even though Document is their first caller; Inspection (Phase 5) will reuse
/// the same seam for photo uploads. Bound from a top-level <c>FileStorage__*</c> env var, same
/// reasoning as AppOptions's own doc comment for why it isn't <c>Modules__Document__*</c>.
/// </summary>
public class FileStorageOptions
{
    /// <summary>Where <see cref="LocalDiskFileStorage"/> writes uploaded file bytes — a real, working local directory, swappable for S3/Blob later without any caller changing.</summary>
    [Required]
    public string RootPath { get; init; } = "./data/documents";
}
