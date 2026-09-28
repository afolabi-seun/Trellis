namespace Trellis.Api.Modules.Billing.Application.Interfaces;

public sealed record ChargeResult(bool Success, string ProviderReference, string? ResponseJson);

public sealed record TokenizeResult(string AuthorizationUrl, string AccessCode, string Reference);

/// <summary>
/// billing_design.md's provider seam — Paystack is this phase's chosen vendor (unlike Ai's
/// still-undecided IAiProvider), so this interface exists to make a FUTURE alternate vendor (e.g.
/// Flutterwave) a cheap swap, not because today's vendor is undecided. Stays in Billing's own
/// Application/Interfaces, not Core — only Billing needs it today (IFileScanner/IFileStorage only
/// moved to Core once a second module needed them).
/// </summary>
public interface IPaymentGateway
{
    Task<ChargeResult> ChargeAsync(string authorizationCode, decimal amount, string currency, string reference);

    Task<TokenizeResult> InitializeCardCaptureAsync(Guid tenantId, string email);
}
