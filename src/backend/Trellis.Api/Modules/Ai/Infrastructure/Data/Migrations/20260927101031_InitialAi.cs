using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Ai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialAi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trellis_ai");

            migrationBuilder.CreateTable(
                name: "extraction_job",
                schema: "trellis_ai",
                columns: table => new
                {
                    extraction_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    extracted_fields_json = table.Column<string>(type: "text", nullable: true),
                    confidence = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    confirmed_fields_json = table.Column<string>(type: "text", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extraction_job", x => x.extraction_job_id);
                });

            migrationBuilder.CreateTable(
                name: "override_log",
                schema: "trellis_ai",
                columns: table => new
                {
                    override_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    suggestion_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    suggestion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_value_json = table.Column<string>(type: "text", nullable: false),
                    corrected_value_json = table.Column<string>(type: "text", nullable: false),
                    overridden_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_override_log", x => x.override_log_id);
                });

            migrationBuilder.CreateTable(
                name: "routing_suggestion",
                schema: "trellis_ai",
                columns: table => new
                {
                    routing_suggestion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: true),
                    suggested_department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    suggested_vendor_category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    confidence = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    final_department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    workflow_instance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routing_suggestion", x => x.routing_suggestion_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_extraction_job_document_version_id",
                schema: "trellis_ai",
                table: "extraction_job",
                column: "document_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_extraction_job_tenant_id",
                schema: "trellis_ai",
                table: "extraction_job",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_extraction_job_tenant_id_status",
                schema: "trellis_ai",
                table: "extraction_job",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_override_log_suggestion_type_suggestion_id",
                schema: "trellis_ai",
                table: "override_log",
                columns: new[] { "suggestion_type", "suggestion_id" });

            migrationBuilder.CreateIndex(
                name: "IX_override_log_tenant_id",
                schema: "trellis_ai",
                table: "override_log",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_routing_suggestion_tenant_id",
                schema: "trellis_ai",
                table: "routing_suggestion",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_routing_suggestion_tenant_id_status",
                schema: "trellis_ai",
                table: "routing_suggestion",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_routing_suggestion_workflow_instance_id",
                schema: "trellis_ai",
                table: "routing_suggestion",
                column: "workflow_instance_id");

            // Plain tenant_isolation on all three tables — no vendor visibility anywhere in this
            // module (Ai's tables are never Vendor-visible; there is no IVendorAssignmentReader
            // need here). override_log gets INSERT+SELECT only, no UPDATE — immutable, matching
            // audit_record's own append-only shape.
            migrationBuilder.Sql(
                """
                ALTER TABLE trellis_ai.extraction_job ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_ai.extraction_job FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_ai.extraction_job
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_ai.routing_suggestion ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_ai.routing_suggestion FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_ai.routing_suggestion
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_ai.override_log ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_ai.override_log FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_ai.override_log
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                GRANT USAGE ON SCHEMA trellis_ai TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_ai.extraction_job TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_ai.routing_suggestion TO trellis_app;
                GRANT SELECT, INSERT ON trellis_ai.override_log TO trellis_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "extraction_job",
                schema: "trellis_ai");

            migrationBuilder.DropTable(
                name: "override_log",
                schema: "trellis_ai");

            migrationBuilder.DropTable(
                name: "routing_suggestion",
                schema: "trellis_ai");
        }
    }
}
