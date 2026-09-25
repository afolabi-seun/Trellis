using FluentAssertions;
using Trellis.Api.Core.Files;
using Xunit;

namespace Trellis.Tests.Document;

public sealed class FileTypeValidatorTests
{
    [Fact]
    public async Task IsAllowedDocumentTypeAsync_WhenPdfMagicNumber_ReturnsTrue()
    {
        using var stream = new MemoryStream([0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34]);

        (await FileTypeValidator.IsAllowedDocumentTypeAsync(stream)).Should().BeTrue();
    }

    [Fact]
    public async Task IsAllowedDocumentTypeAsync_WhenPngMagicNumber_ReturnsTrue()
    {
        using var stream = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        (await FileTypeValidator.IsAllowedDocumentTypeAsync(stream)).Should().BeTrue();
    }

    [Fact]
    public async Task IsAllowedDocumentTypeAsync_WhenExecutableMagicNumber_ReturnsFalse()
    {
        // An .exe renamed to .pdf — the exact attack security_guide.md's File Upload Security names.
        using var stream = new MemoryStream([0x4D, 0x5A, 0x90, 0x00]);

        (await FileTypeValidator.IsAllowedDocumentTypeAsync(stream)).Should().BeFalse();
    }

    [Fact]
    public async Task IsAllowedDocumentTypeAsync_ResetsStreamPositionAfterSniffing()
    {
        using var stream = new MemoryStream([0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34]);

        await FileTypeValidator.IsAllowedDocumentTypeAsync(stream);

        stream.Position.Should().Be(0);
    }
}
