using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Document.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trellis_document");

            migrationBuilder.CreateTable(
                name: "document",
                schema: "trellis_document",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unit_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    current_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expires_at = table.Column<DateOnly>(type: "date", nullable: true),
                    last_alerted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document", x => x.document_id);
                });

            migrationBuilder.CreateTable(
                name: "document_version",
                schema: "trellis_document",
                columns: table => new
                {
                    document_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    scan_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_version", x => x.document_version_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_expires_at_last_alerted_at",
                schema: "trellis_document",
                table: "document",
                columns: new[] { "expires_at", "last_alerted_at" });

            migrationBuilder.CreateIndex(
                name: "IX_document_owner_id",
                schema: "trellis_document",
                table: "document",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_property_id",
                schema: "trellis_document",
                table: "document",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_tenant_id",
                schema: "trellis_document",
                table: "document",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_unit_asset_id",
                schema: "trellis_document",
                table: "document",
                column: "unit_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_version_document_id",
                schema: "trellis_document",
                table: "document_version",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_version_tenant_id",
                schema: "trellis_document",
                table: "document_version",
                column: "tenant_id");

            // data_model.md's standard tenant_isolation policy on both tables — plain policy
            // only, not the tenant_or_vendor_isolation variant (Vendor isn't built until Phase
            // 4). No DELETE grant anywhere — Archive()/status transitions only, never a hard row
            // delete; a scanned-infected DocumentVersion is kept (quarantined), never deleted.
            migrationBuilder.Sql(
                """
                ALTER TABLE trellis_document.document ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_document.document FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_document.document
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_document.document_version ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_document.document_version FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_document.document_version
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                GRANT USAGE ON SCHEMA trellis_document TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_document.document TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_document.document_version TO trellis_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document",
                schema: "trellis_document");

            migrationBuilder.DropTable(
                name: "document_version",
                schema: "trellis_document");
        }
    }
}
