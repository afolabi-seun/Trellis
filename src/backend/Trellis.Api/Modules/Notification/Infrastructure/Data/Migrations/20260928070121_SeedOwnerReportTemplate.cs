using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Notification.Infrastructure.Data.Migrations
{
    /// <summary>
    /// notifications_design.md claims an OWNER_REPORT_READY template already exists — confirmed,
    /// via direct read of every migration in this module, that it does not: no seed migration for
    /// it existed anywhere in code before this one, despite the doc's claim. Same reasoning and
    /// shape as SeedWorkflowAndDocumentTemplates's own doc comment: NotificationService.SendAsync
    /// silently no-ops when no template resolves, so without this insert OwnerReportService.
    /// DeliverByEmailAsync's own send was reaching SendAsync and doing nothing.
    /// </summary>
    public partial class SeedOwnerReportTemplate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO trellis_notification.notification_template
                    (template_id, tenant_id, template_key, channel, subject_template, body_template, locale)
                VALUES
                    (gen_random_uuid(), NULL, 'OWNER_REPORT_READY', 'EMAIL',
                     'Your property report is ready',
                     'Hi {{OwnerName}}, your report for {{PeriodStart}} to {{PeriodEnd}} is ready. It is attached, or can be requested from your property manager.',
                     'en');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM trellis_notification.notification_template
                WHERE tenant_id IS NULL AND template_key = 'OWNER_REPORT_READY';
                """);
        }
    }
}
