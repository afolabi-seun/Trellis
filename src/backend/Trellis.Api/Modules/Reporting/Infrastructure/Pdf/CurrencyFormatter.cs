using System.Globalization;

namespace Trellis.Api.Modules.Reporting.Infrastructure.Pdf;

/// <summary>
/// Formats an amount using the real symbol/grouping for its own ISO 4217 code — the Owner report's
/// figures span more than one tenant market (USD/ZAR/KES so far), so a single hard-coded en-US
/// culture rendered every currency with a "$" regardless of what it actually was (a real, visible
/// bug in the live smoke-test PDF). Built once from every specific .NET culture's own RegionInfo,
/// keyed by ISOCurrencySymbol; an unmapped code (a currency .NET has no culture for) falls back to
/// "{CODE} 1,234.50" rather than guessing a symbol.
/// </summary>
public static class CurrencyFormatter
{
    private static readonly Dictionary<string, CultureInfo> CulturesByIsoCurrency = BuildMap();

    public static string Format(decimal amount, string currencyCode) =>
        CulturesByIsoCurrency.TryGetValue(currencyCode, out var culture)
            ? amount.ToString("C", culture)
            : $"{currencyCode} {amount.ToString("N2", CultureInfo.InvariantCulture)}";

    private static Dictionary<string, CultureInfo> BuildMap()
    {
        var map = new Dictionary<string, CultureInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                map.TryAdd(region.ISOCurrencySymbol, culture);
            }
            catch (Exception ex) when (ex is ArgumentException or CultureNotFoundException)
            {
                // A handful of specific cultures have no matching region (or .NET can't construct one on this platform) — skip, the fallback format covers it.
            }
        }

        return map;
    }
}
