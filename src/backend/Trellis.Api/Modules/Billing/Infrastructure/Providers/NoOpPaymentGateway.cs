using Trellis.Api.Modules.Billing.Application.Interfaces;

namespace Trellis.Api.Modules.Billing.Infrastructure.Providers;

/// <summary>
/// Paystack sandbox test credentials aren't wired up yet (user-confirmed scope for this phase) —
/// deterministic, always-succeeds stand-in so the full invoice→charge→dunning pipeline has
/// something real to run against in tests and demos, same "interface first, provider later"
/// precedent as NoOpFileScanner/NoOpAiProvider. A real PaystackGateway (see its own doc comment)
/// resolves its API key via ISecretProvider.GetSecretAsync("PaystackSecretKey") per call once
/// real test credentials exist.
/// </summary>
public sealed class NoOpPaymentGateway : IPaymentGateway
{
    public Task<ChargeResult> ChargeAsync(string authorizationCode, decimal amount, string currency, string reference) =>
        Task.FromResult(new ChargeResult(true, reference, "{\"gateway\":\"noop\",\"status\":\"success\"}"));

    public Task<TokenizeResult> InitializeCardCaptureAsync(Guid tenantId, string email)
    {
        var reference = $"noop-{Guid.NewGuid():N}";
        return Task.FromResult(new TokenizeResult($"https://paystack.test/pay/{reference}", $"access-{reference}", reference));
    }
}
