using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Tenant.Application.Dtos;

namespace Trellis.Api.Modules.Tenant.Application.Interfaces;

/// <summary>
/// security_guide.md's POPIA data-subject rights, orchestrated across every module's own
/// IPersonalDataHandler — the caller's own tenant only (TenantContext.Current), TENANT_ADMIN
/// only (enforced at the controller). Both actions are audited; erasure additionally requires a
/// typed email confirmation and a password re-entry before anything is touched.
/// </summary>
public interface IDataSubjectService
{
    Task<ServiceResult<DataSubjectExportResponse>> ExportAsync(DataSubjectExportRequest request, Guid actorUserId);

    /// <summary>
    /// Forbidden(Auth.ReauthenticationFailed) on a wrong password; BusinessRuleViolation
    /// (Tenant.CannotEraseSelfOrLastAdmin) when the subject is the caller or the tenant's last
    /// active TENANT_ADMIN — checked before any module's handler runs, so a refused request
    /// touches nothing.
    /// </summary>
    Task<ServiceResult<DataSubjectErasureResponse>> EraseAsync(EraseDataSubjectRequest request, Guid actorUserId);
}
