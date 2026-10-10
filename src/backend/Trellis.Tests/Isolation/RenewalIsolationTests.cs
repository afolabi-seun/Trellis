using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Modules.RentLedger.Infrastructure.Repositories;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>
/// BDR-LSE-010's two new tables against real Postgres: tenant isolation on renewal_offer and
/// rent_change, one open offer per lease, the offer's shape CHECKs, and — only provable here — that
/// a schedule's rent changes load with it and a new change persists through the repository.
/// </summary>
[Collection(PostgresIsolationCollection.Name)]
public sealed class RenewalIsolationTests(PostgresIsolationFixture fixture) : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private readonly Guid _scheduleA = Guid.NewGuid();
    private readonly Guid _leaseA = Guid.NewGuid();
    private readonly Guid _offerA = Guid.NewGuid();
    private readonly Guid _offerB = Guid.NewGuid();

    public Task InitializeAsync() => ExecuteAsOwnerAsync($"""
        insert into trellis_rentledger.rent_schedule
            (rent_schedule_id, tenant_id, unit_asset_id, lease_id, rent_amount, currency, start_date, due_day_of_month, grace_period_days, status, created_at, updated_at)
        values ('{_scheduleA}', '{TenantA}', gen_random_uuid(), '{_leaseA}', 1000, 'USD', '2026-01-01', 5, 5, 'ACTIVE', now(), now());
        insert into trellis_rentledger.rent_change (rent_change_id, tenant_id, rent_schedule_id, effective_from, rent_amount, created_by, created_at)
        values (gen_random_uuid(), '{TenantA}', '{_scheduleA}', '2026-04-01', 1200, gen_random_uuid(), now());
        insert into trellis_leasing.renewal_offer
            (renewal_offer_id, tenant_id, lease_id, unit_asset_id, proposed_term, proposed_end_date, respond_by, status, offered_by, created_at, updated_at)
        values ('{_offerA}', '{TenantA}', '{_leaseA}', gen_random_uuid(), 'FIXED', '2027-12-31', '2026-11-01', 'OFFERED', gen_random_uuid(), now(), now()),
               ('{_offerB}', '{TenantB}', gen_random_uuid(), gen_random_uuid(), 'MONTH_TO_MONTH', null, '2026-11-01', 'OFFERED', gen_random_uuid(), now(), now());
        """);

    public async Task DisposeAsync()
    {
        TenantContext.Current = null;
        await ExecuteAsOwnerAsync($"""
            delete from trellis_leasing.renewal_offer where tenant_id in ('{TenantA}', '{TenantB}');
            delete from trellis_rentledger.rent_change where tenant_id in ('{TenantA}', '{TenantB}');
            delete from trellis_rentledger.rent_schedule where tenant_id in ('{TenantA}', '{TenantB}');
            """);
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task RenewalOffer_EachTenantSeesOnlyItsOwn()
    {
        TenantContext.Current = new TenantContext(TenantA);
        await using var db = fixture.CreateAppLeasingContext();

        (await db.RenewalOffers.ToListAsync()).Should().ContainSingle().Which.RenewalOfferId.Should().Be(_offerA);
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task RentChange_IsInvisibleToAnotherTenant()
    {
        TenantContext.Current = new TenantContext(TenantB);
        await using var db = fixture.CreateAppRentLedgerContext();

        (await db.RentChanges.AnyAsync(c => c.RentScheduleId == _scheduleA)).Should().BeFalse();
    }

    [Fact]
    [Trait("BDR", "BDR-LSE-010")]
    public async Task AScheduleLoadsWithItsRentChanges_AndANewChangePersistsThroughTheRepository()
    {
        TenantContext.Current = new TenantContext(TenantA);
        await using (var db = fixture.CreateAppRentLedgerContext())
        {
            var repository = new RentScheduleRepository(db);
            var schedule = await repository.GetActiveForLeaseAsync(TenantA, _leaseA);

            schedule!.RentChanges.Should().ContainSingle().Which.RentAmount.Should().Be(1200m);
            schedule.RentAmountOn(new DateOnly(2026, 5, 5)).Should().Be(1200m);

            var change = schedule.ScheduleRentChange(new DateOnly(2027, 1, 1), 1350m, null, Guid.NewGuid());
            await repository.AddRentChangeAsync(change);
            await repository.SaveChangesAsync();
        }

        await using var fresh = fixture.CreateAppRentLedgerContext();
        var reloaded = await new RentScheduleRepository(fresh).GetByIdAsync(_scheduleA);
        reloaded!.RentChanges.Select(c => c.RentAmount).Should().BeEquivalentTo([1200m, 1350m]);
    }

    [Fact]
    [Trait("BDR", "BDR-LSE-010")]
    public async Task OneLease_CanHaveOnlyOneOpenOffer_ButAnyNumberOfClosedOnes()
    {
        var closed = () => ExecuteAsOwnerAsync($"""
            insert into trellis_leasing.renewal_offer
                (renewal_offer_id, tenant_id, lease_id, unit_asset_id, proposed_term, proposed_end_date, respond_by, status, outcome_on, offered_by, created_at, updated_at)
            values (gen_random_uuid(), '{TenantA}', '{_leaseA}', gen_random_uuid(), 'FIXED', '2027-12-31', '2026-11-01', 'EXPIRED', '2026-11-02', gen_random_uuid(), now(), now());
            """);
        await closed();
        await closed();

        var secondOpen = () => ExecuteAsOwnerAsync($"""
            insert into trellis_leasing.renewal_offer
                (renewal_offer_id, tenant_id, lease_id, unit_asset_id, proposed_term, proposed_end_date, respond_by, status, offered_by, created_at, updated_at)
            values (gen_random_uuid(), '{TenantA}', '{_leaseA}', gen_random_uuid(), 'FIXED', '2027-12-31', '2026-11-01', 'OFFERED', gen_random_uuid(), now(), now());
            """);

        await secondOpen.Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == PostgresErrorCodes.UniqueViolation);
    }

    [Theory]
    [Trait("BDR", "BDR-LSE-010")]
    [InlineData("'FIXED', null, null, null, 'OFFERED', null, null")]
    [InlineData("'MONTH_TO_MONTH', '2027-12-31', null, null, 'OFFERED', null, null")]
    [InlineData("'MONTH_TO_MONTH', null, 1200, null, 'OFFERED', null, null")]
    [InlineData("'MONTH_TO_MONTH', null, null, null, 'DECLINED', '2026-10-01', null")]
    [InlineData("'MONTH_TO_MONTH', null, null, null, 'ACCEPTED', null, null")]
    public async Task TheShapeConstraints_RejectAnOfferThatDoesntHoldTogether(string termEndRentFromStatusOutcomeReason)
    {
        var insert = () => ExecuteAsOwnerAsync($"""
            insert into trellis_leasing.renewal_offer
                (renewal_offer_id, tenant_id, lease_id, unit_asset_id, proposed_term, proposed_end_date, proposed_rent_amount, proposed_rent_from,
                 status, outcome_on, decline_reason, respond_by, offered_by, created_at, updated_at)
            values (gen_random_uuid(), '{TenantA}', gen_random_uuid(), gen_random_uuid(), {termEndRentFromStatusOutcomeReason},
                    '2026-11-01', gen_random_uuid(), now(), now());
            """);

        await insert.Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == PostgresErrorCodes.CheckViolation);
    }

    private async Task ExecuteAsOwnerAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
