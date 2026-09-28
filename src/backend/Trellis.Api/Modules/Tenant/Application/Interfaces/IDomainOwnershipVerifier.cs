namespace Trellis.Api.Modules.Tenant.Application.Interfaces;

/// <summary>Proves a tenant controls a domain: the DNS TXT record at <paramref name="recordName"/> must contain exactly <paramref name="expectedValue"/>. A seam so tests never touch real DNS.</summary>
public interface IDomainOwnershipVerifier
{
    Task<bool> HasTxtRecordAsync(string recordName, string expectedValue);
}
