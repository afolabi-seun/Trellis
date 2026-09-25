using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Vendor.Domain.Entities;

namespace Trellis.Api.Modules.Vendor.Infrastructure.Data;

public sealed class VendorDbContext(DbContextOptions<VendorDbContext> options) : DbContext(options)
{
    public DbSet<VendorRecord> Vendors => Set<VendorRecord>();
    public DbSet<VendorAssignment> VendorAssignments => Set<VendorAssignment>();
    public DbSet<TenantVendorRelationship> TenantVendorRelationships => Set<TenantVendorRelationship>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        // Scoped by namespace — see PropertyDbContext's identical comment.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(VendorDbContext).Assembly,
            t => t.Namespace?.StartsWith("Trellis.Api.Modules.Vendor.", StringComparison.Ordinal) == true);
}
