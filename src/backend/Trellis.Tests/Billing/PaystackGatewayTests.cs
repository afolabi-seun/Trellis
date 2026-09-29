using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Secrets;
using Trellis.Api.Modules.Billing.Infrastructure.Providers;
using Xunit;

namespace Trellis.Tests.Billing;

/// <summary>
/// Against a fake HttpMessageHandler, not a live Paystack sandbox (PaystackGateway's own doc
/// comment — no real test-mode credentials exist yet). Covers request shape, the bearer auth
/// header, success, decline, a transient-failure retry, and a malformed response — the exact
/// categories the P5 plan calls for.
/// </summary>
public sealed class PaystackGatewayTests
{
    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            await respond(request);
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static PaystackGateway CreateSut(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler(respond)) { BaseAddress = new Uri("https://api.paystack.co/") };
        var secretProvider = Substitute.For<ISecretProvider>();
        secretProvider.GetSecretAsync("PaystackSecretKey").Returns("sk_test_fake");
        return new PaystackGateway(httpClient, secretProvider);
    }

    [Fact]
    public async Task ChargeAsync_SendsTheAmountInSubunits_WithABearerAuthHeader_AndReturnsSuccess()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;

        var sut = CreateSut(async request =>
        {
            captured = request;
            capturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return JsonResponse("""{"status":true,"message":"ok","data":{"status":"success","reference":"ref-1"}}""");
        });

        var result = await sut.ChargeAsync("auth-code", 125.50m, "NGN", "ref-1");

        result.Success.Should().BeTrue();
        result.ProviderReference.Should().Be("ref-1");
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri.Should().Be(new Uri("https://api.paystack.co/charge_authorization"));
        captured.Headers.Authorization.Should().BeEquivalentTo(new AuthenticationHeaderValue("Bearer", "sk_test_fake"));
        capturedBody.Should().Contain("\"amount\":12550");
        capturedBody.Should().Contain("\"authorization_code\":\"auth-code\"");
    }

    [Fact]
    public async Task ChargeAsync_WhenPaystackDeclinesTheCharge_ReturnsAFailedResult_NotAnException()
    {
        var sut = CreateSut(_ => Task.FromResult(
            JsonResponse("""{"status":true,"message":"Declined","data":{"status":"failed","reference":"ref-1"}}""")));

        var result = await sut.ChargeAsync("auth-code", 100m, "NGN", "ref-1");

        result.Success.Should().BeFalse();
        result.ProviderReference.Should().Be("ref-1");
    }

    [Fact]
    public async Task ChargeAsync_WithAMalformedResponseBody_ReturnsAFailedResult_NotAnException()
    {
        var sut = CreateSut(_ => Task.FromResult(JsonResponse("not json")));

        var result = await sut.ChargeAsync("auth-code", 100m, "NGN", "ref-1");

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ChargeAsync_OnATransientNetworkFailure_RetriesOnTheSharedPolicy_AndSucceeds()
    {
        var attempts = 0;

        var sut = CreateSut(_ =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new HttpRequestException("connection reset");
            }

            return Task.FromResult(JsonResponse("""{"status":true,"message":"ok","data":{"status":"success","reference":"ref-1"}}"""));
        });

        var result = await sut.ChargeAsync("auth-code", 100m, "NGN", "ref-1");

        attempts.Should().Be(2);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyTransactionAsync_OnSuccess_ReturnsTheAuthorizationCodeAndCardDetails()
    {
        var sut = CreateSut(_ => Task.FromResult(JsonResponse(
            """{"status":true,"message":"ok","data":{"status":"success","authorization":{"authorization_code":"AUTH_abc","last4":"4081","card_type":"visa"}}}""")));

        var result = await sut.VerifyTransactionAsync("ref-1");

        result.Success.Should().BeTrue();
        result.AuthorizationCode.Should().Be("AUTH_abc");
        result.CardLast4.Should().Be("4081");
        result.CardBrand.Should().Be("visa");
    }

    [Fact]
    public async Task VerifyTransactionAsync_WhenPaystackReportsFailure_ReturnsUnsuccessful_NotThrows()
    {
        var sut = CreateSut(_ => Task.FromResult(JsonResponse("""{"status":true,"message":"ok","data":{"status":"failed"}}""")));

        var result = await sut.VerifyTransactionAsync("ref-1");

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeCardCaptureAsync_OnSuccess_ReturnsTheHostedPageDetails()
    {
        var sut = CreateSut(_ => Task.FromResult(JsonResponse(
            """{"status":true,"message":"ok","data":{"authorization_url":"https://checkout.paystack.com/abc","access_code":"code-1","reference":"ref-1"}}""")));

        var result = await sut.InitializeCardCaptureAsync(Guid.NewGuid(), "admin@example.com");

        result.AuthorizationUrl.Should().Be("https://checkout.paystack.com/abc");
        result.AccessCode.Should().Be("code-1");
        result.Reference.Should().Be("ref-1");
    }

    [Fact]
    public async Task InitializeCardCaptureAsync_WhenPaystackReportsFailure_Throws()
    {
        var sut = CreateSut(_ => Task.FromResult(JsonResponse("""{"status":false,"message":"invalid email"}""")));

        var act = () => sut.InitializeCardCaptureAsync(Guid.NewGuid(), "bad");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
