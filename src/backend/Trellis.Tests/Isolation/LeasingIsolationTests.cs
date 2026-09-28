using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trellis.Api.Core.Middleware;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>
/// Phase 9's own proof of RLS across trellis_leasing — plain tenant_isolation on the 6
/// tenant-owned tables (no vendor branch: a Vendor has no relationship to leasing, ever), the
/// stricter system_only on leasing_webhook_event (no tenant identity at all), plus the two
/// Postgres-level guarantees the spec asks for that only a real database can prove: the partial
/// unique index ("one active listing per unit") and application_event's append-only grants.
/// Exact BillingIsolationTests/ReportingIsolationTests shape.
/// </summary>
[Collection(PostgresIsolationCollection.Name)]
public sealed class LeasingIsolationTests(PostgresIsolationFixture fixture) : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private Guid _listingAId, _listingBId, _photoAId, _applicationAId, _applicationBId, _screeningAId, _eventAId, _tokenAId, _webhookId;
    private string _tokenHashA = null!;
    private string _webhookEventKey = null!;

    public async Task InitializeAsync()
    {
        _listingAId = Guid.NewGuid();
        _listingBId = Guid.NewGuid();
        _photoAId = Guid.NewGuid();
        _applicationAId = Guid.NewGuid();
        _applicationBId = Guid.NewGuid();
        _screeningAId = Guid.NewGuid();
        _eventAId = Guid.NewGuid();
        _tokenAId = Guid.NewGuid();
        _webhookId = Guid.NewGuid();
        _tokenHashA = $"hash-{_tokenAId}";
        _webhookEventKey = $"env-{_webhookId}";

        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var seed = connection.CreateCommand();
        seed.CommandText = """
            insert into trellis_leasing.vacancy_listing
                (vacancy_listing_id, tenant_id, unit_asset_id, rent_amount, currency, available_from, status, created_by, created_at, updated_at)
            values
                (@listingA, @tenantA, gen_random_uuid(), 1500, 'USD', '2026-11-01', 'LISTED', gen_random_uuid(), now(), now()),
                (@listingB, @tenantB, gen_random_uuid(), 900, 'USD', '2026-11-01', 'LISTED', gen_random_uuid(), now(), now());

            insert into trellis_leasing.vacancy_listing_photo
                (vacancy_listing_photo_id, tenant_id, vacancy_listing_id, file_name, content_type, file_size_bytes, storage_key, scan_status, display_order, uploaded_by, created_at)
            values (@photoA, @tenantA, @listingA, 'a.jpg', 'image/jpeg', 10, 'k', 'CLEAN', 0, gen_random_uuid(), now());

            insert into trellis_leasing.application
                (application_id, tenant_id, vacancy_listing_id, applicant_email, applicant_full_name, applicant_phone,
                 applicant_date_of_birth, applicant_current_address, consent_to_screening, status, submitted_at)
            values
                (@applicationA, @tenantA, @listingA, 'a@example.com', 'Ada A', '1', '1990-01-01', 'addr', true, 'SUBMITTED', now()),
                (@applicationB, @tenantB, @listingB, 'b@example.com', 'Bob B', '1', '1990-01-01', 'addr', true, 'SUBMITTED', now());

            insert into trellis_leasing.screening_result (screening_result_id, tenant_id, application_id, provider, outcome_summary, received_at)
            values (@screeningA, @tenantA, @applicationA, 'NOOP', 'PASS', now());

            insert into trellis_leasing.application_event (application_event_id, tenant_id, application_id, event_type, actor_type, created_at)
            values (@eventA, @tenantA, @applicationA, 'SUBMITTED', 'SYSTEM', now());

            insert into trellis_leasing.application_link_token
                (application_link_token_id, tenant_id, vacancy_listing_id, applicant_email, token_hash, status, expires_at, created_at)
            values (@tokenA, @tenantA, @listingA, 'a@example.com', @tokenHashA, 'ACTIVE', now() + interval '7 days', now());

            insert into trellis_leasing.leasing_webhook_event (leasing_webhook_event_id, provider_event_id, event_type, payload_json, received_at)
            values (@webhook, @webhookKey, 'envelope.completed', '{}', now());
            """;
        seed.Parameters.AddWithValue("listingA", _listingAId);
        seed.Parameters.AddWithValue("listingB", _listingBId);
        seed.Parameters.AddWithValue("photoA", _photoAId);
        seed.Parameters.AddWithValue("applicationA", _applicationAId);
        seed.Parameters.AddWithValue("applicationB", _applicationBId);
        seed.Parameters.AddWithValue("screeningA", _screeningAId);
        seed.Parameters.AddWithValue("eventA", _eventAId);
        seed.Parameters.AddWithValue("tokenA", _tokenAId);
        seed.Parameters.AddWithValue("tokenHashA", _tokenHashA);
        seed.Parameters.AddWithValue("webhook", _webhookId);
        seed.Parameters.AddWithValue("webhookKey", _webhookEventKey);
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
            delete from trellis_leasing.application_event where tenant_id in (@tenantA, @tenantB);
            delete from trellis_leasing.screening_result where tenant_id in (@tenantA, @tenantB);
            delete from trellis_leasing.application_link_token where tenant_id in (@tenantA, @tenantB);
            delete from trellis_leasing.application where tenant_id in (@tenantA, @tenantB);
            delete from trellis_leasing.vacancy_listing_photo where tenant_id in (@tenantA, @tenantB);
            delete from trellis_leasing.vacancy_listing where tenant_id in (@tenantA, @tenantB);
            delete from trellis_leasing.leasing_webhook_event where provider_event_id = @webhookKey;
            """;
        cleanup.Parameters.AddWithValue("tenantA", TenantA);
        cleanup.Parameters.AddWithValue("tenantB", TenantB);
        cleanup.Parameters.AddWithValue("webhookKey", _webhookEventKey);
        await cleanup.ExecuteNonQueryAsync();
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task VacancyListing_TenantAContext_SeesOnlyItsOwnRow()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppLeasingContext();
        var results = await db.VacancyListings.ToListAsync();

        results.Should().ContainSingle(l => l.VacancyListingId == _listingAId);
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task VacancyListing_TenantBContext_SeesOnlyItsOwnRow_NeverTenantAs()
    {
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppLeasingContext();
        var results = await db.VacancyListings.ToListAsync();

        results.Should().ContainSingle(l => l.VacancyListingId == _listingBId);
        results.Should().NotContain(l => l.VacancyListingId == _listingAId);
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task Application_TenantBContext_CannotSeeTenantAsApplicant()
    {
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppLeasingContext();
        var results = await db.Applications.ToListAsync();

        results.Should().ContainSingle(a => a.ApplicationId == _applicationBId);
        results.Should().NotContain(a => a.ApplicantEmail == "a@example.com");
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task EveryOtherTenantOwnedTable_TenantBContext_SeesNoneOfTenantAsRows()
    {
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppLeasingContext();

        (await db.VacancyListingPhotos.ToListAsync()).Should().BeEmpty();
        (await db.ScreeningResults.ToListAsync()).Should().BeEmpty();
        (await db.ApplicationEvents.ToListAsync()).Should().BeEmpty();
        (await db.ApplicationLinkTokens.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task TenantAContext_SeesItsOwnRowsInEveryTenantOwnedTable()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppLeasingContext();

        (await db.VacancyListingPhotos.ToListAsync()).Should().ContainSingle(p => p.VacancyListingPhotoId == _photoAId);
        (await db.ScreeningResults.ToListAsync()).Should().ContainSingle(r => r.ScreeningResultId == _screeningAId);
        (await db.ApplicationEvents.ToListAsync()).Should().ContainSingle(e => e.ApplicationEventId == _eventAId);
        (await db.ApplicationLinkTokens.ToListAsync()).Should().ContainSingle(t => t.ApplicationLinkTokenId == _tokenAId);
    }

    [Fact]
    public async Task List_WithNoContextAtAll_ReturnsNoRowsAcrossEveryTenantOwnedTable()
    {
        await using var db = fixture.CreateAppLeasingContext();

        (await db.VacancyListings.ToListAsync()).Should().BeEmpty();
        (await db.VacancyListingPhotos.ToListAsync()).Should().BeEmpty();
        (await db.Applications.ToListAsync()).Should().BeEmpty();
        (await db.ScreeningResults.ToListAsync()).Should().BeEmpty();
        (await db.ApplicationEvents.ToListAsync()).Should().BeEmpty();
        (await db.ApplicationLinkTokens.ToListAsync()).Should().BeEmpty();
    }

    /// <summary>The genuine cross-tenant proof for application links: the anonymous resolve path finds a token by hash under SystemContext (the tenant isn't known yet), while an ordinary tenant-scoped query for a different tenant can never see it.</summary>
    [Fact]
    [Trait("BDR", "BDR-LSE-002")]
    public async Task ApplicationLinkToken_ForTenantA_IsInvisibleUnderTenantBContext_ButResolvableUnderSystemContext()
    {
        TenantContext.Current = new TenantContext(TenantB);
        await using (var tenantBScoped = fixture.CreateAppLeasingContext())
        {
            (await tenantBScoped.ApplicationLinkTokens.FirstOrDefaultAsync(t => t.TokenHash == _tokenHashA)).Should().BeNull();
        }

        TenantContext.Current = null;
        using (SystemContext.Enter())
        {
            await using var systemScoped = fixture.CreateAppLeasingContext();
            var token = await systemScoped.ApplicationLinkTokens.FirstOrDefaultAsync(t => t.TokenHash == _tokenHashA);

            token.Should().NotBeNull();
            token!.TenantId.Should().Be(TenantA);
        }
    }

    [Fact]
    [Trait("BDR", "BDR-LSE-005")]
    public async Task LeasingWebhookEvent_IsInvisibleUnderAnyTenantContext_OnlySystemContextSeesIt()
    {
        TenantContext.Current = new TenantContext(TenantA);
        await using (var tenantScoped = fixture.CreateAppLeasingContext())
        {
            (await tenantScoped.LeasingWebhookEvents.ToListAsync()).Should().BeEmpty();
        }

        TenantContext.Current = null;
        using (SystemContext.Enter())
        {
            await using var systemScoped = fixture.CreateAppLeasingContext();
            (await systemScoped.LeasingWebhookEvents.ToListAsync()).Should().Contain(w => w.LeasingWebhookEventId == _webhookId);
        }
    }

    /// <summary>BDR-LSE-001's "one active listing per unit" — a Postgres partial unique index, which only a real database can prove.</summary>
    [Fact]
    [Trait("BDR", "BDR-LSE-001")]
    public async Task PartialUniqueIndex_RejectsASecondActiveListingForTheSameUnit_ButAllowsOneOnceTheFirstIsWithdrawn()
    {
        var unitId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();

        async Task InsertAsync(string status)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                insert into trellis_leasing.vacancy_listing
                    (vacancy_listing_id, tenant_id, unit_asset_id, rent_amount, currency, available_from, status, created_by, created_at, updated_at)
                values (gen_random_uuid(), @tenant, @unit, 1000, 'USD', '2026-11-01', @status, gen_random_uuid(), now(), now());
                """;
            insert.Parameters.AddWithValue("tenant", TenantA);
            insert.Parameters.AddWithValue("unit", unitId);
            insert.Parameters.AddWithValue("status", status);
            await insert.ExecuteNonQueryAsync();
        }

        await InsertAsync("LISTED");

        var duplicate = () => InsertAsync("UNDER_APPLICATION");
        (await duplicate.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23505");

        await using (var withdraw = connection.CreateCommand())
        {
            withdraw.CommandText = "update trellis_leasing.vacancy_listing set status = 'WITHDRAWN' where unit_asset_id = @unit and status = 'LISTED'";
            withdraw.Parameters.AddWithValue("unit", unitId);
            await withdraw.ExecuteNonQueryAsync();
        }

        await InsertAsync("LISTED");
        await InsertAsync("LEASED");
    }

    /// <summary>application_event is append-only at the grant level: the running app's role has no UPDATE or DELETE on it at all.</summary>
    [Fact]
    [Trait("BDR", "BDR-LSE-007")]
    public async Task ApplicationEvent_TheAppRole_CannotUpdateOrDelete_ItsAppendOnlyAtTheGrantLevel()
    {
        await using var connection = new NpgsqlConnection(fixture.AppConnectionString);
        await connection.OpenAsync();

        await using var update = connection.CreateCommand();
        update.CommandText = "update trellis_leasing.application_event set event_type = 'TAMPERED'";
        var updateAct = async () => await update.ExecuteNonQueryAsync();
        (await updateAct.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");

        await using var delete = connection.CreateCommand();
        delete.CommandText = "delete from trellis_leasing.application_event";
        var deleteAct = async () => await delete.ExecuteNonQueryAsync();
        (await deleteAct.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
    }
}
