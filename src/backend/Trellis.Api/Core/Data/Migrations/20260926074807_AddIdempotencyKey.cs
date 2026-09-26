using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trellis_core");

            migrationBuilder.CreateTable(
                name: "idempotency_key",
                schema: "trellis_core",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    response_json = table.Column<string>(type: "jsonb", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_key", x => new { x.tenant_id, x.idempotency_key });
                });

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_key_expires_at",
                schema: "trellis_core",
                table: "idempotency_key",
                column: "expires_at");

            // Plain tenant_isolation — PostgresIdempotencyStore always runs under
            // SystemContext.Enter() with an explicitly-passed tenantId (api_standards.md's own
            // Idempotency section; the one real caller, a Vendor's offline-sync completion, has no
            // TenantContext.Current at all), so is_system is what actually lets every read/write
            // through here; the tenant_id column and this policy exist for defense-in-depth and for
            // any future non-vendor caller of the same executor.
            migrationBuilder.Sql(
                """
                ALTER TABLE trellis_core.idempotency_key ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_core.idempotency_key FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_core.idempotency_key
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                GRANT USAGE ON SCHEMA trellis_core TO trellis_app;
                GRANT SELECT, INSERT ON trellis_core.idempotency_key TO trellis_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_key",
                schema: "trellis_core");
        }
    }
}
