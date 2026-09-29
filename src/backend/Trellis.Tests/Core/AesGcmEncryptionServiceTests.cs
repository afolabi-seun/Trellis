using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Trellis.Api.Core.Secrets;
using Trellis.Api.Core.Security;
using Xunit;

namespace Trellis.Tests.Core;

public sealed class AesGcmEncryptionServiceTests
{
    private readonly ISecretProvider _secretProvider = Substitute.For<ISecretProvider>();

    public AesGcmEncryptionServiceTests()
    {
        _secretProvider.GetSecretAsync("DataEncryptionKey_1_Base64").Returns(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        _secretProvider.GetSecretAsync("DataEncryptionKey_2_Base64").Returns(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    private AesGcmEncryptionService CreateSut(int currentKeyId = 1) =>
        new(_secretProvider, Options.Create(new EncryptionOptions { CurrentKeyId = currentKeyId }));

    [Fact]
    public async Task EncryptAsync_ThenDecryptAsync_WithTheSameAssociatedData_RoundTripsThePlaintext()
    {
        var sut = CreateSut();

        var envelope = await sut.EncryptAsync("top secret report", "tenant-1:application-1");
        var plaintext = await sut.DecryptAsync(envelope, "tenant-1:application-1");

        plaintext.Should().Be("top secret report");
    }

    [Fact]
    public async Task EncryptAsync_RecordsTheCurrentKeyIdInTheEnvelope()
    {
        var sut = CreateSut(currentKeyId: 1);

        var envelope = await sut.EncryptAsync("report", "aad");

        envelope.Should().StartWith("k1:");
    }

    [Fact]
    public async Task DecryptAsync_WithMismatchedAssociatedData_Throws()
    {
        var sut = CreateSut();
        var envelope = await sut.EncryptAsync("report", "tenant-1:application-1");

        var act = () => sut.DecryptAsync(envelope, "tenant-1:application-2");

        await act.Should().ThrowAsync<CryptographicException>();
    }

    [Fact]
    public async Task DecryptAsync_WithATamperedEnvelope_Throws()
    {
        var sut = CreateSut();
        var envelope = await sut.EncryptAsync("report", "aad");
        var tampered = envelope[..^4] + (envelope[^4] == 'A' ? 'B' : 'A') + envelope[^3..];

        var act = () => sut.DecryptAsync(tampered, "aad");

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task DecryptAsync_WithAnUnknownKeyId_PropagatesTheMissingSecretFailure()
    {
        _secretProvider.GetSecretAsync("DataEncryptionKey_99_Base64")
            .Returns(Task.FromException<string>(new InvalidOperationException("Required secret 'DataEncryptionKey_99_Base64' is not set.")));
        var sut = CreateSut();

        var act = () => sut.DecryptAsync("k99:AAAA", "aad");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task KeyRotation_ACiphertextEncryptedUnderAnOlderKey_StillDecryptsAfterCurrentKeyIdMovesOn()
    {
        var writer = CreateSut(currentKeyId: 1);
        var envelope = await writer.EncryptAsync("report", "aad");

        var readerAfterRotation = CreateSut(currentKeyId: 2);
        var plaintext = await readerAfterRotation.DecryptAsync(envelope, "aad");

        plaintext.Should().Be("report");
    }

    [Fact]
    public async Task DecryptAsync_WithAMalformedEnvelope_ThrowsCryptographicException()
    {
        var sut = CreateSut();

        var act = () => sut.DecryptAsync("not-a-valid-envelope", "aad");

        await act.Should().ThrowAsync<CryptographicException>();
    }
}
