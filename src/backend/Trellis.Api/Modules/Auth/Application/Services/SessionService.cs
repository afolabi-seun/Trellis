using Trellis.Api.Modules.Auth.Application.Interfaces;

namespace Trellis.Api.Modules.Auth.Application.Services;

/// <summary>The single revocation path logout, password reset, and tenant offboarding all share (security_guide.md).</summary>
public sealed class SessionService(ISessionRepository repository) : ISessionService
{
    public async Task RevokeAsync(Guid sessionId)
    {
        var session = await repository.GetByIdAsync(sessionId);
        if (session is null)
        {
            return;
        }

        session.Revoke();
        await repository.SaveChangesAsync();
    }

    public async Task RevokeAllExceptAsync(Guid userId, Guid? exceptSessionId)
    {
        var sessions = await repository.GetActiveForUserAsync(userId);
        foreach (var session in sessions.Where(s => s.SessionId != exceptSessionId))
        {
            session.Revoke();
        }

        await repository.SaveChangesAsync();
    }

    public async Task RevokeAllForTenantAsync(Guid tenantId)
    {
        var sessions = await repository.GetActiveForTenantAsync(tenantId);
        foreach (var session in sessions)
        {
            session.Revoke();
        }

        await repository.SaveChangesAsync();
    }
}
