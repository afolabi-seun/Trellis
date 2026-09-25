using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trellis.Api.Core.Middleware;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>Same structure as <see cref="PropertyIsolationTests"/> — real Postgres, RLS enabled, tenant_isolation only (Workflow's vendor variant is Phase 4 scope, per the Phase 3 plan).</summary>
[Collection(PostgresIsolationCollection.Name)]
public sealed class WorkflowIsolationTests(PostgresIsolationFixture fixture) : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private Guid _departmentAId;
    private Guid _departmentBId;

    public async Task InitializeAsync()
    {
        _departmentAId = Guid.NewGuid();
        _departmentBId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var seed = connection.CreateCommand();
        seed.CommandText = """
            insert into trellis_workflow.department (department_id, tenant_id, name, status, created_at, updated_at)
            values
                (@deptA, @tenantA, 'Maintenance A', 'ACTIVE', now(), now()),
                (@deptB, @tenantB, 'Maintenance B', 'ACTIVE', now(), now());
            """;
        seed.Parameters.AddWithValue("deptA", _departmentAId);
        seed.Parameters.AddWithValue("deptB", _departmentBId);
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
        cleanup.CommandText = "delete from trellis_workflow.department where tenant_id in (@a, @b);";
        cleanup.Parameters.AddWithValue("a", TenantA);
        cleanup.Parameters.AddWithValue("b", TenantB);
        await cleanup.ExecuteNonQueryAsync();
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task GetById_WhenTenantContextMismatched_ReturnsNull()
    {
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppWorkflowContext();
        var result = await db.Departments.FirstOrDefaultAsync(d => d.DepartmentId == _departmentAId);

        result.Should().BeNull();
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task GetById_WhenTenantContextMatches_ReturnsRow()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppWorkflowContext();
        var result = await db.Departments.FirstOrDefaultAsync(d => d.DepartmentId == _departmentAId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Maintenance A");
    }

    [Fact]
    public async Task List_WithNoContextAtAll_ReturnsNoRows()
    {
        await using var db = fixture.CreateAppWorkflowContext();
        var results = await db.Departments.ToListAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task List_WithTenantAContext_ReturnsOnlyTenantAsRow()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppWorkflowContext();
        var results = await db.Departments.ToListAsync();

        results.Should().ContainSingle(d => d.DepartmentId == _departmentAId);
    }

    [Fact]
    public async Task List_UnderSystemContext_ReturnsEveryTenant()
    {
        using (SystemContext.Enter())
        {
            await using var db = fixture.CreateAppWorkflowContext();
            var results = await db.Departments.ToListAsync();

            results.Select(d => d.DepartmentId).Should().BeEquivalentTo([_departmentAId, _departmentBId]);
        }
    }
}
