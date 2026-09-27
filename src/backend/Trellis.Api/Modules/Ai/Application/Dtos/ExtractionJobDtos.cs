namespace Trellis.Api.Modules.Ai.Application.Dtos;

public sealed record ExtractedFieldDto(string FieldName, string? Value);

public sealed record ExtractionJobResponse(
    Guid ExtractionJobId,
    Guid DocumentId,
    Guid DocumentVersionId,
    string DocumentType,
    string Status,
    IReadOnlyList<ExtractedFieldDto>? ExtractedFields,
    decimal? Confidence,
    IReadOnlyList<ExtractedFieldDto>? ConfirmedFields,
    string? FailureReason,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string RowVersion,
    DateTimeOffset CreatedAt);

/// <summary>BDR-AI-002 — the human's confirmed-or-corrected final field values.</summary>
public sealed record ExtractionConfirmRequest(IReadOnlyList<ExtractedFieldDto> Fields, string RowVersion);
