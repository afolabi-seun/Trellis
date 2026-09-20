using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Core.Data.Migrations;

/// <summary>
/// data_model.md, BDR-NFR-008: <c>audit_record</c> is range-partitioned on
/// <c>recorded_at</c> from its first migration, even though archival automation is
/// deferred. Written entirely as raw SQL, not EF Core's generated <c>CreateTable</c> —
/// partitioning, the composite PK a partitioned table requires (Postgres mandates the
/// partition key be part of any PK/unique constraint), RLS, and the append-only trigger are
/// all outside what EF Core's migration generator produces.
///
/// Quarterly partitions are created through end of 2027 as a starting window — creating
/// further partitions ahead of need is an operational task, not yet automated (the same
/// "archival automation deferred" trade-off `data_model.md`'s Retention &amp; Archival
/// section already states).
/// </summary>
public partial class InitialCoreAuditRecord : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE SCHEMA IF NOT EXISTS trellis_audit;

            CREATE TABLE trellis_audit.audit_record (
                audit_id     UUID NOT NULL,
                tenant_id    UUID NULL,
                actor_id     UUID NOT NULL,
                actor_type   VARCHAR(20) NOT NULL,
                audit_type   VARCHAR(50) NOT NULL,
                detail_json  JSONB NULL,
                recorded_at  TIMESTAMPTZ NOT NULL,
                PRIMARY KEY (audit_id, recorded_at)
            ) PARTITION BY RANGE (recorded_at);

            CREATE TABLE trellis_audit.audit_record_2026_q1 PARTITION OF trellis_audit.audit_record
                FOR VALUES FROM ('2026-01-01') TO ('2026-04-01');
            CREATE TABLE trellis_audit.audit_record_2026_q2 PARTITION OF trellis_audit.audit_record
                FOR VALUES FROM ('2026-04-01') TO ('2026-07-01');
            CREATE TABLE trellis_audit.audit_record_2026_q3 PARTITION OF trellis_audit.audit_record
                FOR VALUES FROM ('2026-07-01') TO ('2026-10-01');
            CREATE TABLE trellis_audit.audit_record_2026_q4 PARTITION OF trellis_audit.audit_record
                FOR VALUES FROM ('2026-10-01') TO ('2027-01-01');
            CREATE TABLE trellis_audit.audit_record_2027_q1 PARTITION OF trellis_audit.audit_record
                FOR VALUES FROM ('2027-01-01') TO ('2027-04-01');
            CREATE TABLE trellis_audit.audit_record_2027_q2 PARTITION OF trellis_audit.audit_record
                FOR VALUES FROM ('2027-04-01') TO ('2027-07-01');
            CREATE TABLE trellis_audit.audit_record_2027_q3 PARTITION OF trellis_audit.audit_record
                FOR VALUES FROM ('2027-07-01') TO ('2027-10-01');
            CREATE TABLE trellis_audit.audit_record_2027_q4 PARTITION OF trellis_audit.audit_record
                FOR VALUES FROM ('2027-10-01') TO ('2028-01-01');

            -- data_model.md, "Row-Level Security": the standard tenant_isolation policy —
            -- correct even for a nullable tenant_id, see that section's note.
            ALTER TABLE trellis_audit.audit_record ENABLE ROW LEVEL SECURITY;

            CREATE POLICY tenant_isolation ON trellis_audit.audit_record
                USING (
                    current_setting('app.is_system', true) = 'true'
                    OR tenant_id = current_setting('app.current_tenant_id', true)::uuid
                );

            -- Append-only: a database trigger rejects UPDATE/DELETE outright, independent of
            -- and in addition to the application layer never issuing either.
            CREATE FUNCTION trellis_audit.reject_audit_record_mutation()
            RETURNS TRIGGER AS $$
            BEGIN
                RAISE EXCEPTION 'trellis_audit.audit_record is append-only: % not permitted', TG_OP;
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER audit_record_append_only
                BEFORE UPDATE OR DELETE ON trellis_audit.audit_record
                FOR EACH ROW EXECUTE FUNCTION trellis_audit.reject_audit_record_mutation();

            -- devops_environments.md / security_guide.md: the running API connects as the
            -- unprivileged trellis_app role, not the migration-owner role, so RLS actually
            -- applies (superusers and table owners bypass it outright). No UPDATE/DELETE
            -- grant at all — belt-and-suspenders alongside the append-only trigger above.
            GRANT USAGE ON SCHEMA trellis_audit TO trellis_app;
            GRANT SELECT, INSERT ON trellis_audit.audit_record TO trellis_app;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS audit_record_append_only ON trellis_audit.audit_record;
            DROP FUNCTION IF EXISTS trellis_audit.reject_audit_record_mutation();
            DROP TABLE IF EXISTS trellis_audit.audit_record;
            """);
    }
}
