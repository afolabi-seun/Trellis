using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.PropertyAsset.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPropertyAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trellis_property");

            migrationBuilder.CreateTable(
                name: "owner",
                schema: "trellis_property",
                columns: table => new
                {
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_owner", x => x.owner_id);
                });

            migrationBuilder.CreateTable(
                name: "property",
                schema: "trellis_property",
                columns: table => new
                {
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    address = table.Column<string>(type: "text", nullable: false),
                    property_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    size_sqft = table.Column<decimal>(type: "numeric", nullable: true),
                    acquisition_date = table.Column<DateOnly>(type: "date", nullable: true),
                    acquisition_value = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    current_valuation = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property", x => x.property_id);
                });

            migrationBuilder.CreateTable(
                name: "unit_asset",
                schema: "trellis_property",
                columns: table => new
                {
                    unit_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    size_sqft = table.Column<decimal>(type: "numeric", nullable: true),
                    acquisition_value = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unit_asset", x => x.unit_asset_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_owner_tenant_id",
                schema: "trellis_property",
                table: "owner",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_property_owner_id",
                schema: "trellis_property",
                table: "property",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_property_tenant_id",
                schema: "trellis_property",
                table: "property",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_unit_asset_property_id",
                schema: "trellis_property",
                table: "unit_asset",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "IX_unit_asset_tenant_id",
                schema: "trellis_property",
                table: "unit_asset",
                column: "tenant_id");

            // data_model.md's standard tenant_isolation policy — owner, property, and unit_asset
            // are all genuinely tenant-owned rows, none of them vendor-visible
            // (multi_tenant_design.md's "What a Vendor Never Gets": a Vendor can't list/search/
            // browse a tenant's properties or owners at all). No DELETE grant anywhere — Archive()
            // is always a status transition, never a hard row delete (BDR-AST-003: documents/
            // inspections/workflow history attach at the property or unit_asset level).
            migrationBuilder.Sql(
                """
                ALTER TABLE trellis_property.owner ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_property.owner FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_property.owner
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_property.property ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_property.property FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_property.property
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_property.unit_asset ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_property.unit_asset FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_property.unit_asset
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                GRANT USAGE ON SCHEMA trellis_property TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_property.owner TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_property.property TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_property.unit_asset TO trellis_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "owner",
                schema: "trellis_property");

            migrationBuilder.DropTable(
                name: "property",
                schema: "trellis_property");

            migrationBuilder.DropTable(
                name: "unit_asset",
                schema: "trellis_property");
        }
    }
}
