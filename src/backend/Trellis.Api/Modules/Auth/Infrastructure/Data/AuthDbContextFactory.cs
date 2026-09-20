using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Auth.Infrastructure.Data;

public sealed class AuthDbContextFactory : DesignTimeDbContextFactoryBase<AuthDbContext>
{
    protected override AuthDbContext CreateContext(DbContextOptions<AuthDbContext> options) => new(options);
}
