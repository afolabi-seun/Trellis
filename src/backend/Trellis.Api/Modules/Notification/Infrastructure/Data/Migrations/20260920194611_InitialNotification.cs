using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Notification.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class InitialNotification : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "trellis_notification");

        migrationBuilder.CreateTable(
            name: "notification_delivery",
            schema: "trellis_notification",
            columns: table => new
            {
                delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                recipient_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                recipient_address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                template_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                channel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "QUEUED"),
                provider_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                related_entity_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                related_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notification_delivery", x => x.delivery_id);
            });

        migrationBuilder.CreateTable(
            name: "notification_template",
            schema: "trellis_notification",
            columns: table => new
            {
                template_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                template_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                channel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                subject_template = table.Column<string>(type: "text", nullable: true),
                body_template = table.Column<string>(type: "text", nullable: false),
                locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "en"),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notification_template", x => x.template_id);
            });

        migrationBuilder.CreateTable(
            name: "tenant_notification_channel_pref",
            schema: "trellis_notification",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                template_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                channel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tenant_notification_channel_pref", x => new { x.tenant_id, x.template_key, x.channel });
            });

        migrationBuilder.CreateIndex(
            name: "IX_notification_delivery_status",
            schema: "trellis_notification",
            table: "notification_delivery",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "IX_notification_template_tenant_id_template_key_channel_locale",
            schema: "trellis_notification",
            table: "notification_template",
            columns: new[] { "tenant_id", "template_key", "channel", "locale" },
            unique: true);

        migrationBuilder.Sql(
            """
            -- data_model.md's standard tenant_isolation policy — delivery and channel prefs
            -- are genuinely tenant-owned rows.
            ALTER TABLE trellis_notification.notification_delivery ENABLE ROW LEVEL SECURITY;
            ALTER TABLE trellis_notification.notification_delivery FORCE ROW LEVEL SECURITY;
            CREATE POLICY tenant_isolation ON trellis_notification.notification_delivery
                USING (
                    current_setting('app.is_system', true) = 'true'
                    OR tenant_id::text = current_setting('app.current_tenant_id', true)
                );

            ALTER TABLE trellis_notification.tenant_notification_channel_pref ENABLE ROW LEVEL SECURITY;
            ALTER TABLE trellis_notification.tenant_notification_channel_pref FORCE ROW LEVEL SECURITY;
            CREATE POLICY tenant_isolation ON trellis_notification.tenant_notification_channel_pref
                USING (
                    current_setting('app.is_system', true) = 'true'
                    OR tenant_id::text = current_setting('app.current_tenant_id', true)
                );

            -- notifications_design.md's tenant_or_platform_default policy — every tenant can
            -- also read the platform-default rows (tenant_id IS NULL) it falls back to.
            ALTER TABLE trellis_notification.notification_template ENABLE ROW LEVEL SECURITY;
            ALTER TABLE trellis_notification.notification_template FORCE ROW LEVEL SECURITY;
            CREATE POLICY tenant_or_platform_default ON trellis_notification.notification_template
                USING (
                    current_setting('app.is_system', true) = 'true'
                    OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    OR tenant_id IS NULL
                );

            GRANT USAGE ON SCHEMA trellis_notification TO trellis_app;
            GRANT SELECT, INSERT, UPDATE ON trellis_notification.notification_delivery TO trellis_app;
            GRANT SELECT, INSERT ON trellis_notification.notification_template TO trellis_app;
            GRANT SELECT, INSERT, UPDATE ON trellis_notification.tenant_notification_channel_pref TO trellis_app;

            -- Platform-default templates (tenant_id NULL) for exactly what Phase 1 sends:
            -- Auth's password reset and MFA recovery-code flows. BDR-NOTIF-002's per-tenant
            -- override capability applies on top of these later; nothing overrides them yet.
            INSERT INTO trellis_notification.notification_template
                (template_id, tenant_id, template_key, channel, subject_template, body_template, locale)
            VALUES
                (gen_random_uuid(), NULL, 'PASSWORD_RESET', 'EMAIL',
                 'Reset your Trellis password',
                 'Use this link to reset your password: {{resetLink}}. This link expires in 30 minutes.',
                 'en'),
                (gen_random_uuid(), NULL, 'MFA_RECOVERY_CODE_USED', 'EMAIL',
                 'A recovery code was used on your Trellis account',
                 'A multi-factor recovery code was just used to sign in to your account. If this wasn''t you, reset your password immediately.',
                 'en'),
                (gen_random_uuid(), NULL, 'MFA_RECOVERY_CODES_REGENERATED', 'EMAIL',
                 'Your Trellis recovery codes were regenerated',
                 'Your multi-factor recovery codes were just regenerated. All previous codes are now invalid. If this wasn''t you, reset your password immediately.',
                 'en');
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "notification_delivery",
            schema: "trellis_notification");

        migrationBuilder.DropTable(
            name: "notification_template",
            schema: "trellis_notification");

        migrationBuilder.DropTable(
            name: "tenant_notification_channel_pref",
            schema: "trellis_notification");
    }
}
