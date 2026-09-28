using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Billing.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trellis_billing");

            migrationBuilder.CreateTable(
                name: "invoice",
                schema: "trellis_billing",
                columns: table => new
                {
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    billing_period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    billing_period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    subtotal_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice", x => x.invoice_id);
                });

            migrationBuilder.CreateTable(
                name: "invoice_line_item",
                schema: "trellis_billing",
                columns: table => new
                {
                    line_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    unit_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_line_item", x => x.line_item_id);
                });

            migrationBuilder.CreateTable(
                name: "module_entitlement",
                schema: "trellis_billing",
                columns: table => new
                {
                    entitlement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_key = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    monthly_price = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    enabled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    disabled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_module_entitlement", x => x.entitlement_id);
                });

            migrationBuilder.CreateTable(
                name: "payment_method",
                schema: "trellis_billing",
                columns: table => new
                {
                    payment_method_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    provider_authorization_code = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    card_last4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    card_brand = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_method", x => x.payment_method_id);
                });

            migrationBuilder.CreateTable(
                name: "payment_transaction",
                schema: "trellis_billing",
                columns: table => new
                {
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    provider_response_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transaction", x => x.transaction_id);
                });

            migrationBuilder.CreateTable(
                name: "subscription",
                schema: "trellis_billing",
                columns: table => new
                {
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    billing_cycle_anchor_day = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription", x => x.subscription_id);
                });

            migrationBuilder.CreateTable(
                name: "usage_snapshot",
                schema: "trellis_billing",
                columns: table => new
                {
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    billing_period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    billing_period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    active_unit_count = table.Column<int>(type: "integer", nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_snapshot", x => x.snapshot_id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_event",
                schema: "trellis_billing",
                columns: table => new
                {
                    webhook_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_event_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_event", x => x.webhook_event_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_tenant_id_billing_period_start",
                schema: "trellis_billing",
                table: "invoice",
                columns: new[] { "tenant_id", "billing_period_start" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_tenant_id_status",
                schema: "trellis_billing",
                table: "invoice",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_line_item_invoice_id",
                schema: "trellis_billing",
                table: "invoice_line_item",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_line_item_tenant_id",
                schema: "trellis_billing",
                table: "invoice_line_item",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_module_entitlement_tenant_id_module_key",
                schema: "trellis_billing",
                table: "module_entitlement",
                columns: new[] { "tenant_id", "module_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_method_tenant_id",
                schema: "trellis_billing",
                table: "payment_method",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_method_tenant_id_is_default",
                schema: "trellis_billing",
                table: "payment_method",
                columns: new[] { "tenant_id", "is_default" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_transaction_invoice_id",
                schema: "trellis_billing",
                table: "payment_transaction",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transaction_tenant_id",
                schema: "trellis_billing",
                table: "payment_transaction",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_status",
                schema: "trellis_billing",
                table: "subscription",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_tenant_id",
                schema: "trellis_billing",
                table: "subscription",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usage_snapshot_tenant_id",
                schema: "trellis_billing",
                table: "usage_snapshot",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_usage_snapshot_tenant_id_captured_at",
                schema: "trellis_billing",
                table: "usage_snapshot",
                columns: new[] { "tenant_id", "captured_at" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_event_provider_event_id",
                schema: "trellis_billing",
                table: "webhook_event",
                column: "provider_event_id",
                unique: true);

            // Plain tenant_isolation on every table except webhook_event, which has no tenant_id
            // at all (see WebhookEvent's own doc comment) and instead gets a stricter "system_only"
            // policy — nobody legitimately queries it scoped to their own tenant, only the webhook
            // controller and the background jobs, both under SystemContext.Enter(). No vendor
            // visibility clause anywhere in this schema: a Vendor has no billing relationship with
            // any tenant.
            migrationBuilder.Sql(
                """
                ALTER TABLE trellis_billing.subscription ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_billing.subscription FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_billing.subscription
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_billing.payment_method ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_billing.payment_method FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_billing.payment_method
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_billing.usage_snapshot ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_billing.usage_snapshot FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_billing.usage_snapshot
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_billing.invoice ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_billing.invoice FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_billing.invoice
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_billing.invoice_line_item ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_billing.invoice_line_item FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_billing.invoice_line_item
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_billing.payment_transaction ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_billing.payment_transaction FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_billing.payment_transaction
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_billing.module_entitlement ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_billing.module_entitlement FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_billing.module_entitlement
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_billing.webhook_event ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_billing.webhook_event FORCE ROW LEVEL SECURITY;
                CREATE POLICY system_only ON trellis_billing.webhook_event
                    USING (current_setting('app.is_system', true) = 'true');

                GRANT USAGE ON SCHEMA trellis_billing TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_billing.subscription TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_billing.payment_method TO trellis_app;
                GRANT SELECT, INSERT ON trellis_billing.usage_snapshot TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_billing.invoice TO trellis_app;
                GRANT SELECT, INSERT ON trellis_billing.invoice_line_item TO trellis_app;
                GRANT SELECT, INSERT ON trellis_billing.payment_transaction TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_billing.module_entitlement TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_billing.webhook_event TO trellis_app;
                """);

            // BDR-BIL-009's retrofit: InspectionsController and Ai's two controllers now carry
            // [RequiresModuleEntitlement(...)]. Seeding every pre-existing tenant ACTIVE at $0/mo
            // for both modules makes this purely structural — nothing changes financially or
            // functionally for any tenant that existed before this migration ran.
            migrationBuilder.Sql(
                """
                INSERT INTO trellis_billing.module_entitlement (entitlement_id, tenant_id, module_key, status, monthly_price, enabled_at)
                SELECT gen_random_uuid(), tenant_id, 'INSPECTION', 'ACTIVE', 0, now() FROM trellis_tenant.tenant;

                INSERT INTO trellis_billing.module_entitlement (entitlement_id, tenant_id, module_key, status, monthly_price, enabled_at)
                SELECT gen_random_uuid(), tenant_id, 'AI', 'ACTIVE', 0, now() FROM trellis_tenant.tenant;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoice",
                schema: "trellis_billing");

            migrationBuilder.DropTable(
                name: "invoice_line_item",
                schema: "trellis_billing");

            migrationBuilder.DropTable(
                name: "module_entitlement",
                schema: "trellis_billing");

            migrationBuilder.DropTable(
                name: "payment_method",
                schema: "trellis_billing");

            migrationBuilder.DropTable(
                name: "payment_transaction",
                schema: "trellis_billing");

            migrationBuilder.DropTable(
                name: "subscription",
                schema: "trellis_billing");

            migrationBuilder.DropTable(
                name: "usage_snapshot",
                schema: "trellis_billing");

            migrationBuilder.DropTable(
                name: "webhook_event",
                schema: "trellis_billing");
        }
    }
}
