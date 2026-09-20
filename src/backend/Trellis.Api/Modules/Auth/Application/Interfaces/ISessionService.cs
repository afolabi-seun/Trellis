namespace Trellis.Api.Modules.Auth.Application.Interfaces;

/// <summary>
/// The single session-revocation path every caller shares — logout, password reset, and tenant
/// offboarding all revoke through here, never by writing <c>revoked_at</c> directly
/// (security_guide.md, "Session Revocation &amp; Logout").
/// </summary>
public interface ISessionService
{
    /// <summary>Revokes exactly one session — <c>logout</c>.</summary>
    Task RevokeAsync(Guid sessionId);

    /// <summary>Revokes every session for a user except one — <c>logout/all-other-sessions</c>, and password reset (with no exception).</summary>
    Task RevokeAllExceptAsync(Guid userId, Guid? exceptSessionId);

    /// <summary>
    /// Revokes every active session for every user belonging to a tenant — tenant offboarding's
    /// step 2 (multi_tenant_design.md), the same underlying mechanism as the other two, not a
    /// second implementation.
    /// </summary>
    Task RevokeAllForTenantAsync(Guid tenantId);
}
