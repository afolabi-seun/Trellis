using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Ai.Domain.Exceptions;

namespace Trellis.Api.Modules.Ai.Domain.Entities;

/// <summary>
/// BDR-AI-002 — even a technically-successful extraction isn't authoritative until a human
/// reviews and confirms it, unlike the malware scanner's plain Clean/Infected split. Extracted and
/// ManualReviewRequired are both confirmable end states, not just ManualReviewRequired.
/// </summary>
public enum ExtractionStatus
{
    Pending,
    Extracted,
    ManualReviewRequired,
    Confirmed,
}

/// <summary>One AI extraction attempt against a single DocumentVersion — BDR-AI-001. Never attached to Document's own entities; DocumentVersionId is a plain cross-schema id, no DB-level FK (data_model.md's established convention).</summary>
public sealed class ExtractionJob : ITenantOwned
{
    public Guid ExtractionJobId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid DocumentVersionId { get; private set; }
    public string DocumentType { get; private set; } = null!;
    public ExtractionStatus Status { get; private set; }
    public string? ExtractedFieldsJson { get; private set; }
    public decimal? Confidence { get; private set; }
    public string? ConfirmedFieldsJson { get; private set; }
    public string? FailureReason { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ExtractionJob() { }

    public static ExtractionJob Create(Guid tenantId, Guid documentId, Guid documentVersionId, string documentType)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExtractionJob
        {
            ExtractionJobId = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentId = documentId,
            DocumentVersionId = documentVersionId,
            DocumentType = documentType,
            Status = ExtractionStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void MarkExtracted(string extractedFieldsJson, decimal confidence)
    {
        if (Status != ExtractionStatus.Pending)
        {
            throw new InvalidStatusTransitionException(nameof(ExtractionJob), Status.ToString(), ExtractionStatus.Extracted.ToString());
        }

        Status = ExtractionStatus.Extracted;
        ExtractedFieldsJson = extractedFieldsJson;
        Confidence = confidence;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>error_handling.md's exit criterion — a provider failure degrades here, never throws past the caller.</summary>
    public void MarkManualReviewRequired(string reason)
    {
        if (Status != ExtractionStatus.Pending)
        {
            throw new InvalidStatusTransitionException(nameof(ExtractionJob), Status.ToString(), ExtractionStatus.ManualReviewRequired.ToString());
        }

        Status = ExtractionStatus.ManualReviewRequired;
        FailureReason = reason;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Confirm(string confirmedFieldsJson, Guid reviewedBy)
    {
        if (Status is not (ExtractionStatus.Extracted or ExtractionStatus.ManualReviewRequired))
        {
            throw new InvalidStatusTransitionException(nameof(ExtractionJob), Status.ToString(), ExtractionStatus.Confirmed.ToString());
        }

        Status = ExtractionStatus.Confirmed;
        ConfirmedFieldsJson = confirmedFieldsJson;
        ReviewedBy = reviewedBy;
        ReviewedAt = DateTimeOffset.UtcNow;
        UpdatedAt = ReviewedAt.Value;
    }
}
