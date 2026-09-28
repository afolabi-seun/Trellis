using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Reporting.Infrastructure.Data;

public sealed class ReportingDbContextFactory : DesignTimeDbContextFactoryBase<ReportingDbContext>
{
    protected override ReportingDbContext CreateContext(DbContextOptions<ReportingDbContext> options) => new(options);
}
