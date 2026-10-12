using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Reporting.Infrastructure.Data.Migrations
{
    /// <summary>BDR-RENT-007 — the Owner report's on-time rate and days late, stored as generated. Existing reports keep null: they were generated without it, and nothing is back-filled into a report already delivered.</summary>
    public partial class AddRentPaymentTimeliness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "rent_payment_timeliness",
                schema: "trellis_reporting",
                table: "owner_report",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "rent_payment_timeliness",
                schema: "trellis_reporting",
                table: "owner_report");
        }
    }
}
