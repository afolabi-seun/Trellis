using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Secrets;
using Trellis.Api.Modules.Billing.Infrastructure.Security;
using Xunit;

namespace Trellis.Tests.Billing;

public sealed class PaystackSignatureValidatorTests
{
    private const string Secret = "test-secret";
    private readonly ISecretProvider _secretProvider = Substitute.For<ISecretProvider>();

    public PaystackSignatureValidatorTests() => _secretProvider.GetSecretAsync("PaystackSecretKey").Returns(Secret);

    private PaystackSignatureValidator CreateSut() => new(_secretProvider);

    private static string ComputeSignature(string body, string secret)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }

    [Fact]
    public async Task IsValidAsync_WhenTheSignatureMatchesTheBodyAndSecret_ReturnsTrue()
    {
        const string body = """{"event":"charge.success"}""";
        var sut = CreateSut();

        var result = await sut.IsValidAsync(body, ComputeSignature(body, Secret));

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsValidAsync_WhenTheBodyWasTamperedWithAfterSigning_ReturnsFalse()
    {
        const string signedBody = """{"event":"charge.success"}""";
        const string tamperedBody = """{"event":"charge.failed"}""";
        var sut = CreateSut();

        var result = await sut.IsValidAsync(tamperedBody, ComputeSignature(signedBody, Secret));

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsValidAsync_WhenSignedWithTheWrongSecret_ReturnsFalse()
    {
        const string body = """{"event":"charge.success"}""";
        var sut = CreateSut();

        var result = await sut.IsValidAsync(body, ComputeSignature(body, "wrong-secret"));

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsValidAsync_WhenNoSignatureHeaderIsPresent_ReturnsFalse()
    {
        var sut = CreateSut();

        var result = await sut.IsValidAsync("{}", null);

        result.Should().BeFalse();
    }
}
