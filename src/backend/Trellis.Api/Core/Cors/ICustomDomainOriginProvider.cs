namespace Trellis.Api.Core.Cors;

/// <summary>
/// Lets Core's CORS policy accept a tenant's verified custom-domain origin without Core depending
/// on any module: the Tenant module implements this (it owns the domain table), Core only consumes
/// the interface — the same direction as ISecretProvider.
/// </summary>
public interface ICustomDomainOriginProvider
{
    /// <summary>True when <paramref name="origin"/> (e.g. <c>https://listings.acme.com</c>) is a VERIFIED tenant domain.</summary>
    Task<bool> IsAllowedOriginAsync(string origin);

    /// <summary>Drops the cached origin snapshot — called when a domain is verified or removed so the change takes effect at once.</summary>
    void Invalidate();
}
