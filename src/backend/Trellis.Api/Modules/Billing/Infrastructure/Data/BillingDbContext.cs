using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Infrastructure.Data;

public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<UsageSnapshot> UsageSnapshots => Set<UsageSnapshot>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<ModuleEntitlement> ModuleEntitlements => Set<ModuleEntitlement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(BillingDbContext).Assembly,
            t => t.Namespace?.StartsWith("Trellis.Api.Modules.Billing.", StringComparison.Ordinal) == true);
}
