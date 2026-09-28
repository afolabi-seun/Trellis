using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Xunit;

namespace Trellis.Tests.ApiContract;

/// <summary>
/// The e-signature completion webhook — mirrors PaystackWebhookControllerTests. [AllowAnonymous],
/// signature-verified, idempotency-deduplicated. Only the repository, the processing service and
/// the signature validator are substituted, keeping this DB-free.
/// </summary>
public sealed class ESignatureWebhookControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string CompletedBody = """{"event":"envelope.completed","envelopeId":"env-42"}""";
    private readonly CustomWebApplicationFactory _factory;

    public ESignatureWebhookControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    private HttpClient CreateClient(
        out IAsyncDisposable handle, ILeasingWebhookEventRepository repository, ILeasingWebhookService service, bool signatureValid = true)
    {
        var validator = Substitute.For<IESignatureSignatureValidator>();
        validator.IsValidAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(signatureValid);

        var scopedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => repository);
                services.AddScoped(_ => service);
                services.AddScoped(_ => validator);
            }));
        handle = scopedFactory;
        return scopedFactory.CreateClient();
    }

    private static StringContent Body(string json) => new(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task HandleESignatureWebhook_WithAnInvalidSignature_ReturnsUnauthorized_AndNeverTouchesAnything()
    {
        var repository = Substitute.For<ILeasingWebhookEventRepository>();
        var service = Substitute.For<ILeasingWebhookService>();
        var client = CreateClient(out var handle, repository, service, signatureValid: false);
        await using var _ = handle;

        var response = await client.PostAsync("/webhooks/esignature", Body(CompletedBody));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await repository.DidNotReceiveWithAnyArgs().RecordAsync(default!);
        await service.DidNotReceiveWithAnyArgs().ProcessSignatureCompletedAsync(default!, default!);
    }

    [Fact]
    public async Task HandleESignatureWebhook_ForANewCompletedEnvelope_ProcessesItOnce_ThenMarksItProcessed()
    {
        var repository = Substitute.For<ILeasingWebhookEventRepository>();
        repository.ExistsAsync("env-42").Returns(false);
        var service = Substitute.For<ILeasingWebhookService>();
        var client = CreateClient(out var handle, repository, service);
        await using var _ = handle;

        var response = await client.PostAsync("/webhooks/esignature", Body(CompletedBody));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await repository.Received(1).RecordAsync(Arg.Is<LeasingWebhookEvent>(e => e.ProviderEventId == "env-42" && e.EventType == "envelope.completed"));
        await service.Received(1).ProcessSignatureCompletedAsync("env-42", CompletedBody);
        await repository.Received(1).MarkProcessedAsync("env-42");
    }

    [Fact]
    public async Task HandleESignatureWebhook_ForADuplicateDelivery_ReturnsOk_WithoutProcessingASecondTime()
    {
        var repository = Substitute.For<ILeasingWebhookEventRepository>();
        repository.ExistsAsync("env-42").Returns(true);
        var service = Substitute.For<ILeasingWebhookService>();
        var client = CreateClient(out var handle, repository, service);
        await using var _ = handle;

        var response = await client.PostAsync("/webhooks/esignature", Body(CompletedBody));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await service.DidNotReceiveWithAnyArgs().ProcessSignatureCompletedAsync(default!, default!);
        await repository.DidNotReceiveWithAnyArgs().RecordAsync(default!);
    }

    [Fact]
    public async Task HandleESignatureWebhook_ForANonCompletionEvent_IsRecordedButNeverProcessedAsACompletion()
    {
        var repository = Substitute.For<ILeasingWebhookEventRepository>();
        var service = Substitute.For<ILeasingWebhookService>();
        var client = CreateClient(out var handle, repository, service);
        await using var _ = handle;

        var response = await client.PostAsync("/webhooks/esignature", Body("""{"event":"envelope.sent","envelopeId":"env-42"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await service.DidNotReceiveWithAnyArgs().ProcessSignatureCompletedAsync(default!, default!);
    }
}
