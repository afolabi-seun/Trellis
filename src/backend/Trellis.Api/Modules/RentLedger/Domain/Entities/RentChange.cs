using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.RentLedger.Domain.Entities;

/// <summary>
/// A dated change to a schedule's rent — today only from an accepted lease renewal (BDR-LSE-010).
/// The schedule's own RentAmount covers every period before the first change; each change covers
/// periods due on or after its EffectiveFrom until the next one. Periods already past keep the
/// amount that applied to them, so a renewal at a higher rent never reprices history.
/// </summary>
public sealed class RentChange : ITenantOwned
{
    public Guid RentChangeId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid RentScheduleId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public decimal RentAmount { get; private set; }

    /// <summary>Leasing's renewal offer that produced it — an id only, no cross-schema reference.</summary>
    public Guid? RenewalOfferId { get; private set; }

    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private RentChange() { }

    internal static RentChange Create(
        Guid tenantId, Guid rentScheduleId, DateOnly effectiveFrom, decimal rentAmount, Guid? renewalOfferId, Guid createdBy) => new()
        {
            RentChangeId = Guid.NewGuid(),
            TenantId = tenantId,
            RentScheduleId = rentScheduleId,
            EffectiveFrom = effectiveFrom,
            RentAmount = rentAmount,
            RenewalOfferId = renewalOfferId,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    internal void Correct(decimal rentAmount) => RentAmount = rentAmount;
}
