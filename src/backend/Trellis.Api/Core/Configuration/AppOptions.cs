using System.ComponentModel.DataAnnotations;

namespace Trellis.Api.Core.Configuration;

/// <summary>
/// App-wide, not module-specific — <see cref="BaseUrl"/> is the frontend origin every module's
/// outbound links (password reset, tenant data-export ready, a future Leasing application link)
/// get built against. Bound from a top-level <c>AppBaseUrl</c> env var, not
/// <c>Modules__{X}__*</c>, since it isn't owned by any one module.
/// </summary>
public class AppOptions
{
    [Required]
    [Url]
    public string BaseUrl { get; init; } = "http://localhost:5173";

    /// <summary>
    /// Comma-separated hosts the platform itself serves (the frontend BaseUrl's own host is always
    /// included). A tenant's custom domain may never be one of these or a subdomain of one, and the
    /// frontend uses the same list (VITE_PLATFORM_HOSTS) to decide whether it is running on a
    /// tenant's own domain.
    /// </summary>
    public string PlatformHosts { get; init; } = "localhost,127.0.0.1";

    /// <summary>
    /// Development/test only. Off by default: a tenant's custom domain is only ever an https origin in
    /// production. The e2e suite proves custom-domain rendering by mapping a <c>.test</c> hostname to
    /// localhost, which can only be served over plain http.
    /// </summary>
    public bool AllowInsecureCustomDomainOrigins { get; init; }

    public IReadOnlyList<string> GetPlatformHosts()
    {
        var hosts = PlatformHosts
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(h => h.ToLowerInvariant())
            .ToList();

        if (Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri))
        {
            hosts.Add(baseUri.Host.ToLowerInvariant());
        }

        return hosts.Distinct().ToList();
    }
}
