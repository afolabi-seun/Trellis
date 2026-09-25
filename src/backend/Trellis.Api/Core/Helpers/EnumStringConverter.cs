using System.Text;

namespace Trellis.Api.Core.Helpers;

/// <summary>
/// Converts between C# PascalCase enum members and the SCREAMING_SNAKE_CASE strings every
/// module's DB columns and API payloads use (data_model.md's convention) — e.g.
/// <c>RoutingCriterion.CostThreshold</c> &lt;-&gt; <c>"COST_THRESHOLD"</c>. A bare
/// <c>.ToString().ToUpperInvariant()</c> only round-trips single-word members; PropertyAsset's
/// <c>PropertyType.MixedUse</c> needed its own hand-written switch expression to handle the
/// compound case. Introduced here once the same shape repeated across Workflow's five
/// status/criterion enums — worth a shared helper at that point, not four more copies of the
/// same switch. Doesn't retroactively touch <c>PropertyService</c>'s existing switch (already
/// shipped and tested; not worth the churn for consistency alone) — only new Workflow/Document
/// conversions use this.
/// </summary>
public static class EnumStringConverter
{
    public static string ToUpperSnakeCase(this Enum value)
    {
        var name = value.ToString();
        var builder = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                builder.Append('_');
            }

            builder.Append(char.ToUpperInvariant(name[i]));
        }

        return builder.ToString();
    }

    public static TEnum ParseUpperSnakeCase<TEnum>(string value) where TEnum : struct, Enum =>
        Enum.Parse<TEnum>(value.Replace("_", string.Empty, StringComparison.Ordinal), true);

    public static bool TryParseUpperSnakeCase<TEnum>(string value, out TEnum result) where TEnum : struct, Enum =>
        Enum.TryParse(value.Replace("_", string.Empty, StringComparison.Ordinal), true, out result);
}
