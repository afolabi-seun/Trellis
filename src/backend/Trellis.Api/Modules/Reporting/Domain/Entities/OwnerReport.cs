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
/// WorkflowInstance.CostAmount for this Owner's properties — not "income" or "expense" figures.
/// This is a deliberate scope correction made while implementing this entity, not the original
/// plan: no rent-collection or ledger module exists anywhere in this codebase yet (Leasing/
/// RentLedger are still unbuilt Add-on modules per project_architecture.md's Module Catalogue),
/// so there is no real income data to report. roadmap.md's own Phase 8 framing — "Reporting waits
/// for real usage data" — argues directly against fabricating an income figure here; maintenance
/// cost is the one real financial number this codebase can actually produce today.
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
    public int OpenMaintenanceCount { get; private set; }
    public int CompliantDocumentCount { get; private set; }
    public int ExpiringDocumentCount { get; private set; }
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
            CreatedAt = DateTimeOffset.UtcNow,
        };

    /// <summary>Immutable once Ready — a re-generation for the same Owner/period creates a new row, never overwrites this one.</summary>
    public void MarkReady(
        int propertyCount, decimal totalMaintenanceCost, int openMaintenanceCount,
        int compliantDocumentCount, int expiringDocumentCount, byte[] pdfContent)
    {
        if (Status != OwnerReportStatus.Generating)
        {
            throw new InvalidStatusTransitionException(nameof(OwnerReport), Status.ToString(), OwnerReportStatus.Ready.ToString());
        }

        PropertyCount = propertyCount;
        TotalMaintenanceCost = totalMaintenanceCost;
        OpenMaintenanceCount = openMaintenanceCount;
        CompliantDocumentCount = compliantDocumentCount;
        ExpiringDocumentCount = expiringDocumentCount;
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
}
