using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Interfaces;

namespace Trellis.Api.Modules.Billing.Presentation.Filters;

/// <summary>
/// BDR-BIL-009 — gates an Add-on module's controller behind the caller's tenant having it
/// ACTIVE. Lives inside Billing's own Presentation/Filters, not Core: Core never depends on a
/// Module (confirmed — no existing Core/ file imports any Modules.* namespace), and this filter's
/// only real dependency is IBillingService, which Billing itself owns. Other modules (Inspection,
/// Ai) reference this type via a normal cross-module using, the same way Document already
/// references Workflow's/Ai's own Application interfaces directly. Shaped exactly like
/// RequiresProfessionalTierAttribute (TypeFilterAttribute/IAsyncActionFilter), but takes a
/// moduleKey constructor argument since, unlike the Ai-only tier gate, one attribute instance
/// serves every Add-on module.
/// </summary>
public sealed class RequiresModuleEntitlementAttribute : TypeFilterAttribute
{
    public RequiresModuleEntitlementAttribute(string moduleKey) : base(typeof(RequiresModuleEntitlementFilter))
    {
        Arguments = [moduleKey];
    }
}

internal sealed class RequiresModuleEntitlementFilter(string moduleKey, IBillingService billingService) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var tenantId = TenantContext.Current?.TenantId;
        if (tenantId is null || !await billingService.IsModuleEntitledAsync(tenantId.Value, moduleKey))
        {
            context.Result = ServiceResult<object>.Forbidden(ErrorCodeRegistry.Billing.ModuleNotEntitled).ToActionResult(context.HttpContext);
            return;
        }

        await next();
    }
}
