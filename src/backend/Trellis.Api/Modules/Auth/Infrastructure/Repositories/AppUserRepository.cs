using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Auth.Domain.Entities;
using Trellis.Api.Modules.Auth.Infrastructure.Data;

namespace Trellis.Api.Modules.Auth.Infrastructure.Repositories;

public sealed class AppUserRepository(AuthDbContext dbContext) : IAppUserRepository
{
    public async Task AddAsync(AppUser user) => await dbContext.Users.AddAsync(user);

    public Task<AppUser?> GetByIdAsync(Guid userId) => dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);

    public Task<AppUser?> GetByUsernameAsync(string username) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
