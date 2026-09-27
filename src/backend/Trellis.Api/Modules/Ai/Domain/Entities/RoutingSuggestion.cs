using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Ai.Domain.Exceptions;

namespace Trellis.Api.Modules.Ai.Domain.Entities;

/// <summary>Accepted/Overridden are resolved only at link-time, by comparing the human's final department choice to SuggestedDepartmentId — not at suggestion-creation time.</summary>
public enum RoutingSuggestionStatus
{
    Pending,
    Accepted,
    Overridden,
    Failed,
}

/// <summary>
/// BDR-AI-003 — a destination classification (internal department OR a Vendor category, never a
/// specific vendor) for a free-text request description. Standalone from WorkflowInstance: the
/// human still creates the instance through the existing, unchanged
/// WorkflowInstancesController/CreateAsync flow; this row is only linked back afterward.
/// </summary>
public sealed class RoutingSuggestion : ITenantOwned
{
    public Guid RoutingSuggestionId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Description { get; private set; } = null!;
    public Guid? PropertyId { get; private set; }
    public Guid? SuggestedDepartmentId { get; private set; }
    public string? SuggestedVendorCategory { get; private set; }
    public decimal? Confidence { get; private set; }
    public RoutingSuggestionStatus Status { get; private set; }
    public Guid? FinalDepartmentId { get; private set; }
    public Guid? WorkflowInstanceId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private RoutingSuggestion() { }

    public static RoutingSuggestion Create(Guid tenantId, string description, Guid? propertyId, Guid createdBy)
    {
        var now = DateTimeOffset.UtcNow;
        return new RoutingSuggestion
        {
            RoutingSuggestionId = Guid.NewGuid(),
            TenantId = tenantId,
            Description = description,
            PropertyId = propertyId,
            Status = RoutingSuggestionStatus.Pending,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void MarkClassified(Guid? suggestedDepartmentId, string? suggestedVendorCategory, decimal confidence)
    {
        if (Status != RoutingSuggestionStatus.Pending)
        {
            throw new InvalidStatusTransitionException(nameof(RoutingSuggestion), Status.ToString(), "Classified");
        }

        SuggestedDepartmentId = suggestedDepartmentId;
        SuggestedVendorCategory = suggestedVendorCategory;
        Confidence = confidence;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>error_handling.md's exit criterion — a classifier failure degrades here; the suggestion row simply carries no destination, never blocking instance creation.</summary>
    public void MarkFailed()
    {
        if (Status != RoutingSuggestionStatus.Pending)
        {
            throw new InvalidStatusTransitionException(nameof(RoutingSuggestion), Status.ToString(), RoutingSuggestionStatus.Failed.ToString());
        }

        Status = RoutingSuggestionStatus.Failed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Called once, after the human creates the real WorkflowInstance elsewhere — resolves
    /// Accepted/Overridden by comparing finalDepartmentId to the original suggestion. A Failed
    /// suggestion (classifier threw, or a Vendor-category-only hint with no department ever
    /// suggested) has nothing to compare against, so linking it never counts as an override —
    /// there was no department suggestion to reject in the first place.
    /// </summary>
    public bool Link(Guid workflowInstanceId, Guid? finalDepartmentId)
    {
        if (WorkflowInstanceId is not null)
        {
            throw new InvalidStatusTransitionException(nameof(RoutingSuggestion), "Linked", "Linked");
        }

        var wasOverridden = SuggestedDepartmentId is not null && finalDepartmentId != SuggestedDepartmentId;
        WorkflowInstanceId = workflowInstanceId;
        FinalDepartmentId = finalDepartmentId;
        Status = Status == RoutingSuggestionStatus.Failed
            ? RoutingSuggestionStatus.Failed
            : wasOverridden ? RoutingSuggestionStatus.Overridden : RoutingSuggestionStatus.Accepted;
        UpdatedAt = DateTimeOffset.UtcNow;
        return wasOverridden;
    }
}
