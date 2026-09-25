using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Document.Domain.Entities;

namespace Trellis.Api.Modules.Document.Infrastructure.Data;

public sealed class DocumentDbContext(DbContextOptions<DocumentDbContext> options) : DbContext(options)
{
    public DbSet<DocumentRecord> Documents => Set<DocumentRecord>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        // Scoped by namespace — see PropertyDbContext's identical comment.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(DocumentDbContext).Assembly,
            t => t.Namespace?.StartsWith("Trellis.Api.Modules.Document.", StringComparison.Ordinal) == true);
}
