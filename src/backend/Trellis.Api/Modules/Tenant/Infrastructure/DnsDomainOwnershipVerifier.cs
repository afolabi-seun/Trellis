using DnsClient;
using Trellis.Api.Modules.Tenant.Application.Interfaces;

namespace Trellis.Api.Modules.Tenant.Infrastructure;

/// <summary>
/// The real proof of domain control: a DNS TXT lookup. A lookup that fails for any reason (NXDOMAIN,
/// timeout, SERVFAIL) is "not verified", never "verified" — this fails closed, and the tenant simply
/// retries once DNS has propagated.
/// </summary>
public sealed class DnsDomainOwnershipVerifier : IDomainOwnershipVerifier
{
    private static readonly LookupClient Client = new(new LookupClientOptions
    {
        Timeout = TimeSpan.FromSeconds(5),
        Retries = 1,
        UseCache = false,
    });

    public async Task<bool> HasTxtRecordAsync(string recordName, string expectedValue)
    {
        try
        {
            var response = await Client.QueryAsync(recordName, QueryType.TXT);
            return response.Answers.TxtRecords().Any(r => r.Text.Any(t => string.Equals(t.Trim(), expectedValue, StringComparison.Ordinal)));
        }
        catch (DnsResponseException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
