using System.Security.Cryptography;
using System.Text;
using OtpNet;
using Trellis.Api.Core.Secrets;
using Trellis.Api.Modules.Auth.Application.Interfaces;

namespace Trellis.Api.Modules.Auth.Infrastructure;

/// <summary>
/// security_guide.md's MFA: 30-second TOTP window with ±1 step tolerance, and AES-256-GCM
/// encryption of the stored secret using a single platform-level key from
/// <see cref="ISecretProvider"/> (per-tenant TOTP key isolation deferred alongside per-tenant
/// PII keys, same as the rest of Phase 1's encryption posture).
/// </summary>
public sealed class TotpService(ISecretProvider secretProvider) : ITotpService
{
    private const string EncryptionKeySecretName = "MfaEncryptionKeyBase64";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public (string SecretBase32, string OtpAuthUri) GenerateSecret(string accountLabel)
    {
        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var secretBase32 = Base32Encoding.ToString(secretBytes);
        var otpAuthUri = $"otpauth://totp/Trellis:{Uri.EscapeDataString(accountLabel)}?secret={secretBase32}&issuer=Trellis";
        return (secretBase32, otpAuthUri);
    }

    public bool ValidateCode(string secretBase32, string totpCode)
    {
        var totp = new Totp(Base32Encoding.ToBytes(secretBase32));
        return totp.VerifyTotp(totpCode, out _, new VerificationWindow(previous: 1, future: 1));
    }

    public async Task<byte[]> EncryptAsync(string secretBase32)
    {
        var key = await GetKeyAsync();
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintext = Encoding.UTF8.GetBytes(secretBase32);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        // nonce | tag | ciphertext — all needed to decrypt, none of it secret on its own.
        return [.. nonce, .. tag, .. ciphertext];
    }

    public async Task<string> DecryptAsync(byte[] encrypted)
    {
        var key = await GetKeyAsync();
        var nonce = encrypted[..NonceSize];
        var tag = encrypted[NonceSize..(NonceSize + TagSize)];
        var ciphertext = encrypted[(NonceSize + TagSize)..];
        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    private async Task<byte[]> GetKeyAsync() =>
        Convert.FromBase64String(await secretProvider.GetSecretAsync(EncryptionKeySecretName));
}
