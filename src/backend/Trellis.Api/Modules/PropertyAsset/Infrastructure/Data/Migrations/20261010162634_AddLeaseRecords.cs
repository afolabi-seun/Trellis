using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.PropertyAsset.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 11: the lease record (BDR-LSE-009/011) and a unit's vacant-since date (BDR-AST-005).
    /// The table starts empty — existing tenancies are backfilled by the Leasing and RentLedger
    /// migrations that follow this one, since only they can read their own schemas' history, and
    /// both always migrate after PropertyAsset (data_model.md's dependency order). vacant_since
    /// starts null for every existing unit: unknown, never guessed; the RentLedger backfill fills
    /// it in wherever an ended lease says when the vacancy began.
    /// </summary>
    public partial class AddLeaseRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "vacant_since",
                schema: "trellis_property",
                table: "unit_asset",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "lease",
                schema: "trellis_property",
                columns: table => new
                {
                    lease_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    lease_application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    term_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ended_on = table.Column<DateOnly>(type: "date", nullable: true),
                    end_reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ended_by = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    end_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lease", x => x.lease_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lease_tenant_id",
                schema: "trellis_property",
                table: "lease",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_lease_unit_asset_id",
                schema: "trellis_property",
                table: "lease",
                column: "unit_asset_id");

            // "One ACTIVE lease per unit" (the occupancy invariant) and "one lease per Leasing
            // application" (what makes the e-signature webhook idempotent) — partial unique
            // indexes, not expressible via EF Core's fluent API. The CHECKs make the term rules
            // (end_date exactly when FIXED) and the end rules hold even for a hand-written UPDATE.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_lease_unit_asset_id_active"
                    ON trellis_property.lease (unit_asset_id)
                    WHERE status = 'ACTIVE';

                CREATE UNIQUE INDEX "IX_lease_lease_application_id"
                    ON trellis_property.lease (lease_application_id)
                    WHERE lease_application_id IS NOT NULL;

                ALTER TABLE trellis_property.lease
                    ADD CONSTRAINT ck_lease_source CHECK (
                        (source = 'LEASING' AND lease_application_id IS NOT NULL)
                        OR (source = 'MANUAL' AND lease_application_id IS NULL)),
                    ADD CONSTRAINT ck_lease_term CHECK (
                        (term_type = 'FIXED' AND end_date IS NOT NULL AND end_date > start_date)
                        OR (term_type IN ('MONTH_TO_MONTH', 'NOT_RECORDED') AND end_date IS NULL)),
                    ADD CONSTRAINT ck_lease_status CHECK (
                        (status = 'ACTIVE' AND ended_on IS NULL AND end_reason IS NULL AND ended_by IS NULL)
                        OR (status = 'ENDED' AND ended_on IS NOT NULL AND ended_on >= start_date AND end_reason IS NOT NULL));
                """);

            // Standard tenant_isolation, the same as unit_asset — no vendor branch: a Vendor has no
            // relationship to a lease. No DELETE grant: an ended lease is history, never removed.
            migrationBuilder.Sql(
                """
                ALTER TABLE trellis_property.lease ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_property.lease FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_property.lease
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                GRANT SELECT, INSERT, UPDATE ON trellis_property.lease TO trellis_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lease",
                schema: "trellis_property");

            migrationBuilder.DropColumn(
                name: "vacant_since",
                schema: "trellis_property",
                table: "unit_asset");
        }
    }
}
