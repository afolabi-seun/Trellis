using Trellis.Api.Modules.Ai.Application.Interfaces;

namespace Trellis.Api.Modules.Ai.Infrastructure.Providers;

/// <summary>
/// No real AI vendor is fixed for Phase 1 (a provider choice, out of scope for this build) —
/// deterministic, fixed-shape stand-in so the review UI has something real to render in tests and
/// demos, same "interface first, provider later" precedent as NoOpFileScanner. A real
/// implementation resolves its API key via ISecretProvider.GetSecretAsync("AI_PROVIDER_API_KEY")
/// in its own constructor (security_guide.md: a single platform-level key, not per-tenant).
/// </summary>
public sealed class NoOpAiProvider : IAiProvider
{
    public Task<ExtractionResult> ExtractAsync(Stream content, string fileName, string documentType) =>
        Task.FromResult(new ExtractionResult(
            [
                new ExtractedField("PartyName", "Placeholder Tenant Ltd"),
                new ExtractedField("EffectiveDate", "2026-01-01"),
                new ExtractedField("ExpiryDate", "2027-01-01"),
                new ExtractedField("MonthlyRent", "0.00"),
            ],
            Confidence: 0.62m));

    public Task<RoutingClassification> ClassifyRoutingAsync(string description, IReadOnlyList<DepartmentCandidate> departments)
    {
        var match = departments.FirstOrDefault(d => d.Name.Contains("Maintenance", StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match is not null
            ? new RoutingClassification(match.DepartmentId, null, Confidence: 0.82m)
            : new RoutingClassification(null, "MAINTENANCE", Confidence: 0.55m));
    }
}
