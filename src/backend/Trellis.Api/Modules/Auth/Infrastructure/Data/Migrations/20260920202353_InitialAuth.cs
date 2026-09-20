using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Modules.Auth.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trellis_auth");

            migrationBuilder.CreateTable(
                name: "app_user",
                schema: "trellis_auth",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    department_ids = table.Column<Guid[]>(type: "uuid[]", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDING_ACTIVATION"),
                    failed_login_count = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    totp_secret_encrypted = table.Column<byte[]>(type: "bytea", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_user", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "mfa_recovery_code",
                schema: "trellis_auth",
                columns: table => new
                {
                    recovery_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mfa_recovery_code", x => x.recovery_code_id);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_token",
                schema: "trellis_auth",
                columns: table => new
                {
                    token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    requested_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_token", x => x.token_id);
                });

            migrationBuilder.CreateTable(
                name: "session",
                schema: "trellis_auth",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session", x => x.session_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_app_user_username",
                schema: "trellis_auth",
                table: "app_user",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mfa_recovery_code_user_id_code_hash",
                schema: "trellis_auth",
                table: "mfa_recovery_code",
                columns: new[] { "user_id", "code_hash" });

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_token_token_hash",
                schema: "trellis_auth",
                table: "password_reset_token",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_user_id",
                schema: "trellis_auth",
                table: "session",
                column: "user_id");

            migrationBuilder.Sql(
                """
                -- app_user gets the standard tenant_isolation policy (data_model.md), same as
                -- every other tenant-owned table — including its is_system OR branch. That
                -- branch isn't just for Super Admin here: Auth's own login/MFA-verify/forgot-
                -- password lookups resolve a user by username or partial-token subject BEFORE any
                -- tenant is known, so they run under SystemContext.Enter() (see AuthService.cs)
                -- to cross tenants for that one lookup. tenant_id is nullable (a platform-level
                -- Super Admin user has none); SQL's three-valued logic already makes those rows
                -- invisible to every tenant and visible only under app.is_system, same as
                -- audit_record.
                ALTER TABLE trellis_auth.app_user ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trellis_auth.app_user FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trellis_auth.app_user
                    USING (
                        current_setting('app.is_system', true) = 'true'
                        OR tenant_id::text = current_setting('app.current_tenant_id', true)
                    );

                -- session, password_reset_token, and mfa_recovery_code carry no tenant_id at all
                -- (data_model.md) — they're never listed tenant-scoped, only ever fetched by an
                -- exact, unforgeable identity: a session_id pulled from an already-validated JWT,
                -- a SHA-256 token/code hash. A tenant_isolation-style policy has nothing to
                -- compare here, and a join back to app_user.tenant_id on every session check
                -- would add a subquery (itself gated by app_user's own RLS) to the hottest path
                -- in the app for no real isolation gain over the exact-hash/exact-id lookup
                -- already enforced at the application layer. RLS is deliberately left off these
                -- three tables; isolation for them is the unforgeable-key layer, not RLS.
                GRANT USAGE ON SCHEMA trellis_auth TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_auth.app_user TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_auth.session TO trellis_app;
                GRANT SELECT, INSERT, UPDATE ON trellis_auth.password_reset_token TO trellis_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON trellis_auth.mfa_recovery_code TO trellis_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_user",
                schema: "trellis_auth");

            migrationBuilder.DropTable(
                name: "mfa_recovery_code",
                schema: "trellis_auth");

            migrationBuilder.DropTable(
                name: "password_reset_token",
                schema: "trellis_auth");

            migrationBuilder.DropTable(
                name: "session",
                schema: "trellis_auth");
        }
    }
}
