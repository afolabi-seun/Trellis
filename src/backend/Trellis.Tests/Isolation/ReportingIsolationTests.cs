using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>
/// Phase 8's own proof of RLS across trellis_reporting (owner_report, report_share_link — plain
/// tenant_isolation, exact BillingIsolationTests.cs shape, no vendor branch: a Vendor has no
/// Reporting relationship with any tenant), plus a fresh isolation test for audit_record itself —
/// confirmed no existing test file covered it at all before this phase, despite the table
/// existing since Phase 1. audit_record physically lives in trellis_audit, unaffected by the
/// Phase 8 ownership transfer to ReportingDbContext — see that migration's own doc comment.
/// </summary>
[Collection(PostgresIsolationCollection.Name)]
public sealed class ReportingIsolationTests(PostgresIsolationFixture fixture) : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private Guid _ownerReportAId;
    private Guid _ownerReportBId;
    private Guid _shareLinkAId;
    private Guid _platformAuditId;
    private Guid _tenantAAuditId;

    public async Task InitializeAsync()
    {
        _ownerReportAId = Guid.NewGuid();
        _ownerReportBId = Guid.NewGuid();
        _shareLinkAId = Guid.NewGuid();
        _platformAuditId = Guid.NewGuid();
        _tenantAAuditId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var seed = connection.CreateCommand();
        seed.CommandText = """
            insert into trellis_reporting.owner_report
                (owner_report_id, tenant_id, owner_id, owner_name, period_type, period_start, period_end,
                 status, property_count, total_maintenance_cost, maintenance_currency, open_maintenance_count,
                 compliant_document_count, expiring_document_count, generated_by, created_at)
            values
                (@ownerReportA, @tenantA, gen_random_uuid(), 'Owner A', 'MONTHLY', '2026-01-01', '2026-01-31',
                 'READY', 1, 0, 'USD', 0, 0, 0, gen_random_uuid(), now()),
                (@ownerReportB, @tenantB, gen_random_uuid(), 'Owner B', 'MONTHLY', '2026-01-01', '2026-01-31',
                 'READY', 1, 0, 'USD', 0, 0, 0, gen_random_uuid(), now());

            insert into trellis_reporting.report_share_link
                (report_share_link_id, tenant_id, owner_report_id, token_hash, status, expires_at, created_by, created_at)
            values
                (@shareLinkA, @tenantA, @ownerReportA, @tokenHash, 'ACTIVE', now() + interval '14 days', gen_random_uuid(), now());

            insert into trellis_audit.audit_record (audit_id, tenant_id, actor_id, actor_type, audit_type, recorded_at)
            values
                (@platformAudit, NULL, gen_random_uuid(), 'System', 'ISOLATION_TEST_PLATFORM', now()),
                (@tenantAAudit, @tenantA, gen_random_uuid(), 'HumanUser', 'ISOLATION_TEST_TENANT', now());
            """;
        seed.Parameters.AddWithValue("ownerReportA", _ownerReportAId);
        seed.Parameters.AddWithValue("ownerReportB", _ownerReportBId);
        seed.Parameters.AddWithValue("shareLinkA", _shareLinkAId);
        seed.Parameters.AddWithValue("tokenHash", $"hash-{_shareLinkAId}");
        seed.Parameters.AddWithValue("platformAudit", _platformAuditId);
        seed.Parameters.AddWithValue("tenantAAudit", _tenantAAuditId);
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
        // audit_record's own append-only trigger rejects DELETE unconditionally, even for this
        // privileged owner-role connection (that's the exact behavior two of this file's own
        // tests assert) — so the seeded audit_record rows are never cleaned up here, deliberately.
        // Harmless: this is an ephemeral Testcontainers Postgres, destroyed once the whole test
        // run completes, and audit_id is a fresh GUID per test instance, so no unique-constraint
        // collision risk accumulates across tests the way it would for report_share_link's
        // token_hash.
        cleanup.CommandText = """
            delete from trellis_reporting.report_share_link where tenant_id in (@tenantA, @tenantB);
            delete from trellis_reporting.owner_report where tenant_id in (@tenantA, @tenantB);
            """;
        cleanup.Parameters.AddWithValue("tenantA", TenantA);
        cleanup.Parameters.AddWithValue("tenantB", TenantB);
        await cleanup.ExecuteNonQueryAsync();
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task OwnerReport_TenantAContext_SeesOnlyItsOwnRow()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppReportingContext();
        var results = await db.OwnerReports.ToListAsync();

        results.Should().ContainSingle(r => r.OwnerReportId == _ownerReportAId);
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task ReportShareLink_TenantBContext_SeesNoRows()
    {
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppReportingContext();
        var results = await db.ReportShareLinks.ToListAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task List_WithNoContextAtAll_ReturnsNoRowsAcrossEitherTable()
    {
        await using var db = fixture.CreateAppReportingContext();

        (await db.OwnerReports.ToListAsync()).Should().BeEmpty();
        (await db.ReportShareLinks.ToListAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// The genuine cross-tenant proof for share links: a token resolves to its own tenant's report
    /// under SystemContext (the real anonymous-resolution posture — see IOwnerReportRepository.
    /// GetByIdForTenantAsync's own doc comment for why this path never uses Scoped()), but a normal
    /// tenant-scoped query for a DIFFERENT tenant can never see it.
    /// </summary>
    [Fact]
    [Trait("BDR", "BDR-SEC-003")]
    public async Task ReportShareLink_TokenForTenantA_IsInvisibleUnderTenantBContext_ButVisibleUnderSystemContext()
    {
        TenantContext.Current = new TenantContext(TenantB);
        await using (var tenantBScoped = fixture.CreateAppReportingContext())
        {
            var underWrongTenant = await tenantBScoped.ReportShareLinks.FirstOrDefaultAsync(l => l.ReportShareLinkId == _shareLinkAId);
            underWrongTenant.Should().BeNull();
        }

        TenantContext.Current = null;
        using (SystemContext.Enter())
        {
            await using var systemScoped = fixture.CreateAppReportingContext();
            var link = await systemScoped.ReportShareLinks.FirstOrDefaultAsync(l => l.ReportShareLinkId == _shareLinkAId);

            link.Should().NotBeNull();
            link!.TenantId.Should().Be(TenantA);
        }
    }

    [Fact]
    [Trait("BDR", "BDR-NFR-008")]
    public async Task AuditRecord_WithNullTenantId_IsInvisibleUnderAnyTenantContext_OnlyVisibleUnderSystemContext()
    {
        TenantContext.Current = new TenantContext(TenantA);
        await using (var tenantScoped = fixture.CreateAppReportingContext())
        {
            var underTenantContext = await tenantScoped.AuditRecords.FirstOrDefaultAsync(a => a.AuditId == _platformAuditId);
            underTenantContext.Should().BeNull();
        }

        TenantContext.Current = null;
        using (SystemContext.Enter())
        {
            await using var systemScoped = fixture.CreateAppReportingContext();
            var record = await systemScoped.AuditRecords.FirstOrDefaultAsync(a => a.AuditId == _platformAuditId);

            record.Should().NotBeNull();
            record!.TenantId.Should().BeNull();
        }
    }

    [Fact]
    [Trait("BDR", "BDR-NFR-008")]
    public async Task AuditRecord_WithARealTenantId_RespectsOrdinaryTenantIsolation()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppReportingContext();
        var record = await db.AuditRecords.FirstOrDefaultAsync(a => a.AuditId == _tenantAAuditId);

        record.Should().NotBeNull();
    }

    /// <summary>
    /// The append-only trigger, proven fresh here (never previously asserted by any test) — still
    /// fires for the privileged migrations-owner role, since Postgres triggers apply to every role
    /// including a table's own owner (only RLS is bypassed for an owner/superuser, never triggers).
    /// The unprivileged trellis_app role has no UPDATE/DELETE GRANT on this table at all (belt-and-
    /// suspenders, per the owning migration's own comment), so exercising trellis_app here would
    /// only prove a permission error, not the trigger itself.
    /// </summary>
    [Fact]
    [Trait("BDR", "BDR-NFR-008")]
    public async Task AuditRecord_Update_ThrowsFromTheAppendOnlyTrigger_EvenForThePrivilegedOwnerRole()
    {
        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var update = connection.CreateCommand();
        update.CommandText = "UPDATE trellis_audit.audit_record SET audit_type = 'TAMPERED' WHERE audit_id = @id";
        update.Parameters.AddWithValue("id", _tenantAAuditId);

        var act = async () => await update.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<PostgresException>()).Which.MessageText.Should().Contain("append-only");
    }

    [Fact]
    [Trait("BDR", "BDR-NFR-008")]
    public async Task AuditRecord_Delete_ThrowsFromTheAppendOnlyTrigger_EvenForThePrivilegedOwnerRole()
    {
        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM trellis_audit.audit_record WHERE audit_id = @id";
        delete.Parameters.AddWithValue("id", _tenantAAuditId);

        var act = async () => await delete.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<PostgresException>()).Which.MessageText.Should().Contain("append-only");
    }
}
