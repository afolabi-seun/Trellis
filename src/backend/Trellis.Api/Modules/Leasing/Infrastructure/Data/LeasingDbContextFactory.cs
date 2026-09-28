using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Data;

public sealed class LeasingDbContextFactory : DesignTimeDbContextFactoryBase<LeasingDbContext>
{
    protected override LeasingDbContext CreateContext(DbContextOptions<LeasingDbContext> options) => new(options);
}
