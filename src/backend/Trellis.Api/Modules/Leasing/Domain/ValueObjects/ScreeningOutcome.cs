using Trellis.Api.Modules.Leasing.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Domain.ValueObjects;

/// <summary>IScreeningProvider.RequestScreeningAsync's return shape — maps 1:1 onto ScreeningResult.Create's own parameters.</summary>
public sealed record ScreeningOutcome(string Provider, ScreeningOutcomeSummary OutcomeSummary, string? FullReportCiphertext);
