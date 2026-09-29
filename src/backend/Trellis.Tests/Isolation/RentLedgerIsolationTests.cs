using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Modules.RentLedger.Infrastructure.Repositories;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>
/// Phase 10's own proof of RLS across trellis_rentledger — plain tenant_isolation on both tables
/// (no vendor branch: a Vendor has no relationship to rent collection, ever) — plus the
/// Postgres-level guarantees only a real database can prove: the two partial unique indexes,
/// the CHECK constraints, the cross-tenant read the arrears job depends on, and the absence of any
/// DELETE grant on rent_payment_record. Exact LeasingIsolationTests shape.
/// </summary>
[Collection(PostgresIsolationCollection.Name)]
public sealed class RentLedgerIsolationTests(PostgresIsolationFixture fixture) : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private Guid _scheduleAId, _scheduleBId, _paymentAId, _paymentBId;

    public async Task InitializeAsync()
    {
        _scheduleAId = Guid.NewGuid();
        _scheduleBId = Guid.NewGuid();
        _paymentAId = Guid.NewGuid();
        _paymentBId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var seed = connection.CreateCommand();
        seed.CommandText = """
            insert into trellis_rentledger.rent_schedule
                (rent_schedule_id, tenant_id, unit_asset_id, rent_amount, currency, start_date, due_day_of_month, grace_period_days, status, created_at, updated_at)
            values
                (@scheduleA, @tenantA, gen_random_uuid(), 1000, 'USD', '2026-01-01', 5, 5, 'ACTIVE', now(), now()),
                (@scheduleB, @tenantB, gen_random_uuid(), 900, 'USD', '2026-01-01', 5, 5, 'ACTIVE', now(), now());

            insert into trellis_rentledger.rent_payment_record
                (rent_payment_record_id, tenant_id, rent_schedule_id, amount_received, date_received, recorded_by_user_id, recorded_at)
            values
                (@paymentA, @tenantA, @scheduleA, 1000, '2026-01-08', gen_random_uuid(), now()),
                (@paymentB, @tenantB, @scheduleB, 900, '2026-01-08', gen_random_uuid(), now());
            """;
        seed.Parameters.AddWithValue("scheduleA", _scheduleAId);
        seed.Parameters.AddWithValue("scheduleB", _scheduleBId);
        seed.Parameters.AddWithValue("paymentA", _paymentAId);
        seed.Parameters.AddWithValue("paymentB", _paymentBId);
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
        cleanup.CommandText = """
            delete from trellis_rentledger.rent_payment_record where tenant_id in (@tenantA, @tenantB);
            delete from trellis_rentledger.rent_schedule where tenant_id in (@tenantA, @tenantB);
            """;
        cleanup.Parameters.AddWithValue("tenantA", TenantA);
        cleanup.Parameters.AddWithValue("tenantB", TenantB);
        await cleanup.ExecuteNonQueryAsync();
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task RentSchedule_TenantAContext_SeesOnlyItsOwnRow()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppRentLedgerContext();
        var results = await db.RentSchedules.ToListAsync();

        results.Should().ContainSingle().Which.RentScheduleId.Should().Be(_scheduleAId);
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task RentSchedule_TenantBContext_CannotSeeTenantAsRow()
    {
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppRentLedgerContext();
        var results = await db.RentSchedules.ToListAsync();

        results.Should().NotContain(s => s.RentScheduleId == _scheduleAId);
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task RentPaymentRecord_IsolatedPerTenant()
    {
        TenantContext.Current = new TenantContext(TenantA);
        await using var db = fixture.CreateAppRentLedgerContext();

        var results = await db.RentPaymentRecords.ToListAsync();

        results.Should().ContainSingle().Which.RentPaymentRecordId.Should().Be(_paymentAId);
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task BothTables_WithNoTenantContextAtAll_ReturnNothing()
    {
        TenantContext.Current = null;
        await using var db = fixture.CreateAppRentLedgerContext();

        (await db.RentSchedules.AnyAsync()).Should().BeFalse();
        (await db.RentPaymentRecords.AnyAsync()).Should().BeFalse();
    }

    /// <summary>The arrears job runs under SystemContext with no ambient tenant and reads every tenant's ACTIVE schedules through the unscoped repository method — RLS's is_system branch is what permits it.</summary>
    [Fact]
    [Trait("BDR", "BDR-RENT-003")]
    public async Task ArrearsJob_UnderSystemContext_SeesEveryTenantsActiveSchedules_ThroughTheUnscopedRepositoryMethod()
    {
        await using var db = fixture.CreateAppRentLedgerContext();
        var repository = new RentScheduleRepository(db);

        using (SystemContext.Enter())
        {
            var batch = await repository.ListActiveAcrossTenantsAsync(null, 1000);

            batch.Select(s => s.RentScheduleId).Should().Contain([_scheduleAId, _scheduleBId]);
        }
    }

    [Fact]
    [Trait("BDR", "BDR-RENT-002")]
    public async Task ReceivedTotals_SumOnlyNonVoidedPayments_PerSchedule()
    {
        await using (var connection = new NpgsqlConnection(fixture.MigrationsConnectionString))
        {
            await connection.OpenAsync();
            await using var extra = connection.CreateCommand();
            extra.CommandText = """
                insert into trellis_rentledger.rent_payment_record
                    (rent_payment_record_id, tenant_id, rent_schedule_id, amount_received, date_received, recorded_by_user_id, recorded_at, voided_at, voided_by, void_reason)
                values (gen_random_uuid(), @tenantA, @scheduleA, 250, '2026-01-09', gen_random_uuid(), now(), now(), gen_random_uuid(), 'typo');
                """;
            extra.Parameters.AddWithValue("tenantA", TenantA);
            extra.Parameters.AddWithValue("scheduleA", _scheduleAId);
            await extra.ExecuteNonQueryAsync();
        }

        TenantContext.Current = new TenantContext(TenantA);
        await using var db = fixture.CreateAppRentLedgerContext();
        var repository = new RentPaymentRecordRepository(db);

        var totals = await repository.GetReceivedTotalsAsync([_scheduleAId]);
        var inPeriod = await repository.GetReceivedInPeriodByCurrencyAsync(
            new Dictionary<Guid, string> { [_scheduleAId] = "USD" }, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        totals[_scheduleAId].Should().Be(1000m);
        inPeriod["USD"].Should().Be(1000m);
    }

    [Fact]
    [Trait("BDR", "BDR-RENT-001")]
    public async Task RentSchedule_OnlyOneActiveSchedulePerUnit_ButEndedOnesDoNotCount()
    {
        var unitId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();

        async Task InsertAsync(string status, Guid? leaseApplicationId = null)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                insert into trellis_rentledger.rent_schedule
                    (rent_schedule_id, tenant_id, unit_asset_id, lease_application_id, rent_amount, currency, start_date, due_day_of_month, grace_period_days, status, created_at, updated_at)
                values (gen_random_uuid(), @tenant, @unit, @lease, 1000, 'USD', '2026-01-01', 5, 5, @status, now(), now());
                """;
            insert.Parameters.AddWithValue("tenant", TenantA);
            insert.Parameters.AddWithValue("unit", unitId);
            insert.Parameters.AddWithValue("status", status);
            insert.Parameters.AddWithValue("lease", (object?)leaseApplicationId ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync();
        }

        await InsertAsync("ACTIVE");

        var duplicate = () => InsertAsync("ACTIVE");
        (await duplicate.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23505");

        await using (var end = connection.CreateCommand())
        {
            end.CommandText = "update trellis_rentledger.rent_schedule set status = 'ENDED' where unit_asset_id = @unit";
            end.Parameters.AddWithValue("unit", unitId);
            await end.ExecuteNonQueryAsync();
        }

        await InsertAsync("ACTIVE");
        await InsertAsync("ENDED");
    }

    /// <summary>What makes LeasingWebhookService's retry deterministic: a second schedule for the same lease application is refused by the database itself.</summary>
    [Fact]
    [Trait("BDR", "BDR-RENT-001")]
    public async Task RentSchedule_AtMostOneScheduleIsCreatedPerLeaseApplication()
    {
        var leaseApplicationId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();

        async Task InsertAsync(string status)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                insert into trellis_rentledger.rent_schedule
                    (rent_schedule_id, tenant_id, unit_asset_id, lease_application_id, rent_amount, currency, start_date, due_day_of_month, grace_period_days, status, created_at, updated_at)
                values (gen_random_uuid(), @tenant, gen_random_uuid(), @lease, 1000, 'USD', '2026-01-01', 5, 5, @status, now(), now());
                """;
            insert.Parameters.AddWithValue("tenant", TenantA);
            insert.Parameters.AddWithValue("lease", leaseApplicationId);
            insert.Parameters.AddWithValue("status", status);
            await insert.ExecuteNonQueryAsync();
        }

        await InsertAsync("ACTIVE");

        var duplicate = () => InsertAsync("ENDED");
        (await duplicate.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23505");
    }

    [Theory]
    [InlineData(0, 5, 1000)]
    [InlineData(32, 5, 1000)]
    [InlineData(5, 61, 1000)]
    [InlineData(5, 5, 0)]
    public async Task RentSchedule_TheDatabaseRejectsOutOfRangeTerms(int dueDay, int grace, decimal amount)
    {
        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            insert into trellis_rentledger.rent_schedule
                (rent_schedule_id, tenant_id, unit_asset_id, rent_amount, currency, start_date, due_day_of_month, grace_period_days, status, created_at, updated_at)
            values (gen_random_uuid(), @tenant, gen_random_uuid(), @amount, 'USD', '2026-01-01', @dueDay, @grace, 'ACTIVE', now(), now());
            """;
        insert.Parameters.AddWithValue("tenant", TenantA);
        insert.Parameters.AddWithValue("dueDay", dueDay);
        insert.Parameters.AddWithValue("grace", grace);
        insert.Parameters.AddWithValue("amount", amount);

        var act = async () => await insert.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23514");
    }

    /// <summary>The app role can insert and void (UPDATE) a payment record, but has no DELETE grant at all — a mistaken entry is voided, never removed.</summary>
    [Fact]
    [Trait("BDR", "BDR-RENT-002")]
    public async Task RentPaymentRecord_TheAppRole_CannotDelete()
    {
        await using var connection = new NpgsqlConnection(fixture.AppConnectionString);
        await connection.OpenAsync();
        await using var delete = connection.CreateCommand();
        delete.CommandText = "delete from trellis_rentledger.rent_payment_record";

        var act = async () => await delete.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
    }
}
