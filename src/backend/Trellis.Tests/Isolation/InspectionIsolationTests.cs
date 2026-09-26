using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trellis.Api.Core.Middleware;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>
/// Phase 5's own proof that tenant_or_vendor_isolation holds for inspection AND its two child
/// tables (inspection_item, inspection_photo) — the child tables use the simpler, JOIN-free shape
/// (denormalized tenant_id + an EXISTS keyed directly on their own inspection_id), unlike
/// document_version which has to join through its parent. Models VendorIsolationTests.cs's shape.
/// </summary>
[Collection(PostgresIsolationCollection.Name)]
public sealed class InspectionIsolationTests(PostgresIsolationFixture fixture) : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid VendorV = Guid.NewGuid();
    private static readonly Guid VendorW = Guid.NewGuid();
    private Guid _inspectionAId;
    private Guid _inspectionBId;
    private Guid _itemAId;
    private Guid _photoAId;

    public async Task InitializeAsync()
    {
        _inspectionAId = Guid.NewGuid();
        _inspectionBId = Guid.NewGuid();
        _itemAId = Guid.NewGuid();
        _photoAId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var seed = connection.CreateCommand();
        seed.CommandText = """
            insert into trellis_inspection.inspection
                (inspection_id, tenant_id, scheduled_by_user_id, status, scheduled_at, created_at, updated_at)
            values
                (@inspectionA, @tenantA, @userId, 'SCHEDULED', now(), now(), now()),
                (@inspectionB, @tenantB, @userId, 'SCHEDULED', now(), now(), now());

            insert into trellis_inspection.inspection_item
                (inspection_item_id, tenant_id, inspection_id, label, status, sort_order, created_at, updated_at)
            values (@itemA, @tenantA, @inspectionA, 'Smoke Detectors', 'PENDING', 0, now(), now());

            insert into trellis_inspection.inspection_photo
                (inspection_photo_id, tenant_id, inspection_id, file_name, content_type, file_size_bytes,
                 storage_key, scan_status, captured_at, uploaded_by, created_at)
            values (@photoA, @tenantA, @inspectionA, 'a.jpg', 'image/jpeg', 100, 'key-a', 'CLEAN', now(), @userId, now());

            insert into trellis_vendor.vendor (vendor_id, vendor_name, category, created_at)
            values (@vendorV, 'Vendor V', 'MAINTENANCE', now());

            insert into trellis_vendor.vendor_assignment
                (assignment_id, vendor_id, tenant_id, work_item_type, work_item_id, status, assigned_at)
            values (gen_random_uuid(), @vendorV, @tenantA, 'INSPECTION', @inspectionA, 'ASSIGNED', now());
            """;
        seed.Parameters.AddWithValue("inspectionA", _inspectionAId);
        seed.Parameters.AddWithValue("inspectionB", _inspectionBId);
        seed.Parameters.AddWithValue("itemA", _itemAId);
        seed.Parameters.AddWithValue("photoA", _photoAId);
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
            delete from trellis_inspection.inspection_photo where tenant_id in (@tenantA, @tenantB);
            delete from trellis_inspection.inspection_item where tenant_id in (@tenantA, @tenantB);
            delete from trellis_inspection.inspection where tenant_id in (@tenantA, @tenantB);
            """;
        cleanup.Parameters.AddWithValue("vendorV", VendorV);
        cleanup.Parameters.AddWithValue("vendorW", VendorW);
        cleanup.Parameters.AddWithValue("tenantA", TenantA);
        cleanup.Parameters.AddWithValue("tenantB", TenantB);
        await cleanup.ExecuteNonQueryAsync();
    }

    [Fact]
    [Trait("BDR", "BDR-INSP-001")]
    public async Task VendorContext_SeesOnlyItsAssignedInspection_NotTheUnrelatedTenantsOwn()
    {
        VendorContext.Current = new VendorContext(VendorV);

        await using var db = fixture.CreateAppInspectionContext();
        var results = await db.Inspections.ToListAsync();

        results.Should().ContainSingle(i => i.InspectionId == _inspectionAId);
    }

    [Fact]
    [Trait("BDR", "BDR-INSP-001")]
    public async Task VendorContext_SeesTheAssignedInspectionsItemsAndPhotos()
    {
        VendorContext.Current = new VendorContext(VendorV);

        await using var db = fixture.CreateAppInspectionContext();
        var items = await db.InspectionItems.ToListAsync();
        var photos = await db.InspectionPhotos.ToListAsync();

        items.Should().ContainSingle(i => i.InspectionItemId == _itemAId);
        photos.Should().ContainSingle(p => p.InspectionPhotoId == _photoAId);
    }

    [Fact]
    [Trait("BDR", "BDR-INSP-001")]
    public async Task VendorContext_ForADifferentUnassignedVendor_SeesNothing()
    {
        VendorContext.Current = new VendorContext(VendorW);

        await using var db = fixture.CreateAppInspectionContext();
        (await db.Inspections.ToListAsync()).Should().BeEmpty();
        (await db.InspectionItems.ToListAsync()).Should().BeEmpty();
        (await db.InspectionPhotos.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task TenantContext_StillSeesOnlyItsOwnInspection_UnaffectedByTheVendorPolicy()
    {
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppInspectionContext();
        var results = await db.Inspections.ToListAsync();

        results.Should().ContainSingle(i => i.InspectionId == _inspectionBId);
    }
}
