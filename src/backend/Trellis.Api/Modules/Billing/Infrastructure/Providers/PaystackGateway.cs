using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Trellis.Api.Core.Resilience;
using Trellis.Api.Core.Secrets;
using Trellis.Api.Modules.Billing.Application.Interfaces;

namespace Trellis.Api.Modules.Billing.Infrastructure.Providers;

/// <summary>
/// Real Paystack HTTP integration — registered instead of NoOpPaymentGateway only when
/// Modules:Billing:PaymentGateway is "Paystack" (BillingModuleExtensions; NoOp stays the default
/// and is what every environment actually runs today). No real Paystack test-mode credentials
/// exist yet (user-confirmed scope), so this is <b>not verified against a live Paystack
/// sandbox</b> — only unit-tested against a fake HttpMessageHandler. The secret key is resolved
/// via ISecretProvider.GetSecretAsync("PaystackSecretKey") per call, not at construction, matching
/// PaystackSignatureValidator's own per-call convention. A network fault (not a decline) is
/// retried via ResiliencePolicies.ExternalProviderPolicy and, if still failing afterwards,
/// reported as a failed ChargeResult/VerifyResult rather than thrown — DunningService's own
/// batch loop over many tenants has no per-tenant try/catch, so one tenant's Paystack outage must
/// never abort the whole run. InitializeCardCaptureAsync is the one exception: it has no
/// "failed" shape to return today (TokenizeResult is success-only), so a gateway fault there
/// surfaces as an unhandled exception → ExceptionHandlingMiddleware's generic 500, same as any
/// other genuine dependency fault on a synchronous request.
/// </summary>
public sealed class PaystackGateway(HttpClient httpClient, ISecretProvider secretProvider) : IPaymentGateway
{
    private const string SecretKeyName = "PaystackSecretKey";
    private static readonly JsonSerializerOptions ResponseJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<ChargeResult> ChargeAsync(string authorizationCode, decimal amount, string currency, string reference)
    {
        var (ok, body) = await PostAsync("charge_authorization", new
        {
            authorization_code = authorizationCode,
            amount = ToSubunits(amount),
            currency,
            reference,
        });

        var envelope = TryParse(body);
        var success = ok && envelope?.Status == true && envelope.Data?.Status == "success";
        return new ChargeResult(success, envelope?.Data?.Reference ?? reference, body);
    }

    public async Task<TokenizeResult> InitializeCardCaptureAsync(Guid tenantId, string email)
    {
        var reference = $"paystack-{Guid.NewGuid():N}";

        // No amount is meaningful here yet — this only requests a hosted page to capture and
        // tokenize a card, not to charge one; a real integration may need a nominal authorization
        // amount instead of 0, which is exactly the kind of vendor-contract detail this phase
        // can't confirm without a live sandbox.
        var (ok, body) = await PostAsync("transaction/initialize", new { email, amount = 0, reference });
        var envelope = TryParse(body);

        if (!ok || envelope?.Status != true || envelope.Data is null)
        {
            throw new InvalidOperationException($"Paystack transaction initialize failed: {body}");
        }

        return new TokenizeResult(envelope.Data.AuthorizationUrl ?? "", envelope.Data.AccessCode ?? "", envelope.Data.Reference ?? reference);
    }

    public async Task<VerifyResult> VerifyTransactionAsync(string reference)
    {
        var (ok, body) = await GetAsync($"transaction/verify/{Uri.EscapeDataString(reference)}");
        var envelope = TryParse(body);
        var authorization = envelope?.Data?.Authorization;

        var success = ok && envelope?.Status == true && envelope.Data?.Status == "success" && authorization?.AuthorizationCode is not null;
        return new VerifyResult(success, authorization?.AuthorizationCode ?? "", authorization?.Last4 ?? "", authorization?.CardType ?? "");
    }

    private Task<(bool Ok, string Body)> PostAsync(string path, object payload) =>
        SendAsync(() => new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(payload) });

    private Task<(bool Ok, string Body)> GetAsync(string path) =>
        SendAsync(() => new HttpRequestMessage(HttpMethod.Get, path));

    private async Task<(bool Ok, string Body)> SendAsync(Func<HttpRequestMessage> requestFactory)
    {
        var secret = await secretProvider.GetSecretAsync(SecretKeyName);

        try
        {
            using var response = await ResiliencePolicies.ExternalProviderPolicy.ExecuteAsync(async () =>
            {
                using var request = requestFactory();
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
                return await httpClient.SendAsync(request);
            });

            var body = await response.Content.ReadAsStringAsync();
            return (response.IsSuccessStatusCode, body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (false, $$"""{"error":"{{ex.GetType().Name}}"}""");
        }
    }

    private static long ToSubunits(decimal amount) => (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);

    private static PaystackEnvelope? TryParse(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<PaystackEnvelope>(body, ResponseJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record PaystackEnvelope(bool Status, string? Message, PaystackData? Data);

    private sealed record PaystackData(
        string? Status, string? Reference, string? AuthorizationUrl, string? AccessCode, PaystackAuthorization? Authorization);

    private sealed record PaystackAuthorization(string? AuthorizationCode, string? Last4, string? CardType);
}
