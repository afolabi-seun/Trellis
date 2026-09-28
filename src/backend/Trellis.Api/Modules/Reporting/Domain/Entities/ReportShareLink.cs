using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Reporting.Domain.Exceptions;

namespace Trellis.Api.Modules.Reporting.Domain.Entities;

public enum ReportShareLinkStatus
{
    Active,
    Revoked,
}

/// <summary>
/// BDR-SEC-003 — a time-limited, unauthenticated public link to one <see cref="OwnerReport"/>'s
/// PDF. <see cref="TokenHash"/> stores only the SHA-256 hash of the raw token (Auth's own
/// password-reset-token posture) — the raw token is returned to the caller exactly once, at
/// creation, and never persisted in plaintext. <see cref="TenantId"/> is denormalized from the
/// linked <see cref="OwnerReport"/> so the standard <c>tenant_isolation</c> RLS policy applies
/// here too — deliberately not <c>WebhookEvent</c>'s <c>system_only</c> shape, since a share link
/// genuinely belongs to a tenant (unlike a Paystack webhook identity, which has none); only its
/// anonymous *resolution* path runs cross-tenant, under <c>SystemContext.Enter()</c>.
/// </summary>
public sealed class ReportShareLink : ITenantOwned
{
    public Guid ReportShareLinkId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OwnerReportId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public ReportShareLinkStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? LastAccessedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ReportShareLink() { }

    public static ReportShareLink Create(Guid tenantId, Guid ownerReportId, string tokenHash, DateTimeOffset expiresAt, Guid createdBy) => new()
    {
        ReportShareLinkId = Guid.NewGuid(),
        TenantId = tenantId,
        OwnerReportId = ownerReportId,
        TokenHash = tokenHash,
        Status = ReportShareLinkStatus.Active,
        ExpiresAt = expiresAt,
        CreatedBy = createdBy,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    /// <summary>No guard — called on every successful resolve; "last accessed" is the useful signal, not a state transition.</summary>
    public void RecordAccess() => LastAccessedAt = DateTimeOffset.UtcNow;

    public void Revoke()
    {
        if (Status != ReportShareLinkStatus.Active)
        {
            throw new InvalidStatusTransitionException(nameof(ReportShareLink), Status.ToString(), ReportShareLinkStatus.Revoked.ToString());
        }

        Status = ReportShareLinkStatus.Revoked;
    }

    /// <summary>Resolution's own combined liveness check — active, not expired. A separate method rather than three call sites re-deriving the same two-part condition.</summary>
    public bool IsUsable() => Status == ReportShareLinkStatus.Active && ExpiresAt > DateTimeOffset.UtcNow;
}
