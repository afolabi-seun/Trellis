using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.RentLedger.Domain.Entities;

namespace Trellis.Api.Modules.RentLedger.Infrastructure.Data;

public sealed class RentLedgerDbContext(DbContextOptions<RentLedgerDbContext> options) : DbContext(options)
{
    public DbSet<RentSchedule> RentSchedules => Set<RentSchedule>();
    public DbSet<RentPaymentRecord> RentPaymentRecords => Set<RentPaymentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(RentLedgerDbContext).Assembly,
            t => t.Namespace?.StartsWith("Trellis.Api.Modules.RentLedger.", StringComparison.Ordinal) == true);
}
