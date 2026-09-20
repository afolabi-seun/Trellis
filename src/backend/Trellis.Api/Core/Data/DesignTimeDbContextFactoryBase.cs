using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Trellis.Api.Core.Data;

/// <summary>
/// `dotnet ef migrations add`/`database update` build the whole app's host to get a configured
/// <c>DbContext</c> by default — which means every module's Application services (including
/// cross-module dependencies like Tenant's <c>IAuthService</c>) must already be registered
/// before ANY module's first migration can be generated, since ASP.NET Core's
/// <c>ValidateOnBuild</c> eagerly checks the entire DI graph at startup, not just what a given
/// command touches. Each module's <c>{Module}DbContextFactory</c> sidesteps this — a design-time
/// factory builds only the one <c>DbContextOptions</c> the CLI actually needs, independent of
/// whether every other module exists yet. Uses <c>ConnectionStrings:TrellisMigrations</c> (the
/// privileged migration-owner role — data_model.md's Row-Level Security section) if set, falling
/// back to the app's own runtime connection string otherwise.
/// </summary>
public abstract class DesignTimeDbContextFactoryBase<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : DbContext
{
    public TContext CreateDbContext(string[] args)
    {
        if (File.Exists(".env"))
        {
            DotNetEnv.Env.Load();
        }

        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var connectionString = config.GetConnectionString("TrellisMigrations") ?? config.GetConnectionString("Trellis");

        var optionsBuilder = new DbContextOptionsBuilder<TContext>().UseNpgsql(connectionString);
        return CreateContext(optionsBuilder.Options);
    }

    protected abstract TContext CreateContext(DbContextOptions<TContext> options);
}
