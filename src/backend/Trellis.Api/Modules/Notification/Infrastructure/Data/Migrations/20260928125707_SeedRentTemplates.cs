using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Notification.Infrastructure.Data.Migrations
{
    /// <summary>
    /// notifications_design.md names RENT_ARREARS_FLAGGED (entity type RENT_SCHEDULE) but nothing
    /// seeds it — and NotificationService.SendAsync silently no-ops when no template resolves, so
    /// without this insert every arrears alert would reach SendAsync and do nothing (the same gap
    /// SeedLeasingTemplates/SeedOwnerReportTemplate closed). Worded per BDR-RENT-006: a statement
    /// about payments RECORDED in Trellis, never about money Trellis holds or handles.
    /// </summary>
    public partial class SeedRentTemplates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO trellis_notification.notification_template
                    (template_id, tenant_id, template_key, channel, subject_template, body_template, locale)
                VALUES
                    (gen_random_uuid(), NULL, 'RENT_ARREARS_FLAGGED', 'EMAIL',
                     'Rent in arrears: {{UnitLabel}}',
                     'No payment has been recorded as received for {{UnitLabel}} covering {{PeriodsInArrears}} rent period(s) past their grace period (oldest unpaid due date {{OldestUnpaidDueDate}}; rent {{RentAmount}} {{Currency}} per period; {{AmountShort}} {{Currency}} not yet recorded as received). This reflects payments recorded in Trellis, which records payments made outside the platform and does not process or hold funds.',
                     'en');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM trellis_notification.notification_template
                WHERE tenant_id IS NULL AND template_key = 'RENT_ARREARS_FLAGGED';
                """);
        }
    }
}
