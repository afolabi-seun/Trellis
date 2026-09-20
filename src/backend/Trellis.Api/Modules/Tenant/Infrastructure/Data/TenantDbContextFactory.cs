using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Tenant.Infrastructure.Data;

public sealed class TenantDbContextFactory : DesignTimeDbContextFactoryBase<TenantDbContext>
{
    protected override TenantDbContext CreateContext(DbContextOptions<TenantDbContext> options) => new(options);
}
