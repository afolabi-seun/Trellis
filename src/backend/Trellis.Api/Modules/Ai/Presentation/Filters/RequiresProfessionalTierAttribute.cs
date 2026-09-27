using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Ai.Application.Interfaces;

namespace Trellis.Api.Modules.Ai.Presentation.Filters;

/// <summary>
/// billing_design.md — the Ai module is reserved for PROFESSIONAL+ tenants. Shaped like
/// VendorAssignedToWorkItemAttribute (TypeFilterAttribute/IAsyncActionFilter), but 403s (not 404):
/// unlike a Vendor scope mismatch, a tier gate isn't hiding the existence of another tenant's
/// data — it's telling the caller's own tenant its plan doesn't include this module.
/// </summary>
public sealed class RequiresProfessionalTierAttribute : TypeFilterAttribute
{
    public RequiresProfessionalTierAttribute() : base(typeof(RequiresProfessionalTierFilter))
    {
    }
}

internal sealed class RequiresProfessionalTierFilter(IAiTierGate tierGate) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var tenantId = TenantContext.Current?.TenantId;
        if (tenantId is null || !await tierGate.IsEntitledAsync(tenantId.Value))
        {
            context.Result = ServiceResult<object>.Forbidden(ErrorCodeRegistry.Ai.TierNotEntitled).ToActionResult(context.HttpContext);
            return;
        }

        await next();
    }
}
