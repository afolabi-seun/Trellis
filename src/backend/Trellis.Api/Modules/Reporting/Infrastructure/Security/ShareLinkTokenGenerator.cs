using Trellis.Api.Core.Security;

namespace Trellis.Api.Modules.Reporting.Infrastructure.Security;

/// <summary>
/// BDR-SEC-003's share-link token. Thin wrapper around <see cref="OpaqueTokenGenerator"/> (moved
/// there in Phase 9 once Leasing needed the identical mechanics for its own application-link
/// tokens) — kept here, under this name, so no existing caller's `using` needs to change.
/// </summary>
public static class ShareLinkTokenGenerator
{
    public static string GenerateRawToken() => OpaqueTokenGenerator.GenerateRawToken();

    public static string Hash(string rawToken) => OpaqueTokenGenerator.Hash(rawToken);
}
