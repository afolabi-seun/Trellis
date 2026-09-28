using Trellis.Api.Core.Audit;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Dtos;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Application.Services;

public sealed class ModuleEntitlementService(IModuleEntitlementRepository repository, IAuditService auditService) : IModuleEntitlementService
{
    // The same Add-on modules the InitialBilling migration's retrofit backfilled for pre-existing
    // tenants — kept in sync here so this bootstrap step and that one-time migration seed produce
    // an identical result, just for tenants that didn't exist yet when the migration ran.
    private static readonly string[] DefaultFreeModuleKeys = ["INSPECTION", "AI"];

    public async Task<bool> IsEntitledAsync(Guid tenantId, string moduleKey)
    {
        var entitlement = await repository.GetAsync(tenantId, moduleKey);
        return entitlement is { Status: EntitlementStatus.Active };
    }

    public async Task<ServiceResult<IReadOnlyList<ModuleEntitlementResponse>>> ListForTenantAsync(Guid tenantId)
    {
        var entitlements = await repository.ListForTenantAsync(tenantId);
        return ServiceResult<IReadOnlyList<ModuleEntitlementResponse>>.Success(entitlements.Select(ToResponse).ToList());
    }

    public async Task<ServiceResult<ModuleEntitlementResponse>> SetAsync(Guid tenantId, string moduleKey, ModuleEntitlementSetRequest request)
    {
        var entitlement = await repository.GetAsync(tenantId, moduleKey);
        var targetActive = request.Status == "ACTIVE";

        if (entitlement is null)
        {
            entitlement = ModuleEntitlement.Create(tenantId, moduleKey, request.MonthlyPrice);
            if (!targetActive)
            {
                entitlement.Disable();
            }

            await repository.AddAsync(entitlement);
        }
        else
        {
            entitlement.UpdatePrice(request.MonthlyPrice);
            if (targetActive && entitlement.Status == EntitlementStatus.Disabled)
            {
                entitlement.Enable();
            }
            else if (!targetActive && entitlement.Status == EntitlementStatus.Active)
            {
                entitlement.Disable();
            }
        }

        await repository.SaveChangesAsync();

        await auditService.RecordAsync(
            tenantId, tenantId, AuditActorType.System, AuditTypes.Billing.ModuleEntitlementChanged,
            $"{{\"moduleKey\":\"{moduleKey}\",\"status\":\"{request.Status}\",\"monthlyPrice\":{request.MonthlyPrice}}}");

        return ServiceResult<ModuleEntitlementResponse>.Success(ToResponse(entitlement));
    }

    public async Task EnsureDefaultEntitlementsAsync(Guid tenantId)
    {
        var wroteAny = false;

        foreach (var moduleKey in DefaultFreeModuleKeys)
        {
            if (await repository.GetAsync(tenantId, moduleKey) is not null)
            {
                continue;
            }

            await repository.AddAsync(ModuleEntitlement.Create(tenantId, moduleKey, 0));
            wroteAny = true;
        }

        if (wroteAny)
        {
            await repository.SaveChangesAsync();
        }
    }

    private static ModuleEntitlementResponse ToResponse(ModuleEntitlement entitlement) => new(
        entitlement.EntitlementId,
        entitlement.ModuleKey,
        entitlement.Status.ToUpperSnakeCase(),
        entitlement.MonthlyPrice,
        entitlement.EnabledAt,
        entitlement.DisabledAt);
}
