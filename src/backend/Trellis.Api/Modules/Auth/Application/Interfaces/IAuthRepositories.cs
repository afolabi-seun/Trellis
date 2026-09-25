using Trellis.Api.Modules.Auth.Domain.Entities;

namespace Trellis.Api.Modules.Auth.Application.Interfaces;

public interface IAppUserRepository
{
    Task AddAsync(AppUser user);

    Task<AppUser?> GetByIdAsync(Guid userId);

    /// <summary>Username is globally unique (data_model.md) — not tenant-scoped, so this isn't tenant-filtered.</summary>
    Task<AppUser?> GetByUsernameAsync(string username);

    /// <summary>Every TENANT_ADMIN plus every DEPARTMENT_STAFF whose DepartmentIds contains <paramref name="departmentId"/> — Workflow's own notify-the-newly-active-step's-department fan-out (BDR-WF-003).</summary>
    Task<IReadOnlyList<AppUser>> GetByDepartmentAsync(Guid tenantId, Guid departmentId);

    Task SaveChangesAsync();
}

public interface ISessionRepository
{
    Task AddAsync(Session session);

    Task<Session?> GetByIdAsync(Guid sessionId);

    /// <summary>Looked up by hash — the raw refresh token itself is never stored (security_guide.md).</summary>
    Task<Session?> GetByRefreshTokenHashAsync(string refreshTokenHash);

    Task<IReadOnlyList<Session>> GetActiveForUserAsync(Guid userId);

    Task<IReadOnlyList<Session>> GetActiveForTenantAsync(Guid tenantId);

    Task SaveChangesAsync();
}

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token);

    Task<PasswordResetToken?> GetByHashAsync(string tokenHash);

    Task SaveChangesAsync();
}

public interface IMfaRecoveryCodeRepository
{
    Task AddRangeAsync(IEnumerable<MfaRecoveryCode> codes);

    Task<MfaRecoveryCode?> GetByHashAsync(Guid userId, string codeHash);

    /// <summary>Full regeneration invalidates every previously issued code (security_guide.md) — deletes rather than marks used, since a used code should stay auditable but a superseded-by-regeneration one has nothing left to audit.</summary>
    Task DeleteAllForUserAsync(Guid userId);

    Task SaveChangesAsync();
}
