using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Reporting.Domain.Exceptions;

namespace Trellis.Api.Modules.Reporting.Domain.Entities;

public enum OwnerReportStatus
{
    Generating,
    Ready,
    Failed,
}

public enum OwnerReportPeriodType
{
    Monthly,
    Quarterly,
}

/// <summary>
/// BDR-OWN-002/003/004 — a point-in-time snapshot handed to a Property Owner, generated on
/// demand (no background job: QuestPDF rendering is fast enough to run inline within the
/// request). <see cref="OwnerName"/> is denormalized at generation time, not read live from
/// PropertyAsset's Owner — a report already emailed/shared must keep showing the name (and every
/// other figure) it showed at generation time, even if the Owner record is renamed afterward.
///
/// The financial section is <see cref="TotalMaintenanceCost"/> — a sum of Workflow's own
/// WorkflowInstance.CostAmount for this Owner's properties — not "income" or "expense" figures,
/// which nothing in Trellis records. Rent collection (BDR-RENT-005) adds a separate, nullable
/// rent section: how many of the Owner's rent schedules are current, overdue, or in arrears as of
/// generation, and the rent recorded as received (outside Trellis — Trellis never processes or
/// holds funds) during the period, broken down by currency (never summed across currencies) since a
/// single Owner's units can be let in more than one. All Rent* fields are NULL when the tenant isn't
/// entitled to the Rent Collection add-on — "not tracked", never a misleading zero — and are stored
/// as scalar snapshots (not re-queried) so a CSV re-download reproduces the figures the PDF showed.
/// MaintenanceCurrency labels TotalMaintenanceCost with the tenant's own default currency
/// (WorkflowInstance.CostAmount itself carries no currency) and is always set.
/// </summary>
public sealed class OwnerReport : ITenantOwned
{
    public Guid OwnerReportId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OwnerId { get; private set; }
    public string OwnerName { get; private set; } = null!;
    public OwnerReportPeriodType PeriodType { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public OwnerReportStatus Status { get; private set; }
    public int PropertyCount { get; private set; }
    public decimal TotalMaintenanceCost { get; private set; }
    public string MaintenanceCurrency { get; private set; } = null!;
    public int OpenMaintenanceCount { get; private set; }
    public int CompliantDocumentCount { get; private set; }
    public int ExpiringDocumentCount { get; private set; }
    public int? RentCurrentCount { get; private set; }
    public int? RentOverdueCount { get; private set; }
    public int? RentArrearsCount { get; private set; }
    /// <summary>JSON-serialized `IReadOnlyList&lt;CurrencyAmount&gt;` — a plain scalar column, not a queryable structure, so RentLedger's own DTO shape is reused as-is rather than mapping to something Reporting-specific.</summary>
    public string? RentReceivedByCurrencyJson { get; private set; }
    public DateOnly? RentAsOf { get; private set; }
    public byte[]? PdfContent { get; private set; }
    public Guid GeneratedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadyAt { get; private set; }

    private OwnerReport() { }

    public static OwnerReport Create(
        Guid tenantId, Guid ownerId, string ownerName, OwnerReportPeriodType periodType,
        DateOnly periodStart, DateOnly periodEnd, Guid generatedBy) => new()
        {
            OwnerReportId = Guid.NewGuid(),
            TenantId = tenantId,
            OwnerId = ownerId,
            OwnerName = ownerName,
            PeriodType = periodType,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Status = OwnerReportStatus.Generating,
            GeneratedBy = generatedBy,
            MaintenanceCurrency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
        };

    /// <summary>Immutable once Ready — a re-generation for the same Owner/period creates a new row, never overwrites this one.</summary>
    public void MarkReady(
        int propertyCount, decimal totalMaintenanceCost, string maintenanceCurrency, int openMaintenanceCount,
        int compliantDocumentCount, int expiringDocumentCount, byte[] pdfContent,
        int? rentCurrentCount = null, int? rentOverdueCount = null, int? rentArrearsCount = null,
        string? rentReceivedByCurrencyJson = null, DateOnly? rentAsOf = null)
    {
        if (Status != OwnerReportStatus.Generating)
        {
            throw new InvalidStatusTransitionException(nameof(OwnerReport), Status.ToString(), OwnerReportStatus.Ready.ToString());
        }

        PropertyCount = propertyCount;
        TotalMaintenanceCost = totalMaintenanceCost;
        MaintenanceCurrency = maintenanceCurrency;
        OpenMaintenanceCount = openMaintenanceCount;
        CompliantDocumentCount = compliantDocumentCount;
        ExpiringDocumentCount = expiringDocumentCount;
        RentCurrentCount = rentCurrentCount;
        RentOverdueCount = rentOverdueCount;
        RentArrearsCount = rentArrearsCount;
        RentReceivedByCurrencyJson = rentReceivedByCurrencyJson;
        RentAsOf = rentAsOf;
        PdfContent = pdfContent;
        Status = OwnerReportStatus.Ready;
        ReadyAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed()
    {
        if (Status != OwnerReportStatus.Generating)
        {
            throw new InvalidStatusTransitionException(nameof(OwnerReport), Status.ToString(), OwnerReportStatus.Failed.ToString());
        }

        Status = OwnerReportStatus.Failed;
    }

    /// <summary>POPIA erasure — OwnerName is a denormalized snapshot (deliberately never re-synced from Owner, per this class's own field), and the rendered PdfContent embeds it too, so both are cleared; the row and its financial figures are kept, matching the retained-not-erased rent/maintenance history the PROPERTYASSET/Leasing handlers leave alone.</summary>
    public void RedactOwnerPii()
    {
        OwnerName = "Erased owner";
        PdfContent = null;
    }
}
