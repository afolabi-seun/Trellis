using System.Text.Json;
using System.Text.Json.Nodes;
using Trellis.Api.Core.Helpers;

namespace Trellis.Api.Modules.Tenant.Domain.ValueObjects;

/// <summary>Which roles receive which alert. Defaults to both staff roles — exactly what was hard-coded before it became a setting.</summary>
public sealed record NotificationRecipientSettings(
    IReadOnlyList<string> ApplicationSubmittedRoles, IReadOnlyList<string> RentArrearsRoles)
{
    public static NotificationRecipientSettings Default { get; } = new(StaffRoles.All, StaffRoles.All);
}

/// <summary>
/// BDR-LSE-010's configurable list of reasons a lessee declines a renewal. "Other" is never in the
/// list: every screen offers it as well, with a note, so the list only needs the common reasons.
/// </summary>
public static class RenewalDeclineReasons
{
    public const int MaxReasons = 15;
    public const int MaxReasonLength = 100;
    public const string Other = "Other";

    public static IReadOnlyList<string> Default { get; } =
    [
        "Rent too high",
        "Moving out of the area",
        "Buying a home",
        "Needs a different size",
        "Unhappy with the property or service",
    ];

    /// <summary>1–15 distinct (case-insensitive), non-blank reasons of at most 100 characters, none of them "Other".</summary>
    public static bool IsValidList(IReadOnlyCollection<string>? reasons) =>
        reasons is { Count: > 0 and <= MaxReasons }
        && reasons.All(r => !string.IsNullOrWhiteSpace(r) && r.Trim().Length <= MaxReasonLength
            && !string.Equals(r.Trim(), Other, StringComparison.OrdinalIgnoreCase))
        && reasons.Select(r => r.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == reasons.Count;
}

/// <summary>
/// Reads and writes tenant settings inside <c>tenant.config_json</c> (the JSONB column data_model.md
/// designates for "tenant-level overrides"), under a <c>notifications</c> key. Writing merges into the
/// existing document rather than replacing it, so any other key already stored there — a future
/// setting, a feature flag — survives an update to this one. Malformed or missing JSON reads as the defaults,
/// never an exception: a bad blob must not stop a tenant's alerts.
/// </summary>
public static class TenantSettingsSerializer
{
    private const string NotificationsKey = "notifications";
    private const string ApplicationRolesKey = "applicationSubmittedRoles";
    private const string ArrearsRolesKey = "rentArrearsRoles";
    private const string LeasingKey = "leasing";
    private const string DeclineReasonsKey = "renewalDeclineReasons";

    /// <summary>Missing, malformed, or invalid reads as the default list — a bad blob must not leave staff with no reasons to choose.</summary>
    public static IReadOnlyList<string> ReadRenewalDeclineReasons(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return RenewalDeclineReasons.Default;
        }

        try
        {
            if ((JsonNode.Parse(configJson) as JsonObject)?[LeasingKey]?[DeclineReasonsKey] is not JsonArray array)
            {
                return RenewalDeclineReasons.Default;
            }

            var reasons = new List<string>();
            foreach (var node in array)
            {
                if (node is not JsonValue value || !value.TryGetValue<string>(out var reason))
                {
                    return RenewalDeclineReasons.Default;
                }

                reasons.Add(reason);
            }

            return RenewalDeclineReasons.IsValidList(reasons) ? reasons : RenewalDeclineReasons.Default;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return RenewalDeclineReasons.Default;
        }
    }

    /// <summary>Merges into the existing document under the <c>leasing</c> key, like WriteNotifications — other keys survive.</summary>
    public static string WriteRenewalDeclineReasons(string? existingConfigJson, IReadOnlyList<string> reasons)
    {
        var root = ParseRoot(existingConfigJson);
        var leasing = root[LeasingKey] as JsonObject ?? [];
        leasing[DeclineReasonsKey] = ToArray(reasons.Select(r => r.Trim()));
        root[LeasingKey] = leasing.DeepClone();
        return root.ToJsonString();
    }

    private static JsonObject ParseRoot(string? existingConfigJson)
    {
        try
        {
            return string.IsNullOrWhiteSpace(existingConfigJson) ? [] : JsonNode.Parse(existingConfigJson) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static NotificationRecipientSettings ReadNotifications(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return NotificationRecipientSettings.Default;
        }

        try
        {
            var notifications = (JsonNode.Parse(configJson) as JsonObject)?[NotificationsKey];
            return new NotificationRecipientSettings(
                ReadRoles(notifications, ApplicationRolesKey),
                ReadRoles(notifications, ArrearsRolesKey));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return NotificationRecipientSettings.Default;
        }
    }

    public static string WriteNotifications(string? existingConfigJson, NotificationRecipientSettings settings)
    {
        JsonObject root;
        try
        {
            root = string.IsNullOrWhiteSpace(existingConfigJson) ? [] : JsonNode.Parse(existingConfigJson) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            root = [];
        }

        root[NotificationsKey] = new JsonObject
        {
            [ApplicationRolesKey] = ToArray(settings.ApplicationSubmittedRoles),
            [ArrearsRolesKey] = ToArray(settings.RentArrearsRoles),
        };

        return root.ToJsonString();
    }

    private static JsonArray ToArray(IEnumerable<string> roles)
    {
        var array = new JsonArray();
        foreach (var role in roles)
        {
            array.Add(role);
        }

        return array;
    }

    /// <summary>A stored list that is missing, empty, or contains anything but the two known roles falls back to the default — a corrupted setting never silences an alert.</summary>
    private static IReadOnlyList<string> ReadRoles(JsonNode? notifications, string key)
    {
        if (notifications?[key] is not JsonArray array)
        {
            return StaffRoles.All;
        }

        var roles = new List<string>();
        foreach (var node in array)
        {
            if (node is JsonValue value && value.TryGetValue<string>(out var role))
            {
                roles.Add(role);
            }
            else
            {
                return StaffRoles.All;
            }
        }

        return StaffRoles.IsValidSelection(roles) ? roles : StaffRoles.All;
    }
}
