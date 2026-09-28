using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.RentLedger.Infrastructure.Data.Migrations
{
    /// <summary>rent_collection_design.md's two tables plus the columns the design omits: rent_schedule.start_date, rent_payment_record.tenant_id (RLS needs it), and the void columns (a mistaken payment record is voided, never edited or deleted).</summary>
    public partial class InitialRentLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trellis_rentledger");

            migrationBuilder.CreateTable(
                name: "rent_payment_record",
                schema: "trellis_rentledger",
                columns: table => new
                {
                    rent_payment_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rent_schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_received = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    date_received = table.Column<DateOnly>(type: "date", nullable: false),
                    method_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    voided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    void_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rent_payment_record", x => x.rent_payment_record_id);
                });

            migrationBuilder.CreateTable(
                name: "rent_schedule",
                schema: "trellis_rentledger",
                columns: table => new
                {
                    rent_schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lease_application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rent_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_day_of_month = table.Column<int>(type: "integer", nullable: false),
                    grace_period_days = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    last_arrears_alerted_period = table.Column<DateOnly>(type: "date", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rent_schedule", x => x.rent_schedule_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rent_payment_record_rent_schedule_id",
                schema: "trellis_rentledger",
                table: "rent_payment_record",
                column: "rent_schedule_id");

            migrationBuilder.CreateIndex(
                name: "IX_rent_payment_record_tenant_id",
                schema: "trellis_rentledger",
                table: "rent_payment_record",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_rent_schedule_tenant_id",
                schema: "trellis_rentledger",
                table: "rent_schedule",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_rent_schedule_unit_asset_id",
                schema: "trellis_rentledger",
                table: "rent_schedule",
                column: "unit_asset_id");

            // "One ACTIVE schedule per unit" and "one schedule per lease application" — Postgres partial
            // unique indexes, not expressible via EF Core's fluent API. The second is what makes
            // LeasingWebhookService's retry deterministic (idempotent get-or-create by application).
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_rent_schedule_unit_asset_id_active"
                    ON trellis_rentledger.rent_schedule (unit_asset_id)
                    WHERE status = 'ACTIVE';

                CREATE UNIQUE INDEX "IX_rent_schedule_lease_application_id"
                    ON trellis_rentledger.rent_schedule (lease_application_id)
                    WHERE lease_application_id IS NOT NULL;

                ALTER TABLE trellis_rentledger.rent_schedule
                    ADD CONSTRAINT ck_rent_schedule_due_day CHECK (due_day_of_month BETWEEN 1 AND 31),
                    ADD CONSTRAINT ck_rent_schedule_grace CHECK (grace_period_days BETWEEN 0 AND 60),
                    ADD CONSTRAINT ck_rent_schedule_amount CHECK (rent_amount > 0);

                ALTER TABLE trellis_rentledger.rent_payment_record
                    ADD CONSTRAINT ck_rent_payment_record_amount CHECK (amount_received > 0);
                """);

            // Standard tenant_isolation on both tables — no vendor branch (a Vendor has no relationship
            // to rent collection, ever). The arrears background job reads across tenants under
            // app.is_system, which the same policy's first branch already permits.
            migrationBuilder.Sql(
                """
                ALTER TABLE trellis_rentledger.rent_schedule ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_rentledger.rent_schedule FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_rentledger.rent_schedule
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_rentledger.rent_payment_record ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_rentledger.rent_payment_record FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_rentledger.rent_payment_record
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                GRANT USAGE ON SCHEMA trellis_rentledger TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_rentledger.rent_schedule TO trellis_app;
                -- UPDATE is needed only for voiding (voided_at/voided_by/void_reason); amount, date and
                -- reference are never changed by any code path, and there is no DELETE grant at all.
                GRANT SELECT, INSERT, UPDATE ON trellis_rentledger.rent_payment_record TO trellis_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rent_payment_record",
                schema: "trellis_rentledger");

            migrationBuilder.DropTable(
                name: "rent_schedule",
                schema: "trellis_rentledger");
        }
    }
}
