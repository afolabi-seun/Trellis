using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Vendor.Domain.Exceptions;

namespace Trellis.Api.Modules.Vendor.Domain.Entities;

public enum TenantVendorRelationshipStatus
{
    Active,
    Terminated,
}

/// <summary>
/// BDR-VEN-006 — the per-tenant engagement lifecycle a Vendor's platform-wide identity never has
/// on its own (see <see cref="VendorRecord"/>'s own doc comment). Terminating gates only *new*
/// <see cref="VendorAssignment"/> rows for this tenant+vendor pair going forward — it never
/// touches assignments that already existed, exactly as BDR-VEN-006's own text requires.
/// </summary>
public sealed class TenantVendorRelationship : ITenantOwned
{
    public Guid TenantVendorRelationshipId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid VendorId { get; private set; }
    public TenantVendorRelationshipStatus Status { get; private set; }
    public DateTimeOffset? TerminatedAt { get; private set; }
    public Guid? TerminatedByUserId { get; private set; }

    /// <summary>Gap-fill, same reasoning as Owner's own precedent (Phase 2) — the documented schema has no timestamp at all, but keyset pagination needs a stable, meaningful ordering key.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    private TenantVendorRelationship() { }

    public static TenantVendorRelationship Create(Guid tenantId, Guid vendorId) => new()
    {
        TenantVendorRelationshipId = Guid.NewGuid(),
        TenantId = tenantId,
        VendorId = vendorId,
        Status = TenantVendorRelationshipStatus.Active,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    public void Terminate(Guid terminatedByUserId)
    {
        if (Status != TenantVendorRelationshipStatus.Active)
        {
            throw new InvalidStatusTransitionException(
                "TenantVendorRelationship", Status.ToString(), TenantVendorRelationshipStatus.Terminated.ToString());
        }

        Status = TenantVendorRelationshipStatus.Terminated;
        TerminatedAt = DateTimeOffset.UtcNow;
        TerminatedByUserId = terminatedByUserId;
    }
}
