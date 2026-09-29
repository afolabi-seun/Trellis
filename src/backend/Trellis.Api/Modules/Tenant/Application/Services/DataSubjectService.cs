using System.Security.Cryptography;
using System.Text;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Privacy;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;

namespace Trellis.Api.Modules.Tenant.Application.Services;

public sealed class DataSubjectService(
    IEnumerable<IPersonalDataHandler> handlers, IAuthService authService, IAuditService auditService) : IDataSubjectService
{
    /// <summary>
    /// A fixed run order, not DI registration order (which is an implementation detail no caller
    /// should depend on): Reporting resolves an owner subject by asking PropertyAsset's own
    /// IOwnerService.GetByEmailAsync for the owner id, so it must run before PropertyAsset's own
    /// handler has redacted that same email out from under it — found the hard way, via a live
    /// smoke test where an owner's report silently stopped being erased once Owner.RedactPii ran
    /// first. Every other handler resolves directly off its own module's data, so ties are broken
    /// by ModuleName for a stable, predictable order.
    /// </summary>
    private static readonly string[] ErasureOrder = ["Reporting", "PropertyAsset", "Leasing", "Notification", "Auth"];

    private List<IPersonalDataHandler> OrderedHandlers() => handlers
        .OrderBy(h => Array.IndexOf(ErasureOrder, h.ModuleName) is var i && i >= 0 ? i : int.MaxValue)
        .ThenBy(h => h.ModuleName, StringComparer.Ordinal)
        .ToList();

    public async Task<ServiceResult<DataSubjectExportResponse>> ExportAsync(DataSubjectExportRequest request, Guid actorUserId)
    {
        var modules = new Dictionary<string, object>();
        foreach (var handler in OrderedHandlers())
        {
            var data = await handler.ExportAsync(request.SubjectEmail);
            if (data is not null)
            {
                modules[handler.ModuleName] = data;
            }
        }

        var tenantId = TenantContext.Current!.TenantId;
        await auditService.RecordAsync(
            tenantId, actorUserId, AuditActorType.HumanUser, AuditTypes.Tenant.DataSubjectExported,
            $$"""{"subjectEmailSha256":"{{HashEmail(request.SubjectEmail)}}","modules":{{modules.Count}}}""");

        return ServiceResult<DataSubjectExportResponse>.Success(new DataSubjectExportResponse(modules));
    }

    public async Task<ServiceResult<DataSubjectErasureResponse>> EraseAsync(EraseDataSubjectRequest request, Guid actorUserId)
    {
        var reauth = await authService.ReauthenticateAsync(actorUserId, request.CurrentPassword);
        if (reauth.Outcome != ServiceResultOutcome.Success)
        {
            return ServiceResult<DataSubjectErasureResponse>.Forbidden(ErrorCodeRegistry.Auth.ReauthenticationFailed);
        }

        if (!await authService.CanEraseStaffAsync(request.SubjectEmail, actorUserId))
        {
            return ServiceResult<DataSubjectErasureResponse>.BusinessRuleViolation(ErrorCodeRegistry.Tenant.CannotEraseSelfOrLastAdmin);
        }

        var counts = new List<ModuleErasureCount>();
        foreach (var handler in OrderedHandlers())
        {
            var summary = await handler.EraseAsync(request.SubjectEmail);
            counts.Add(new ModuleErasureCount(summary.ModuleName, summary.RecordsAffected));
        }

        var tenantId = TenantContext.Current!.TenantId;
        var countsJson = string.Join(',', counts.Select(c => $$"""{"module":"{{c.ModuleName}}","recordsAffected":{{c.RecordsAffected}}}"""));
        var reasonJson = request.Reason is null ? "null" : $"\"{request.Reason.Replace("\"", "'")}\"";
        await auditService.RecordAsync(
            tenantId, actorUserId, AuditActorType.HumanUser, AuditTypes.Tenant.DataErasure,
            $$"""{"subjectEmailSha256":"{{HashEmail(request.SubjectEmail)}}","reason":{{reasonJson}},"counts":[{{countsJson}}]}""");

        return ServiceResult<DataSubjectErasureResponse>.Success(new DataSubjectErasureResponse(counts));
    }

    private static string HashEmail(string email) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()))).ToLowerInvariant();
}
