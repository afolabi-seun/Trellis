using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Document.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorEvidenceToDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "workflow_instance_id",
                schema: "trellis_document",
                table: "document",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_workflow_instance_id",
                schema: "trellis_document",
                table: "document",
                column: "workflow_instance_id");

            // Upgrades document/document_version from plain tenant_isolation to
            // tenant_or_vendor_isolation (data_model.md's shape, same reasoning as Workflow's own
            // AddVendorIsolationToWorkflow migration) — a Vendor uploading evidence
            // (BDR-VEN-003) needs to INSERT/SELECT rows whose tenant_id doesn't match
            // app.current_tenant_id (they have none), only app.current_vendor_id via the
            // work item's vendor_assignment. document_version has no workflow_instance_id of its
            // own, so its EXISTS clause joins back through document. Must run AFTER Vendor's own
            // InitialVendor migration (Postgres validates a policy's expression, including
            // trellis_vendor.vendor_assignment, at CREATE POLICY time) and Workflow's own
            // AddVendorIsolationToWorkflow (no ordering dependency between the two, but both must
            // exist before this one for the same reason).
            migrationBuilder.Sql(
                """
                DROP POLICY tenant_isolation ON trellis_document.document;
                CREATE POLICY tenant_or_vendor_isolation ON trellis_document.document
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                        OR EXISTS (
                            SELECT 1 FROM trellis_vendor.vendor_assignment va
                            WHERE va.vendor_id::text = current_setting('app.current_vendor_id', true)
                              AND va.tenant_id = document.tenant_id
                              AND va.work_item_id = document.workflow_instance_id
                        )
                    );

                DROP POLICY tenant_isolation ON trellis_document.document_version;
                CREATE POLICY tenant_or_vendor_isolation ON trellis_document.document_version
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                        OR EXISTS (
                            SELECT 1 FROM trellis_document.document d
                            JOIN trellis_vendor.vendor_assignment va
                                ON va.work_item_id = d.workflow_instance_id AND va.tenant_id = d.tenant_id
                            WHERE d.document_id = document_version.document_id
                              AND va.vendor_id::text = current_setting('app.current_vendor_id', true)
                        )
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY tenant_or_vendor_isolation ON trellis_document.document;
                CREATE POLICY tenant_isolation ON trellis_document.document
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                DROP POLICY tenant_or_vendor_isolation ON trellis_document.document_version;
                CREATE POLICY tenant_isolation ON trellis_document.document_version
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );
                """);

            migrationBuilder.DropIndex(
                name: "IX_document_workflow_instance_id",
                schema: "trellis_document",
                table: "document");

            migrationBuilder.DropColumn(
                name: "workflow_instance_id",
                schema: "trellis_document",
                table: "document");
        }
    }
}
