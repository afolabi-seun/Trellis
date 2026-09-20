namespace Trellis.Api.Configuration;

/// <summary>
/// The set of built modules, keyed by their Module Catalogue key (project_architecture.md) —
/// what <see cref="SwaggerConfiguration"/> uses to generate one OpenAPI doc per module
/// (api_standards.md, "OpenAPI Grouping By Module"). Empty in Phase 0: no module exists yet.
/// Each module adds its own key here in the same commit that adds its
/// <c>Add{Module}Module()</c> registration (coding_standards.md) — e.g. Phase 1 adds
/// <c>"auth"</c>, <c>"tenant"</c>, <c>"notification"</c>.
/// </summary>
public static class ModuleRegistry
{
    // "tenant" and "auth" only — Notification has no Presentation/Controllers, so it never
    // registers its own OpenAPI group (project_architecture.md's Module Catalogue).
    public static IReadOnlyCollection<string> Keys { get; } = ["tenant", "auth"];
}
