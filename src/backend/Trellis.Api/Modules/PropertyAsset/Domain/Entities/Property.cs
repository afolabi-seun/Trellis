using Trellis.Api.Core.Data;
using Trellis.Api.Modules.PropertyAsset.Domain.Exceptions;

namespace Trellis.Api.Modules.PropertyAsset.Domain.Entities;

/// <summary>data_model.md's `property.property_type`.</summary>
public enum PropertyType
{
    Residential,
    Commercial,
    MixedUse,
}

public enum PropertyStatus
{
    Active,
    Archived,
}

/// <summary>
/// data_model.md's `trellis_property.property` — BDR-AST-001/002. Carries
/// <see cref="SizeSqft"/>/<see cref="AcquisitionDate"/>/<see cref="AcquisitionValue"/>/
/// <see cref="CurrentValuation"/>, added this phase to close a gap between the Must-priority
/// BDR-AST-002 ("address, type, size, acquisition/valuation data, status") and the schema as
/// originally documented, which only had address/type/status.
/// </summary>
public sealed class Property : ITenantOwned
{
    public Guid PropertyId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Address { get; private set; } = null!;
    public PropertyType PropertyType { get; private set; }
    public PropertyStatus Status { get; private set; }
    public decimal? SizeSqft { get; private set; }
    public DateOnly? AcquisitionDate { get; private set; }
    public decimal? AcquisitionValue { get; private set; }
    public decimal? CurrentValuation { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Property() { }

    public static Property Create(
        Guid tenantId, Guid ownerId, string address, PropertyType propertyType,
        decimal? sizeSqft, DateOnly? acquisitionDate, decimal? acquisitionValue, decimal? currentValuation)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Address is required.", nameof(address));
        }

        var now = DateTimeOffset.UtcNow;
        return new Property
        {
            PropertyId = Guid.NewGuid(),
            TenantId = tenantId,
            OwnerId = ownerId,
            Address = address,
            PropertyType = propertyType,
            Status = PropertyStatus.Active,
            SizeSqft = sizeSqft,
            AcquisitionDate = acquisitionDate,
            AcquisitionValue = acquisitionValue,
            CurrentValuation = currentValuation,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Update(
        Guid ownerId, string address, PropertyType propertyType,
        decimal? sizeSqft, DateOnly? acquisitionDate, decimal? acquisitionValue, decimal? currentValuation)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Address is required.", nameof(address));
        }

        OwnerId = ownerId;
        Address = address;
        PropertyType = propertyType;
        SizeSqft = sizeSqft;
        AcquisitionDate = acquisitionDate;
        AcquisitionValue = acquisitionValue;
        CurrentValuation = currentValuation;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Never a hard delete — BDR-AST-003's documents/inspections/workflow history attach at this record.</summary>
    public void Archive()
    {
        if (Status != PropertyStatus.Active)
        {
            throw new InvalidStatusTransitionException("Property", Status.ToString(), PropertyStatus.Archived.ToString());
        }

        Status = PropertyStatus.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
