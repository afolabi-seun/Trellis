using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trellis.Api.Core.Middleware;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>
/// BDR-NFR-006 (coding_standards.md's Optimistic Concurrency section): "the system shall detect
/// a concurrent update to a stale version of a record and reject it, rather than silently
/// overwriting a change made by another user." <c>app_user</c> is where Phase 1 actually
/// exercises this (`TenantDtos.cs`'s own comment on why Tenant doesn't yet) — proven here against
/// real Postgres, since the mechanism is Npgsql mapping the `xmin` system column as a shadow
/// row-version property (`AppUserConfiguration.cs`), not application code.
/// </summary>
[Collection(PostgresIsolationCollection.Name)]
public sealed class AppUserConcurrencyTests(PostgresIsolationFixture fixture) : IAsyncLifetime
{
    private Guid _userId;

    public async Task InitializeAsync()
    {
        _userId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var seed = connection.CreateCommand();
        seed.CommandText = """
            insert into trellis_auth.app_user (user_id, tenant_id, username, password_hash, role, status)
            values (@userId, null, @username, 'original-hash', 'SUPER_ADMIN', 'ACTIVE');
            """;
        seed.Parameters.AddWithValue("userId", _userId);
        seed.Parameters.AddWithValue("username", $"concurrency-{_userId:N}");
        await seed.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var cleanup = connection.CreateCommand();
        cleanup.CommandText = "delete from trellis_auth.app_user where user_id = @userId;";
        cleanup.Parameters.AddWithValue("userId", _userId);
        await cleanup.ExecuteNonQueryAsync();
    }

    [Fact]
    [Trait("BDR", "BDR-NFR-006")]
    public async Task SaveChanges_WhenRowChangedSinceLoad_ThrowsConcurrencyExceptionNotSilentOverwrite()
    {
        // Two independent contexts loading the same row, standing in for two concurrent
        // requests — SystemContext.Enter() just to bypass tenant filtering, which
        // AppUserIsolationTests already covers on its own; this test is purely about xmin.
        using var scope = SystemContext.Enter();

        await using var firstRequest = fixture.CreateAppAuthContext();
        var userAsSeenByFirstRequest = await firstRequest.Users.SingleAsync(u => u.UserId == _userId);

        await using var secondRequest = fixture.CreateAppAuthContext();
        var userAsSeenBySecondRequest = await secondRequest.Users.SingleAsync(u => u.UserId == _userId);

        userAsSeenByFirstRequest.SetPasswordHash("hash-from-first-request");
        await firstRequest.SaveChangesAsync();

        userAsSeenBySecondRequest.SetPasswordHash("hash-from-second-request");
        var act = async () => await secondRequest.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();

        // And the first request's write actually stuck — never silently overwritten.
        await using var verify = fixture.CreateAppAuthContext();
        var finalState = await verify.Users.SingleAsync(u => u.UserId == _userId);
        finalState.PasswordHash.Should().Be("hash-from-first-request");
    }
}
