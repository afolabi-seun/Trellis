namespace Trellis.Api.Modules.Auth.Application.Interfaces;

/// <summary>security_guide.md's MFA — TOTP enrollment/verification and AES-256-GCM secret encryption.</summary>
public interface ITotpService
{
    /// <summary>Generates a new base32 TOTP secret and its `otpauth://` URI, for showing a QR code at enrollment.</summary>
    (string SecretBase32, string OtpAuthUri) GenerateSecret(string accountLabel);

    /// <summary>30-second window, ±1 step tolerance (security_guide.md).</summary>
    bool ValidateCode(string secretBase32, string totpCode);

    Task<byte[]> EncryptAsync(string secretBase32);

    Task<string> DecryptAsync(byte[] encrypted);
}
