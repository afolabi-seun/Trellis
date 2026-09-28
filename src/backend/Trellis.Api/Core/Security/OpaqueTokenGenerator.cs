using System.Security.Cryptography;
using System.Text;

namespace Trellis.Api.Core.Security;

/// <summary>
/// Byte-for-byte identical, stateless crypto logic — moved here from Reporting's own
/// ShareLinkTokenGenerator (Phase 8) once Leasing (Phase 9) needed the exact same mechanics for
/// its own application-link tokens. Unlike AuditService/InvalidStatusTransitionException (real
/// per-module concepts, correctly duplicated per module), there is zero module-specific variation
/// in "generate a random opaque token, hash it for storage" — a second hand copy would be
/// copy-paste, not module ownership. Core gains no dependency on either module by hosting this.
/// </summary>
public static class OpaqueTokenGenerator
{
    public static string GenerateRawToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
}
