using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Leasing.Infrastructure.Data.Migrations
{
    /// <summary>BDR-LSE-010 — renewal offers and their outcomes. lease_id points at trellis_property.lease (plain column, no cross-schema FK).</summary>
    public partial class AddRenewalOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "renewal_offer",
                schema: "trellis_leasing",
                columns: table => new
                {
                    renewal_offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lease_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_term = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    proposed_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    proposed_rent_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    proposed_rent_from = table.Column<DateOnly>(type: "date", nullable: true),
                    respond_by = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    outcome_on = table.Column<DateOnly>(type: "date", nullable: true),
                    decline_reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    decline_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    rent_change_applied = table.Column<bool>(type: "boolean", nullable: true),
                    offered_by = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome_recorded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_renewal_offer", x => x.renewal_offer_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_renewal_offer_lease_id",
                schema: "trellis_leasing",
                table: "renewal_offer",
                column: "lease_id");

            migrationBuilder.CreateIndex(
                name: "IX_renewal_offer_tenant_id",
                schema: "trellis_leasing",
                table: "renewal_offer",
                column: "tenant_id");

            // One open offer per lease, and the shape rules the entity enforces, held by Postgres too.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_renewal_offer_lease_id_offered"
                    ON trellis_leasing.renewal_offer (lease_id)
                    WHERE status = 'OFFERED';

                ALTER TABLE trellis_leasing.renewal_offer
                    ADD CONSTRAINT ck_renewal_offer_term CHECK (
                        (proposed_term = 'FIXED' AND proposed_end_date IS NOT NULL)
                        OR (proposed_term = 'MONTH_TO_MONTH' AND proposed_end_date IS NULL)),
                    ADD CONSTRAINT ck_renewal_offer_rent CHECK (
                        (proposed_rent_amount IS NULL AND proposed_rent_from IS NULL)
                        OR (proposed_rent_amount > 0 AND proposed_rent_from IS NOT NULL)),
                    ADD CONSTRAINT ck_renewal_offer_outcome CHECK (
                        (status = 'OFFERED' AND outcome_on IS NULL AND decline_reason IS NULL)
                        OR (status = 'DECLINED' AND outcome_on IS NOT NULL AND decline_reason IS NOT NULL)
                        OR (status IN ('ACCEPTED', 'EXPIRED') AND outcome_on IS NOT NULL AND decline_reason IS NULL));

                ALTER TABLE trellis_leasing.renewal_offer ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_leasing.renewal_offer FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_leasing.renewal_offer
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                GRANT SELECT, INSERT, UPDATE ON trellis_leasing.renewal_offer TO trellis_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "renewal_offer",
                schema: "trellis_leasing");
        }
    }
}
