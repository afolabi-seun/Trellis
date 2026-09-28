using Trellis.Api.Modules.Billing.Application.Interfaces;

namespace Trellis.Api.Modules.Billing.Infrastructure.Providers;

/// <summary>
/// STUB ONLY — not registered in DI this phase (see BillingModuleExtensions.cs's own comment;
/// NoOpPaymentGateway is registered instead). Real implementation deferred to a follow-up once
/// real Paystack test-mode credentials are available: ChargeAsync should POST to
/// https://api.paystack.co/transaction/charge_authorization, InitializeCardCaptureAsync to
/// https://api.paystack.co/transaction/initialize, both taking an ISecretProvider dependency and
/// resolving the secret key via GetSecretAsync("PaystackSecretKey") per call (not at construction
/// — these are per-request HTTP calls with no reason to block the constructor the way
/// JwtTokenService's RSA import does).
/// </summary>
public sealed class PaystackGateway : IPaymentGateway
{
    public Task<ChargeResult> ChargeAsync(string authorizationCode, decimal amount, string currency, string reference) =>
        throw new NotImplementedException("PaystackGateway's real HTTP integration is deferred — see this class's own doc comment.");

    public Task<TokenizeResult> InitializeCardCaptureAsync(Guid tenantId, string email) =>
        throw new NotImplementedException("PaystackGateway's real HTTP integration is deferred — see this class's own doc comment.");
}
