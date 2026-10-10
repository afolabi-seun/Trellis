using Trellis.Api.Core.Data;
using Trellis.Api.Modules.PropertyAsset.Domain.Exceptions;

namespace Trellis.Api.Modules.PropertyAsset.Domain.Entities;

public enum UnitAssetStatus
{
    Active,
    Archived,
}

/// <summary>
/// A separate dimension from <see cref="UnitAssetStatus"/> (Active/Archived governs whether a
/// unit is in the property's active inventory at all; this governs whether someone lives there) —
/// conflating both into one enum would make "Active" and "Occupied" mutually exclusive when they
/// aren't. Added Phase 9 (Leasing, BDR-LSE-006).
/// </summary>
public enum UnitAssetOccupancyStatus
{
    Vacant,
    Occupied,
}

/// <summary>
/// data_model.md's `trellis_property.unit_asset` — deliberately one table covering both rental
/// units ("Unit 4B") and physical assets ("Rooftop HVAC"), per BDR-AST-001's single
/// Tenant → Owner → Property → Unit/Asset hierarchy level. <see cref="TenantId"/> is denormalised
/// (always matches the parent property's) purely so RLS can filter this table directly without a
/// join back to <c>property</c> on every query. <see cref="CreatedAt"/>/<see cref="UpdatedAt"/>
/// weren't in the originally documented schema (which had no timestamps at all) — added this
/// phase since keyset pagination needs a stable, meaningful ordering key, the same reason every
/// other paginated entity in this codebase carries one.
/// </summary>
public sealed class UnitAsset : ITenantOwned
{
    public Guid UnitAssetId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PropertyId { get; private set; }
    public string Label { get; private set; } = null!;
    public UnitAssetStatus Status { get; private set; }
    public UnitAssetOccupancyStatus OccupancyStatus { get; private set; } = UnitAssetOccupancyStatus.Vacant;

    /// <summary>
    /// BDR-AST-005 — the date this unit most recently became vacant; null while occupied, and null
    /// for a vacant unit whose vacancy start is genuinely unknown (units that predate lease
    /// records). Never guessed: a unit created today is vacant since today, a unit whose lease
    /// ended is vacant since that lease's end date, and nothing else sets it.
    /// </summary>
    public DateOnly? VacantSince { get; private set; }
    public decimal? SizeSqft { get; private set; }
    public decimal? AcquisitionValue { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private UnitAsset() { }

    public static UnitAsset Create(
        Guid tenantId, Guid propertyId, string label, decimal? sizeSqft, decimal? acquisitionValue)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("Label is required.", nameof(label));
        }

        var now = DateTimeOffset.UtcNow;
        return new UnitAsset
        {
            UnitAssetId = Guid.NewGuid(),
            TenantId = tenantId,
            PropertyId = propertyId,
            Label = label,
            Status = UnitAssetStatus.Active,
            VacantSince = DateOnly.FromDateTime(now.UtcDateTime),
            SizeSqft = sizeSqft,
            AcquisitionValue = acquisitionValue,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Update(string label, decimal? sizeSqft, decimal? acquisitionValue)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("Label is required.", nameof(label));
        }

        Label = label;
        SizeSqft = sizeSqft;
        AcquisitionValue = acquisitionValue;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Never a hard delete — BDR-AST-003's documents/inspections/workflow history attach at this record.</summary>
    public void Archive()
    {
        if (Status != UnitAssetStatus.Active)
        {
            throw new InvalidStatusTransitionException("UnitAsset", Status.ToString(), UnitAssetStatus.Archived.ToString());
        }

        Status = UnitAssetStatus.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Only ever called by LeaseService when a lease starts — a unit is OCCUPIED exactly when it has an ACTIVE lease.</summary>
    public void MarkOccupied()
    {
        if (OccupancyStatus != UnitAssetOccupancyStatus.Vacant)
        {
            throw new InvalidStatusTransitionException("UnitAsset", OccupancyStatus.ToString(), UnitAssetOccupancyStatus.Occupied.ToString());
        }

        OccupancyStatus = UnitAssetOccupancyStatus.Occupied;
        VacantSince = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Only ever called by LeaseService when a lease ends, with that lease's end date.</summary>
    public void MarkVacant(DateOnly vacantSince)
    {
        if (OccupancyStatus != UnitAssetOccupancyStatus.Occupied)
        {
            throw new InvalidStatusTransitionException("UnitAsset", OccupancyStatus.ToString(), UnitAssetOccupancyStatus.Vacant.ToString());
        }

        OccupancyStatus = UnitAssetOccupancyStatus.Vacant;
        VacantSince = vacantSince;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
