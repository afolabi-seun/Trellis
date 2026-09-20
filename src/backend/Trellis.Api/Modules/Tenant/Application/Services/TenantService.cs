using Trellis.Api.Core.Audit;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Trellis.Api.Modules.Tenant.Infrastructure.Data;
using TenantEntity = Trellis.Api.Modules.Tenant.Domain.Entities.Tenant;

namespace Trellis.Api.Modules.Tenant.Application.Services;

public sealed class TenantService(
    ITenantRepository repository,
    TenantDbContext dbContext,
    IAuthService authService,
    ISessionService sessionService,
    IAuditService auditService) : ITenantService
{
    public async Task<ServiceResult<TenantResponse>> ProvisionTenantAsync(TenantProvisionRequest request)
    {
        // multi_tenant_design.md's ProvisionTenantAsync: one transaction, spanning a cross-module
        // call into Auth. This is the one place TenantService touches TenantDbContext directly
        // rather than only ITenantRepository — a deliberate, narrow exception to "Application
        // never touches an Infrastructure concrete class," matching exactly what the given spec
        // code does, for transaction orchestration only; every query still goes through the
        // repository.
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var tenant = TenantEntity.Create(request.TenantName, request.CountryCode);
        await repository.AddAsync(tenant);
        await repository.SaveChangesAsync();

        var adminResult = await authService.CreateTenantAdminAsync(tenant.TenantId, request.AdminEmail);
        if (adminResult.Outcome != ServiceResultOutcome.Success)
        {
            await transaction.RollbackAsync();
            return ServiceResult<TenantResponse>.Failure(
                ErrorCodeRegistry.Tenant.AdminCreationFailed, "Tenant admin creation failed; tenant was not provisioned.");
        }

        await auditService.RecordAsync(
            tenant.TenantId, adminResult.Data!.UserId, AuditActorType.HumanUser, AuditTypes.Tenant.TenantProvisioned);

        await transaction.CommitAsync();
        return ServiceResult<TenantResponse>.Success(ToResponse(tenant));
    }

    public async Task<ServiceResult<TenantResponse>> GetByIdAsync(Guid tenantId)
    {
        var tenant = await repository.GetByIdAsync(tenantId);
        return tenant is null
            ? ServiceResult<TenantResponse>.NotFound()
            : ServiceResult<TenantResponse>.Success(ToResponse(tenant));
    }

    public async Task<ServiceResult<PagedResponse<TenantResponse>>> ListAsync(string? cursor, int limit)
    {
        var decoded = CursorCodec.Decode(cursor);
        var (items, hasMore, totalCount) = await repository.ListAsync(decoded, limit);

        var nextCursor = hasMore && items.Count > 0
            ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].TenantId)
            : null;

        return ServiceResult<PagedResponse<TenantResponse>>.Success(
            new PagedResponse<TenantResponse>(items.Select(ToResponse).ToList(), nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<TenantResponse>> OffboardTenantAsync(Guid tenantId)
    {
        var tenant = await repository.GetByIdAsync(tenantId);
        if (tenant is null)
        {
            return ServiceResult<TenantResponse>.NotFound();
        }

        try
        {
            tenant.BeginOffboarding();
        }
        catch (Domain.Exceptions.InvalidTenantStatusTransitionException)
        {
            return ServiceResult<TenantResponse>.BusinessRuleViolation(ErrorCodeRegistry.Tenant.InvalidStatusTransition);
        }

        await repository.SaveChangesAsync();

        // multi_tenant_design.md, Tenant Offboarding step 2 — the same revocation path logout
        // and password reset use, never a second mechanism.
        await sessionService.RevokeAllForTenantAsync(tenantId);

        await auditService.RecordAsync(
            tenantId, tenantId, AuditActorType.System, AuditTypes.Tenant.TenantOffboardingInitiated);

        // Steps 3 (async export job) and 5 (scheduled PII erasure at window expiry) are a
        // tracked follow-up, not built this phase — see the Phase 1 plan's deferral note.

        return ServiceResult<TenantResponse>.Success(ToResponse(tenant));
    }

    private static TenantResponse ToResponse(TenantEntity tenant) => new(
        tenant.TenantId,
        tenant.TenantName,
        tenant.CountryCode,
        tenant.Status.ToString().ToUpperInvariant(),
        tenant.SubscriptionTier.ToString().ToUpperInvariant(),
        tenant.CreatedAt);
}
