using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Leasing.Infrastructure.Data.Migrations
{
    /// <summary>Both columns nullable: applications sent for signature before this existed keep NULL and fall back to the old file-a-new-document behaviour in LeasingWebhookService.</summary>
    public partial class AddSignedLeaseTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "draft_document_id",
                schema: "trellis_leasing",
                table: "application",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "signed_lease_version_id",
                schema: "trellis_leasing",
                table: "application",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "draft_document_id",
                schema: "trellis_leasing",
                table: "application");

            migrationBuilder.DropColumn(
                name: "signed_lease_version_id",
                schema: "trellis_leasing",
                table: "application");
        }
    }
}
