using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Reporting.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Replaces the single-currency rent_received_amount with a per-currency JSON breakdown
    /// (BDR-RENT-005: an Owner's schedules may not all share one currency), and labels
    /// total_maintenance_cost with the tenant's own default currency instead of assuming USD.
    /// Existing rows are dev data only — the app was never live before this build — so the backfill
    /// (USD for maintenance_currency; a single-entry USD array for any recorded rent amount) is a
    /// reasonable best effort, not a claim about what currency that money actually was.
    /// </summary>
    public partial class AddCurrencyToOwnerReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "rent_received_by_currency",
                schema: "trellis_reporting",
                table: "owner_report",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE trellis_reporting.owner_report
                SET rent_received_by_currency = json_build_array(json_build_object('Currency', 'USD', 'Amount', rent_received_amount))::jsonb
                WHERE rent_received_amount IS NOT NULL;
                """);

            migrationBuilder.AddColumn<string>(
                name: "maintenance_currency",
                schema: "trellis_reporting",
                table: "owner_report",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.Sql("UPDATE trellis_reporting.owner_report SET maintenance_currency = 'USD' WHERE maintenance_currency IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "maintenance_currency",
                schema: "trellis_reporting",
                table: "owner_report",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "rent_received_amount",
                schema: "trellis_reporting",
                table: "owner_report");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "maintenance_currency",
                schema: "trellis_reporting",
                table: "owner_report");

            migrationBuilder.DropColumn(
                name: "rent_received_by_currency",
                schema: "trellis_reporting",
                table: "owner_report");

            migrationBuilder.AddColumn<decimal>(
                name: "rent_received_amount",
                schema: "trellis_reporting",
                table: "owner_report",
                type: "numeric(12,2)",
                nullable: true);
        }
    }
}
