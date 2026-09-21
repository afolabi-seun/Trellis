using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Middleware;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>
/// BDR-NFR-006 (coding_standards.md's Optimistic Concurrency section) — Property is that
/// section's own worked example (<c>PropertyConfiguration.cs</c>, <c>PropertyResponse</c>'s
/// <c>RowVersion</c> field), proven here against real Postgres the same way
/// <see cref="AppUserConcurrencyTests"/> proved it for Auth.
/// </summary>
[Collection(PostgresIsolationCollection.Name)]
public sealed class PropertyConcurrencyTests(PostgresIsolationFixture fixture) : IAsyncLifetime
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private Guid _ownerId;
    private Guid _propertyId;

    public async Task InitializeAsync()
    {
        _ownerId = Guid.NewGuid();
        _propertyId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var seed = connection.CreateCommand();
        seed.CommandText = """
            insert into trellis_property.owner (owner_id, tenant_id, name, status, created_at, updated_at)
            values (@ownerId, @tenantId, 'Concurrency Owner', 'ACTIVE', now(), now());

            insert into trellis_property.property
                (property_id, tenant_id, owner_id, address, property_type, status, created_at, updated_at)
            values (@propertyId, @tenantId, @ownerId, 'Original Address', 'RESIDENTIAL', 'ACTIVE', now(), now());
            """;
        seed.Parameters.AddWithValue("ownerId", _ownerId);
        seed.Parameters.AddWithValue("propertyId", _propertyId);
        seed.Parameters.AddWithValue("tenantId", Tenant);
        await seed.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        TenantContext.Current = null;

        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var cleanup = connection.CreateCommand();
        cleanup.CommandText = "delete from trellis_property.property where tenant_id = @t; delete from trellis_property.owner where tenant_id = @t;";
        cleanup.Parameters.AddWithValue("t", Tenant);
        await cleanup.ExecuteNonQueryAsync();
    }

    [Fact]
    [Trait("BDR", "BDR-NFR-006")]
    public async Task SaveChanges_WhenRowChangedSinceLoad_ThrowsConcurrencyExceptionNotSilentOverwrite()
    {
        TenantContext.Current = new TenantContext(Tenant);

        await using var firstRequest = fixture.CreateAppPropertyContext();
        var propertyAsSeenByFirstRequest = await firstRequest.Properties.SingleAsync(p => p.PropertyId == _propertyId);

        await using var secondRequest = fixture.CreateAppPropertyContext();
        var propertyAsSeenBySecondRequest = await secondRequest.Properties.SingleAsync(p => p.PropertyId == _propertyId);

        propertyAsSeenByFirstRequest.Update(
            _ownerId, "Address From First Request", propertyAsSeenByFirstRequest.PropertyType, null, null, null, null);
        await firstRequest.SaveChangesAsync();

        propertyAsSeenBySecondRequest.Update(
            _ownerId, "Address From Second Request", propertyAsSeenBySecondRequest.PropertyType, null, null, null, null);
        var act = async () => await secondRequest.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await using var verify = fixture.CreateAppPropertyContext();
        var finalState = await verify.Properties.SingleAsync(p => p.PropertyId == _propertyId);
        finalState.Address.Should().Be("Address From First Request");
    }

    /// <summary>
    /// The mechanism <see cref="RowVersionHelper"/> exists for — a real
    /// client presenting a `RowVersion` string back on Update, as opposed to the previous test's
    /// two-DbContexts-both-freshly-loaded scenario (which relies on EF's own auto-captured
    /// original value, not a round-tripped one). Never exercised before this phase; Tenant/Auth
    /// had no Update endpoint that surfaces RowVersion to a client at all.
    /// </summary>
    [Fact]
    [Trait("BDR", "BDR-NFR-006")]
    public async Task RowVersionHelper_RoundTrip_AcceptsCurrentVersionButRejectsStale()
    {
        TenantContext.Current = new TenantContext(Tenant);

        await using var loadContext = fixture.CreateAppPropertyContext();
        var loaded = await loadContext.Properties.SingleAsync(p => p.PropertyId == _propertyId);
        var staleRowVersion = RowVersionHelper.Get(loadContext, loaded);

        // Someone else updates the row first — the row's real xmin moves on.
        await using var otherRequest = fixture.CreateAppPropertyContext();
        var otherEntity = await otherRequest.Properties.SingleAsync(p => p.PropertyId == _propertyId);
        otherEntity.Update(_ownerId, "Changed By Someone Else", otherEntity.PropertyType, null, null, null, null);
        await otherRequest.SaveChangesAsync();
        var currentRowVersion = RowVersionHelper.Get(otherRequest, otherEntity);

        // Presenting the now-stale RowVersion (captured before the other update) must fail.
        await using var staleAttempt = fixture.CreateAppPropertyContext();
        var staleTarget = await staleAttempt.Properties.SingleAsync(p => p.PropertyId == _propertyId);
        staleTarget.Update(_ownerId, "Attempted With Stale Version", staleTarget.PropertyType, null, null, null, null);
        RowVersionHelper.SetExpected(staleAttempt, staleTarget, staleRowVersion);
        var staleAct = async () => await staleAttempt.SaveChangesAsync();
        await staleAct.Should().ThrowAsync<DbUpdateConcurrencyException>();

        // Presenting the actual current RowVersion must succeed.
        await using var currentAttempt = fixture.CreateAppPropertyContext();
        var currentTarget = await currentAttempt.Properties.SingleAsync(p => p.PropertyId == _propertyId);
        currentTarget.Update(_ownerId, "Attempted With Current Version", currentTarget.PropertyType, null, null, null, null);
        RowVersionHelper.SetExpected(currentAttempt, currentTarget, currentRowVersion);
        await currentAttempt.SaveChangesAsync();

        await using var verify = fixture.CreateAppPropertyContext();
        var finalState = await verify.Properties.SingleAsync(p => p.PropertyId == _propertyId);
        finalState.Address.Should().Be("Attempted With Current Version");
    }
}
