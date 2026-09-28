using Trellis.Api.Core.Security;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Security;

/// <summary>BDR-LSE-002's application-link token — thin wrapper over Core's OpaqueTokenGenerator, shape-identical to Reporting's ShareLinkTokenGenerator. Stored as a SHA-256 hash; the raw value goes into the emailed link and is never persisted.</summary>
public static class ApplicationLinkTokenGenerator
{
    public static string GenerateRawToken() => OpaqueTokenGenerator.GenerateRawToken();

    public static string Hash(string rawToken) => OpaqueTokenGenerator.Hash(rawToken);
}
