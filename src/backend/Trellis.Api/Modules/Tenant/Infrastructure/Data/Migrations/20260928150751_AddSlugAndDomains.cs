using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Tenant.Infrastructure.Data.Migrations
{
    /// <summary>
    /// A tenant's public slug (replaces the raw tenant GUID in public URLs) plus its custom domains.
    /// The slug column is added nullable, backfilled for existing tenants from the name plus the first
    /// six hex characters of the id (unique by construction and always a valid slug), and only then made
    /// NOT NULL with its unique index — a plain NOT NULL add would fail on any existing row.
    /// </summary>
    public partial class AddSlugAndDomains : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "slug",
                schema: "trellis_tenant",
                table: "tenant",
                type: "character varying(63)",
                maxLength: 63,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE trellis_tenant.tenant
                SET slug = left(
                        coalesce(nullif(trim(both '-' from regexp_replace(lower(tenant_name), '[^a-z0-9]+', '-', 'g')), ''), 'tenant'),
                        50)
                    || '-' || left(replace(tenant_id::text, '-', ''), 6)
                WHERE slug IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "slug",
                schema: "trellis_tenant",
                table: "tenant",
                type: "character varying(63)",
                maxLength: 63,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(63)",
                oldMaxLength: 63,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "tenant_domain",
                schema: "trellis_tenant",
                columns: table => new
                {
                    tenant_domain_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    verification_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_domain", x => x.tenant_domain_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_slug",
                schema: "trellis_tenant",
                table: "tenant",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_domain_domain",
                schema: "trellis_tenant",
                table: "tenant_domain",
                column: "domain");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_domain_tenant_id",
                schema: "trellis_tenant",
                table: "tenant_domain",
                column: "tenant_id");

            // Anyone may REGISTER a domain, but only one tenant may hold it VERIFIED — the first to prove
            // DNS control wins. A partial unique index, not expressible via EF Core's fluent API.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_tenant_domain_domain_verified"
                    ON trellis_tenant.tenant_domain (domain)
                    WHERE status = 'VERIFIED';

                ALTER TABLE trellis_tenant.tenant_domain ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_tenant.tenant_domain FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_tenant.tenant_domain
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                -- DELETE is granted here (unlike most tables): removing a domain is a genuine delete, not a status flip.
                GRANT SELECT, INSERT, UPDATE, DELETE ON trellis_tenant.tenant_domain TO trellis_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_domain",
                schema: "trellis_tenant");

            migrationBuilder.DropIndex(
                name: "IX_tenant_slug",
                schema: "trellis_tenant",
                table: "tenant");

            migrationBuilder.DropColumn(
                name: "slug",
                schema: "trellis_tenant",
                table: "tenant");
        }
    }
}
