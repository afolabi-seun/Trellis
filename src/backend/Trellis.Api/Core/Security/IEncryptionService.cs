namespace Trellis.Api.Core.Security;

/// <summary>
/// security_guide.md's Encryption Architecture — application-level AES-256-GCM for a column that
/// platform-level DB-at-rest encryption alone doesn't cover (currently: Leasing's
/// screening_result.full_report_ciphertext, the single most sensitive column in the schema).
/// <paramref name="associatedData"/> is bound into the GCM auth tag, not encrypted itself — the
/// caller's own stable identifier for the record (e.g. "{tenantId}:{applicationId}") so a
/// ciphertext can't be copied onto a different row and still decrypt. The envelope carries its own
/// key id (<c>k{id}:...</c>), so <see cref="DecryptAsync"/> always uses the key a ciphertext was
/// actually encrypted with, even after <see cref="Configuration.EncryptionOptions.CurrentKeyId"/>
/// has moved on to a newer key — key rotation without a re-encryption migration.
/// </summary>
public interface IEncryptionService
{
    Task<string> EncryptAsync(string plaintext, string associatedData);

    Task<string> DecryptAsync(string envelope, string associatedData);
}
