using System.Security.Cryptography;

namespace Trellis.Api.Modules.Reporting.Infrastructure.Security;

/// <summary>
/// BDR-SEC-003's share-link token — mirrors Auth's own opaque-token pattern
/// (RandomNumberGenerator.GetBytes(32)) but Base64Url-encoded (URL-path-safe) instead of plain
/// Base64, since this token is a URL path segment, not a header/cookie value. Stored as a SHA-256
/// hash (TokenHash); the raw value returned here is shown to the caller exactly once and never
/// persisted in plaintext — identical posture to Auth's own password-reset tokens.
/// </summary>
public static class ShareLinkTokenGenerator
{
    public static string GenerateRawToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
}
