using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Inspection.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialInspection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trellis_inspection");

            migrationBuilder.CreateTable(
                name: "inspection",
                schema: "trellis_inspection",
                columns: table => new
                {
                    inspection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unit_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_to_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scheduled_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inspection", x => x.inspection_id);
                });

            migrationBuilder.CreateTable(
                name: "inspection_item",
                schema: "trellis_inspection",
                columns: table => new
                {
                    inspection_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inspection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inspection_item", x => x.inspection_item_id);
                });

            migrationBuilder.CreateTable(
                name: "inspection_photo",
                schema: "trellis_inspection",
                columns: table => new
                {
                    inspection_photo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inspection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inspection_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    scan_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inspection_photo", x => x.inspection_photo_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inspection_property_id",
                schema: "trellis_inspection",
                table: "inspection",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "IX_inspection_tenant_id",
                schema: "trellis_inspection",
                table: "inspection",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_inspection_unit_asset_id",
                schema: "trellis_inspection",
                table: "inspection",
                column: "unit_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_inspection_item_inspection_id",
                schema: "trellis_inspection",
                table: "inspection_item",
                column: "inspection_id");

            migrationBuilder.CreateIndex(
                name: "IX_inspection_item_tenant_id",
                schema: "trellis_inspection",
                table: "inspection_item",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_inspection_photo_inspection_id",
                schema: "trellis_inspection",
                table: "inspection_photo",
                column: "inspection_id");

            migrationBuilder.CreateIndex(
                name: "IX_inspection_photo_inspection_item_id",
                schema: "trellis_inspection",
                table: "inspection_photo",
                column: "inspection_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_inspection_photo_tenant_id",
                schema: "trellis_inspection",
                table: "inspection_photo",
                column: "tenant_id");

            // Inspection migrates after Vendor (data_model.md's dependency order), so
            // tenant_or_vendor_isolation can be written directly here, no follow-up migration
            // needed — the same shape InitialVendor.cs used for its own vendor_assignment table.
            //
            // inspection IS the work item (identical shape to workflow_instance): a plain
            // tenant_id match OR an EXISTS against vendor_assignment keyed on this table's own PK.
            //
            // inspection_item/inspection_photo are children that already carry inspection_id as a
            // direct FK to the row that IS the work item (exactly workflow_step_instance's
            // situation, not document_version's, which has to join through a parent) — so both get
            // the same JOIN-free shape, denormalized tenant_id + an EXISTS keyed on their own
            // inspection_id column directly.
            migrationBuilder.Sql(
                """
                ALTER TABLE trellis_inspection.inspection ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_inspection.inspection FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_or_vendor_isolation ON trellis_inspection.inspection
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                        OR EXISTS (
                            SELECT 1 FROM trellis_vendor.vendor_assignment va
                            WHERE va.vendor_id::text = current_setting('app.current_vendor_id', true)
                              AND va.tenant_id = inspection.tenant_id
                              AND va.work_item_id = inspection.inspection_id
                        )
                    );

                ALTER TABLE trellis_inspection.inspection_item ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_inspection.inspection_item FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_or_vendor_isolation ON trellis_inspection.inspection_item
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                        OR EXISTS (
                            SELECT 1 FROM trellis_vendor.vendor_assignment va
                            WHERE va.vendor_id::text = current_setting('app.current_vendor_id', true)
                              AND va.tenant_id = inspection_item.tenant_id
                              AND va.work_item_id = inspection_item.inspection_id
                        )
                    );

                ALTER TABLE trellis_inspection.inspection_photo ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_inspection.inspection_photo FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_or_vendor_isolation ON trellis_inspection.inspection_photo
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                        OR EXISTS (
                            SELECT 1 FROM trellis_vendor.vendor_assignment va
                            WHERE va.vendor_id::text = current_setting('app.current_vendor_id', true)
                              AND va.tenant_id = inspection_photo.tenant_id
                              AND va.work_item_id = inspection_photo.inspection_id
                        )
                    );

                GRANT USAGE ON SCHEMA trellis_inspection TO trellis_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON trellis_inspection.inspection_item TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_inspection.inspection TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_inspection.inspection_photo TO trellis_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inspection",
                schema: "trellis_inspection");

            migrationBuilder.DropTable(
                name: "inspection_item",
                schema: "trellis_inspection");

            migrationBuilder.DropTable(
                name: "inspection_photo",
                schema: "trellis_inspection");
        }
    }
}
