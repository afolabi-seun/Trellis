using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trellis.Api.Core.Middleware;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>
/// Phase 6's own proof of RLS across all 3 trellis_ai tables — plain tenant_isolation shape, no
/// vendor branch (this module has no VendorContext-visible surface at all), the same shape
/// NotificationIsolationTests.cs models. override_log is insert-only in the app but still needs the
/// same read-isolation proof as the other two.
/// </summary>
[Collection(PostgresIsolationCollection.Name)]
public sealed class AiIsolationTests(PostgresIsolationFixture fixture) : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private Guid _jobAId;
    private Guid _jobBId;
    private Guid _suggestionAId;
    private Guid _suggestionBId;
    private Guid _overrideAId;
    private Guid _overrideBId;

    public async Task InitializeAsync()
    {
        _jobAId = Guid.NewGuid();
        _jobBId = Guid.NewGuid();
        _suggestionAId = Guid.NewGuid();
        _suggestionBId = Guid.NewGuid();
        _overrideAId = Guid.NewGuid();
        _overrideBId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var seed = connection.CreateCommand();
        seed.CommandText = """
            insert into trellis_ai.extraction_job
                (extraction_job_id, tenant_id, document_id, document_version_id, document_type, status, created_at, updated_at)
            values
                (@jobA, @tenantA, gen_random_uuid(), gen_random_uuid(), 'LEASE', 'PENDING', now(), now()),
                (@jobB, @tenantB, gen_random_uuid(), gen_random_uuid(), 'LEASE', 'PENDING', now(), now());

            insert into trellis_ai.routing_suggestion
                (routing_suggestion_id, tenant_id, description, status, created_by, created_at, updated_at)
            values
                (@suggestionA, @tenantA, 'Leaking pipe', 'PENDING', @userId, now(), now()),
                (@suggestionB, @tenantB, 'Leaking pipe', 'PENDING', @userId, now(), now());

            insert into trellis_ai.override_log
                (override_log_id, tenant_id, suggestion_type, suggestion_id, original_value_json, corrected_value_json, overridden_by, created_at)
            values
                (@overrideA, @tenantA, 'EXTRACTION', @jobA, '{}', '{}', @userId, now()),
                (@overrideB, @tenantB, 'EXTRACTION', @jobB, '{}', '{}', @userId, now());
            """;
        seed.Parameters.AddWithValue("jobA", _jobAId);
        seed.Parameters.AddWithValue("jobB", _jobBId);
        seed.Parameters.AddWithValue("suggestionA", _suggestionAId);
        seed.Parameters.AddWithValue("suggestionB", _suggestionBId);
        seed.Parameters.AddWithValue("overrideA", _overrideAId);
        seed.Parameters.AddWithValue("overrideB", _overrideBId);
        seed.Parameters.AddWithValue("tenantA", TenantA);
        seed.Parameters.AddWithValue("tenantB", TenantB);
        seed.Parameters.AddWithValue("userId", userId);
        await seed.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        TenantContext.Current = null;

        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var cleanup = connection.CreateCommand();
        cleanup.CommandText = """
            delete from trellis_ai.override_log where tenant_id in (@tenantA, @tenantB);
            delete from trellis_ai.routing_suggestion where tenant_id in (@tenantA, @tenantB);
            delete from trellis_ai.extraction_job where tenant_id in (@tenantA, @tenantB);
            """;
        cleanup.Parameters.AddWithValue("tenantA", TenantA);
        cleanup.Parameters.AddWithValue("tenantB", TenantB);
        await cleanup.ExecuteNonQueryAsync();
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task ExtractionJobs_TenantAContext_SeesOnlyItsOwnJob()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppAiContext();
        var results = await db.ExtractionJobs.ToListAsync();

        results.Should().ContainSingle(j => j.ExtractionJobId == _jobAId);
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task ExtractionJobs_TenantBContext_SeesOnlyItsOwnJob()
    {
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppAiContext();
        var results = await db.ExtractionJobs.ToListAsync();

        results.Should().ContainSingle(j => j.ExtractionJobId == _jobBId);
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task RoutingSuggestions_TenantAContext_SeesOnlyItsOwnSuggestion()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppAiContext();
        var results = await db.RoutingSuggestions.ToListAsync();

        results.Should().ContainSingle(r => r.RoutingSuggestionId == _suggestionAId);
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task RoutingSuggestions_TenantBContext_SeesOnlyItsOwnSuggestion()
    {
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppAiContext();
        var results = await db.RoutingSuggestions.ToListAsync();

        results.Should().ContainSingle(r => r.RoutingSuggestionId == _suggestionBId);
    }

    [Fact]
    [Trait("BDR", "BDR-AI-004")]
    public async Task OverrideLog_TenantAContext_SeesOnlyItsOwnEntry()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppAiContext();
        var results = await db.OverrideLogEntries.ToListAsync();

        results.Should().ContainSingle(e => e.OverrideLogId == _overrideAId);
    }

    [Fact]
    [Trait("BDR", "BDR-AI-004")]
    public async Task OverrideLog_TenantBContext_SeesOnlyItsOwnEntry()
    {
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppAiContext();
        var results = await db.OverrideLogEntries.ToListAsync();

        results.Should().ContainSingle(e => e.OverrideLogId == _overrideBId);
    }

    [Fact]
    public async Task List_WithNoContextAtAll_ReturnsNoRowsAcrossAllThreeTables()
    {
        await using var db = fixture.CreateAppAiContext();

        (await db.ExtractionJobs.ToListAsync()).Should().BeEmpty();
        (await db.RoutingSuggestions.ToListAsync()).Should().BeEmpty();
        (await db.OverrideLogEntries.ToListAsync()).Should().BeEmpty();
    }
}
