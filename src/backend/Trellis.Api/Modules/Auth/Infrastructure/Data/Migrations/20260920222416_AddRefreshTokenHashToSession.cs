using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Auth.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenHashToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "refresh_token_hash",
                schema: "trellis_auth",
                table: "session",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_session_refresh_token_hash",
                schema: "trellis_auth",
                table: "session",
                column: "refresh_token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_session_refresh_token_hash",
                schema: "trellis_auth",
                table: "session");

            migrationBuilder.DropColumn(
                name: "refresh_token_hash",
                schema: "trellis_auth",
                table: "session");
        }
    }
}
