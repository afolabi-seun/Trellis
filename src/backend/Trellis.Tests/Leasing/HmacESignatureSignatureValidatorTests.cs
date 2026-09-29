using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Secrets;
using Trellis.Api.Modules.Leasing.Infrastructure.Providers;
using Xunit;

namespace Trellis.Tests.Leasing;

public sealed class HmacESignatureSignatureValidatorTests
{
    private const string Secret = "test-secret";
    private readonly ISecretProvider _secretProvider = Substitute.For<ISecretProvider>();

    private HmacESignatureSignatureValidator CreateSut() => new(_secretProvider);

    private static string ComputeSignature(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }

    [Fact]
    public async Task IsValidAsync_WhenTheSignatureMatchesTheBodyAndSecret_ReturnsTrue()
    {
        _secretProvider.GetSecretAsync("ESignatureWebhookSecret").Returns(Secret);
        const string body = """{"event":"envelope.completed","envelopeId":"env-1"}""";
        var sut = CreateSut();

        var result = await sut.IsValidAsync(body, ComputeSignature(body, Secret));

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsValidAsync_WhenTheBodyWasTamperedWithAfterSigning_ReturnsFalse()
    {
        _secretProvider.GetSecretAsync("ESignatureWebhookSecret").Returns(Secret);
        const string signedBody = """{"event":"envelope.completed","envelopeId":"env-1"}""";
        const string tamperedBody = """{"event":"envelope.completed","envelopeId":"env-2"}""";
        var sut = CreateSut();

        var result = await sut.IsValidAsync(tamperedBody, ComputeSignature(signedBody, Secret));

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsValidAsync_WhenSignedWithTheWrongSecret_ReturnsFalse()
    {
        _secretProvider.GetSecretAsync("ESignatureWebhookSecret").Returns(Secret);
        const string body = """{"event":"envelope.completed"}""";
        var sut = CreateSut();

        var result = await sut.IsValidAsync(body, ComputeSignature(body, "wrong-secret"));

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsValidAsync_WhenNoSignatureHeaderIsPresent_ReturnsFalse()
    {
        var sut = CreateSut();

        var result = await sut.IsValidAsync("{}", "");

        result.Should().BeFalse();
        await _secretProvider.DidNotReceiveWithAnyArgs().GetSecretAsync(default!);
    }

    [Fact]
    public async Task IsValidAsync_WhenTheSecretIsNotConfigured_FailsClosed_ReturnsFalse_NotThrows()
    {
        _secretProvider.GetSecretAsync("ESignatureWebhookSecret")
            .Returns(Task.FromException<string>(new InvalidOperationException("Required secret 'ESignatureWebhookSecret' is not set.")));
        var sut = CreateSut();

        var result = await sut.IsValidAsync("{}", "some-signature");

        result.Should().BeFalse();
    }
}
