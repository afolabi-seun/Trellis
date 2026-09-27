using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Ai.Domain.Entities;

namespace Trellis.Api.Modules.Ai.Infrastructure.Data;

public sealed class AiDbContext(DbContextOptions<AiDbContext> options) : DbContext(options)
{
    public DbSet<ExtractionJob> ExtractionJobs => Set<ExtractionJob>();
    public DbSet<RoutingSuggestion> RoutingSuggestions => Set<RoutingSuggestion>();
    public DbSet<OverrideLogEntry> OverrideLogEntries => Set<OverrideLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AiDbContext).Assembly,
            t => t.Namespace?.StartsWith("Trellis.Api.Modules.Ai.", StringComparison.Ordinal) == true);
}
