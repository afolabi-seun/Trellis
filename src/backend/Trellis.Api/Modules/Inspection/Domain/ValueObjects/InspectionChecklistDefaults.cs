namespace Trellis.Api.Modules.Inspection.Domain.ValueObjects;

/// <summary>
/// BDR-INSP-002's "checklists" — the Handbook never specifies whether these are configurable, and
/// its own schema catalogue caps trellis_inspection at exactly 3 tables (no template table like
/// WorkflowTemplate's). Decision: a fixed baseline, in application code, that every new inspection
/// auto-populates; the inspector can still add/remove/edit items on that specific inspection.
/// </summary>
public static class InspectionChecklistDefaults
{
    public static readonly IReadOnlyList<string> BaselineLabels =
        ["Smoke Detectors", "Plumbing", "HVAC", "Structural", "Electrical"];
}
