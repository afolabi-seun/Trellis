using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trellis.Api.Core.Middleware;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>
/// The highest-value test in Phase 4 — proves the new tenant_or_vendor_isolation policy variant
/// actually does what the plan claims: a vendor context sees only its own assignments and only
/// the specific work items it's assigned to, across two tenants it has no other relationship to,
/// while a different vendor sees none of it. Models WorkflowIsolationTests.cs's shape exactly.
/// </summary>
[Collection(PostgresIsolationCollection.Name)]
public sealed class VendorIsolationTests(PostgresIsolationFixture fixture) : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid VendorV = Guid.NewGuid();
    private static readonly Guid VendorW = Guid.NewGuid();
    private Guid _instanceAId;
    private Guid _instanceBId;
    private Guid _templateAId;
    private Guid _templateBId;

    public async Task InitializeAsync()
    {
        _instanceAId = Guid.NewGuid();
        _instanceBId = Guid.NewGuid();
        _templateAId = Guid.NewGuid();
        _templateBId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var seed = connection.CreateCommand();
        seed.CommandText = """
            insert into trellis_workflow.workflow_instance
                (workflow_instance_id, tenant_id, workflow_template_id, status, current_step_order, created_by, created_at, updated_at)
            values
                (@instanceA, @tenantA, @templateA, 'IN_PROGRESS', 0, @userId, now(), now()),
                (@instanceB, @tenantB, @templateB, 'IN_PROGRESS', 0, @userId, now(), now());

            insert into trellis_vendor.vendor (vendor_id, vendor_name, category, created_at)
            values (@vendorV, 'Vendor V', 'MAINTENANCE', now());

            insert into trellis_vendor.vendor_assignment
                (assignment_id, vendor_id, tenant_id, work_item_type, work_item_id, status, assigned_at)
            values (gen_random_uuid(), @vendorV, @tenantA, 'WORKFLOW_INSTANCE', @instanceA, 'ASSIGNED', now());
            """;
        seed.Parameters.AddWithValue("instanceA", _instanceAId);
        seed.Parameters.AddWithValue("instanceB", _instanceBId);
        seed.Parameters.AddWithValue("templateA", _templateAId);
        seed.Parameters.AddWithValue("templateB", _templateBId);
        seed.Parameters.AddWithValue("tenantA", TenantA);
        seed.Parameters.AddWithValue("tenantB", TenantB);
        seed.Parameters.AddWithValue("userId", userId);
        seed.Parameters.AddWithValue("vendorV", VendorV);
        await seed.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        TenantContext.Current = null;
        VendorContext.Current = null;

        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var cleanup = connection.CreateCommand();
        cleanup.CommandText = """
            delete from trellis_vendor.vendor_assignment where vendor_id in (@vendorV, @vendorW);
            delete from trellis_vendor.vendor where vendor_id in (@vendorV, @vendorW);
            delete from trellis_workflow.workflow_instance where tenant_id in (@tenantA, @tenantB);
            """;
        cleanup.Parameters.AddWithValue("vendorV", VendorV);
        cleanup.Parameters.AddWithValue("vendorW", VendorW);
        cleanup.Parameters.AddWithValue("tenantA", TenantA);
        cleanup.Parameters.AddWithValue("tenantB", TenantB);
        await cleanup.ExecuteNonQueryAsync();
    }

    [Fact]
    [Trait("BDR", "BDR-VEN-002")]
    public async Task VendorContext_SeesOnlyItsOwnAssignments()
    {
        VendorContext.Current = new VendorContext(VendorV);

        await using var db = fixture.CreateAppVendorContext();
        var results = await db.VendorAssignments.ToListAsync();

        results.Should().ContainSingle(a => a.WorkItemId == _instanceAId && a.TenantId == TenantA);
    }

    [Fact]
    [Trait("BDR", "BDR-VEN-002")]
    public async Task VendorContext_ForADifferentUnassignedVendor_SeesNoAssignments()
    {
        VendorContext.Current = new VendorContext(VendorW);

        await using var db = fixture.CreateAppVendorContext();
        var results = await db.VendorAssignments.ToListAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    [Trait("BDR", "BDR-VEN-002")]
    public async Task VendorContext_SeesOnlyItsAssignedWorkflowInstance_NotTheUnrelatedTenantsOwn()
    {
        VendorContext.Current = new VendorContext(VendorV);

        await using var db = fixture.CreateAppWorkflowContext();
        var results = await db.WorkflowInstances.ToListAsync();

        results.Should().ContainSingle(i => i.WorkflowInstanceId == _instanceAId);
    }

    [Fact]
    [Trait("BDR", "BDR-VEN-002")]
    public async Task VendorContext_ForADifferentUnassignedVendor_SeesNoWorkflowInstances()
    {
        VendorContext.Current = new VendorContext(VendorW);

        await using var db = fixture.CreateAppWorkflowContext();
        var results = await db.WorkflowInstances.ToListAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task TenantContext_StillSeesOnlyItsOwnInstance_UnaffectedByTheVendorPolicyUpgrade()
    {
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppWorkflowContext();
        var results = await db.WorkflowInstances.ToListAsync();

        results.Should().ContainSingle(i => i.WorkflowInstanceId == _instanceBId);
    }
}
