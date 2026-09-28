using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Notification.Infrastructure.Data.Migrations
{
    /// <summary>
    /// notifications_design.md anticipates APPLICATION_LINK and SCREENING_COMPLETE, but neither is
    /// seeded anywhere in real code (confirmed by reading every migration in this module) — the same
    /// doc-claims-a-template-exists gap SeedOwnerReportTemplate closed for Phase 8.
    /// NotificationService.SendAsync silently no-ops when no template resolves, so without this
    /// insert every Leasing send would reach SendAsync and do nothing. APPLICATION_SUBMITTED is not
    /// in the design doc at all — added for staff-notify symmetry with WORKFLOW_STEP_AWAITING_DECISION.
    /// </summary>
    public partial class SeedLeasingTemplates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO trellis_notification.notification_template
                    (template_id, tenant_id, template_key, channel, subject_template, body_template, locale)
                VALUES
                    (gen_random_uuid(), NULL, 'APPLICATION_LINK', 'EMAIL',
                     'Complete your rental application',
                     'Hi, use the link below within {{ExpiryDays}} days to complete your application for {{UnitLabel}}: {{ApplicationLinkUrl}}',
                     'en'),
                    (gen_random_uuid(), NULL, 'SCREENING_COMPLETE', 'EMAIL',
                     'Screening complete for your application',
                     'Hi {{ApplicantFullName}}, screening has completed for your rental application. Our team will be in touch.',
                     'en'),
                    (gen_random_uuid(), NULL, 'APPLICATION_SUBMITTED', 'EMAIL',
                     'New rental application submitted',
                     'A new rental application was submitted by {{ApplicantFullName}} for {{UnitLabel}}. Review it in the Applications queue.',
                     'en');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM trellis_notification.notification_template
                WHERE tenant_id IS NULL AND template_key IN ('APPLICATION_LINK', 'SCREENING_COMPLETE', 'APPLICATION_SUBMITTED');
                """);
        }
    }
}
