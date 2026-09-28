using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.RentLedger.Application.Dtos;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Domain.Entities;
using Trellis.Api.Modules.RentLedger.Domain.Exceptions;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.RentLedger.Application.Services;

/// <summary>
/// BDR-RENT-002 — a staff member's record that a rent payment was received outside the platform.
/// A record of an external event, not a transaction: nothing here processes, holds, or moves funds
/// (BDR-RENT-006), so who recorded or voided each entry is the audit trail.
/// </summary>
public sealed class RentPaymentService(
    IRentScheduleRepository scheduleRepository,
    IRentPaymentRecordRepository paymentRepository,
    IAuditService auditService) : IRentPaymentService
{
    public async Task<ServiceResult<RentPaymentRecordResponse>> RecordAsync(
        Guid rentScheduleId, RentPaymentRecordCreateRequest request, Guid recordedByUserId)
    {
        var schedule = await scheduleRepository.GetByIdAsync(rentScheduleId);
        if (schedule is null)
        {
            return ServiceResult<RentPaymentRecordResponse>.NotFound();
        }

        if (schedule.Status != RentScheduleStatus.Active)
        {
            return ServiceResult<RentPaymentRecordResponse>.BusinessRuleViolation(ErrorCodeRegistry.RentLedger.ScheduleNotActive);
        }

        var record = RentPaymentRecord.Create(
            TenantContext.Current!.TenantId, rentScheduleId, request.AmountReceived, request.DateReceived,
            request.MethodReference, recordedByUserId);

        await paymentRepository.AddAsync(record);
        await paymentRepository.SaveChangesAsync();

        await auditService.RecordAsync(
            record.TenantId, recordedByUserId, AuditActorType.HumanUser, AuditTypes.RentLedger.RentPaymentRecorded,
            $"{{\"rentScheduleId\":\"{rentScheduleId}\",\"rentPaymentRecordId\":\"{record.RentPaymentRecordId}\"}}");

        return ServiceResult<RentPaymentRecordResponse>.Success(ToResponse(record));
    }

    public async Task<ServiceResult<RentPaymentRecordResponse>> VoidAsync(
        Guid rentScheduleId, Guid rentPaymentRecordId, RentPaymentRecordVoidRequest request, Guid voidedByUserId)
    {
        var record = await paymentRepository.GetByIdAsync(rentPaymentRecordId);
        if (record is null || record.RentScheduleId != rentScheduleId)
        {
            return ServiceResult<RentPaymentRecordResponse>.NotFound();
        }

        try
        {
            record.Void(voidedByUserId, request.Reason);
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<RentPaymentRecordResponse>.BusinessRuleViolation(ErrorCodeRegistry.RentLedger.InvalidStatusTransition);
        }

        await paymentRepository.SaveChangesAsync();

        await auditService.RecordAsync(
            record.TenantId, voidedByUserId, AuditActorType.HumanUser, AuditTypes.RentLedger.RentPaymentVoided,
            $"{{\"rentScheduleId\":\"{rentScheduleId}\",\"rentPaymentRecordId\":\"{record.RentPaymentRecordId}\"}}");

        return ServiceResult<RentPaymentRecordResponse>.Success(ToResponse(record));
    }

    public async Task<ServiceResult<IReadOnlyList<RentPaymentRecordResponse>>> ListAsync(Guid rentScheduleId)
    {
        if (await scheduleRepository.GetByIdAsync(rentScheduleId) is null)
        {
            return ServiceResult<IReadOnlyList<RentPaymentRecordResponse>>.NotFound();
        }

        var records = await paymentRepository.ListForScheduleAsync(rentScheduleId);
        return ServiceResult<IReadOnlyList<RentPaymentRecordResponse>>.Success(records.Select(ToResponse).ToList());
    }

    private static RentPaymentRecordResponse ToResponse(RentPaymentRecord record) => new(
        record.RentPaymentRecordId, record.RentScheduleId, record.AmountReceived, record.DateReceived, record.MethodReference,
        record.RecordedByUserId, record.RecordedAt, record.IsVoided, record.VoidedAt, record.VoidReason);
}
