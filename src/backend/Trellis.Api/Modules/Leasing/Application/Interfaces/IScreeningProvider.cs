using Trellis.Api.Modules.Leasing.Domain.ValueObjects;

namespace Trellis.Api.Modules.Leasing.Application.Interfaces;

/// <summary>BDR-LSE-003 — leasing_design.md deliberately doesn't commit to a named provider (TransUnion/Metropol/etc., chosen per-tenant by CountryCode). NoOpScreeningProvider is the only implementation this phase, matching NoOpPaymentGateway/NoOpAiProvider's own twice-proven "ship one stub, defer the real one" convention.</summary>
public interface IScreeningProvider
{
    Task<ScreeningOutcome> RequestScreeningAsync(ApplicantDetails details);
}
