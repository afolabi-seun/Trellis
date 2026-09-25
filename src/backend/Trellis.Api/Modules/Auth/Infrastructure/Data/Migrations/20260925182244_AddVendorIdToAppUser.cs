using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Auth.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorIdToAppUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "vendor_id",
                schema: "trellis_auth",
                table: "app_user",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "vendor_id",
                schema: "trellis_auth",
                table: "app_user");
        }
    }
}
