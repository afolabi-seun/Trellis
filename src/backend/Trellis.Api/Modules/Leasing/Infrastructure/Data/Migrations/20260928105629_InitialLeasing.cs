using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Leasing.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialLeasing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trellis_leasing");

            migrationBuilder.CreateTable(
                name: "application",
                schema: "trellis_leasing",
                columns: table => new
                {
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vacancy_listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    applicant_full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    applicant_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    applicant_date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    applicant_current_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    applicant_employer_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    applicant_monthly_income = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    consent_to_screening = table.Column<bool>(type: "boolean", nullable: false),
                    envelope_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    sent_for_signature_by = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application", x => x.application_id);
                });

            migrationBuilder.CreateTable(
                name: "application_event",
                schema: "trellis_leasing",
                columns: table => new
                {
                    application_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actor_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    detail_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_event", x => x.application_event_id);
                });

            migrationBuilder.CreateTable(
                name: "application_link_token",
                schema: "trellis_leasing",
                columns: table => new
                {
                    application_link_token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vacancy_listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_link_token", x => x.application_link_token_id);
                });

            migrationBuilder.CreateTable(
                name: "leasing_webhook_event",
                schema: "trellis_leasing",
                columns: table => new
                {
                    leasing_webhook_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_event_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leasing_webhook_event", x => x.leasing_webhook_event_id);
                });

            migrationBuilder.CreateTable(
                name: "screening_result",
                schema: "trellis_leasing",
                columns: table => new
                {
                    screening_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    outcome_summary = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    full_report_ciphertext = table.Column<string>(type: "text", nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_screening_result", x => x.screening_result_id);
                });

            migrationBuilder.CreateTable(
                name: "vacancy_listing",
                schema: "trellis_leasing",
                columns: table => new
                {
                    vacancy_listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rent_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    available_from = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacancy_listing", x => x.vacancy_listing_id);
                });

            migrationBuilder.CreateTable(
                name: "vacancy_listing_photo",
                schema: "trellis_leasing",
                columns: table => new
                {
                    vacancy_listing_photo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vacancy_listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    scan_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacancy_listing_photo", x => x.vacancy_listing_photo_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_envelope_id",
                schema: "trellis_leasing",
                table: "application",
                column: "envelope_id");

            migrationBuilder.CreateIndex(
                name: "IX_application_tenant_id",
                schema: "trellis_leasing",
                table: "application",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_application_vacancy_listing_id",
                schema: "trellis_leasing",
                table: "application",
                column: "vacancy_listing_id");

            migrationBuilder.CreateIndex(
                name: "IX_application_event_application_id",
                schema: "trellis_leasing",
                table: "application_event",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_application_event_tenant_id",
                schema: "trellis_leasing",
                table: "application_event",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_application_link_token_tenant_id",
                schema: "trellis_leasing",
                table: "application_link_token",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_application_link_token_token_hash",
                schema: "trellis_leasing",
                table: "application_link_token",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leasing_webhook_event_provider_event_id",
                schema: "trellis_leasing",
                table: "leasing_webhook_event",
                column: "provider_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_screening_result_application_id",
                schema: "trellis_leasing",
                table: "screening_result",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_screening_result_tenant_id",
                schema: "trellis_leasing",
                table: "screening_result",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_vacancy_listing_tenant_id",
                schema: "trellis_leasing",
                table: "vacancy_listing",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_vacancy_listing_unit_asset_id",
                schema: "trellis_leasing",
                table: "vacancy_listing",
                column: "unit_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_vacancy_listing_photo_tenant_id",
                schema: "trellis_leasing",
                table: "vacancy_listing_photo",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_vacancy_listing_photo_vacancy_listing_id",
                schema: "trellis_leasing",
                table: "vacancy_listing_photo",
                column: "vacancy_listing_id");

            // BDR-LSE-001: "one active listing per unit" — a Postgres partial unique index, not
            // expressible via EF Core's fluent API. Both non-terminal statuses count as active.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_vacancy_listing_unit_asset_id_active"
                    ON trellis_leasing.vacancy_listing (unit_asset_id)
                    WHERE status IN ('LISTED', 'UNDER_APPLICATION');
                """);

            // Standard tenant_isolation on the six tenant-owned tables — no vendor branch (a Vendor
            // has no relationship to leasing, ever). leasing_webhook_event has no tenant_id at all
            // (a provider's webhook carries no Trellis tenant identity) and gets the stricter
            // "system_only" policy instead, exact webhook_event precedent.
            migrationBuilder.Sql(
                """
                ALTER TABLE trellis_leasing.vacancy_listing ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_leasing.vacancy_listing FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_leasing.vacancy_listing
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_leasing.vacancy_listing_photo ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_leasing.vacancy_listing_photo FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_leasing.vacancy_listing_photo
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_leasing.application ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_leasing.application FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_leasing.application
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_leasing.screening_result ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_leasing.screening_result FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_leasing.screening_result
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_leasing.application_event ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_leasing.application_event FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_leasing.application_event
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_leasing.application_link_token ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_leasing.application_link_token FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_leasing.application_link_token
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                ALTER TABLE trellis_leasing.leasing_webhook_event ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_leasing.leasing_webhook_event FORCE ROW LEVEL SECURITY;
                CREATE POLICY system_only ON trellis_leasing.leasing_webhook_event
                    USING (current_setting('app.is_system', true) = 'true');

                GRANT USAGE ON SCHEMA trellis_leasing TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_leasing.vacancy_listing TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_leasing.vacancy_listing_photo TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_leasing.application TO trellis_app;
                GRANT SELECT, INSERT ON trellis_leasing.screening_result TO trellis_app;
                -- application_event is genuinely append-only: no UPDATE grant at all, enforced at the
                -- grant level (audit_record's own belt-and-suspenders posture, without a trigger).
                GRANT SELECT, INSERT ON trellis_leasing.application_event TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_leasing.application_link_token TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_leasing.leasing_webhook_event TO trellis_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application",
                schema: "trellis_leasing");

            migrationBuilder.DropTable(
                name: "application_event",
                schema: "trellis_leasing");

            migrationBuilder.DropTable(
                name: "application_link_token",
                schema: "trellis_leasing");

            migrationBuilder.DropTable(
                name: "leasing_webhook_event",
                schema: "trellis_leasing");

            migrationBuilder.DropTable(
                name: "screening_result",
                schema: "trellis_leasing");

            migrationBuilder.DropTable(
                name: "vacancy_listing",
                schema: "trellis_leasing");

            migrationBuilder.DropTable(
                name: "vacancy_listing_photo",
                schema: "trellis_leasing");
        }
    }
}
