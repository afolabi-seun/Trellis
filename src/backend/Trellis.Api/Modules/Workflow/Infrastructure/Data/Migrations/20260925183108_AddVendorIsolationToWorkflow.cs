using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Workflow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorIsolationToWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Upgrades workflow_instance/workflow_step_instance from the plain tenant_isolation
            // policy (InitialWorkflow's own comment named this exact upgrade point as Phase 4
            // scope) to tenant_or_vendor_isolation (data_model.md's literal SQL) — a Vendor
            // session's connection carries app.current_vendor_id, never app.current_tenant_id, so
            // without this a Vendor could never see their own assigned instance at all. Must run
            // AFTER Vendor's own InitialVendor migration (Postgres validates a policy's
            // expression, including trellis_vendor.vendor_assignment, at CREATE POLICY time).
            //
            // RLS is per-table, not inherited through a join/Include, so workflow_step_instance
            // needs its own EXISTS clause keyed on its own workflow_instance_id column, not a
            // shared/derived check off workflow_instance's policy.
            migrationBuilder.Sql(
                """
                DROP POLICY tenant_isolation ON trellis_workflow.workflow_instance;
                CREATE POLICY tenant_or_vendor_isolation ON trellis_workflow.workflow_instance
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                        OR EXISTS (
                            SELECT 1 FROM trellis_vendor.vendor_assignment va
                            WHERE va.vendor_id::text = current_setting('app.current_vendor_id', true)
                              AND va.tenant_id = workflow_instance.tenant_id
                              AND va.work_item_id = workflow_instance.workflow_instance_id
                        )
                    );

                DROP POLICY tenant_isolation ON trellis_workflow.workflow_step_instance;
                CREATE POLICY tenant_or_vendor_isolation ON trellis_workflow.workflow_step_instance
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                        OR EXISTS (
                            SELECT 1 FROM trellis_vendor.vendor_assignment va
                            WHERE va.vendor_id::text = current_setting('app.current_vendor_id', true)
                              AND va.tenant_id = workflow_step_instance.tenant_id
                              AND va.work_item_id = workflow_step_instance.workflow_instance_id
                        )
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY tenant_or_vendor_isolation ON trellis_workflow.workflow_instance;
                CREATE POLICY tenant_isolation ON trellis_workflow.workflow_instance
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                DROP POLICY tenant_or_vendor_isolation ON trellis_workflow.workflow_step_instance;
                CREATE POLICY tenant_isolation ON trellis_workflow.workflow_step_instance
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );
                """);
        }
    }
}
