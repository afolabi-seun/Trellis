using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using LeasingApplication = Trellis.Api.Modules.Leasing.Domain.Entities.Application;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Data;

public sealed class LeasingDbContext(DbContextOptions<LeasingDbContext> options) : DbContext(options)
{
    public DbSet<VacancyListing> VacancyListings => Set<VacancyListing>();
    public DbSet<VacancyListingPhoto> VacancyListingPhotos => Set<VacancyListingPhoto>();
    public DbSet<LeasingApplication> Applications => Set<LeasingApplication>();
    public DbSet<ScreeningResult> ScreeningResults => Set<ScreeningResult>();
    public DbSet<ApplicationEvent> ApplicationEvents => Set<ApplicationEvent>();
    public DbSet<ApplicationLinkToken> ApplicationLinkTokens => Set<ApplicationLinkToken>();
    public DbSet<LeasingWebhookEvent> LeasingWebhookEvents => Set<LeasingWebhookEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(LeasingDbContext).Assembly,
            t => t.Namespace?.StartsWith("Trellis.Api.Modules.Leasing.", StringComparison.Ordinal) == true);
}
