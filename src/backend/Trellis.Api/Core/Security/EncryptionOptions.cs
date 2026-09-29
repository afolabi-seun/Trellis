using System.ComponentModel.DataAnnotations;

namespace Trellis.Api.Core.Security;

/// <summary>coding_standards.md's Configuration pattern — flat, not <c>Modules__</c>-prefixed, same as FileStorageOptions: this is Core's own cross-cutting concern, not one module's.</summary>
public class EncryptionOptions
{
    /// <summary>Which key id new ciphertexts are encrypted with; rotate by bumping this once a new <c>DataEncryptionKey_{id}_Base64</c> secret exists — old ciphertexts keep decrypting under their own recorded key id.</summary>
    [Range(1, int.MaxValue)]
    public int CurrentKeyId { get; init; } = 1;
}
