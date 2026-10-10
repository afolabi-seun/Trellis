using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Ai.Infrastructure.Data;
using Trellis.Api.Modules.Auth.Infrastructure.Data;
using Trellis.Api.Modules.Billing.Infrastructure.Data;
using Trellis.Api.Modules.Document.Infrastructure.Data;
using Trellis.Api.Modules.Inspection.Infrastructure.Data;
using Trellis.Api.Modules.Leasing.Infrastructure.Data;
using Trellis.Api.Modules.Notification.Infrastructure.Data;
using Trellis.Api.Modules.PropertyAsset.Infrastructure.Data;
using Trellis.Api.Modules.RentLedger.Infrastructure.Data;
using Trellis.Api.Modules.Reporting.Infrastructure.Data;
using Trellis.Api.Modules.Tenant.Infrastructure.Data;
using Trellis.Api.Modules.Vendor.Infrastructure.Data;
using Trellis.Api.Modules.Workflow.Infrastructure.Data;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>
/// Phase 11's data migration, proven against real pre-Phase-11 data rather than an empty database:
/// a fresh database in the shared container is migrated to just before lease records existed,
/// seeded with every shape of tenancy the old schema could hold, then migrated forward. The three
/// migrations that matter (PropertyAsset's AddLeaseRecords, Leasing's
/// AddLeaseTermAndMoveLeaseRecords, RentLedger's LinkRentSchedulesToLeases) run in the real
/// dependency order. Its own database, so nothing here touches the shared one's rows.
/// </summary>
[Collection(PostgresIsolationCollection.Name)]
public sealed class LeaseBackfillMigrationTests(PostgresIsolationFixture fixture) : IAsyncLifetime
{
    private readonly string _database = $"trellis_backfill_{Guid.NewGuid():N}";
    private string _connectionString = string.Empty;

    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid(), Property = Guid.NewGuid();
    private static readonly Guid LeasedUnit = Guid.NewGuid(), EndedLeaseUnit = Guid.NewGuid(), ManualUnit = Guid.NewGuid(),
        EndedManualUnit = Guid.NewGuid(), NeverLeasedUnit = Guid.NewGuid();
    private static readonly Guid LeasedListing = Guid.NewGuid(), EndedListing = Guid.NewGuid();
    private static readonly Guid LeasedApplication = Guid.NewGuid(), EndedApplication = Guid.NewGuid();
    private static readonly Guid LeasedSchedule = Guid.NewGuid(), EndedLeaseSchedule = Guid.NewGuid(), ManualSchedule = Guid.NewGuid(),
        EndedManualSchedule = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await using (var admin = new NpgsqlConnection(fixture.MigrationsConnectionString))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            create.CommandText = $"CREATE DATABASE {_database}";
            await create.ExecuteNonQueryAsync();
        }

        _connectionString = new NpgsqlConnectionStringBuilder(fixture.MigrationsConnectionString) { Database = _database }.ConnectionString;

        // Everything up to date except the three modules Phase 11 changes, which stop just before it.
        await MigrateAsync<CoreDbContext>(o => new CoreDbContext(o));
        await MigrateAsync<TenantDbContext>(o => new TenantDbContext(o));
        await MigrateAsync<NotificationDbContext>(o => new NotificationDbContext(o));
        await MigrateAsync<AuthDbContext>(o => new AuthDbContext(o));
        await MigrateAsync<PropertyDbContext>(o => new PropertyDbContext(o), "20260928105609_AddUnitAssetOccupancyStatus");
        await MigrateAsync<VendorDbContext>(o => new VendorDbContext(o));
        await MigrateAsync<DocumentDbContext>(o => new DocumentDbContext(o));
        await MigrateAsync<WorkflowDbContext>(o => new WorkflowDbContext(o));
        await MigrateAsync<InspectionDbContext>(o => new InspectionDbContext(o));
        await MigrateAsync<AiDbContext>(o => new AiDbContext(o));
        await MigrateAsync<BillingDbContext>(o => new BillingDbContext(o));
        await MigrateAsync<ReportingDbContext>(o => new ReportingDbContext(o));
        await MigrateAsync<LeasingDbContext>(o => new LeasingDbContext(o), "20260929064634_GrantScreeningCiphertextErase");
        await MigrateAsync<RentLedgerDbContext>(o => new RentLedgerDbContext(o), "20260928125717_InitialRentLedger");

        await SeedPrePhase11DataAsync();

        await MigrateAsync<PropertyDbContext>(o => new PropertyDbContext(o));
        await MigrateAsync<LeasingDbContext>(o => new LeasingDbContext(o));
        await MigrateAsync<RentLedgerDbContext>(o => new RentLedgerDbContext(o));
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await admin.OpenAsync();
        await using var drop = admin.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS {_database} WITH (FORCE)";
        await drop.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task EveryLeasedOrEndedApplication_BecomesALeasingLease_WithNothingGuessed()
    {
        var active = await LeaseByApplicationAsync(LeasedApplication);
        active["source"].Should().Be("LEASING");
        active["status"].Should().Be("ACTIVE");
        active["term_type"].Should().Be("NOT_RECORDED");
        active["end_date"].Should().BeNull();
        active["start_date"].Should().Be(new DateOnly(2026, 3, 1));

        var ended = await LeaseByApplicationAsync(EndedApplication);
        ended["status"].Should().Be("ENDED");
        ended["ended_on"].Should().Be(new DateOnly(2026, 6, 30));
        ended["end_reason"].Should().Be("NOT_RECORDED");
        ended["ended_by"].Should().BeNull();
        // The old free-text reason survives as the note before its column is dropped.
        ended["end_note"].Should().Be("Tenant relocated");
    }

    [Fact]
    public async Task ManualSchedules_GetManualLeases_AndEverySchedulePointsAtItsLease()
    {
        var manual = await LeaseByIdAsync(ManualSchedule);
        manual["source"].Should().Be("MANUAL");
        manual["status"].Should().Be("ACTIVE");
        manual["start_date"].Should().Be(new DateOnly(2026, 2, 1));

        var endedManual = await LeaseByIdAsync(EndedManualSchedule);
        endedManual["status"].Should().Be("ENDED");
        endedManual["ended_on"].Should().Be(new DateOnly(2026, 5, 15));

        (await ScalarAsync<Guid>($"select lease_id from trellis_rentledger.rent_schedule where rent_schedule_id = '{LeasedSchedule}'"))
            .Should().Be((Guid)(await LeaseByApplicationAsync(LeasedApplication))["lease_id"]!);
        (await ScalarAsync<Guid>($"select lease_id from trellis_rentledger.rent_schedule where rent_schedule_id = '{EndedLeaseSchedule}'"))
            .Should().Be((Guid)(await LeaseByApplicationAsync(EndedApplication))["lease_id"]!);
        (await ScalarAsync<long>("select count(*) from trellis_rentledger.rent_schedule where lease_id = '00000000-0000-0000-0000-000000000000'"))
            .Should().Be(0);
    }

    [Fact]
    public async Task UnitOccupancy_FollowsLeasesExactly_AndVacantSinceComesOnlyFromAnEndedLease()
    {
        (await UnitAsync(LeasedUnit)).Should().Be(("OCCUPIED", null));
        (await UnitAsync(ManualUnit)).Should().Be(("OCCUPIED", null));
        (await UnitAsync(EndedLeaseUnit)).Should().Be(("VACANT", new DateOnly(2026, 6, 30)));
        // Ending a manual schedule never used to vacate its unit — reconciled here.
        (await UnitAsync(EndedManualUnit)).Should().Be(("VACANT", new DateOnly(2026, 5, 15)));
        // Unknown, so null — never guessed from created_at.
        (await UnitAsync(NeverLeasedUnit)).Should().Be(("VACANT", null));
    }

    [Fact]
    public async Task TheApplicationsFreeTextReasonColumn_IsGone()
    {
        (await ScalarAsync<long>(
            "select count(*) from information_schema.columns where table_schema = 'trellis_leasing' and table_name = 'application' and column_name = 'lease_ended_reason'"))
            .Should().Be(0);
    }

    private async Task MigrateAsync<TContext>(Func<DbContextOptions<TContext>, TContext> factory, string? target = null) where TContext : DbContext
    {
        await using var context = factory(new DbContextOptionsBuilder<TContext>().UseNpgsql(_connectionString).Options);
        await context.GetService<IMigrator>().MigrateAsync(target);
    }

    private async Task SeedPrePhase11DataAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var seed = connection.CreateCommand();
        seed.CommandText = $"""
            insert into trellis_property.owner (owner_id, tenant_id, name, status, created_at, updated_at)
            values ('{Owner}', '{Tenant}', 'Owner', 'ACTIVE', now(), now());
            insert into trellis_property.property (property_id, tenant_id, owner_id, address, property_type, status, created_at, updated_at)
            values ('{Property}', '{Tenant}', '{Owner}', '1 Main St', 'RESIDENTIAL', 'ACTIVE', now(), now());
            insert into trellis_property.unit_asset (unit_asset_id, tenant_id, property_id, label, status, occupancy_status, created_at, updated_at)
            values ('{LeasedUnit}', '{Tenant}', '{Property}', 'Leased', 'ACTIVE', 'OCCUPIED', now(), now()),
                   ('{EndedLeaseUnit}', '{Tenant}', '{Property}', 'Ended lease', 'ACTIVE', 'VACANT', now(), now()),
                   ('{ManualUnit}', '{Tenant}', '{Property}', 'Manual', 'ACTIVE', 'OCCUPIED', now(), now()),
                   ('{EndedManualUnit}', '{Tenant}', '{Property}', 'Ended manual', 'ACTIVE', 'OCCUPIED', now(), now()),
                   ('{NeverLeasedUnit}', '{Tenant}', '{Property}', 'Never leased', 'ACTIVE', 'VACANT', now(), now());

            insert into trellis_leasing.vacancy_listing
                (vacancy_listing_id, tenant_id, unit_asset_id, rent_amount, currency, available_from, status, created_by, created_at, updated_at)
            values ('{LeasedListing}', '{Tenant}', '{LeasedUnit}', 1000, 'ZAR', '2026-03-01', 'LEASED', gen_random_uuid(), now(), now()),
                   ('{EndedListing}', '{Tenant}', '{EndedLeaseUnit}', 1000, 'ZAR', '2026-01-01', 'LEASED', gen_random_uuid(), now(), now());
            insert into trellis_leasing.application
                (application_id, tenant_id, vacancy_listing_id, applicant_email, applicant_full_name, applicant_phone,
                 applicant_date_of_birth, applicant_current_address, consent_to_screening, status, submitted_at,
                 sent_for_signature_by, lease_ended_date, lease_ended_reason)
            values ('{LeasedApplication}', '{Tenant}', '{LeasedListing}', 'a@example.com', 'A', '1', '1990-01-01', 'addr', true, 'LEASED',
                    '2026-02-01', gen_random_uuid(), null, null),
                   ('{EndedApplication}', '{Tenant}', '{EndedListing}', 'b@example.com', 'B', '1', '1990-01-01', 'addr', true, 'LEASE_ENDED',
                    '2025-12-01', gen_random_uuid(), '2026-06-30', 'Tenant relocated');

            insert into trellis_rentledger.rent_schedule
                (rent_schedule_id, tenant_id, unit_asset_id, lease_application_id, rent_amount, currency, start_date,
                 due_day_of_month, grace_period_days, status, ended_at, created_at, updated_at)
            values ('{LeasedSchedule}', '{Tenant}', '{LeasedUnit}', '{LeasedApplication}', 1000, 'ZAR', '2026-03-01', 1, 5, 'ACTIVE', null, now(), now()),
                   ('{EndedLeaseSchedule}', '{Tenant}', '{EndedLeaseUnit}', '{EndedApplication}', 1000, 'ZAR', '2026-01-01', 1, 5, 'ENDED', '2026-06-30', now(), now()),
                   ('{ManualSchedule}', '{Tenant}', '{ManualUnit}', null, 1000, 'ZAR', '2026-02-01', 1, 5, 'ACTIVE', null, now(), now()),
                   ('{EndedManualSchedule}', '{Tenant}', '{EndedManualUnit}', null, 1000, 'ZAR', '2026-01-01', 1, 5, 'ENDED', '2026-05-15 10:00+00', now(), now());
            """;
        await seed.ExecuteNonQueryAsync();
    }

    private Task<Dictionary<string, object?>> LeaseByApplicationAsync(Guid applicationId) =>
        RowAsync($"select * from trellis_property.lease where lease_application_id = '{applicationId}'");

    private Task<Dictionary<string, object?>> LeaseByIdAsync(Guid leaseId) =>
        RowAsync($"select * from trellis_property.lease where lease_id = '{leaseId}'");

    private async Task<(string Occupancy, DateOnly? VacantSince)> UnitAsync(Guid unitId)
    {
        var row = await RowAsync($"select occupancy_status, vacant_since from trellis_property.unit_asset where unit_asset_id = '{unitId}'");
        return ((string)row["occupancy_status"]!, (DateOnly?)row["vacant_since"]);
    }

    private async Task<Dictionary<string, object?>> RowAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue($"expected a row for: {sql}");

        var row = new Dictionary<string, object?>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = reader.IsDBNull(i)
                ? null
                : reader.GetFieldType(i) == typeof(DateTime) && reader.GetDataTypeName(i) == "date"
                    ? reader.GetFieldValue<DateOnly>(i)
                    : reader.GetValue(i);
        }

        return row;
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }
}
