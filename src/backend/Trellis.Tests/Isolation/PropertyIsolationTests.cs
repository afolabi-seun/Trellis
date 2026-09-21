using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trellis.Api.Core.Middleware;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>
/// testing_strategy.md's own flagship example, made real: <c>Property</c> is the Handbook's
/// canonical multi-tenant isolation test subject (the doc literally names
/// <c>GetProperty_WhenTenantContextMismatched_Returns404NotLeak</c> before this module existed).
/// Same structure as <see cref="AppUserIsolationTests"/> — real Postgres, RLS enabled, no
/// in-memory provider. <c>PropertyRepository</c> applies its own app-layer
/// <c>TenantAwareRepository&lt;T&gt;.Scoped()</c> filter too, so these tests are also, by
/// construction, proving RLS holds even when queried directly against
/// <see cref="Trellis.Api.Modules.PropertyAsset.Infrastructure.Data.PropertyDbContext"/> with
/// that app-layer filter bypassed entirely.
/// </summary>
[Collection(PostgresIsolationCollection.Name)]
public sealed class PropertyIsolationTests(PostgresIsolationFixture fixture) : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private Guid _ownerAId;
    private Guid _propertyAId;
    private Guid _propertyBId;

    public async Task InitializeAsync()
    {
        _ownerAId = Guid.NewGuid();
        _propertyAId = Guid.NewGuid();
        _propertyBId = Guid.NewGuid();
        var ownerBId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var seed = connection.CreateCommand();
        seed.CommandText = """
            insert into trellis_property.owner (owner_id, tenant_id, name, status, created_at, updated_at)
            values
                (@ownerA, @tenantA, 'Owner A', 'ACTIVE', now(), now()),
                (@ownerB, @tenantB, 'Owner B', 'ACTIVE', now(), now());

            insert into trellis_property.property
                (property_id, tenant_id, owner_id, address, property_type, status, created_at, updated_at)
            values
                (@propertyA, @tenantA, @ownerA, '1 Tenant A St', 'RESIDENTIAL', 'ACTIVE', now(), now()),
                (@propertyB, @tenantB, @ownerB, '1 Tenant B St', 'COMMERCIAL', 'ACTIVE', now(), now());
            """;
        seed.Parameters.AddWithValue("ownerA", _ownerAId);
        seed.Parameters.AddWithValue("ownerB", ownerBId);
        seed.Parameters.AddWithValue("propertyA", _propertyAId);
        seed.Parameters.AddWithValue("propertyB", _propertyBId);
        seed.Parameters.AddWithValue("tenantA", TenantA);
        seed.Parameters.AddWithValue("tenantB", TenantB);
        await seed.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        TenantContext.Current = null;

        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var cleanup = connection.CreateCommand();
        cleanup.CommandText = "delete from trellis_property.property where tenant_id in (@a, @b); delete from trellis_property.owner where tenant_id in (@a, @b);";
        cleanup.Parameters.AddWithValue("a", TenantA);
        cleanup.Parameters.AddWithValue("b", TenantB);
        await cleanup.ExecuteNonQueryAsync();
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task GetById_WhenTenantContextMismatched_ReturnsNull()
    {
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppPropertyContext();
        var result = await db.Properties.FirstOrDefaultAsync(p => p.PropertyId == _propertyAId);

        // Not a 403/forbidden shape at this layer, but the same principle testing_strategy.md's
        // GetProperty_WhenTenantContextMismatched_Returns404NotLeak names: the row simply isn't
        // there — existence isn't confirmed or denied, it's absent.
        result.Should().BeNull();
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task GetById_WhenTenantContextMatches_ReturnsRow()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppPropertyContext();
        var result = await db.Properties.FirstOrDefaultAsync(p => p.PropertyId == _propertyAId);

        result.Should().NotBeNull();
        result!.Address.Should().Be("1 Tenant A St");
    }

    [Fact]
    public async Task List_WithNoContextAtAll_ReturnsNoRows()
    {
        await using var db = fixture.CreateAppPropertyContext();
        var results = await db.Properties.ToListAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task List_WithTenantAContext_ReturnsOnlyTenantAsRow()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppPropertyContext();
        var results = await db.Properties.ToListAsync();

        results.Should().ContainSingle(p => p.PropertyId == _propertyAId);
    }

    [Fact]
    public async Task List_UnderSystemContext_ReturnsEveryTenant()
    {
        using (SystemContext.Enter())
        {
            await using var db = fixture.CreateAppPropertyContext();
            var results = await db.Properties.ToListAsync();

            results.Select(p => p.PropertyId).Should().BeEquivalentTo([_propertyAId, _propertyBId]);
        }
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task Owner_WhenTenantContextMismatched_ReturnsNull()
    {
        // Owner gets the exact same policy as property — one direct check, since the app_user
        // suite already covers the general nullable-tenant-id/is_system cases exhaustively.
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppPropertyContext();
        var result = await db.Owners.FirstOrDefaultAsync(o => o.OwnerId == _ownerAId);

        result.Should().BeNull();
    }
}
