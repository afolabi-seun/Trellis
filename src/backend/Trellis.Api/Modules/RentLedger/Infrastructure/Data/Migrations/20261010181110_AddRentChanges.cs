using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.RentLedger.Infrastructure.Data.Migrations
{
    /// <summary>
    /// BDR-LSE-010 — dated rent changes on a schedule, so a renewal at a new rent applies only from
    /// its date. Append-only for the app: no UPDATE beyond correcting an amount (the same-date
    /// idempotency path), and no DELETE.
    /// </summary>
    public partial class AddRentChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rent_change",
                schema: "trellis_rentledger",
                columns: table => new
                {
                    rent_change_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rent_schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    rent_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    renewal_offer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rent_change", x => x.rent_change_id);
                    table.ForeignKey(
                        name: "FK_rent_change_rent_schedule_rent_schedule_id",
                        column: x => x.rent_schedule_id,
                        principalSchema: "trellis_rentledger",
                        principalTable: "rent_schedule",
                        principalColumn: "rent_schedule_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rent_change_rent_schedule_id_effective_from",
                schema: "trellis_rentledger",
                table: "rent_change",
                columns: new[] { "rent_schedule_id", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rent_change_tenant_id",
                schema: "trellis_rentledger",
                table: "rent_change",
                column: "tenant_id");

            migrationBuilder.Sql(
                """
                ALTER TABLE trellis_rentledger.rent_change
                    ADD CONSTRAINT ck_rent_change_amount CHECK (rent_amount > 0);

                ALTER TABLE trellis_rentledger.rent_change ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_rentledger.rent_change FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_rentledger.rent_change
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                GRANT SELECT, INSERT, UPDATE ON trellis_rentledger.rent_change TO trellis_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rent_change",
                schema: "trellis_rentledger");
        }
    }
}
