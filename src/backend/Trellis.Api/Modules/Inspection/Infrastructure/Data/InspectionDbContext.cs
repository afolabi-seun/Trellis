using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Inspection.Domain.Entities;

namespace Trellis.Api.Modules.Inspection.Infrastructure.Data;

public sealed class InspectionDbContext(DbContextOptions<InspectionDbContext> options) : DbContext(options)
{
    public DbSet<InspectionRecord> Inspections => Set<InspectionRecord>();
    public DbSet<InspectionItem> InspectionItems => Set<InspectionItem>();
    public DbSet<InspectionPhoto> InspectionPhotos => Set<InspectionPhoto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(InspectionDbContext).Assembly,
            t => t.Namespace?.StartsWith("Trellis.Api.Modules.Inspection.", StringComparison.Ordinal) == true);
}
