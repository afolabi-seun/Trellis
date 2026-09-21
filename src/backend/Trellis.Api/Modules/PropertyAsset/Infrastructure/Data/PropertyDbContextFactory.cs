using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.PropertyAsset.Infrastructure.Data;

public sealed class PropertyDbContextFactory : DesignTimeDbContextFactoryBase<PropertyDbContext>
{
    protected override PropertyDbContext CreateContext(DbContextOptions<PropertyDbContext> options) => new(options);
}
