using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Reporting.Application.Dtos;

namespace Trellis.Api.Modules.Reporting.Application.Interfaces;

public interface IDashboardService
{
    /// <summary>Tenant-scoped implicitly via TenantContext.Current, matching every other own-tenant summary method's own convention of no explicit tenant parameter.</summary>
    Task<ServiceResult<DashboardSummaryResponse>> GetSummaryAsync();
}
