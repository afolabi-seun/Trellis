using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Workflow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trellis_workflow");

            migrationBuilder.CreateTable(
                name: "department",
                schema: "trellis_workflow",
                columns: table => new
                {
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_department", x => x.department_id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_instance",
                schema: "trellis_workflow",
                columns: table => new
                {
                    workflow_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requesting_department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cost_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    current_step_order = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_instance", x => x.workflow_instance_id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_template",
                schema: "trellis_workflow",
                columns: table => new
                {
                    workflow_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    request_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_template", x => x.workflow_template_id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_step_instance",
                schema: "trellis_workflow",
                columns: table => new
                {
                    workflow_step_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_step_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_order = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    routed_department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_step_instance", x => x.workflow_step_instance_id);
                    table.ForeignKey(
                        name: "FK_workflow_step_instance_workflow_instance_workflow_instance_~",
                        column: x => x.workflow_instance_id,
                        principalSchema: "trellis_workflow",
                        principalTable: "workflow_instance",
                        principalColumn: "workflow_instance_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_step_template",
                schema: "trellis_workflow",
                columns: table => new
                {
                    workflow_step_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_order = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    default_department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_step_template", x => x.workflow_step_template_id);
                    table.ForeignKey(
                        name: "FK_workflow_step_template_workflow_template_workflow_template_~",
                        column: x => x.workflow_template_id,
                        principalSchema: "trellis_workflow",
                        principalTable: "workflow_template",
                        principalColumn: "workflow_template_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_routing_rule",
                schema: "trellis_workflow",
                columns: table => new
                {
                    workflow_routing_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_step_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_order = table.Column<int>(type: "integer", nullable: false),
                    criterion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    @operator = table.Column<string>(name: "operator", type: "character varying(30)", maxLength: 30, nullable: false),
                    comparison_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    target_department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_routing_rule", x => x.workflow_routing_rule_id);
                    table.ForeignKey(
                        name: "FK_workflow_routing_rule_workflow_step_template_workflow_step_~",
                        column: x => x.workflow_step_template_id,
                        principalSchema: "trellis_workflow",
                        principalTable: "workflow_step_template",
                        principalColumn: "workflow_step_template_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_department_tenant_id",
                schema: "trellis_workflow",
                table: "department",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_instance_tenant_id",
                schema: "trellis_workflow",
                table: "workflow_instance",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_instance_tenant_id_status",
                schema: "trellis_workflow",
                table: "workflow_instance",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_instance_workflow_template_id",
                schema: "trellis_workflow",
                table: "workflow_instance",
                column: "workflow_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_routing_rule_tenant_id",
                schema: "trellis_workflow",
                table: "workflow_routing_rule",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_routing_rule_workflow_step_template_id",
                schema: "trellis_workflow",
                table: "workflow_routing_rule",
                column: "workflow_step_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_step_instance_tenant_id",
                schema: "trellis_workflow",
                table: "workflow_step_instance",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_step_instance_workflow_instance_id",
                schema: "trellis_workflow",
                table: "workflow_step_instance",
                column: "workflow_instance_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_step_template_tenant_id",
                schema: "trellis_workflow",
                table: "workflow_step_template",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_step_template_workflow_template_id",
                schema: "trellis_workflow",
                table: "workflow_step_template",
                column: "workflow_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_template_tenant_id",
                schema: "trellis_workflow",
                table: "workflow_template",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_template_tenant_id_request_type",
                schema: "trellis_workflow",
                table: "workflow_template",
                columns: new[] { "tenant_id", "request_type" });

            // data_model.md's standard tenant_isolation policy on all six tables — plain policy
            // only, not the tenant_or_vendor_isolation variant (roadmap.md scopes Vendor
            // visibility into Workflow to Phase 4, not built here). No DELETE grant anywhere —
            // Archive()/status transitions only, never a hard row delete.
            migrationBuilder.Sql(
                """
                ALTER TABLE trellis_workflow.department ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_workflow.department FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_workflow.department
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_workflow.workflow_template ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_workflow.workflow_template FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_workflow.workflow_template
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_workflow.workflow_step_template ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_workflow.workflow_step_template FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_workflow.workflow_step_template
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_workflow.workflow_routing_rule ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_workflow.workflow_routing_rule FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_workflow.workflow_routing_rule
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_workflow.workflow_instance ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_workflow.workflow_instance FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_workflow.workflow_instance
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_workflow.workflow_step_instance ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_workflow.workflow_step_instance FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_workflow.workflow_step_instance
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                GRANT USAGE ON SCHEMA trellis_workflow TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_workflow.department TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_workflow.workflow_template TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_workflow.workflow_step_template TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_workflow.workflow_routing_rule TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_workflow.workflow_instance TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_workflow.workflow_step_instance TO trellis_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "department",
                schema: "trellis_workflow");

            migrationBuilder.DropTable(
                name: "workflow_routing_rule",
                schema: "trellis_workflow");

            migrationBuilder.DropTable(
                name: "workflow_step_instance",
                schema: "trellis_workflow");

            migrationBuilder.DropTable(
                name: "workflow_step_template",
                schema: "trellis_workflow");

            migrationBuilder.DropTable(
                name: "workflow_instance",
                schema: "trellis_workflow");

            migrationBuilder.DropTable(
                name: "workflow_template",
                schema: "trellis_workflow");
        }
    }
}
