using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Ai.Application.Dtos;

namespace Trellis.Api.Modules.Ai.Application.Interfaces;

public interface IExtractionService
{
    /// <summary>
    /// Called inline by DocumentService right after its own malware scan (decision 2) — never
    /// throws past the caller (error_handling.md's resilience policy degrades to
    /// ManualReviewRequired on a provider failure), and silently no-ops for a STARTER tenant (the
    /// tier gate never touches the upload path).
    /// </summary>
    Task ExtractAsync(Guid tenantId, Guid documentId, Guid documentVersionId, string documentType, string storageKey, string fileName, Guid initiatedBy);

    Task<ServiceResult<ExtractionJobResponse>> GetByIdAsync(Guid extractionJobId);

    Task<ServiceResult<PagedResponse<ExtractionJobResponse>>> ListAsync(string? cursor, int limit);

    /// <summary>BDR-AI-002 — the human's review/confirmation step. Writes an override_log row iff any field actually changed from what was extracted.</summary>
    Task<ServiceResult<ExtractionJobResponse>> ConfirmAsync(Guid extractionJobId, ExtractionConfirmRequest request, Guid reviewedBy);
}
