namespace Trellis.Api.Modules.Billing.Application.Interfaces;

public sealed record ChargeResult(bool Success, string ProviderReference, string? ResponseJson);

public sealed record TokenizeResult(string AuthorizationUrl, string AccessCode, string Reference);

public sealed record VerifyResult(bool Success, string AuthorizationCode, string CardLast4, string CardBrand);

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

    /// <summary>
    /// Paystack's own "verify the transaction to retrieve the authorization_code" step — the real
    /// counterpart to InitializeCardCaptureAsync, called once the applicant returns from the
    /// hosted card page. Success is a business outcome (a card can genuinely fail to verify), not
    /// a gateway fault, so this never throws for that case.
    /// </summary>
    Task<VerifyResult> VerifyTransactionAsync(string reference);
}
