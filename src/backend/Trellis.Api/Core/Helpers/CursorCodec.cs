using System.Globalization;
using System.Text;

namespace Trellis.Api.Core.Helpers;

/// <summary>
/// Encodes/decodes the opaque keyset-pagination cursor api_standards.md's Pagination Pattern
/// specifies — a base64 string carrying `(createdAt ticks, id)`, the tie-break every cursor
/// query in this codebase sorts and filters on.
/// </summary>
public static class CursorCodec
{
    public static string Encode(DateTimeOffset createdAt, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{createdAt.UtcTicks}|{id}"));

    public static (DateTimeOffset CreatedAt, Guid Id)? Decode(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            return null;
        }

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|', 2);
            return (new DateTimeOffset(long.Parse(parts[0], CultureInfo.InvariantCulture), TimeSpan.Zero), Guid.Parse(parts[1]));
        }
        catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException)
        {
            return null; // a malformed cursor is treated as "start from the beginning," not an error
        }
    }
}
