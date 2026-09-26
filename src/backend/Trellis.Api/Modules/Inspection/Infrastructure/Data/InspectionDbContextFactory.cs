using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Inspection.Infrastructure.Data;

public sealed class InspectionDbContextFactory : DesignTimeDbContextFactoryBase<InspectionDbContext>
{
    protected override InspectionDbContext CreateContext(DbContextOptions<InspectionDbContext> options) => new(options);
}
