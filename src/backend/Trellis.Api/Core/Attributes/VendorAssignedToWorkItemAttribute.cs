using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Results;

namespace Trellis.Api.Core.Attributes;

/// <summary>
/// multi_tenant_design.md, named exactly: gates a route by "is the caller (if a VENDOR_USER) the
/// vendor actually assigned to this work item" — reads the id from a route param (default
/// <c>workItemId</c>, matching api_standards.md's own usage example) rather than a request body,
/// since the caller never gets to assert their own scope. A non-vendor caller (TENANT_ADMIN/
/// DEPARTMENT_STAFF) skips this check entirely — the controller's own role-based [Authorize]
/// already gates who reaches here at all; this only narrows the VENDOR_USER branch.
///
/// Returns 404, not 403, on mismatch — same reasoning api_standards.md and this codebase's own
/// TenantAwareRepository give for out-of-scope reads generally: existence isn't confirmed or
/// denied to a caller who was never entitled to ask.
/// </summary>
public sealed class VendorAssignedToWorkItemAttribute : TypeFilterAttribute
{
    public VendorAssignedToWorkItemAttribute(string workItemType, string routeParam = "workItemId")
        : base(typeof(VendorAssignmentFilter))
    {
        // Fed positionally, matched by type, alongside whatever the target filter's remaining
        // constructor parameters (IVendorAssignmentReader) resolve from DI.
        Arguments = [workItemType, routeParam];
    }
}

internal sealed class VendorAssignmentFilter(string workItemType, string routeParam, IVendorAssignmentReader vendorAssignmentReader)
    : IAsyncActionFilter
{
    private const string VendorUserRole = "VENDOR_USER";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.User.FindFirstValue("role") != VendorUserRole)
        {
            await next();
            return;
        }

        var vendorIdClaim = context.HttpContext.User.FindFirstValue("vendor_id");
        var hasRouteValue = context.RouteData.Values.TryGetValue(routeParam, out var routeValue);

        if (vendorIdClaim is null || !hasRouteValue || !Guid.TryParse(routeValue?.ToString(), out var workItemId))
        {
            context.Result = ServiceResult<object>.NotFound().ToActionResult(context.HttpContext);
            return;
        }

        var vendorId = Guid.Parse(vendorIdClaim);
        if (!await vendorAssignmentReader.IsAssignedAsync(vendorId, workItemType, workItemId))
        {
            context.Result = ServiceResult<object>.NotFound().ToActionResult(context.HttpContext);
            return;
        }

        await next();
    }
}
