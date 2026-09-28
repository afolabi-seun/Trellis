using Trellis.Api.Core.Data;
using Trellis.Api.Modules.RentLedger.Domain.Exceptions;

namespace Trellis.Api.Modules.RentLedger.Domain.Entities;

/// <summary>
/// BDR-RENT-002 — a staff member's record that a rent payment was received OUTSIDE the platform
/// (bank transfer, cash, an external app). A record of an external event, not a transaction:
/// nothing here is processed, held, or moved (BDR-RENT-006). The amount, date, and reference are
/// immutable once recorded; a mistaken entry is corrected by voiding it (excluded from every total,
/// kept for audit) rather than editing or deleting it. MethodReference is free text — there is
/// deliberately no field shaped like a card or account number.
/// </summary>
public sealed class RentPaymentRecord : ITenantOwned
{
    public Guid RentPaymentRecordId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid RentScheduleId { get; private set; }
    public decimal AmountReceived { get; private set; }
    public DateOnly DateReceived { get; private set; }
    public string? MethodReference { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public DateTimeOffset? VoidedAt { get; private set; }
    public Guid? VoidedBy { get; private set; }
    public string? VoidReason { get; private set; }

    public bool IsVoided => VoidedAt is not null;

    private RentPaymentRecord() { }

    public static RentPaymentRecord Create(
        Guid tenantId, Guid rentScheduleId, decimal amountReceived, DateOnly dateReceived,
        string? methodReference, Guid recordedByUserId)
    {
        if (amountReceived <= 0)
        {
            throw new ArgumentException("Amount received must be positive.", nameof(amountReceived));
        }

        return new RentPaymentRecord
        {
            RentPaymentRecordId = Guid.NewGuid(),
            TenantId = tenantId,
            RentScheduleId = rentScheduleId,
            AmountReceived = amountReceived,
            DateReceived = dateReceived,
            MethodReference = methodReference,
            RecordedByUserId = recordedByUserId,
            RecordedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Void(Guid voidedBy, string reason)
    {
        if (IsVoided)
        {
            throw new InvalidStatusTransitionException(nameof(RentPaymentRecord), "Voided", "Voided");
        }

        VoidedAt = DateTimeOffset.UtcNow;
        VoidedBy = voidedBy;
        VoidReason = reason;
    }
}
