using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trellis.Api.Core.Middleware;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>
/// The multi-tenant isolation suite testing_strategy.md calls "the highest-value test suite in
/// the whole project" — adapted to Phase 1's real entities (<c>app_user</c>; <c>Property</c>
/// doesn't exist yet). Runs against a real, ephemeral Postgres 16 container with RLS enabled
/// (<see cref="PostgresIsolationFixture"/>), through the unprivileged <c>trellis_app</c> role,
/// exactly as multi_tenant_design.md's 3-layer model requires — not an in-memory provider, which
/// wouldn't enforce RLS at all.
///
/// <c>AppUserRepository</c> applies no tenant filter of its own (`Modules/Auth/Infrastructure/
/// Repositories/AppUserRepository.cs`) — every test here is therefore also, by construction,
/// testing_strategy.md's second required case: RLS holds even when the application layer applies
/// none of its own.
/// </summary>
[Collection(PostgresIsolationCollection.Name)]
public sealed class AppUserIsolationTests(PostgresIsolationFixture fixture) : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private Guid _aliceId;
    private Guid _bobId;
    private Guid _rootId;

    public async Task InitializeAsync()
    {
        _aliceId = Guid.NewGuid();
        _bobId = Guid.NewGuid();
        _rootId = Guid.NewGuid();

        // Seeded directly over the privileged connection (RLS is inert for the table owner) —
        // testing_strategy.md's Integration Test Data row: seed independently of the code path
        // under test, so the test doesn't validate itself.
        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var seed = connection.CreateCommand();
        seed.CommandText = """
            insert into trellis_auth.app_user (user_id, tenant_id, username, password_hash, role, status)
            values
                (@aliceId, @tenantA, @aliceUsername, 'x', 'TENANT_ADMIN', 'ACTIVE'),
                (@bobId, @tenantB, @bobUsername, 'x', 'TENANT_ADMIN', 'ACTIVE'),
                (@rootId, null, @rootUsername, 'x', 'SUPER_ADMIN', 'ACTIVE');
            """;
        seed.Parameters.AddWithValue("aliceId", _aliceId);
        seed.Parameters.AddWithValue("bobId", _bobId);
        seed.Parameters.AddWithValue("rootId", _rootId);
        seed.Parameters.AddWithValue("tenantA", TenantA);
        seed.Parameters.AddWithValue("tenantB", TenantB);
        seed.Parameters.AddWithValue("aliceUsername", $"alice-{_aliceId:N}");
        seed.Parameters.AddWithValue("bobUsername", $"bob-{_bobId:N}");
        seed.Parameters.AddWithValue("rootUsername", $"root-{_rootId:N}");
        await seed.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        TenantContext.Current = null;

        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var cleanup = connection.CreateCommand();
        cleanup.CommandText = "delete from trellis_auth.app_user where user_id in (@a, @b, @r);";
        cleanup.Parameters.AddWithValue("a", _aliceId);
        cleanup.Parameters.AddWithValue("b", _bobId);
        cleanup.Parameters.AddWithValue("r", _rootId);
        await cleanup.ExecuteNonQueryAsync();
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task GetById_WhenTenantContextMismatched_ReturnsNull()
    {
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppAuthContext();
        var result = await db.Users.FirstOrDefaultAsync(u => u.UserId == _aliceId);

        // Not a 403/forbidden shape at this layer, but the same principle one level down:
        // the row simply isn't there — existence isn't confirmed or denied, it's absent.
        result.Should().BeNull();
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task GetById_WhenTenantContextMatches_ReturnsRow()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppAuthContext();
        var result = await db.Users.FirstOrDefaultAsync(u => u.UserId == _aliceId);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(_aliceId);
    }

    [Fact]
    public async Task List_WithNoContextAtAll_ReturnsNoRows()
    {
        // No TenantContext, no SystemContext.Enter() — the "unauthenticated or malformed
        // request" case TenantScopeMiddleware's own doc comment calls out as needing to see
        // nothing, not everything.
        await using var db = fixture.CreateAppAuthContext();
        var results = await db.Users.ToListAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task List_WithTenantAContext_ReturnsOnlyTenantAsRow()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppAuthContext();
        var results = await db.Users.ToListAsync();

        results.Should().ContainSingle(u => u.UserId == _aliceId);
    }

    [Fact]
    public async Task List_UnderSystemContext_ReturnsEveryTenantIncludingPlatformRow()
    {
        // The exact shape LoginAsync/VerifyMfaAsync/RequestPasswordResetAsync rely on
        // (AuthService.cs) to resolve a user before any tenant is known.
        using (SystemContext.Enter())
        {
            await using var db = fixture.CreateAppAuthContext();
            var results = await db.Users.ToListAsync();

            results.Select(u => u.UserId).Should().BeEquivalentTo([_aliceId, _bobId, _rootId]);
        }
    }

    [Fact]
    public async Task GetById_ForPlatformLevelUser_InvisibleUnderAnyTenantContext()
    {
        // root's tenant_id is NULL — SQL's three-valued logic must make it invisible to a
        // tenant filter (not an error, not a match), the same behavior audit_record relies on.
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppAuthContext();
        var result = await db.Users.FirstOrDefaultAsync(u => u.UserId == _rootId);

        result.Should().BeNull();
    }
}
