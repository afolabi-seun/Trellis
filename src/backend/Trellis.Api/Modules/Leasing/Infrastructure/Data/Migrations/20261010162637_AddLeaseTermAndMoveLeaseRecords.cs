using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Leasing.Infrastructure.Data.Migrations
{
    /// <summary>
    /// BDR-LSE-009: the lease term captured at send-for-signature. And the move of the lease record
    /// itself out of this schema: Application stops doubling as the lease, and every executed
    /// lease gets a row in trellis_property.lease (created empty by PropertyAsset's
    /// AddLeaseRecords, which always migrates first). Down restores the column but not its text.
    /// </summary>
    public partial class AddLeaseTermAndMoveLeaseRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "lease_start_date",
                schema: "trellis_leasing",
                table: "application",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "lease_end_date",
                schema: "trellis_leasing",
                table: "application",
                type: "date",
                nullable: true);

            // Every LEASED / LEASE_ENDED application becomes a lease record before
            // lease_ended_reason is dropped, so its free text survives as the lease's end_note.
            // Nothing here is guessed: the term is NOT_RECORDED (staff record it afterwards), an
            // ended lease's reason is NOT_RECORDED with no ending party, and the start date is the
            // listing's availability date, which is what the rent schedule already started from.
            // If two LEASED applications ever share a unit (no code path allows it), the newest
            // stays ACTIVE and the other is recorded as ended, so the one-active-lease-per-unit
            // index can't fail the migration. is_system: lease has FORCE ROW LEVEL SECURITY.
            migrationBuilder.Sql(
                """
                SELECT set_config('app.is_system', 'true', true);

                WITH leased AS (
                    SELECT a.application_id, a.tenant_id, v.unit_asset_id, v.available_from, a.status,
                           a.lease_ended_date, a.lease_ended_reason, a.submitted_at, a.sent_for_signature_by,
                           row_number() OVER (PARTITION BY v.unit_asset_id, a.status ORDER BY a.submitted_at DESC) AS newest
                    FROM trellis_leasing.application a
                    JOIN trellis_leasing.vacancy_listing v ON v.vacancy_listing_id = a.vacancy_listing_id
                    WHERE a.status IN ('LEASED', 'LEASE_ENDED')
                ), classified AS (
                    SELECT *, (status = 'LEASED' AND newest = 1) AS still_active FROM leased
                )
                INSERT INTO trellis_property.lease
                    (lease_id, tenant_id, unit_asset_id, source, lease_application_id, start_date, term_type, end_date,
                     status, ended_on, end_reason, ended_by, end_note, created_by, created_at, updated_at)
                SELECT gen_random_uuid(), tenant_id, unit_asset_id, 'LEASING', application_id, available_from, 'NOT_RECORDED', NULL,
                       CASE WHEN still_active THEN 'ACTIVE' ELSE 'ENDED' END,
                       CASE WHEN still_active THEN NULL ELSE GREATEST(COALESCE(lease_ended_date, submitted_at::date), available_from) END,
                       CASE WHEN still_active THEN NULL ELSE 'NOT_RECORDED' END,
                       NULL,
                       CASE WHEN still_active THEN NULL ELSE lease_ended_reason END,
                       sent_for_signature_by, now(), now()
                FROM classified;
                """);

            migrationBuilder.DropColumn(
                name: "lease_ended_reason",
                schema: "trellis_leasing",
                table: "application");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "lease_end_date",
                schema: "trellis_leasing",
                table: "application");

            migrationBuilder.DropColumn(
                name: "lease_start_date",
                schema: "trellis_leasing",
                table: "application");

            migrationBuilder.AddColumn<string>(
                name: "lease_ended_reason",
                schema: "trellis_leasing",
                table: "application",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
