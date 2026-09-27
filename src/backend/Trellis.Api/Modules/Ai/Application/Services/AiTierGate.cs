using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Ai.Application.Interfaces;
using Trellis.Api.Modules.Tenant.Application.Interfaces;

namespace Trellis.Api.Modules.Ai.Application.Services;

public sealed class AiTierGate(ITenantService tenantService) : IAiTierGate
{
    public async Task<bool> IsEntitledAsync(Guid tenantId)
    {
        var tenant = await tenantService.GetByIdAsync(tenantId);
        return tenant.Outcome == ServiceResultOutcome.Success && tenant.Data!.SubscriptionTier != "STARTER";
    }
}
