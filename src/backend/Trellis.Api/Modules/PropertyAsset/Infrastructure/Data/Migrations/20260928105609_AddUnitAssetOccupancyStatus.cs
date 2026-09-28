using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.PropertyAsset.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitAssetOccupancyStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "occupancy_status",
                schema: "trellis_property",
                table: "unit_asset",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "VACANT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "occupancy_status",
                schema: "trellis_property",
                table: "unit_asset");
        }
    }
}
