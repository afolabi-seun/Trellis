using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSubstitute;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Application.Dtos;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Services;
using Trellis.Api.Modules.Tenant.Infrastructure.Repositories;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>
/// Real-Postgres proof of what the unit tests can only substitute: the unique slug index, real slug
/// derivation + collision suffixing through ProvisionTenantAsync's actual transaction, the
/// tenant_domain table's RLS, and its "one tenant per VERIFIED domain" partial unique index.
/// </summary>
[Collection(PostgresIsolationCollection.Name)]
public sealed class TenantSlugAndDomainTests(PostgresIsolationFixture fixture) : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private readonly string _unique = Guid.NewGuid().ToString("N")[..8];
    private readonly List<string> _slugsToClean = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        TenantContext.Current = null;
        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var cleanup = connection.CreateCommand();
        cleanup.CommandText = """
            delete from trellis_tenant.tenant_domain where tenant_id in (@a, @b);
            delete from trellis_tenant.tenant where tenant_id in (@a, @b) or slug = any(@slugs);
            """;
        cleanup.Parameters.AddWithValue("a", TenantA);
        cleanup.Parameters.AddWithValue("b", TenantB);
        cleanup.Parameters.AddWithValue("slugs", _slugsToClean.ToArray());
        await cleanup.ExecuteNonQueryAsync();
    }

    private async Task SeedTenantAsync(Guid tenantId, string slug)
    {
        _slugsToClean.Add(slug);
        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            insert into trellis_tenant.tenant (tenant_id, tenant_name, slug, country_code, status, subscription_tier, created_at, updated_at)
            values (@id, 'Seeded', @slug, 'US', 'ACTIVE', 'ENTERPRISE', now(), now());
            """;
        insert.Parameters.AddWithValue("id", tenantId);
        insert.Parameters.AddWithValue("slug", slug);
        await insert.ExecuteNonQueryAsync();
    }

    private async Task InsertDomainAsync(Guid tenantId, string domain, string status)
    {
        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            insert into trellis_tenant.tenant_domain (tenant_domain_id, tenant_id, domain, status, verification_token, created_at)
            values (gen_random_uuid(), @tenant, @domain, @status, 'tok', now());
            """;
        insert.Parameters.AddWithValue("tenant", tenantId);
        insert.Parameters.AddWithValue("domain", domain);
        insert.Parameters.AddWithValue("status", status);
        await insert.ExecuteNonQueryAsync();
    }

    private static TenantService NewService(Trellis.Api.Modules.Tenant.Infrastructure.Data.TenantDbContext db)
    {
        var auth = Substitute.For<IAuthService>();
        auth.CreateTenantAdminAsync(Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(ServiceResult<AppUserSummary>.Success(new AppUserSummary(Guid.NewGuid(), "admin@example.com", null)));
        return new TenantService(new TenantRepository(db), db, auth, Substitute.For<ISessionService>(), Substitute.For<IAuditService>());
    }

    // ---- Slug provisioning ----

    [Fact]
    public async Task Provision_WithoutASlug_DerivesOneFromTheName_AndSuffixesOnCollision()
    {
        var name = $"Zeta Holdings {_unique}";
        var expected = $"zeta-holdings-{_unique}";
        _slugsToClean.AddRange([expected, expected + "-2", expected + "-3"]);

        await using var db1 = fixture.CreateAppTenantContext();
        var first = await NewService(db1).ProvisionTenantAsync(new TenantProvisionRequest(name, "US", "a@example.com"));
        await using var db2 = fixture.CreateAppTenantContext();
        var second = await NewService(db2).ProvisionTenantAsync(new TenantProvisionRequest(name, "US", "b@example.com"));
        await using var db3 = fixture.CreateAppTenantContext();
        var third = await NewService(db3).ProvisionTenantAsync(new TenantProvisionRequest(name, "US", "c@example.com"));

        first.Data!.Slug.Should().Be(expected);
        second.Data!.Slug.Should().Be(expected + "-2");
        third.Data!.Slug.Should().Be(expected + "-3");
    }

    [Fact]
    public async Task Provision_WithAnExplicitSlugAnotherTenantHolds_IsAConflict_AndCreatesNothing()
    {
        var taken = $"taken-{_unique}";
        await SeedTenantAsync(TenantA, taken);

        await using var db = fixture.CreateAppTenantContext();
        var result = await NewService(db).ProvisionTenantAsync(new TenantProvisionRequest($"Other {_unique}", "US", "x@example.com", taken));

        result.Outcome.Should().Be(ServiceResultOutcome.Conflict);
        result.ErrorCode.Should().Be("TRL-E-TEN-2003");
        (await db.Tenants.CountAsync(t => t.TenantName == $"Other {_unique}")).Should().Be(0);
    }

    [Fact]
    public async Task Provision_WithAnExplicitFreeSlug_UsesItVerbatim()
    {
        var slug = $"chosen-{_unique}";
        _slugsToClean.Add(slug);

        await using var db = fixture.CreateAppTenantContext();
        var result = await NewService(db).ProvisionTenantAsync(new TenantProvisionRequest($"Chosen {_unique}", "US", "x@example.com", slug));

        result.Data!.Slug.Should().Be(slug);
    }

    [Fact]
    public async Task TheDatabase_RefusesADuplicateSlug_EvenIfTheApplicationPreCheckIsBypassed()
    {
        var slug = $"dupe-{_unique}";
        await SeedTenantAsync(TenantA, slug);

        var act = () => SeedTenantAsync(TenantB, slug);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23505");
    }

    [Fact]
    public async Task ResolveSlug_ThroughTheRealRepository_FindsTheTenant_AndIsCaseSensitiveLowercase()
    {
        var slug = $"resolve-{_unique}";
        await SeedTenantAsync(TenantA, slug);

        await using var db = fixture.CreateAppTenantContext();
        var service = NewService(db);

        (await service.ResolveSlugAsync(slug)).Data!.TenantId.Should().Be(TenantA);
        (await service.ResolveSlugAsync(slug.ToUpperInvariant())).Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    // ---- tenant_domain ----

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task TenantDomain_IsIsolatedPerTenant_AndInvisibleWithNoContext()
    {
        await SeedTenantAsync(TenantA, $"da-{_unique}");
        await SeedTenantAsync(TenantB, $"db-{_unique}");
        await InsertDomainAsync(TenantA, $"a-{_unique}.example.com", "PENDING_VERIFICATION");
        await InsertDomainAsync(TenantB, $"b-{_unique}.example.com", "PENDING_VERIFICATION");

        TenantContext.Current = new TenantContext(TenantA);
        await using (var db = fixture.CreateAppTenantContext())
        {
            (await db.TenantDomains.Select(d => d.Domain).ToListAsync()).Should().ContainSingle().Which.Should().Be($"a-{_unique}.example.com");
        }

        TenantContext.Current = null;
        await using (var db = fixture.CreateAppTenantContext())
        {
            (await db.TenantDomains.AnyAsync()).Should().BeFalse();
        }
    }

    [Fact]
    public async Task TenantDomain_TheAnonymousHostResolver_FindsAVerifiedDomain_UnderSystemContextOnly()
    {
        var host = $"resolve-{_unique}.example.com";
        await SeedTenantAsync(TenantA, $"dr-{_unique}");
        await InsertDomainAsync(TenantA, host, "VERIFIED");

        await using var db = fixture.CreateAppTenantContext();
        var repository = new TenantDomainRepository(db);

        (await repository.GetVerifiedByHostAsync(host)).Should().BeNull("with no context and no system scope, RLS hides every row");
        using (SystemContext.Enter())
        {
            (await repository.GetVerifiedByHostAsync(host))!.TenantId.Should().Be(TenantA);
        }
    }

    [Fact]
    public async Task TenantDomain_OnlyOneTenantMayHoldADomainVerified_ButAnyNumberMayRegisterItPending()
    {
        var host = $"contested-{_unique}.example.com";
        await SeedTenantAsync(TenantA, $"dc1-{_unique}");
        await SeedTenantAsync(TenantB, $"dc2-{_unique}");

        await InsertDomainAsync(TenantA, host, "PENDING_VERIFICATION");
        await InsertDomainAsync(TenantB, host, "PENDING_VERIFICATION");
        await InsertDomainAsync(TenantA, host, "VERIFIED");

        var act = () => InsertDomainAsync(TenantB, host, "VERIFIED");

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23505");
    }

    [Fact]
    public async Task TenantDomain_TheAppRoleCanDeleteADomain_BecauseRemovingOneIsAGenuineDelete()
    {
        await SeedTenantAsync(TenantA, $"dd-{_unique}");
        await InsertDomainAsync(TenantA, $"del-{_unique}.example.com", "PENDING_VERIFICATION");

        TenantContext.Current = new TenantContext(TenantA);
        await using var db = fixture.CreateAppTenantContext();
        var domain = await db.TenantDomains.SingleAsync();
        db.TenantDomains.Remove(domain);
        await db.SaveChangesAsync();

        (await db.TenantDomains.AnyAsync()).Should().BeFalse();
    }
}
