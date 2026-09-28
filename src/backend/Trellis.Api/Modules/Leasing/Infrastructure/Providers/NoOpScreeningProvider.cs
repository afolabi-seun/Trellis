using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Leasing.Domain.ValueObjects;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Providers;

/// <summary>
/// No real screening bureau is fixed for this phase (leasing_design.md: a business/legal decision —
/// data-sharing agreements, per-check cost, per-market regulatory registration — outside
/// engineering scope). Always returns Pass with no ciphertext. A real implementation (e.g. a
/// TransUnion- or Metropol-shaped one, chosen per-tenant by ApplicantDetails.CountryCode) resolves
/// its API key via ISecretProvider.GetSecretAsync per call and encrypts FullReportCiphertext at
/// rest — same swap-point convention as NoOpPaymentGateway/NoOpAiProvider.
/// </summary>
public sealed class NoOpScreeningProvider : IScreeningProvider
{
    public Task<ScreeningOutcome> RequestScreeningAsync(ApplicantDetails details) =>
        Task.FromResult(new ScreeningOutcome("NOOP", ScreeningOutcomeSummary.Pass, null));
}
