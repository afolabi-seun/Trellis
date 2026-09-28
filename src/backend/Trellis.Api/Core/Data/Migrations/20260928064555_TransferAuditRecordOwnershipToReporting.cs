using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trellis.Api.Core.Data.Migrations
{
    /// <summary>
    /// A schema-transfer, not a data migration (Phase 8's plan) — trellis_audit.audit_record
    /// never moves physically, is never dropped, and no row is copied; only which EF Core
    /// DbContext maps it changes (CoreDbContext to ReportingDbContext). The auto-generated
    /// DropTable this migration originally scaffolded (a byproduct of AuditRecord leaving
    /// CoreDbContext's model) was deleted and replaced with a no-op — the real, physical
    /// audit_record table plus its RLS policy, partitioning, and append-only trigger are all
    /// still owned by the original InitialCoreAuditRecord migration and remain completely
    /// untouched. This migration's only real effect is updating CoreDbContextModelSnapshot.cs.
    /// See ReportingDbContext's own InitialReporting migration for the matching other half — it
    /// picks the table back up into its own model without ever re-creating it either.
    /// </summary>
    public partial class TransferAuditRecordOwnershipToReporting : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("-- audit_record ownership transfer only; see ReportingDbContext's InitialReporting migration. No physical change.");

        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("-- ownership transfer rollback is a model-only change; no physical change to revert.");
    }
}
