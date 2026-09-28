using System.Text;

namespace Trellis.Api.Modules.Reporting.Infrastructure.Csv;

/// <summary>
/// Hand-rolled RFC 4180 writer, not a library — Owner-report/dashboard CSV data is flat, small,
/// and always tenant-scoped (never a cross-tenant export), so a ~15-line helper is the
/// right-sized tool here, not a dependency like CsvHelper.
/// </summary>
public static class CsvWriter
{
    public static byte[] Write(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        // RFC 4180 line endings are \r\n explicitly — StringBuilder.AppendLine uses
        // Environment.NewLine, which is platform-dependent (\n on Linux/macOS), not a real RFC
        // 4180 writer despite this class's own doc comment claiming one; caught by this class's
        // own unit test asserting exact line content.
        var builder = new StringBuilder();
        builder.Append(string.Join(',', headers.Select(Escape))).Append("\r\n");
        foreach (var row in rows)
        {
            builder.Append(string.Join(',', row.Select(Escape))).Append("\r\n");
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string Escape(string field)
    {
        if (field.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return field;
        }

        return $"\"{field.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
