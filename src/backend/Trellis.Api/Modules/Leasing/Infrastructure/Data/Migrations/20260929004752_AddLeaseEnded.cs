using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Leasing.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaseEnded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "lease_ended_date",
                schema: "trellis_leasing",
                table: "application",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lease_ended_reason",
                schema: "trellis_leasing",
                table: "application",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "lease_ended_date",
                schema: "trellis_leasing",
                table: "application");

            migrationBuilder.DropColumn(
                name: "lease_ended_reason",
                schema: "trellis_leasing",
                table: "application");
        }
    }
}
