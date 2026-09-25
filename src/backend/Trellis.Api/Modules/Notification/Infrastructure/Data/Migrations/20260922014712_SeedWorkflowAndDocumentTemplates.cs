using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Notification.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedWorkflowAndDocumentTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Platform-default templates (tenant_id NULL) for Phase 3's two new senders —
            // WorkflowInstanceService.NotifyActiveStepDepartmentAsync and
            // DocumentExpiryAlertBackgroundService. Same reasoning and shape as InitialNotification's
            // own seed for Phase 1's PASSWORD_RESET/MFA templates: NotificationService.SendAsync
            // silently no-ops when no template resolves (a configuration problem to log, never an
            // exception that could fail the caller's own operation), so without this insert both
            // sends were reaching SendAsync and doing nothing — found via this phase's own live
            // smoke test, not a unit test, since nothing exercises SendAsync's template-miss path
            // end-to-end otherwise.
            migrationBuilder.Sql(
                """
                INSERT INTO trellis_notification.notification_template
                    (template_id, tenant_id, template_key, channel, subject_template, body_template, locale)
                VALUES
                    (gen_random_uuid(), NULL, 'WORKFLOW_STEP_AWAITING_DECISION', 'EMAIL',
                     'A workflow step needs your decision',
                     'A workflow step ("{{StepName}}") on instance {{WorkflowInstanceId}} is awaiting your approval or rejection.',
                     'en'),
                    (gen_random_uuid(), NULL, 'DOCUMENT_EXPIRING_SOON', 'EMAIL',
                     'A document you uploaded is expiring soon',
                     'Your uploaded document {{DocumentId}} expires on {{ExpiresAt}}. Please review and renew it if needed.',
                     'en');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM trellis_notification.notification_template
                WHERE tenant_id IS NULL AND template_key IN ('WORKFLOW_STEP_AWAITING_DECISION', 'DOCUMENT_EXPIRING_SOON');
                """);
        }
    }
}
