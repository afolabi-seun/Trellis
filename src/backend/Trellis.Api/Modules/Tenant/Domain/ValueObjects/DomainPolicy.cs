using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Trellis.Api.Modules.Tenant.Domain.ValueObjects;

/// <summary>
/// Validation for a tenant's custom domain. A registrable-looking fully-qualified hostname only: no
/// scheme/port/path, no IP address, no single-label host like <c>localhost</c>, and never the platform's
/// own host or a subdomain of it (a tenant must not be able to claim <c>login.trellis…</c>).
/// </summary>
public static partial class DomainPolicy
{
    public const int MaxLength = 253;

    [GeneratedRegex("^([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\\.)+[a-z]{2,63}$")]
    private static partial Regex Fqdn();

    /// <summary>Lowercased, trimmed, trailing dot removed, IDN converted to punycode. Null if it isn't a plausible hostname at all.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var host = input.Trim().TrimEnd('.').ToLowerInvariant();
        if (host.Length is 0 or > MaxLength || host.Contains('/') || host.Contains(':') || host.Contains(' '))
        {
            return null;
        }

        try
        {
            host = new IdnMapping().GetAscii(host);
        }
        catch (ArgumentException)
        {
            return null;
        }

        return host;
    }

    public static bool IsValid(string normalizedHost, IReadOnlyList<string> platformHosts) =>
        normalizedHost.Length <= MaxLength
        && Fqdn().IsMatch(normalizedHost)
        && !IPAddress.TryParse(normalizedHost, out _)
        && !platformHosts.Any(p => normalizedHost == p || normalizedHost.EndsWith("." + p, StringComparison.Ordinal));
}
