using Trellis.Api.Modules.Leasing.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Domain.ValueObjects;

/// <summary>
/// IScreeningProvider.RequestScreeningAsync's return shape. FullReportJson is the provider's
/// plaintext report — the provider never touches encryption keys; ApplicationService encrypts it
/// (Core/Security/IEncryptionService) before it ever reaches ScreeningResult.Create.
/// </summary>
public sealed record ScreeningOutcome(string Provider, ScreeningOutcomeSummary OutcomeSummary, string? FullReportJson);
