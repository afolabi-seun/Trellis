using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trellis.Api.Core.Middleware;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>
/// The one RLS-bearing module in Phases 1-4 without its own dedicated isolation test until now —
/// every other module (Auth, PropertyAsset, Document, Workflow, Vendor) already has one of these.
/// Notification's own <c>tenant_isolation</c> policy is the same plain shape as <c>app_user</c>'s
/// (a nullable <c>tenant_id</c>, OR'd with <c>is_system</c>), and <see
/// cref="NotificationDeliveryRepository.GetQueuedAsync"/> applies no tenant filter of its own —
/// deliberately, since NotificationDispatchBackgroundService reads every tenant's queued rows in
/// one batch under SystemContext, the same pattern DocumentExpiryAlertBackgroundService uses. That
/// makes this suite, like AppUserIsolationTests, also proof that RLS alone holds the line even
/// where the application layer applies none of its own.
/// </summary>
[Collection(PostgresIsolationCollection.Name)]
public sealed class NotificationIsolationTests(PostgresIsolationFixture fixture) : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private Guid _tenantADeliveryId;
    private Guid _tenantBDeliveryId;
    private Guid _platformDeliveryId;

    public async Task InitializeAsync()
    {
        _tenantADeliveryId = Guid.NewGuid();
        _tenantBDeliveryId = Guid.NewGuid();
        _platformDeliveryId = Guid.NewGuid();

        // Seeded directly over the privileged connection (RLS is inert for the table owner) —
        // testing_strategy.md's Integration Test Data row: seed independently of the code path
        // under test, so the test doesn't validate itself.
        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var seed = connection.CreateCommand();
        seed.CommandText = """
            insert into trellis_notification.notification_delivery
                (delivery_id, tenant_id, recipient_type, recipient_address, template_key, channel, status, created_at)
            values
                (@a, @tenantA, 'APP_USER', 'alice@tenant-a.test', 'DOCUMENT_EXPIRING_SOON', 'EMAIL', 'QUEUED', now()),
                (@b, @tenantB, 'APP_USER', 'bob@tenant-b.test', 'DOCUMENT_EXPIRING_SOON', 'EMAIL', 'QUEUED', now()),
                (@p, null, 'APP_USER', 'root@trellis-platform.test', 'PASSWORD_RESET', 'EMAIL', 'QUEUED', now());
            """;
        seed.Parameters.AddWithValue("a", _tenantADeliveryId);
        seed.Parameters.AddWithValue("b", _tenantBDeliveryId);
        seed.Parameters.AddWithValue("p", _platformDeliveryId);
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
        cleanup.CommandText = "delete from trellis_notification.notification_delivery where delivery_id in (@a, @b, @p);";
        cleanup.Parameters.AddWithValue("a", _tenantADeliveryId);
        cleanup.Parameters.AddWithValue("b", _tenantBDeliveryId);
        cleanup.Parameters.AddWithValue("p", _platformDeliveryId);
        await cleanup.ExecuteNonQueryAsync();
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task GetById_WhenTenantContextMismatched_ReturnsNull()
    {
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppNotificationContext();
        var result = await db.Deliveries.FirstOrDefaultAsync(d => d.DeliveryId == _tenantADeliveryId);

        result.Should().BeNull();
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task GetById_WhenTenantContextMatches_ReturnsRow()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppNotificationContext();
        var result = await db.Deliveries.FirstOrDefaultAsync(d => d.DeliveryId == _tenantADeliveryId);

        result.Should().NotBeNull();
        result!.DeliveryId.Should().Be(_tenantADeliveryId);
    }

    [Fact]
    public async Task List_WithNoContextAtAll_ReturnsNoRows()
    {
        await using var db = fixture.CreateAppNotificationContext();
        var results = await db.Deliveries.ToListAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task List_WithTenantAContext_ReturnsOnlyTenantAsRow()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppNotificationContext();
        var results = await db.Deliveries.ToListAsync();

        results.Should().ContainSingle(d => d.DeliveryId == _tenantADeliveryId);
    }

    [Fact]
    public async Task List_UnderSystemContext_ReturnsEveryTenantIncludingPlatformRow()
    {
        // The exact shape NotificationDispatchBackgroundService relies on to dispatch every
        // tenant's queued deliveries in one batch.
        using (SystemContext.Enter())
        {
            await using var db = fixture.CreateAppNotificationContext();
            var results = await db.Deliveries.ToListAsync();

            results.Select(d => d.DeliveryId).Should()
                .BeEquivalentTo([_tenantADeliveryId, _tenantBDeliveryId, _platformDeliveryId]);
        }
    }

    [Fact]
    public async Task GetById_ForPlatformLevelDelivery_InvisibleUnderAnyTenantContext()
    {
        // A platform-level delivery's tenant_id is NULL — SQL's three-valued logic must make it
        // invisible to a tenant filter, the same behavior app_user's own root row relies on.
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppNotificationContext();
        var result = await db.Deliveries.FirstOrDefaultAsync(d => d.DeliveryId == _platformDeliveryId);

        result.Should().BeNull();
    }
}
