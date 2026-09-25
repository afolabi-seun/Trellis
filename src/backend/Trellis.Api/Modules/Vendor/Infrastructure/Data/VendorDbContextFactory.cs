using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Vendor.Infrastructure.Data;

public sealed class VendorDbContextFactory : DesignTimeDbContextFactoryBase<VendorDbContext>
{
    protected override VendorDbContext CreateContext(DbContextOptions<VendorDbContext> options) => new(options);
}
