using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Privacy;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Auth.Domain.Entities;

namespace Trellis.Api.Modules.Auth.Application.Services;

/// <summary>
/// A staff subject (app_user) — matched by Username, the only stable identifier AppUser has (no
/// email column at all; every seeded staff login in this codebase is email-shaped anyway).
/// DataSubjectService's own CanEraseStaffAsync guard (self/last-admin) runs before this handler
/// is ever invoked, so EraseAsync itself never needs to re-check that.
/// </summary>
public sealed class AuthPersonalDataHandler(IAppUserRepository userRepository, ISessionService sessionService) : IPersonalDataHandler
{
    public string ModuleName => "Auth";

    public async Task<object?> ExportAsync(string subjectEmail)
    {
        var user = await ResolveAsync(subjectEmail);
        return user is null ? null : new { user.UserId, user.Username, Role = user.Role.ToString(), Status = user.Status.ToString() };
    }

    public async Task<ErasureSummary> EraseAsync(string subjectEmail)
    {
        var user = await ResolveAsync(subjectEmail);
        if (user is null)
        {
            return new ErasureSummary(ModuleName, 0);
        }

        user.RedactForErasure();
        await userRepository.SaveChangesAsync();
        await sessionService.RevokeAllExceptAsync(user.UserId, exceptSessionId: null);

        return new ErasureSummary(ModuleName, 1);
    }

    /// <summary>GetByUsernameAsync is global (Username is unique platform-wide), so the tenant match is checked explicitly here — AppUser isn't ITenantOwned/TenantAwareRepository-based.</summary>
    private async Task<AppUser?> ResolveAsync(string subjectEmail)
    {
        var user = await userRepository.GetByUsernameAsync(subjectEmail);
        return user?.TenantId == TenantContext.Current!.TenantId ? user : null;
    }
}
