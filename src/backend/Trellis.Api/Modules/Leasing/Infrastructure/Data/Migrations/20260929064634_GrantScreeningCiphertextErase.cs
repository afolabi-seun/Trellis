using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Leasing.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class GrantScreeningCiphertextErase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // security_guide.md's POPIA erasure — the one deliberate loosening of
            // screening_result's otherwise INSERT-only grant (data_model.md), scoped to exactly
            // the one column that can legitimately need clearing after the fact. Every other
            // column, and every other table's INSERT-only posture, is unaffected.
            migrationBuilder.Sql(
                "GRANT UPDATE (full_report_ciphertext) ON trellis_leasing.screening_result TO trellis_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "REVOKE UPDATE (full_report_ciphertext) ON trellis_leasing.screening_result FROM trellis_app;");
        }
    }
}
