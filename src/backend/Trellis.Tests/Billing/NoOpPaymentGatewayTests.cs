using FluentAssertions;
using Trellis.Api.Modules.Billing.Infrastructure.Providers;
using Xunit;

namespace Trellis.Tests.Billing;

public sealed class NoOpPaymentGatewayTests
{
    private readonly NoOpPaymentGateway _sut = new();

    [Fact]
    public async Task ChargeAsync_AlwaysSucceeds_AndEchoesTheReference()
    {
        var result = await _sut.ChargeAsync("auth-code", 100, "USD", "my-reference");

        result.Success.Should().BeTrue();
        result.ProviderReference.Should().Be("my-reference");
    }

    [Fact]
    public async Task InitializeCardCaptureAsync_ReturnsADeterministicHostedCaptureShape()
    {
        var result = await _sut.InitializeCardCaptureAsync(Guid.NewGuid(), "admin@example.com");

        result.AuthorizationUrl.Should().Contain(result.Reference);
        result.AccessCode.Should().Contain(result.Reference);
    }
}
