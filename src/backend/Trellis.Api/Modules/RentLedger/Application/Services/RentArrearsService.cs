using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Notification.Application.Dtos;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Domain.Entities;
using Trellis.Api.Modules.RentLedger.Domain.ValueObjects;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.RentLedger.Application.Services;

/// <summary>
/// BDR-RENT-003/004. Whether a schedule is in arrears is computed on read (RentStatusCalculator);
/// this only decides when to tell staff. The dedupe key is per period, not a once-ever timestamp:
/// LastArrearsAlertedPeriod holds the due date of the latest past-grace unpaid period at the last
/// alert, so a schedule that stays unpaid alerts again each time another period slips past grace,
/// and one that is paid up then falls behind later alerts afresh.
///
/// Runs under the caller's SystemContext with no ambient tenant, so the cross-tenant batch query is
/// unscoped and each tenant's entitlement is checked explicitly (a job can't use the attribute).
/// IUnitAssetService (the unit label for the email) is a Scoped()/TenantContext-shaped cross-module
/// service, so <c>TenantContext.Current</c> is set here, in this method's own loop body — never in
/// a nested async helper, where an AsyncLocal write would not flow back to this frame.
/// </summary>
public sealed class RentArrearsService(
    IRentScheduleRepository scheduleRepository,
    IRentPaymentRecordRepository paymentRepository,
    IModuleEntitlementService moduleEntitlementService,
    IAuthService authService,
    IUnitAssetService unitAssetService,
    INotificationService notificationService,
    IAuditService auditService,
    ILogger<RentArrearsService> logger) : IRentArrearsService
{
    private const int BatchSize = 100;
    private static readonly string[] StaffRoles = ["TENANT_ADMIN", "DEPARTMENT_STAFF"];

    public async Task<int> RaiseArrearsAlertsAsync(DateOnly asOf)
    {
        var raised = 0;
        var entitlement = new Dictionary<Guid, bool>();
        Guid? afterId = null;

        try
        {
            while (true)
            {
                var batch = await scheduleRepository.ListActiveAcrossTenantsAsync(afterId, BatchSize);
                if (batch.Count == 0)
                {
                    break;
                }

                afterId = batch[^1].RentScheduleId;
                var totals = await paymentRepository.GetReceivedTotalsAsync(batch.Select(s => s.RentScheduleId).ToList());

                foreach (var schedule in batch)
                {
                    try
                    {
                        if (!entitlement.TryGetValue(schedule.TenantId, out var entitled))
                        {
                            entitled = await moduleEntitlementService.IsEntitledAsync(schedule.TenantId, RentScheduleService.ModuleKey);
                            entitlement[schedule.TenantId] = entitled;
                        }

                        if (!entitled)
                        {
                            continue;
                        }

                        var evaluation = schedule.Evaluate(totals.GetValueOrDefault(schedule.RentScheduleId), asOf);
                        if (evaluation.Status != RentStatus.InArrears || evaluation.LatestPastGraceDue is not { } period)
                        {
                            continue;
                        }

                        if (schedule.LastArrearsAlertedPeriod is { } lastAlerted && lastAlerted >= period)
                        {
                            continue;
                        }

                        TenantContext.Current = new TenantContext(schedule.TenantId);

                        await NotifyStaffAsync(schedule, evaluation);

                        schedule.MarkArrearsAlerted(period);
                        await scheduleRepository.SaveChangesAsync();

                        await auditService.RecordAsync(
                            schedule.TenantId, schedule.TenantId, AuditActorType.System, AuditTypes.RentLedger.RentArrearsFlagged,
                            $"{{\"rentScheduleId\":\"{schedule.RentScheduleId}\",\"period\":\"{period:yyyy-MM-dd}\"}}");

                        raised++;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // One bad schedule (e.g. a concurrent edit tripping the row version) must not starve every other tenant's alerts; it is retried on the next poll.
                        scheduleRepository.DiscardChanges(schedule);
                        logger.LogError(ex, "Arrears alert failed for rent schedule {RentScheduleId}", schedule.RentScheduleId);
                    }
                }
            }
        }
        finally
        {
            TenantContext.Current = null;
        }

        return raised;
    }

    private async Task NotifyStaffAsync(RentSchedule schedule, RentEvaluation evaluation)
    {
        var unit = await unitAssetService.GetByIdAsync(schedule.UnitAssetId);
        var unitLabel = unit.Outcome == ServiceResultOutcome.Success ? unit.Data!.Label : "a unit";

        var staff = await authService.GetUsersByRolesAsync(schedule.TenantId, StaffRoles);
        foreach (var user in staff)
        {
            await notificationService.SendAsync(
                schedule.TenantId, "RENT_ARREARS_FLAGGED", user.Email,
                new
                {
                    UnitLabel = unitLabel,
                    RentAmount = schedule.RentAmount.ToString("F2", CultureInfo.InvariantCulture),
                    schedule.Currency,
                    PeriodsInArrears = evaluation.PeriodsInArrears.ToString(CultureInfo.InvariantCulture),
                    OldestUnpaidDueDate = evaluation.OldestUnpaidDueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                    AmountShort = evaluation.AmountShort.ToString("F2", CultureInfo.InvariantCulture),
                },
                new RelatedEntityRef("RentSchedule", schedule.RentScheduleId));
        }
    }
}
