using Microsoft.Extensions.Options;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Dtos;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Configuration;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Application.Services;

public sealed class SubscriptionService(
    ISubscriptionRepository repository,
    IUsageSnapshotRepository usageSnapshotRepository,
    IInvoiceRepository invoiceRepository,
    IAuditService auditService,
    IOptions<BillingOptions> options) : ISubscriptionService
{
    public async Task<ServiceResult<SubscriptionSummaryResponse>> GetSummaryForTenantAsync(Guid tenantId)
    {
        var subscription = await repository.GetByTenantIdAsync(tenantId);
        if (subscription is null)
        {
            return ServiceResult<SubscriptionSummaryResponse>.NotFound();
        }

        var latestSnapshot = await usageSnapshotRepository.GetLatestForTenantAsync(tenantId);
        var projected = latestSnapshot is null ? (decimal?)null : latestSnapshot.ActiveUnitCount * options.Value.PerUnitRate;

        return ServiceResult<SubscriptionSummaryResponse>.Success(
            new SubscriptionSummaryResponse(ToResponse(subscription), latestSnapshot?.ActiveUnitCount, projected));
    }

    public async Task<Subscription> GetOrCreateForTenantAsync(Guid tenantId, string countryCode, DateTimeOffset tenantCreatedAt)
    {
        var existing = await repository.GetByTenantIdAsync(tenantId);
        if (existing is not null)
        {
            return existing;
        }

        var currency = CurrencyDefaults.ForCountry(countryCode);
        var anchorDay = Math.Clamp(tenantCreatedAt.Day, 1, 28);

        var subscription = Subscription.Create(tenantId, currency, anchorDay);
        await repository.AddAsync(subscription);
        await repository.SaveChangesAsync();

        await auditService.RecordAsync(tenantId, tenantId, AuditActorType.System, AuditTypes.Billing.SubscriptionCreated);

        return subscription;
    }

    public async Task<ServiceResult<PagedResponse<SubscriptionResponse>>> ListAllAsync(string? cursor, int limit)
    {
        var decoded = CursorCodec.Decode(cursor);
        var (items, hasMore, totalCount) = await repository.ListAllAsync(decoded, limit);

        var nextCursor = hasMore && items.Count > 0
            ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].SubscriptionId)
            : null;

        return ServiceResult<PagedResponse<SubscriptionResponse>>.Success(
            new PagedResponse<SubscriptionResponse>(items.Select(ToResponse).ToList(), nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<PlatformRevenueSummaryResponse>> GetPlatformRevenueSummaryAsync()
    {
        var counts = await repository.CountByStatusAsync();
        var mrr = await invoiceRepository.SumPaidTotalSinceAsync(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30));

        return ServiceResult<PlatformRevenueSummaryResponse>.Success(new PlatformRevenueSummaryResponse(
            mrr,
            counts.GetValueOrDefault(SubscriptionStatus.Trialing),
            counts.GetValueOrDefault(SubscriptionStatus.Active),
            counts.GetValueOrDefault(SubscriptionStatus.PastDue),
            counts.GetValueOrDefault(SubscriptionStatus.Suspended),
            counts.GetValueOrDefault(SubscriptionStatus.Canceled)));
    }

    private static SubscriptionResponse ToResponse(Subscription subscription) => new(
        subscription.SubscriptionId,
        subscription.TenantId,
        subscription.Status.ToUpperSnakeCase(),
        subscription.Currency,
        subscription.BillingCycleAnchorDay,
        subscription.CreatedAt);
}
