using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trellis.Api.Modules.Tenant.Domain.ValueObjects;

/// <summary>
/// A tenant's public, human-friendly identifier (the <c>acme-pm</c> in <c>/vacancies/acme-pm</c>) —
/// replaces the raw tenant GUID in public URLs. Lowercase letters, digits and hyphens, 3–63
/// characters, no leading/trailing hyphen (a valid DNS label, so it could equally be a subdomain
/// later). The reserved list keeps a slug from ever colliding with a top-level route or a name a
/// visitor would mistake for the platform itself.
/// </summary>
public static partial class SlugPolicy
{
    public const int MinLength = 3;
    public const int MaxLength = 63;

    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "admin", "api", "platform", "login", "www", "app", "apply", "vacancies", "listings", "reports",
        "webhooks", "vendor", "mfa-verify", "forgot-password", "reset-password", "static", "assets",
        "trellis", "support", "help", "mail", "status", "docs",
    };

    [GeneratedRegex("^[a-z0-9]([a-z0-9-]*[a-z0-9])?$")]
    private static partial Regex ValidShape();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugRun();

    public static bool IsValid(string? slug) =>
        slug is { Length: >= MinLength and <= MaxLength }
        && ValidShape().IsMatch(slug)
        && !Reserved.Contains(slug);

    /// <summary>Best-effort slug from a display name: accents stripped, everything else collapsed to single hyphens, capped so a uniqueness suffix still fits. Always returns something valid.</summary>
    public static string FromName(string name)
    {
        var decomposed = name.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        var slug = NonSlugRun().Replace(builder.ToString().ToLowerInvariant(), "-").Trim('-');
        if (slug.Length > 50)
        {
            slug = slug[..50].TrimEnd('-');
        }

        if (slug.Length < MinLength)
        {
            slug = "tenant";
        }

        return Reserved.Contains(slug) ? $"{slug}-pm" : slug;
    }

    /// <summary>The n-th uniqueness candidate for a base slug: <c>acme</c>, <c>acme-2</c>, <c>acme-3</c>…</summary>
    public static string WithSuffix(string slug, int attempt) => attempt <= 1 ? slug : $"{slug}-{attempt.ToString(CultureInfo.InvariantCulture)}";
}
