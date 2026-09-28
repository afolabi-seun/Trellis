using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Billing.Infrastructure.Data;

public sealed class BillingDbContextFactory : DesignTimeDbContextFactoryBase<BillingDbContext>
{
    protected override BillingDbContext CreateContext(DbContextOptions<BillingDbContext> options) => new(options);
}
