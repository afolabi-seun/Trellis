using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Trellis.Api.Core.Secrets;

namespace Trellis.Api.Core.Security;

/// <summary>
/// TotpService's own AES-256-GCM pattern, generalised: per-call key resolution via
/// <see cref="ISecretProvider"/>, plus the two things a single MFA-only key never needed — a
/// key-id prefix for rotation and caller-supplied associated data so a ciphertext is bound to one
/// record. Secret name per key id: <c>DataEncryptionKey_{id}_Base64</c>.
/// </summary>
public sealed class AesGcmEncryptionService(ISecretProvider secretProvider, IOptions<EncryptionOptions> options) : IEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public async Task<string> EncryptAsync(string plaintext, string associatedData)
    {
        var keyId = options.Value.CurrentKeyId;
        var key = await GetKeyAsync(keyId);
        var aad = Encoding.UTF8.GetBytes(associatedData);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag, aad);

        // nonce | tag | ciphertext — all needed to decrypt, none of it secret on its own.
        var payload = Convert.ToBase64String([.. nonce, .. tag, .. ciphertext]);
        return $"k{keyId}:{payload}";
    }

    public async Task<string> DecryptAsync(string envelope, string associatedData)
    {
        var separator = envelope.IndexOf(':');
        if (!envelope.StartsWith('k') || separator < 2 || !int.TryParse(envelope[1..separator], out var keyId))
        {
            throw new CryptographicException("Malformed encryption envelope — missing or unparsable key id prefix.");
        }

        var key = await GetKeyAsync(keyId);
        var aad = Encoding.UTF8.GetBytes(associatedData);
        var encrypted = Convert.FromBase64String(envelope[(separator + 1)..]);
        var nonce = encrypted[..NonceSize];
        var tag = encrypted[NonceSize..(NonceSize + TagSize)];
        var ciphertext = encrypted[(NonceSize + TagSize)..];
        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, aad);

        return Encoding.UTF8.GetString(plaintext);
    }

    private async Task<byte[]> GetKeyAsync(int keyId) =>
        Convert.FromBase64String(await secretProvider.GetSecretAsync($"DataEncryptionKey_{keyId}_Base64"));
}
