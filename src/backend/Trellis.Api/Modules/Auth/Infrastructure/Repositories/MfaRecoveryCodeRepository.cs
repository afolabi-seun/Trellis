using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Auth.Domain.Entities;
using Trellis.Api.Modules.Auth.Infrastructure.Data;

namespace Trellis.Api.Modules.Auth.Infrastructure.Repositories;

public sealed class MfaRecoveryCodeRepository(AuthDbContext dbContext) : IMfaRecoveryCodeRepository
{
    public async Task AddRangeAsync(IEnumerable<MfaRecoveryCode> codes) => await dbContext.MfaRecoveryCodes.AddRangeAsync(codes);

    public Task<MfaRecoveryCode?> GetByHashAsync(Guid userId, string codeHash) =>
        dbContext.MfaRecoveryCodes.FirstOrDefaultAsync(c => c.UserId == userId && c.CodeHash == codeHash);

    public async Task DeleteAllForUserAsync(Guid userId)
    {
        var codes = await dbContext.MfaRecoveryCodes.Where(c => c.UserId == userId).ToListAsync();
        dbContext.MfaRecoveryCodes.RemoveRange(codes);
    }

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
