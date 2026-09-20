using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Auth.Domain.Entities;
using Trellis.Api.Modules.Auth.Infrastructure.Data;

namespace Trellis.Api.Modules.Auth.Infrastructure.Repositories;

public sealed class SessionRepository(AuthDbContext dbContext) : ISessionRepository
{
    public async Task AddAsync(Session session) => await dbContext.Sessions.AddAsync(session);

    public Task<Session?> GetByIdAsync(Guid sessionId) =>
        dbContext.Sessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);

    public Task<Session?> GetByRefreshTokenHashAsync(string refreshTokenHash) =>
        dbContext.Sessions.FirstOrDefaultAsync(s => s.RefreshTokenHash == refreshTokenHash);

    public Task<IReadOnlyList<Session>> GetActiveForUserAsync(Guid userId) =>
        QueryActive(dbContext.Sessions.Where(s => s.UserId == userId));

    public Task<IReadOnlyList<Session>> GetActiveForTenantAsync(Guid tenantId)
    {
        var query =
            from session in dbContext.Sessions
            join user in dbContext.Users on session.UserId equals user.UserId
            where user.TenantId == tenantId
            select session;

        return QueryActive(query);
    }

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();

    private static async Task<IReadOnlyList<Session>> QueryActive(IQueryable<Session> query) =>
        await query.Where(s => s.RevokedAt == null && s.ExpiresAt > DateTimeOffset.UtcNow).ToListAsync();
}
