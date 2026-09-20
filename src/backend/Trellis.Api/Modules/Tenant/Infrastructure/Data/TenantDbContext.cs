using Microsoft.EntityFrameworkCore;
using TenantEntity = Trellis.Api.Modules.Tenant.Domain.Entities.Tenant;

namespace Trellis.Api.Modules.Tenant.Infrastructure.Data;

public sealed class TenantDbContext(DbContextOptions<TenantDbContext> options) : DbContext(options)
{
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        // Scoped by namespace — data_model.md's EF Core Conventions section (Phase 1 correction):
        // an unscoped ApplyConfigurationsFromAssembly(assembly) call would pick up every other
        // module's IEntityTypeConfiguration<T> too, in this single-project setup.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(TenantDbContext).Assembly,
            t => t.Namespace?.StartsWith("Trellis.Api.Modules.Tenant.", StringComparison.Ordinal) == true);
}
