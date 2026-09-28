using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Tenant.Domain.Exceptions;

namespace Trellis.Api.Modules.Tenant.Domain.Entities;

public enum TenantDomainStatus
{
    PendingVerification,
    Verified,
    Disabled,
}

/// <summary>
/// A tenant's own domain for its public pages (ENTERPRISE tier — the Handbook's stated trigger for
/// custom domains). Registering a domain proves nothing, so it starts PENDING_VERIFICATION and only
/// becomes usable — for host resolution, CORS, and the base URL of emailed links — after the tenant
/// publishes <c>_trellis-verify.{domain}</c> TXT = <c>trellis-verify={token}</c>. Uniqueness is
/// enforced only across VERIFIED rows (a partial unique index in the migration): anyone can register a
/// name, but only whoever can actually prove control of the DNS keeps it.
/// </summary>
public sealed class TenantDomain : ITenantOwned
{
    public Guid TenantDomainId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Domain { get; private set; } = null!;
    public TenantDomainStatus Status { get; private set; }
    public string VerificationToken { get; private set; } = null!;
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private TenantDomain() { }

    public static TenantDomain Create(Guid tenantId, string normalizedDomain, string verificationToken) => new()
    {
        TenantDomainId = Guid.NewGuid(),
        TenantId = tenantId,
        Domain = normalizedDomain,
        Status = TenantDomainStatus.PendingVerification,
        VerificationToken = verificationToken,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    /// <summary>The exact TXT value the tenant must publish at <c>_trellis-verify.{domain}</c>.</summary>
    public string ExpectedTxtValue => $"trellis-verify={VerificationToken}";

    public void MarkVerified()
    {
        if (Status == TenantDomainStatus.Verified)
        {
            return;
        }

        if (Status != TenantDomainStatus.PendingVerification)
        {
            throw new InvalidTenantStatusTransitionException(Status.ToString(), TenantDomainStatus.Verified.ToString());
        }

        Status = TenantDomainStatus.Verified;
        VerifiedAt = DateTimeOffset.UtcNow;
    }
}
