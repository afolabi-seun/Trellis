using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.RentLedger.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Every rent schedule tracks a PropertyAsset lease (BDR-LSE-009). Runs last in the migration
    /// order, so it is also where existing tenancies the Leasing backfill couldn't see — schedules
    /// started manually — get lease records, and where unit occupancy is reconciled with leases.
    /// Nothing is guessed: backfilled terms and end reasons are NOT_RECORDED, and a unit's
    /// vacant_since comes only from an ended lease's own date.
    /// </summary>
    public partial class LinkRentSchedulesToLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "lease_id",
                schema: "trellis_rentledger",
                table: "rent_schedule",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_rent_schedule_lease_id",
                schema: "trellis_rentledger",
                table: "rent_schedule",
                column: "lease_id");

            migrationBuilder.Sql(
                """
                SELECT set_config('app.is_system', 'true', true);

                -- 1. Schedules started by a Leasing application share that application's lease.
                UPDATE trellis_rentledger.rent_schedule s
                SET lease_id = l.lease_id
                FROM trellis_property.lease l
                WHERE l.lease_application_id = s.lease_application_id;

                -- 2. An active manual schedule on a unit that already has an active lease attaches to it.
                UPDATE trellis_rentledger.rent_schedule s
                SET lease_id = l.lease_id
                FROM trellis_property.lease l
                WHERE s.lease_id = '00000000-0000-0000-0000-000000000000' AND s.status = 'ACTIVE'
                  AND l.unit_asset_id = s.unit_asset_id AND l.status = 'ACTIVE';

                -- 3. Every other schedule is a manual tenancy of its own. The new lease reuses the
                --    schedule's id (different table, still unique) purely so step 4 can link them.
                INSERT INTO trellis_property.lease
                    (lease_id, tenant_id, unit_asset_id, source, lease_application_id, start_date, term_type, end_date,
                     status, ended_on, end_reason, ended_by, end_note, created_by, created_at, updated_at)
                SELECT s.rent_schedule_id, s.tenant_id, s.unit_asset_id, 'MANUAL', NULL, s.start_date, 'NOT_RECORDED', NULL,
                       CASE WHEN s.status = 'ACTIVE' THEN 'ACTIVE' ELSE 'ENDED' END,
                       CASE WHEN s.status = 'ACTIVE' THEN NULL ELSE GREATEST(COALESCE(s.ended_at::date, s.start_date), s.start_date) END,
                       CASE WHEN s.status = 'ACTIVE' THEN NULL ELSE 'NOT_RECORDED' END,
                       NULL, NULL, s.created_by, s.created_at, now()
                FROM trellis_rentledger.rent_schedule s
                WHERE s.lease_id = '00000000-0000-0000-0000-000000000000';

                -- 4. Link them.
                UPDATE trellis_rentledger.rent_schedule
                SET lease_id = rent_schedule_id
                WHERE lease_id = '00000000-0000-0000-0000-000000000000';

                ALTER TABLE trellis_rentledger.rent_schedule ALTER COLUMN lease_id DROP DEFAULT;

                -- 5. Occupancy now follows leases exactly. Before this, ending a manual rent schedule
                --    never vacated its unit, so such units are vacated here; the reverse (an active
                --    lease on a VACANT unit) has no known cause but is corrected the same way.
                UPDATE trellis_property.unit_asset u
                SET occupancy_status = 'VACANT', updated_at = now()
                WHERE u.occupancy_status = 'OCCUPIED'
                  AND NOT EXISTS (SELECT 1 FROM trellis_property.lease l WHERE l.unit_asset_id = u.unit_asset_id AND l.status = 'ACTIVE');

                UPDATE trellis_property.unit_asset u
                SET occupancy_status = 'OCCUPIED', vacant_since = NULL, updated_at = now()
                WHERE u.occupancy_status = 'VACANT'
                  AND EXISTS (SELECT 1 FROM trellis_property.lease l WHERE l.unit_asset_id = u.unit_asset_id AND l.status = 'ACTIVE');

                -- 6. BDR-AST-005: a vacant unit whose last lease ended is vacant since that date.
                UPDATE trellis_property.unit_asset u
                SET vacant_since = last_end.ended_on
                FROM (SELECT unit_asset_id, max(ended_on) AS ended_on FROM trellis_property.lease
                      WHERE status = 'ENDED' GROUP BY unit_asset_id) last_end
                WHERE u.unit_asset_id = last_end.unit_asset_id
                  AND u.occupancy_status = 'VACANT' AND u.vacant_since IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rent_schedule_lease_id",
                schema: "trellis_rentledger",
                table: "rent_schedule");

            migrationBuilder.DropColumn(
                name: "lease_id",
                schema: "trellis_rentledger",
                table: "rent_schedule");
        }
    }
}
