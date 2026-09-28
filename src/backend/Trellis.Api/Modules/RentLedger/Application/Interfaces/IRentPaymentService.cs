using Trellis.Api.Core.Results;
using Trellis.Api.Modules.RentLedger.Application.Dtos;

namespace Trellis.Api.Modules.RentLedger.Application.Interfaces;

/// <summary>BDR-RENT-002 — recording a payment that was received outside the platform. Nothing here processes, holds, or moves funds (BDR-RENT-006).</summary>
public interface IRentPaymentService
{
    Task<ServiceResult<RentPaymentRecordResponse>> RecordAsync(Guid rentScheduleId, RentPaymentRecordCreateRequest request, Guid recordedByUserId);

    Task<ServiceResult<RentPaymentRecordResponse>> VoidAsync(Guid rentScheduleId, Guid rentPaymentRecordId, RentPaymentRecordVoidRequest request, Guid voidedByUserId);

    Task<ServiceResult<IReadOnlyList<RentPaymentRecordResponse>>> ListAsync(Guid rentScheduleId);
}
