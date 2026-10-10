using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trellis.Api.Core.Middleware;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>
/// Phase 11's trellis_property.lease: plain tenant_isolation (no vendor branch), plus the
/// Postgres-level guarantees only a real database can prove — one ACTIVE lease per unit, one lease
/// per Leasing application, the term/end CHECK constraints, and no DELETE grant.
/// </summary>
[Collection(PostgresIsolationCollection.Name)]
public sealed class LeaseIsolationTests(PostgresIsolationFixture fixture) : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private Guid _leaseAId, _leaseBId;

    public async Task InitializeAsync()
    {
        _leaseAId = Guid.NewGuid();
        _leaseBId = Guid.NewGuid();
        await ExecuteAsOwnerAsync($"""
            insert into trellis_property.lease
                (lease_id, tenant_id, unit_asset_id, source, start_date, term_type, end_date, status, created_at, updated_at)
            values
                ('{_leaseAId}', '{TenantA}', gen_random_uuid(), 'MANUAL', '2026-01-01', 'FIXED', '2026-12-31', 'ACTIVE', now(), now()),
                ('{_leaseBId}', '{TenantB}', gen_random_uuid(), 'MANUAL', '2026-01-01', 'MONTH_TO_MONTH', null, 'ACTIVE', now(), now());
            """);
    }

    public async Task DisposeAsync()
    {
        TenantContext.Current = null;
        await ExecuteAsOwnerAsync($"delete from trellis_property.lease where tenant_id in ('{TenantA}', '{TenantB}')");
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task Lease_EachTenantSeesOnlyItsOwnRows()
    {
        TenantContext.Current = new TenantContext(TenantA);
        await using var db = fixture.CreateAppPropertyContext();

        var results = await db.Leases.ToListAsync();

        results.Should().ContainSingle().Which.LeaseId.Should().Be(_leaseAId);
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task Lease_WithNoTenantContext_ReturnsNothing()
    {
        TenantContext.Current = null;
        await using var db = fixture.CreateAppPropertyContext();

        (await db.Leases.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    [Trait("BDR", "BDR-LSE-009")]
    public async Task OneUnit_CanHaveOnlyOneActiveLease_ButAnyNumberOfEndedOnes()
    {
        var unit = Guid.NewGuid();
        await InsertAsync(unit, "ACTIVE");
        await InsertAsync(unit, "ENDED");
        await InsertAsync(unit, "ENDED");

        var second = () => InsertAsync(unit, "ACTIVE");

        await second.Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    [Trait("BDR", "BDR-LSE-009")]
    public async Task OneLeasingApplication_CanHaveOnlyOneLease()
    {
        var application = Guid.NewGuid();
        await InsertAsync(Guid.NewGuid(), "ENDED", application);

        var second = () => InsertAsync(Guid.NewGuid(), "ENDED", application);

        await second.Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == PostgresErrorCodes.UniqueViolation);
    }

    [Theory]
    [Trait("BDR", "BDR-LSE-009")]
    [InlineData("'FIXED', null")]
    [InlineData("'FIXED', '2025-12-31'")]
    [InlineData("'MONTH_TO_MONTH', '2026-12-31'")]
    [InlineData("'NOT_RECORDED', '2026-12-31'")]
    public async Task TheTermConstraint_RejectsATermThatDoesntHoldTogether(string termAndEnd)
    {
        var insert = () => ExecuteAsOwnerAsync($"""
            insert into trellis_property.lease
                (lease_id, tenant_id, unit_asset_id, source, start_date, term_type, end_date, status, created_at, updated_at)
            values (gen_random_uuid(), '{TenantA}', gen_random_uuid(), 'MANUAL', '2026-01-01', {termAndEnd}, 'ACTIVE', now(), now());
            """);

        await insert.Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == PostgresErrorCodes.CheckViolation);
    }

    [Theory]
    [Trait("BDR", "BDR-LSE-011")]
    [InlineData("'ENDED', null, 'NON_RENEWAL'")]
    [InlineData("'ENDED', '2026-06-30', null")]
    [InlineData("'ENDED', '2025-06-30', 'NON_RENEWAL'")]
    [InlineData("'ACTIVE', '2026-06-30', null")]
    public async Task TheStatusConstraint_RejectsAnEndedLeaseWithoutItsEnd_OrAnActiveOneWithOne(string statusEndedOnReason)
    {
        var insert = () => ExecuteAsOwnerAsync($"""
            insert into trellis_property.lease
                (lease_id, tenant_id, unit_asset_id, source, start_date, term_type, status, ended_on, end_reason, created_at, updated_at)
            values (gen_random_uuid(), '{TenantA}', gen_random_uuid(), 'MANUAL', '2026-01-01', 'MONTH_TO_MONTH', {statusEndedOnReason}, now(), now());
            """);

        await insert.Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    [Trait("BDR", "BDR-LSE-011")]
    public async Task TheAppRole_HasNoDeleteGrant_AnEndedLeaseIsHistory()
    {
        TenantContext.Current = new TenantContext(TenantA);
        await using var db = fixture.CreateAppPropertyContext();

        var delete = () => db.Leases.Where(l => l.LeaseId == _leaseAId).ExecuteDeleteAsync();

        await delete.Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == PostgresErrorCodes.InsufficientPrivilege);
    }

    private Task InsertAsync(Guid unit, string status, Guid? application = null) => ExecuteAsOwnerAsync($"""
        insert into trellis_property.lease
            (lease_id, tenant_id, unit_asset_id, source, lease_application_id, start_date, term_type, status, ended_on, end_reason, created_at, updated_at)
        values (gen_random_uuid(), '{TenantA}', '{unit}', {(application is null ? "'MANUAL', null" : $"'LEASING', '{application}'")},
                '2026-01-01', 'MONTH_TO_MONTH', '{status}',
                {(status == "ENDED" ? "'2026-06-30', 'NON_RENEWAL'" : "null, null")}, now(), now());
        """);

    private async Task ExecuteAsOwnerAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
