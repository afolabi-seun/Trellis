using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Auth.Domain.Entities;
using Trellis.Api.Modules.Auth.Infrastructure.Data;

namespace Trellis.Api.Modules.Auth.Infrastructure.Repositories;

public sealed class PasswordResetTokenRepository(AuthDbContext dbContext) : IPasswordResetTokenRepository
{
    public async Task AddAsync(PasswordResetToken token) => await dbContext.PasswordResetTokens.AddAsync(token);

    public Task<PasswordResetToken?> GetByHashAsync(string tokenHash) =>
        dbContext.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
