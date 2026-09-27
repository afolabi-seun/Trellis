using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Ai.Domain.Entities;

public enum SuggestionType
{
    Extraction,
    Routing,
}

/// <summary>
/// BDR-AI-004's "log the override for future model improvement" — a dedicated, queryable table
/// rather than a generic audit_record entry (decision 1: audit_record has zero existing
/// structured-query consumers anywhere in this codebase). Polymorphic via SuggestionType +
/// SuggestionId, the identical shape trellis_vendor.vendor_assignment's own work_item_type/
/// work_item_id already uses for the same reason. Insert-only, immutable — no update method, no
/// xmin, matching audit_record's own append-only shape.
/// </summary>
public sealed class OverrideLogEntry : ITenantOwned
{
    public Guid OverrideLogId { get; private set; }
    public Guid TenantId { get; private set; }
    public SuggestionType SuggestionType { get; private set; }
    public Guid SuggestionId { get; private set; }
    public string OriginalValueJson { get; private set; } = null!;
    public string CorrectedValueJson { get; private set; } = null!;
    public Guid OverriddenBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private OverrideLogEntry() { }

    public static OverrideLogEntry Create(
        Guid tenantId, SuggestionType suggestionType, Guid suggestionId, string originalValueJson, string correctedValueJson, Guid overriddenBy) => new()
        {
            OverrideLogId = Guid.NewGuid(),
            TenantId = tenantId,
            SuggestionType = suggestionType,
            SuggestionId = suggestionId,
            OriginalValueJson = originalValueJson,
            CorrectedValueJson = correctedValueJson,
            OverriddenBy = overriddenBy,
            CreatedAt = DateTimeOffset.UtcNow,
        };
}
