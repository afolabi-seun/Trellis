using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Trellis.Api.Core.Configuration;
using Trellis.Api.Core.Cors;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Modules.Tenant.Application.Interfaces;

namespace Trellis.Api.Modules.Tenant.Infrastructure;

/// <summary>
/// The CORS origin snapshot: every VERIFIED tenant domain, cached for a minute so a preflight is not
/// a database round-trip. Refreshed immediately when a domain is verified or removed
/// (<see cref="Invalidate"/>), so a newly verified domain works without waiting out the cache.
/// The query runs under SystemContext (it is cross-tenant by nature and no request tenant exists yet).
/// </summary>
public sealed class TenantDomainOriginProvider(
    IMemoryCache cache, ITenantDomainRepository repository, IOptions<AppOptions> appOptions) : ICustomDomainOriginProvider
{
    private const string CacheKey = "tenant-domain-origins";
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public async Task<bool> IsAllowedOriginAsync(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // Custom domains are https in production; plain http is accepted only when explicitly enabled for dev/test.
        var schemeOk = uri.Scheme == Uri.UriSchemeHttps
            || (uri.Scheme == Uri.UriSchemeHttp && appOptions.Value.AllowInsecureCustomDomainOrigins);
        if (!schemeOk)
        {
            return false;
        }

        var hosts = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            using var systemScope = SystemContext.Enter();
            return (await repository.ListAllVerifiedHostsAsync()).ToHashSet(StringComparer.Ordinal);
        });

        return hosts!.Contains(uri.Host.ToLowerInvariant());
    }

    public void Invalidate() => cache.Remove(CacheKey);
}
