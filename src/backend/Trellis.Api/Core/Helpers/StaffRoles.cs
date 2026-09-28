namespace Trellis.Api.Core.Helpers;

/// <summary>
/// The two roles a tenant's alerts may be sent to (BDR-RENT-004's "configured staff", and Leasing's
/// "new application" notice). One place for the strings that used to be duplicated as private arrays
/// in each notifying service; the tenant chooses which of these receive which alert (Tenant module's
/// settings), defaulting to both.
/// </summary>
public static class StaffRoles
{
    public const string TenantAdmin = "TENANT_ADMIN";
    public const string DepartmentStaff = "DEPARTMENT_STAFF";

    public static readonly IReadOnlyList<string> All = [TenantAdmin, DepartmentStaff];

    public static bool IsValidSelection(IReadOnlyCollection<string>? roles) =>
        roles is { Count: > 0 } && roles.All(All.Contains) && roles.Distinct().Count() == roles.Count;
}
