using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Tenant.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class InitialTenant : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "trellis_tenant");

        migrationBuilder.CreateTable(
            name: "tenant",
            schema: "trellis_tenant",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                subscription_tier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                config_json = table.Column<string>(type: "jsonb", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tenant", x => x.tenant_id);
            });

        // devops_environments.md / security_guide.md: the running API connects as the
        // unprivileged trellis_app role, not the migration-owner role. No RLS policy here —
        // `tenant` is the aggregate root RLS isolates *around*, not a tenant-owned child
        // table; access control is RBAC (SUPER_ADMIN only), enforced at the application
        // layer. No DELETE grant — offboarding transitions status, it never deletes a row.
        migrationBuilder.Sql(
            """
            GRANT USAGE ON SCHEMA trellis_tenant TO trellis_app;
            GRANT SELECT, INSERT, UPDATE ON trellis_tenant.tenant TO trellis_app;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "tenant",
            schema: "trellis_tenant");
    }
}
