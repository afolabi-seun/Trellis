namespace Trellis.Api.Modules.RentLedger.Domain.ValueObjects;

/// <summary>BDR-RENT-003/005's trio: nothing unpaid that's due (Current), something unpaid but every unpaid period still inside its grace window (Overdue), or something unpaid past its grace period (InArrears).</summary>
public enum RentStatus
{
    Current,
    Overdue,
    InArrears,
}

/// <param name="Status">The computed status as of the evaluation date.</param>
/// <param name="PeriodsInArrears">How many whole rent periods are unpaid AND past grace (0 unless InArrears).</param>
/// <param name="AmountShort">Rent for periods already due minus the total recorded as received (never negative).</param>
/// <param name="OldestUnpaidDueDate">The due date of the earliest period the recorded payments don't yet cover; null when nothing due is unpaid.</param>
/// <param name="LatestPastGraceDue">Due date of the latest period that is past its grace window — the alert dedupe key when InArrears; null otherwise.</param>
public sealed record RentEvaluation(
    RentStatus Status, int PeriodsInArrears, decimal AmountShort, DateOnly? OldestUnpaidDueDate, DateOnly? LatestPastGraceDue);
