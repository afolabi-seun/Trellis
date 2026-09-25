using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trellis.Api.Core.Middleware;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>Same structure as <see cref="WorkflowIsolationTests"/> — real Postgres, RLS enabled, tenant_isolation only (Vendor's own variant is Phase 4 scope).</summary>
[Collection(PostgresIsolationCollection.Name)]
public sealed class DocumentIsolationTests(PostgresIsolationFixture fixture) : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private Guid _documentAId;
    private Guid _documentBId;

    public async Task InitializeAsync()
    {
        _documentAId = Guid.NewGuid();
        _documentBId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var seed = connection.CreateCommand();
        seed.CommandText = """
            insert into trellis_document.document (document_id, tenant_id, document_type, status, created_at, updated_at)
            values
                (@docA, @tenantA, 'LEASE', 'ACTIVE', now(), now()),
                (@docB, @tenantB, 'INVOICE', 'ACTIVE', now(), now());
            """;
        seed.Parameters.AddWithValue("docA", _documentAId);
        seed.Parameters.AddWithValue("docB", _documentBId);
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
        cleanup.CommandText = "delete from trellis_document.document where tenant_id in (@a, @b);";
        cleanup.Parameters.AddWithValue("a", TenantA);
        cleanup.Parameters.AddWithValue("b", TenantB);
        await cleanup.ExecuteNonQueryAsync();
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task GetById_WhenTenantContextMismatched_ReturnsNull()
    {
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppDocumentContext();
        var result = await db.Documents.FirstOrDefaultAsync(d => d.DocumentId == _documentAId);

        result.Should().BeNull();
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task GetById_WhenTenantContextMatches_ReturnsRow()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppDocumentContext();
        var result = await db.Documents.FirstOrDefaultAsync(d => d.DocumentId == _documentAId);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task List_WithNoContextAtAll_ReturnsNoRows()
    {
        await using var db = fixture.CreateAppDocumentContext();
        var results = await db.Documents.ToListAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task List_UnderSystemContext_ReturnsEveryTenant()
    {
        using (SystemContext.Enter())
        {
            await using var db = fixture.CreateAppDocumentContext();
            var results = await db.Documents.ToListAsync();

            results.Select(d => d.DocumentId).Should().BeEquivalentTo([_documentAId, _documentBId]);
        }
    }
}
