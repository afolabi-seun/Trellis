using Trellis.Api.Core.Data;
using Trellis.Api.Modules.PropertyAsset.Domain.Exceptions;

namespace Trellis.Api.Modules.PropertyAsset.Domain.Entities;

/// <summary>data_model.md's `trellis_property.owner` — filled in this phase; the Handbook only ever referenced it as an FK target from `property`, never defined its own columns.</summary>
public enum OwnerStatus
{
    Active,
    Archived,
}

/// <summary>
/// BDR-OWN-001 — a PM company's record of a property owner. Owners never authenticate
/// (BDR-OWN-002); this is a plain tenant-owned record, not an `app_user` row.
/// </summary>
public sealed class Owner : ITenantOwned
{
    public Guid OwnerId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public OwnerStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Owner() { }

    public static Owner Create(Guid tenantId, string name, string? email, string? phone)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Owner name is required.", nameof(name));
        }

        var now = DateTimeOffset.UtcNow;
        return new Owner
        {
            OwnerId = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Email = email,
            Phone = phone,
            Status = OwnerStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Update(string name, string? email, string? phone)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Owner name is required.", nameof(name));
        }

        Name = name;
        Email = email;
        Phone = phone;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Never a hard delete — a property's owner_id FK must keep resolving even for a retired owner.</summary>
    public void Archive()
    {
        if (Status != OwnerStatus.Active)
        {
            throw new InvalidStatusTransitionException("Owner", Status.ToString(), OwnerStatus.Archived.ToString());
        }

        Status = OwnerStatus.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>POPIA erasure — Name is NOT NULL (Create/Update both guard against blank), so it's tombstoned rather than cleared; the row is kept, never Archived by this alone, since Property.OwnerId must keep resolving regardless of the owner's own status.</summary>
    public void RedactPii()
    {
        Name = "Erased owner";
        Email = null;
        Phone = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
