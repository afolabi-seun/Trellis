using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Document.Infrastructure.Data;

public sealed class DocumentDbContextFactory : DesignTimeDbContextFactoryBase<DocumentDbContext>
{
    protected override DocumentDbContext CreateContext(DbContextOptions<DocumentDbContext> options) => new(options);
}
