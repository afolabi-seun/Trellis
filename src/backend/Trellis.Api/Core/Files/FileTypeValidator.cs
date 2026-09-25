namespace Trellis.Api.Core.Files;

/// <summary>
/// security_guide.md's File Upload Security, step 1 — the file's actual bytes (magic-number
/// sniffing), never the client-supplied Content-Type header. A small, no-dependency
/// implementation (unlike <see cref="IFileScanner"/>, there's no external vendor choice needed
/// here), scoped to the allow-list the doc names for documents: PDF/DOCX/XLSX/JPG/PNG. DOCX and
/// XLSX are both OOXML (zip-based) — indistinguishable from a plain zip by magic number alone,
/// which the malware scan step (not this one) is the actual backstop for.
/// </summary>
public static class FileTypeValidator
{
    private static readonly byte[] PdfMagic = [0x25, 0x50, 0x44, 0x46];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpgMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04];

    public static async Task<bool> IsAllowedDocumentTypeAsync(Stream content)
    {
        var originalPosition = content.Position;
        var header = new byte[8];
        var read = await content.ReadAsync(header);
        content.Position = originalPosition;

        return StartsWith(header, read, PdfMagic)
            || StartsWith(header, read, PngMagic)
            || StartsWith(header, read, JpgMagic)
            || StartsWith(header, read, ZipMagic);
    }

    private static bool StartsWith(byte[] buffer, int bufferLength, byte[] magic) =>
        bufferLength >= magic.Length && magic.AsSpan().SequenceEqual(buffer.AsSpan(0, magic.Length));
}
