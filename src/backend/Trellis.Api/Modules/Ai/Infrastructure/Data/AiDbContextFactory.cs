using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Ai.Infrastructure.Data;

public sealed class AiDbContextFactory : DesignTimeDbContextFactoryBase<AiDbContext>
{
    protected override AiDbContext CreateContext(DbContextOptions<AiDbContext> options) => new(options);
}
