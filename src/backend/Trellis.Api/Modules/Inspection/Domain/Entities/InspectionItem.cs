using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Inspection.Domain.Entities;

public enum InspectionItemStatus
{
    Pending,
    Pass,
    Fail,
    NotApplicable,
}

/// <summary>One checklist row — either baseline-seeded at inspection creation or added ad hoc by the inspector.</summary>
public sealed class InspectionItem : ITenantOwned
{
    public Guid InspectionItemId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InspectionId { get; private set; }
    public string Label { get; private set; } = null!;
    public InspectionItemStatus Status { get; private set; } = InspectionItemStatus.Pending;
    public string? Notes { get; private set; }
    public int SortOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private InspectionItem() { }

    public static InspectionItem Create(Guid tenantId, Guid inspectionId, string label, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("Label is required.", nameof(label));
        }

        var now = DateTimeOffset.UtcNow;
        return new InspectionItem
        {
            InspectionItemId = Guid.NewGuid(),
            TenantId = tenantId,
            InspectionId = inspectionId,
            Label = label,
            SortOrder = sortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Update(InspectionItemStatus status, string? notes)
    {
        Status = status;
        Notes = notes;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
