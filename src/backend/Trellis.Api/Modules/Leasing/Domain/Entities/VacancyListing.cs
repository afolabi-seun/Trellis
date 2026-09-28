using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Leasing.Domain.Exceptions;

namespace Trellis.Api.Modules.Leasing.Domain.Entities;

public enum VacancyListingStatus
{
    Listed,
    UnderApplication,
    Leased,
    Withdrawn,
}

/// <summary>
/// BDR-LSE-001 — a Unit/Asset marketed as available for lease. <see cref="UnitAssetId"/> is
/// unique while this listing is LISTED or UNDER_APPLICATION (a Postgres partial unique index in
/// the owning migration, not expressible via EF Core's fluent API).
/// </summary>
public sealed class VacancyListing : ITenantOwned
{
    public Guid VacancyListingId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UnitAssetId { get; private set; }
    public decimal RentAmount { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateOnly AvailableFrom { get; private set; }
    public string? Description { get; private set; }
    public VacancyListingStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private VacancyListing() { }

    public static VacancyListing Create(
        Guid tenantId, Guid unitAssetId, decimal rentAmount, string currency,
        DateOnly availableFrom, string? description, Guid createdBy)
    {
        if (rentAmount <= 0)
        {
            throw new ArgumentException("Rent amount must be positive.", nameof(rentAmount));
        }

        if (currency.Length != 3)
        {
            throw new ArgumentException("Currency must be a 3-letter ISO 4217 code.", nameof(currency));
        }

        var now = DateTimeOffset.UtcNow;
        return new VacancyListing
        {
            VacancyListingId = Guid.NewGuid(),
            TenantId = tenantId,
            UnitAssetId = unitAssetId,
            RentAmount = rentAmount,
            Currency = currency.ToUpperInvariant(),
            AvailableFrom = availableFrom,
            Description = description,
            Status = VacancyListingStatus.Listed,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>Only while LISTED — an in-flight application (UNDER_APPLICATION) locks the commercial terms so a mid-review rent change can't invalidate a screening already run against the old figure.</summary>
    public void Update(decimal rentAmount, string currency, DateOnly availableFrom, string? description)
    {
        if (Status != VacancyListingStatus.Listed)
        {
            throw new InvalidStatusTransitionException(nameof(VacancyListing), Status.ToString(), Status.ToString());
        }

        if (rentAmount <= 0)
        {
            throw new ArgumentException("Rent amount must be positive.", nameof(rentAmount));
        }

        RentAmount = rentAmount;
        Currency = currency.ToUpperInvariant();
        AvailableFrom = availableFrom;
        Description = description;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Idempotent, not exception-guarded — multiple applicants may submit against one listing (the partial unique index constrains UnitAsset-to-active-listing, never listing-to-application-count), so a 2nd..nth submission's own transition is a harmless no-op.</summary>
    public void MarkUnderApplication()
    {
        if (Status != VacancyListingStatus.Listed)
        {
            return;
        }

        Status = VacancyListingStatus.UnderApplication;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Withdraw()
    {
        if (Status is not (VacancyListingStatus.Listed or VacancyListingStatus.UnderApplication))
        {
            throw new InvalidStatusTransitionException(nameof(VacancyListing), Status.ToString(), VacancyListingStatus.Withdrawn.ToString());
        }

        Status = VacancyListingStatus.Withdrawn;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>BDR-LSE-006 — fired only by LeasingWebhookService on e-signature completion, never a direct staff action.</summary>
    public void MarkLeased()
    {
        if (Status is not (VacancyListingStatus.Listed or VacancyListingStatus.UnderApplication))
        {
            throw new InvalidStatusTransitionException(nameof(VacancyListing), Status.ToString(), VacancyListingStatus.Leased.ToString());
        }

        Status = VacancyListingStatus.Leased;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool AcceptsApplications() => Status is VacancyListingStatus.Listed or VacancyListingStatus.UnderApplication;
}
