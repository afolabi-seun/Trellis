namespace Trellis.Api.Core.Helpers;

/// <summary>
/// A tenant's default currency, derived from its ISO 3166-1 country code — a small, explicit map
/// rather than a third-party ISO-4217-by-country dependency, for the two markets Trellis names
/// (billing_design.md: ZAR, KES); everything else falls back to USD. Extracted from Billing so the same
/// answer labels a tenant's Owner-report figures and defaults its listing/rent forms, instead of each
/// place guessing "USD".
/// </summary>
public static class CurrencyDefaults
{
    private static readonly Dictionary<string, string> CountryCurrencyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ZA"] = "ZAR",
        ["KE"] = "KES",
    };

    public static string ForCountry(string? countryCode) =>
        countryCode is not null && CountryCurrencyMap.TryGetValue(countryCode, out var currency) ? currency : "USD";
}
