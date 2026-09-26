using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Auth.Infrastructure.Data;
using Trellis.Api.Modules.Document.Infrastructure.Data;
using Trellis.Api.Modules.Inspection.Infrastructure.Data;
using Trellis.Api.Modules.Notification.Infrastructure.Data;
using Trellis.Api.Modules.PropertyAsset.Infrastructure.Data;
using Trellis.Api.Modules.Vendor.Infrastructure.Data;
using Trellis.Api.Modules.Workflow.Infrastructure.Data;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>
/// One real, ephemeral Postgres 16 container, migrated the same way local dev is
/// (`src/infrastructure/init-app-role.sql`'s role split, then every module's own EF Core
/// migrations run as the privileged owner role) — testing_strategy.md's explicit rule that RLS
/// tests never run against an in-memory provider, since that wouldn't exercise the policies at
/// all. Shared across the whole isolation test collection: starting Postgres is the expensive
/// part, and the tests otherwise clean up their own rows.
/// </summary>
public sealed class PostgresIsolationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("trellis_platform")
        .WithUsername("trellis")
        .WithPassword("trellis_dev_only")
        .Build();

    /// <summary>The privileged, table-owner connection — RLS is inert here, migrations only.</summary>
    public string MigrationsConnectionString => _container.GetConnectionString();

    /// <summary>
    /// The unprivileged <c>trellis_app</c> connection every isolation test actually queries
    /// through — the same role the running API uses, the only one RLS applies to at all.
    /// </summary>
    public string AppConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using (var connection = new NpgsqlConnection(MigrationsConnectionString))
        {
            await connection.OpenAsync();
            // Mirrors infrastructure/init-app-role.sql exactly — same role name, same dev-only
            // password, same reasoning (that file's own comment): RLS does nothing for a
            // superuser or a table's owner, so the app must connect as this separate role.
            await using var createRole = connection.CreateCommand();
            createRole.CommandText = """
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'trellis_app') THEN
                        CREATE ROLE trellis_app LOGIN PASSWORD 'trellis_app_dev_only' NOSUPERUSER NOCREATEDB NOCREATEROLE;
                    END IF;
                END
                $$;
                GRANT CONNECT ON DATABASE trellis_platform TO trellis_app;
                """;
            await createRole.ExecuteNonQueryAsync();
        }

        AppConnectionString = new NpgsqlConnectionStringBuilder(MigrationsConnectionString)
        {
            Username = "trellis_app",
            Password = "trellis_app_dev_only",
        }.ConnectionString;

        // Tenant → Notification → Auth → PropertyAsset → Vendor → Document → Workflow, same
        // order Program.cs registers modules in (data_model.md's migration dependency order) —
        // Core's audit table migrates first. Vendor must migrate before Document/Workflow: both
        // modules' own follow-up migrations (AddVendorEvidenceToDocument,
        // AddVendorIsolationToWorkflow) reference trellis_vendor.vendor_assignment in a
        // CREATE POLICY expression, which Postgres validates against the real schema at creation
        // time — the table must already exist.
        await MigrateAsync(() => new CoreDbContext(BuildOptions<CoreDbContext>()));
        await MigrateAsync(() => new Trellis.Api.Modules.Tenant.Infrastructure.Data.TenantDbContext(BuildOptions<Trellis.Api.Modules.Tenant.Infrastructure.Data.TenantDbContext>()));
        await MigrateAsync(() => new NotificationDbContext(BuildOptions<NotificationDbContext>()));
        await MigrateAsync(() => new AuthDbContext(BuildOptions<AuthDbContext>()));
        await MigrateAsync(() => new PropertyDbContext(BuildOptions<PropertyDbContext>()));
        await MigrateAsync(() => new VendorDbContext(BuildOptions<VendorDbContext>()));
        await MigrateAsync(() => new DocumentDbContext(BuildOptions<DocumentDbContext>()));
        await MigrateAsync(() => new WorkflowDbContext(BuildOptions<WorkflowDbContext>()));
        await MigrateAsync(() => new InspectionDbContext(BuildOptions<InspectionDbContext>()));
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// An <see cref="AuthDbContext"/> wired with the same <see cref="TenantScopeConnectionInterceptor"/>
    /// the running API uses, connected as the unprivileged <c>trellis_app</c> role — reads
    /// <see cref="Trellis.Api.Core.Middleware.TenantContext.Current"/> /
    /// <see cref="Trellis.Api.Core.Middleware.SystemContext.IsSystem"/> exactly like a real request would.
    /// </summary>
    public AuthDbContext CreateAppAuthContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql(AppConnectionString)
            .AddInterceptors(new TenantScopeConnectionInterceptor())
            .Options;
        return new AuthDbContext(options);
    }

    /// <summary>Same shape as <see cref="CreateAppAuthContext"/>, for Notification's own isolation tests.</summary>
    public NotificationDbContext CreateAppNotificationContext()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(AppConnectionString)
            .AddInterceptors(new TenantScopeConnectionInterceptor())
            .Options;
        return new NotificationDbContext(options);
    }

    /// <summary>Same shape as <see cref="CreateAppAuthContext"/>, for PropertyAsset's own isolation/concurrency tests.</summary>
    public PropertyDbContext CreateAppPropertyContext()
    {
        var options = new DbContextOptionsBuilder<PropertyDbContext>()
            .UseNpgsql(AppConnectionString)
            .AddInterceptors(new TenantScopeConnectionInterceptor())
            .Options;
        return new PropertyDbContext(options);
    }

    /// <summary>Same shape as <see cref="CreateAppAuthContext"/>, for Workflow's own isolation tests.</summary>
    public WorkflowDbContext CreateAppWorkflowContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseNpgsql(AppConnectionString)
            .AddInterceptors(new TenantScopeConnectionInterceptor())
            .Options;
        return new WorkflowDbContext(options);
    }

    /// <summary>Same shape as <see cref="CreateAppAuthContext"/>, for Document's own isolation tests.</summary>
    public DocumentDbContext CreateAppDocumentContext()
    {
        var options = new DbContextOptionsBuilder<DocumentDbContext>()
            .UseNpgsql(AppConnectionString)
            .AddInterceptors(new TenantScopeConnectionInterceptor())
            .Options;
        return new DocumentDbContext(options);
    }

    /// <summary>Same shape as <see cref="CreateAppAuthContext"/>, for Vendor's own isolation tests.</summary>
    public VendorDbContext CreateAppVendorContext()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseNpgsql(AppConnectionString)
            .AddInterceptors(new TenantScopeConnectionInterceptor())
            .Options;
        return new VendorDbContext(options);
    }

    /// <summary>Same shape as <see cref="CreateAppAuthContext"/>, for Inspection's own isolation tests.</summary>
    public InspectionDbContext CreateAppInspectionContext()
    {
        var options = new DbContextOptionsBuilder<InspectionDbContext>()
            .UseNpgsql(AppConnectionString)
            .AddInterceptors(new TenantScopeConnectionInterceptor())
            .Options;
        return new InspectionDbContext(options);
    }

    private DbContextOptions<TContext> BuildOptions<TContext>() where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>().UseNpgsql(MigrationsConnectionString).Options;

    private static async Task MigrateAsync<TContext>(Func<TContext> factory) where TContext : DbContext
    {
        await using var context = factory();
        await context.Database.MigrateAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresIsolationCollection : ICollectionFixture<PostgresIsolationFixture>
{
    public const string Name = "Postgres Isolation";
}
