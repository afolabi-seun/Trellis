using Microsoft.EntityFrameworkCore;

namespace Trellis.Api.Core.Data;

public sealed class CoreDbContextFactory : DesignTimeDbContextFactoryBase<CoreDbContext>
{
    protected override CoreDbContext CreateContext(DbContextOptions<CoreDbContext> options) => new(options);
}
