using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.PropertyAsset.Domain.Entities;

namespace Trellis.Api.Modules.PropertyAsset.Infrastructure.Data;

public sealed class PropertyDbContext(DbContextOptions<PropertyDbContext> options) : DbContext(options)
{
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<UnitAsset> UnitAssets => Set<UnitAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        // Scoped by namespace — an unscoped ApplyConfigurationsFromAssembly(assembly) call would
        // pick up every other module's IEntityTypeConfiguration<T> too, in this single-project
        // setup (data_model.md's EF Core Conventions section, corrected during Phase 1).
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PropertyDbContext).Assembly,
            t => t.Namespace?.StartsWith("Trellis.Api.Modules.PropertyAsset.", StringComparison.Ordinal) == true);
}
