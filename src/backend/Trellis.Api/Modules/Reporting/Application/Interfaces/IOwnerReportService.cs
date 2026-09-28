using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Reporting.Application.Dtos;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.Reporting.Application.Interfaces;

public interface IOwnerReportService
{
    Task<ServiceResult<OwnerReportResponse>> GenerateAsync(
        Guid ownerId, OwnerReportPeriodType periodType, DateOnly periodStart, DateOnly periodEnd, Guid generatedBy);

    Task<ServiceResult<OwnerReportResponse>> GetByIdAsync(Guid ownerReportId);

    Task<ServiceResult<PagedResponse<OwnerReportResponse>>> ListByOwnerAsync(Guid? ownerId, string? cursor, int limit);

    Task<ServiceResult<byte[]>> DownloadPdfAsync(Guid ownerReportId);

    Task<ServiceResult<byte[]>> DownloadCsvAsync(Guid ownerReportId);

    /// <summary>Dispatches through Notification's own INotificationService — never blocks on delivery succeeding (BDR-NOTIF-006), same as every other module's own SendAsync call site.</summary>
    Task<ServiceResult<bool>> DeliverByEmailAsync(Guid ownerReportId, Guid actorId);
}
