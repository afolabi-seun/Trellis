using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Reporting.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Creates the two tables genuinely new to this module — owner_report, report_share_link —
    /// plus their RLS policies, exact InitialBilling.cs shape. Deliberately does NOT create
    /// audit_record: that table already exists in trellis_audit, created by Core's own original
    /// InitialCoreAuditRecord migration (RLS, quarterly partitioning, append-only trigger all
    /// already in place there, untouched by this migration). EF's scaffolder generated a
    /// CreateTable/DropTable pair for audit_record too, since it's newly part of
    /// ReportingDbContext's own model — that pair (and the trellis_audit EnsureSchema call, a
    /// schema Reporting doesn't own) was deleted here; see Core's own
    /// TransferAuditRecordOwnershipToReporting migration for the other half of this schema
    /// transfer.
    /// </summary>
    public partial class InitialReporting : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trellis_reporting");

            migrationBuilder.CreateTable(
                name: "owner_report",
                schema: "trellis_reporting",
                columns: table => new
                {
                    owner_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    period_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    property_count = table.Column<int>(type: "integer", nullable: false),
                    total_maintenance_cost = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    open_maintenance_count = table.Column<int>(type: "integer", nullable: false),
                    compliant_document_count = table.Column<int>(type: "integer", nullable: false),
                    expiring_document_count = table.Column<int>(type: "integer", nullable: false),
                    pdf_content = table.Column<byte[]>(type: "bytea", nullable: true),
                    generated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ready_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_owner_report", x => x.owner_report_id);
                });

            migrationBuilder.CreateTable(
                name: "report_share_link",
                schema: "trellis_reporting",
                columns: table => new
                {
                    report_share_link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_accessed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_share_link", x => x.report_share_link_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_owner_report_tenant_id_created_at",
                schema: "trellis_reporting",
                table: "owner_report",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_owner_report_tenant_id_owner_id",
                schema: "trellis_reporting",
                table: "owner_report",
                columns: new[] { "tenant_id", "owner_id" });

            migrationBuilder.CreateIndex(
                name: "IX_report_share_link_tenant_id",
                schema: "trellis_reporting",
                table: "report_share_link",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_report_share_link_token_hash",
                schema: "trellis_reporting",
                table: "report_share_link",
                column: "token_hash",
                unique: true);

            // Standard tenant_isolation on both tables — report_share_link genuinely belongs to a
            // tenant (unlike webhook_event's system_only shape), so no exception here.
            migrationBuilder.Sql(
                """
                ALTER TABLE trellis_reporting.owner_report ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_reporting.owner_report FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_reporting.owner_report
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_reporting.report_share_link ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_reporting.report_share_link FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_reporting.report_share_link
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                GRANT USAGE ON SCHEMA trellis_reporting TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_reporting.owner_report TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_reporting.report_share_link TO trellis_app;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "owner_report",
                schema: "trellis_reporting");

            migrationBuilder.DropTable(
                name: "report_share_link",
                schema: "trellis_reporting");
        }
    }
}
