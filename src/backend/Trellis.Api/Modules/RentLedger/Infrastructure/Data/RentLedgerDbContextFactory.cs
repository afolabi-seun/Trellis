using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.RentLedger.Infrastructure.Data;

public sealed class RentLedgerDbContextFactory : DesignTimeDbContextFactoryBase<RentLedgerDbContext>
{
    protected override RentLedgerDbContext CreateContext(DbContextOptions<RentLedgerDbContext> options) => new(options);
}
