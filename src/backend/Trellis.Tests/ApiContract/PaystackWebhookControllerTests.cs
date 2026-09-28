using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Xunit;

namespace Trellis.Tests.ApiContract;

/// <summary>
/// billing_design.md's Testing section, first of its 3 explicitly required tests: "a test proving
/// a duplicated webhook delivery (same provider_event_id twice) only processes once (BDR-BIL-008)".
/// Exercises the real HTTP pipeline, [AllowAnonymous], and the real PaystackSignatureValidator
/// (unsubstituted — pure crypto, no DB dependency) against a synthetic payload signed with the
/// factory's own PaystackSecretKey test value; only IWebhookEventRepository/IBillingService are
/// substituted, keeping this DB-free per the ApiContract layer's own scope.
/// </summary>
public sealed class PaystackWebhookControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string TestSecret = "sk_test_apicontract_placeholder";
    private readonly CustomWebApplicationFactory _factory;

    public PaystackWebhookControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static string Sign(string body)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(TestSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }

    private static StringContent BuildBody(string body) => new(body, Encoding.UTF8, "application/json");

    [Fact]
    public async Task HandlePaystackWebhook_WithNoSignatureHeader_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        const string body = """{"event":"charge.success","data":{"reference":"no-sig-ref"}}""";

        var response = await client.PostAsync("/webhooks/paystack", BuildBody(body));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HandlePaystackWebhook_WithATamperedSignature_ReturnsUnauthorized_AndNeverTouchesTheRepository()
    {
        var webhookEventRepository = Substitute.For<IWebhookEventRepository>();
        await using var scopedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped(_ => webhookEventRepository)));
        var client = scopedFactory.CreateClient();
        const string body = """{"event":"charge.success","data":{"reference":"tampered-ref"}}""";

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/paystack") { Content = BuildBody(body) };
        request.Headers.Add("x-paystack-signature", "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await webhookEventRepository.DidNotReceiveWithAnyArgs().ExistsAsync(default!);
    }

    [Fact]
    public async Task HandlePaystackWebhook_WhenTheSameSignedEventIsDeliveredTwice_OnlyProcessesItOnce()
    {
        var seenEventIds = new HashSet<string>();
        var webhookEventRepository = Substitute.For<IWebhookEventRepository>();
        webhookEventRepository.ExistsAsync(Arg.Any<string>()).Returns(ci => seenEventIds.Contains(ci.Arg<string>()));
        _ = webhookEventRepository.RecordAsync(Arg.Do<WebhookEvent>(w => seenEventIds.Add(w.ProviderEventId)));
        var billingService = Substitute.For<IBillingService>();

        await using var scopedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => webhookEventRepository);
                services.AddScoped(_ => billingService);
            }));
        var client = scopedFactory.CreateClient();

        const string body = """{"event":"charge.success","data":{"subscriptionId":"11111111-1111-1111-1111-111111111111","reference":"dup-ref-1"}}""";
        var signature = Sign(body);

        var first = new HttpRequestMessage(HttpMethod.Post, "/webhooks/paystack") { Content = BuildBody(body) };
        first.Headers.Add("x-paystack-signature", signature);
        var firstResponse = await client.SendAsync(first);

        var second = new HttpRequestMessage(HttpMethod.Post, "/webhooks/paystack") { Content = BuildBody(body) };
        second.Headers.Add("x-paystack-signature", signature);
        var secondResponse = await client.SendAsync(second);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await billingService.Received(1).ProcessWebhookEventAsync(Arg.Any<string>());
    }
}
