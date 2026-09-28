using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Reporting.Infrastructure.Data.Migrations
{
    /// <summary>BDR-RENT-005 — every column nullable, so existing reports (generated before Rent Collection existed) simply stay NULL = "not tracked", never a misleading zero.</summary>
    public partial class AddRentStatusToOwnerReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "rent_arrears_count",
                schema: "trellis_reporting",
                table: "owner_report",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "rent_as_of",
                schema: "trellis_reporting",
                table: "owner_report",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rent_current_count",
                schema: "trellis_reporting",
                table: "owner_report",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rent_overdue_count",
                schema: "trellis_reporting",
                table: "owner_report",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "rent_received_amount",
                schema: "trellis_reporting",
                table: "owner_report",
                type: "numeric(12,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "rent_arrears_count",
                schema: "trellis_reporting",
                table: "owner_report");

            migrationBuilder.DropColumn(
                name: "rent_as_of",
                schema: "trellis_reporting",
                table: "owner_report");

            migrationBuilder.DropColumn(
                name: "rent_current_count",
                schema: "trellis_reporting",
                table: "owner_report");

            migrationBuilder.DropColumn(
                name: "rent_overdue_count",
                schema: "trellis_reporting",
                table: "owner_report");

            migrationBuilder.DropColumn(
                name: "rent_received_amount",
                schema: "trellis_reporting",
                table: "owner_report");
        }
    }
}
