using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Vendor.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialVendor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trellis_vendor");

            migrationBuilder.CreateTable(
                name: "tenant_vendor_relationship",
                schema: "trellis_vendor",
                columns: table => new
                {
                    tenant_vendor_relationship_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vendor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    terminated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    terminated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_vendor_relationship", x => x.tenant_vendor_relationship_id);
                });

            migrationBuilder.CreateTable(
                name: "vendor",
                schema: "trellis_vendor",
                columns: table => new
                {
                    vendor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vendor_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendor", x => x.vendor_id);
                });

            migrationBuilder.CreateTable(
                name: "vendor_assignment",
                schema: "trellis_vendor",
                columns: table => new
                {
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vendor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_item_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ASSIGNED"),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendor_assignment", x => x.assignment_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_vendor_relationship_tenant_id",
                schema: "trellis_vendor",
                table: "tenant_vendor_relationship",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_vendor_relationship_tenant_id_vendor_id",
                schema: "trellis_vendor",
                table: "tenant_vendor_relationship",
                columns: new[] { "tenant_id", "vendor_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vendor_assignment_tenant_id",
                schema: "trellis_vendor",
                table: "vendor_assignment",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_assignment_vendor_id",
                schema: "trellis_vendor",
                table: "vendor_assignment",
                column: "vendor_id");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_assignment_work_item_type_work_item_id",
                schema: "trellis_vendor",
                table: "vendor_assignment",
                columns: new[] { "work_item_type", "work_item_id" });

            // Three distinct policy shapes (Phase 4 plan's decision 5), not one pattern reused
            // three times:
            //  - vendor: no RLS at all — it has no tenant_id, is the aggregate root everything
            //    else scopes *around*, same GRANT-only precedent as trellis_tenant.tenant's own
            //    migration. Access control is RBAC ([Authorize(Roles="TENANT_ADMIN")]) at the
            //    controller, not RLS.
            //  - vendor_assignment: a plain three-way OR, no subquery — it carries both tenant_id
            //    and vendor_id directly, unlike workflow_instance/document which only reach this
            //    table via an EXISTS join.
            //  - tenant_vendor_relationship: plain tenant_isolation — a Vendor never browses its
            //    own relationships, only a Tenant Admin manages them.
            migrationBuilder.Sql(
                """
                ALTER TABLE trellis_vendor.vendor_assignment ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_vendor.vendor_assignment FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_or_vendor_isolation ON trellis_vendor.vendor_assignment
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                        OR vendor_id::text = current_setting('app.current_vendor_id', true)
                    );

                ALTER TABLE trellis_vendor.tenant_vendor_relationship ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_vendor.tenant_vendor_relationship FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_vendor.tenant_vendor_relationship
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                GRANT USAGE ON SCHEMA trellis_vendor TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_vendor.vendor TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_vendor.vendor_assignment TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_vendor.tenant_vendor_relationship TO trellis_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_vendor_relationship",
                schema: "trellis_vendor");

            migrationBuilder.DropTable(
                name: "vendor",
                schema: "trellis_vendor");

            migrationBuilder.DropTable(
                name: "vendor_assignment",
                schema: "trellis_vendor");
        }
    }
}
