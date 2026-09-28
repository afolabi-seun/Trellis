# Data Model

PostgreSQL 16 with Row-Level Security. A single database (`trellis_platform`) organised into
**14 logical schemas — one per module**. Each module owns its schemas exclusively via its own
EF Core `DbContext`, mapped only to that module's tables. No module's `DbContext` maps a table
it does not own — cross-module data needs go through that module's service class (see
`project_architecture.md`), never a shared `DbContext` or a cross-schema SQL join.

---

## Schema Catalogue

| Schema | Module | Tables | Notes |
|--------|--------|--------|-------|
| `trellis_tenant` | Tenant | 1 | tenant |
| `trellis_notification` | Notification | 3 | notification_template, notification_delivery, tenant_notification_channel_pref — see `notifications_design.md` |
| `trellis_auth` | Auth | 4 | app_user, session, password_reset_token, mfa_recovery_code — see `security_guide.md` |
| `trellis_property` | PropertyAsset | 3 | owner, property, unit_asset |
| `trellis_document` | Document | 2 | document, document_version |
| `trellis_workflow` | Workflow | 4 | department, workflow_template, workflow_instance, workflow_step |
| `trellis_inspection` | Inspection | 3 | inspection, inspection_item, inspection_photo |
| `trellis_vendor` | Vendor | 3 | vendor, vendor_user, vendor_assignment |
| `trellis_ai` | Ai | 2 | extraction_job, routing_suggestion |
| `trellis_audit` | Reporting | 1 | audit_record (append-only) |
| `trellis_billing` | Billing | 8 | subscription, payment_method, usage_snapshot, invoice, invoice_line_item, payment_transaction, webhook_event, module_entitlement — see `billing_design.md` |
| `trellis_leasing` | Leasing | 4 | vacancy_listing, application, screening_result, application_event — see `leasing_design.md` |
| `trellis_rentledger` | Rent Collection | 2 | rent_schedule, rent_payment_record — see `rent_collection_design.md` |
| `trellis_support` | Support | 1 | support_ticket — see `support_design.md`; Triggered, Not Scheduled (`roadmap.md`), not yet phase-built |

`trellis_reporting` (materialised views for dashboards and Owner reports) is deferred until
there is real usage data to build views against.

**Corrected during Phase 1 planning**: two catalogue entries never matched anything actually
specified elsewhere in the handbook. `tenant_config` was listed alongside `tenant` with no
column list or stated purpose anywhere — `tenant.config_json` (below) already covers the one
need ever described for it ("feature flags, tenant-level overrides"), so it's dropped rather
than built speculatively. `role_assignment` was listed under Auth, but role has only ever been
specified as a single `app_user.role` column — every JWT claim, the RBAC table
(`api_standards.md`), and the login flow (`security_guide.md`) all treat role as one value per
user, never a separate assignment table. Both were removed rather than filled in, per this
handbook's own "don't build ahead of need" principle applied to documentation, not just code.

---

## Core Table Definitions

### trellis_tenant.tenant

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| tenant_id | UUID | PK DEFAULT gen_random_uuid() | Primary tenant identifier — the PM company |
| tenant_name | VARCHAR(200) | NOT NULL | Registered business name |
| slug | VARCHAR(63) | NOT NULL UNIQUE | Public, human-friendly identifier used in public URLs instead of the raw tenant id (`/vacancies/acme-pm`). Lowercase letters/digits/hyphens, 3–63 chars, no leading/trailing hyphen, never a reserved word (admin, api, login, vacancies, …). Derived from `tenant_name` (with a numeric suffix on collision) when not supplied at provisioning; changeable only by a SUPER_ADMIN, audited, because a rename breaks every public link already shared. |
| country_code | CHAR(2) | NOT NULL | ISO 3166-1 alpha-2 (data residency, BDR-SEC-004) |
| status | VARCHAR(20) | NOT NULL DEFAULT 'ONBOARDING' | ONBOARDING\|ACTIVE\|SUSPENDED\|OFFBOARDING\|OFFBOARDED |
| subscription_tier | VARCHAR(20) | NOT NULL DEFAULT 'STARTER' | STARTER\|PROFESSIONAL\|ENTERPRISE |
| config_json | JSONB | NULLABLE | Feature flags, tenant-level overrides |
| created_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |
| updated_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |

Supports BDR-TEN-001, BDR-TEN-004, BDR-TEN-005. `OFFBOARDING` is the new interim value (BDR-TEN-006,
real as of BRD v0.4) between an active tenant and the terminal `OFFBOARDED` — see
`multi_tenant_design.md` for the full offboarding flow this status drives.

### trellis_tenant.tenant_domain

A tenant's own domain for its public pages — **ENTERPRISE tier only** (the trigger `README.md` names for
custom domains). Tenant-owned, so standard `tenant_isolation` RLS; the anonymous host resolver reads it
cross-tenant under `SystemContext` (the same narrow pattern as the Leasing link-token lookup). DELETE is
granted to the app role — removing a domain is a genuine delete, not a status flip.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| tenant_domain_id | UUID | PK | |
| tenant_id | UUID | NOT NULL | |
| domain | VARCHAR(253) | NOT NULL | Lowercased, punycode. Never an IP, a single label, or the platform's own host / a subdomain of it |
| status | VARCHAR(30) | NOT NULL | PENDING_VERIFICATION\|VERIFIED\|DISABLED |
| verification_token | VARCHAR(64) | NOT NULL | Random; the tenant proves DNS control by publishing TXT `_trellis-verify.{domain}` = `trellis-verify={token}` |
| verified_at | TIMESTAMPTZ | NULLABLE | |
| created_at | TIMESTAMPTZ | NOT NULL | |

`UNIQUE (domain) WHERE status = 'VERIFIED'` (partial index): anyone may *register* a name, but only one tenant
may hold it verified — the first to prove DNS control wins. A VERIFIED domain (a) resolves an anonymous host to
its tenant, (b) is echoed by CORS (dynamic policy, 60 s cache invalidated on verify/remove), and (c) is the base
URL of application links emailed to prospects.

### trellis_auth.app_user

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| user_id | UUID | PK | |
| tenant_id | UUID | NOT NULL (NULL for Vendor users — see below) | Owning tenant, for Tenant Admin / Department Staff |
| username | VARCHAR(100) | UNIQUE NOT NULL | Email address |
| password_hash | VARCHAR(255) | NULLABLE | Argon2id. NULL for SSO-only accounts |
| role | VARCHAR(30) | NOT NULL | SUPER_ADMIN\|TENANT_ADMIN\|DEPARTMENT_STAFF\|VENDOR_USER |
| department_ids | UUID[] | NULLABLE | For DEPARTMENT_STAFF — array of assigned department ids (BDR-DEPT-003) |
| status | VARCHAR(20) | NOT NULL DEFAULT 'PENDING_ACTIVATION' | PENDING_ACTIVATION\|ACTIVE\|LOCKED\|DEACTIVATED |
| failed_login_count | SMALLINT | NOT NULL DEFAULT 0 | |
| locked_until | TIMESTAMPTZ | NULLABLE | |

A `VENDOR_USER` row here holds only identity (username, password). Their actual access is
resolved entirely through `trellis_vendor.vendor_assignment`, not `tenant_id` — see
`multi_tenant_design.md`. There is deliberately no `owner` role: Owners do not log in for
Phase 1 (BDR-OWN-002).

### trellis_auth.session (revised — one new column)

`revoked_at TIMESTAMPTZ NULLABLE` (BDR-SEC-006, real as of BRD v0.4): a refresh attempt
against a session with `revoked_at IS NOT NULL` is rejected regardless of the session's natural
`expires_at`. Set on explicit logout, on "sign out of all other sessions," and automatically on
every successful password reset — see `security_guide.md`.

### trellis_auth.password_reset_token (new — BDR-SEC-005, real as of BRD v0.4)

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| token_id | UUID | PK | |
| user_id | UUID | FK → app_user NOT NULL | |
| token_hash | VARCHAR(64) | NOT NULL | SHA-256 hex of the raw token — the raw token itself is never stored |
| expires_at | TIMESTAMPTZ | NOT NULL | Issued-at + 30 minutes |
| used_at | TIMESTAMPTZ | NULLABLE | Set once; a used token is dead even if not yet expired |
| requested_ip | VARCHAR(45) | NULLABLE | Audit trail only, never part of validation. Plain text, not native `INET` as an earlier version of this table specified — Npgsql's EF Core provider won't map a C# `string` to `INET` without a value converter, and nothing here ever parses or compares it as an address, so the simpler type costs nothing (found at Phase 1 implementation). |

See `security_guide.md` for the full reset flow this table supports.

### trellis_auth.mfa_recovery_code (new — BDR-SEC-008, real as of BRD v0.4)

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| recovery_code_id | UUID | PK | |
| user_id | UUID | FK → app_user NOT NULL | |
| code_hash | VARCHAR(64) | NOT NULL | SHA-256 hex of the raw code — the raw code itself is never stored |
| created_at | TIMESTAMPTZ | NOT NULL | |
| used_at | TIMESTAMPTZ | NULLABLE | Set once; a used code is dead (single-use) |

10 rows are inserted per user at MFA enrollment and again, replacing all prior rows for that
user, on regeneration. See `security_guide.md` for the full flow, including the deliberately
manual fallback (a Support-mediated MFA reset) when both TOTP and recovery codes are lost.

### trellis_property.owner (schema filled in at Phase 2 implementation — previously only referenced as an FK target, never given its own columns)

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| owner_id | UUID | PK | |
| tenant_id | UUID | NOT NULL | Owning tenant |
| name | VARCHAR(200) | NOT NULL | |
| email | VARCHAR(255) | NULLABLE | |
| phone | VARCHAR(30) | NULLABLE | |
| status | VARCHAR(20) | NOT NULL DEFAULT 'ACTIVE' | ACTIVE\|ARCHIVED |
| created_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |
| updated_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |

Supports BDR-OWN-001. Never a hard delete (`ARCHIVED`, not removed) — a `property.owner_id` FK
must keep resolving even for a retired owner, and BDR-OWN-003's future reporting needs the
historical record either way.

### trellis_property.property

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| property_id | UUID | PK | |
| tenant_id | UUID | NOT NULL | Owning tenant |
| owner_id | UUID | FK → trellis_property.owner NOT NULL | |
| address | TEXT | NOT NULL | |
| property_type | VARCHAR(30) | NOT NULL | RESIDENTIAL\|COMMERCIAL\|MIXED_USE |
| status | VARCHAR(20) | NOT NULL DEFAULT 'ACTIVE' | ACTIVE\|ARCHIVED |
| size_sqft | NUMERIC | NULLABLE | Added at Phase 2 implementation — BDR-AST-002 (Must priority) names "size" among the required core attributes; the originally documented schema didn't have it |
| acquisition_date | DATE | NULLABLE | Added at Phase 2 implementation, same BDR-AST-002 gap |
| acquisition_value | NUMERIC(14,2) | NULLABLE | Added at Phase 2 implementation, same BDR-AST-002 gap |
| current_valuation | NUMERIC(14,2) | NULLABLE | Added at Phase 2 implementation, same BDR-AST-002 gap |
| created_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |
| updated_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |

Supports BDR-AST-001, BDR-AST-002. Never a hard delete, same reasoning as `owner` — BDR-AST-003's
documents/workflow history attach at this record and can't be left dangling.

### trellis_property.unit_asset

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| unit_asset_id | UUID | PK | |
| tenant_id | UUID | NOT NULL | Denormalised for RLS — always matches parent property's tenant_id |
| property_id | UUID | FK → property NOT NULL | |
| label | VARCHAR(100) | NOT NULL | e.g. "Unit 4B", "Rooftop HVAC" |
| status | VARCHAR(20) | NOT NULL DEFAULT 'ACTIVE' | ACTIVE\|ARCHIVED |
| size_sqft | NUMERIC | NULLABLE | Added at Phase 2 implementation, same BDR-AST-002 gap as `property` |
| acquisition_value | NUMERIC(14,2) | NULLABLE | Added at Phase 2 implementation, same BDR-AST-002 gap — acquisition_date omitted at this level, since a unit/asset's acquisition date is rarely tracked separately from its parent property's |
| created_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | Added at Phase 2 implementation — the originally documented schema had no timestamps at all, but keyset pagination needs a stable, meaningful ordering key |
| updated_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |

Documents, workflow instances, and inspections attach at the `property_id` or
`unit_asset_id` level (BDR-AST-003). Never a hard delete, same reasoning as `property`.

### trellis_vendor.vendor

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| vendor_id | UUID | PK | Global identity — NOT scoped to any single tenant |
| vendor_name | VARCHAR(200) | NOT NULL | |
| category | VARCHAR(50) | NOT NULL | MAINTENANCE\|LEGAL\|INSPECTION\|OTHER |
| created_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |

A single `vendor` row can be linked to many tenants via `vendor_assignment` — this is what
lets one vendor identity work across multiple PM companies on Trellis (BDR-VEN-004).

### trellis_vendor.vendor_assignment

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| assignment_id | UUID | PK | |
| vendor_id | UUID | FK → vendor NOT NULL | |
| tenant_id | UUID | NOT NULL | Which PM company assigned this work |
| work_item_type | VARCHAR(30) | NOT NULL | WORKFLOW_INSTANCE\|INSPECTION |
| work_item_id | UUID | NOT NULL | Polymorphic reference — resolved by work_item_type |
| status | VARCHAR(20) | NOT NULL DEFAULT 'ASSIGNED' | ASSIGNED\|IN_PROGRESS\|COMPLETED\|CLOSED |
| assigned_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |
| closed_at | TIMESTAMPTZ | NULLABLE | |

This is the table the Vendor cross-tenant access model is built on — see
`multi_tenant_design.md` for how it drives Row-Level Security and repository scoping.
Supports BDR-VEN-001–003, BDR-VEN-005.

### trellis_vendor.tenant_vendor_relationship (new — BDR-VEN-006, real as of BRD v0.4)

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| tenant_vendor_relationship_id | UUID | PK | |
| tenant_id | UUID | NOT NULL | |
| vendor_id | UUID | FK → vendor NOT NULL | |
| status | VARCHAR(20) | NOT NULL DEFAULT 'ACTIVE' | ACTIVE\|TERMINATED |
| terminated_at | TIMESTAMPTZ | NULLABLE | |
| terminated_by_user_id | UUID | FK → app_user NULLABLE | |
| | | UNIQUE (tenant_id, vendor_id) | |

Distinct from `vendor_assignment`: this table is the standing relationship between one tenant
and one Vendor identity, not any individual work item. A `TERMINATED` row gates new
assignments for that pair; it does not touch existing `vendor_assignment` rows, which keep
their own lifecycle unchanged — see `multi_tenant_design.md` for the full reasoning.

### trellis_document.document

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| document_id | UUID | PK | |
| tenant_id | UUID | NOT NULL | |
| property_id | UUID | NULLABLE FK → property | Linked record (one of property/unit_asset/owner/vendor) |
| unit_asset_id | UUID | NULLABLE FK → unit_asset | |
| document_type | VARCHAR(50) | NOT NULL | LEASE\|INSURANCE_CERT\|LICENCE\|INVOICE\|OTHER |
| current_version_id | UUID | FK → document_version | |
| expires_at | DATE | NULLABLE | Drives the expiry alert (BDR-DOC-003) |
| created_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |

### File scan status (BDR-SEC-007, real as of BRD v0.4)

`scan_status VARCHAR(20) NOT NULL DEFAULT 'PENDING'` (`PENDING|CLEAN|INFECTED|SCAN_FAILED`) is
added to `trellis_document.document_version` and `trellis_inspection.inspection_photo` — the
two tables that actually hold file bytes or a reference to them. A row with `scan_status !=
'CLEAN'` is excluded from every query path except one scoped to its own uploader, enforced at
the repository layer the same way tenant scoping is (`TenantAwareRepository<T>` in
`multi_tenant_design.md`), not left to each caller to remember. See `security_guide.md` for the
full validation and scanning pipeline this column supports.

### trellis_audit.audit_record (append-only)

| Column | Type | Description |
|--------|------|-------------|
| audit_id | UUID | PK |
| tenant_id | UUID | NULLABLE — NULL only for platform-level (Super Admin) actions |
| actor_id | UUID | User, Vendor, or system account |
| actor_type | VARCHAR(20) | SYSTEM\|HUMAN_USER\|VENDOR_USER |
| audit_type | VARCHAR(50) | See registry below |
| detail_json | JSONB | Structured detail |
| recorded_at | TIMESTAMPTZ | NOT NULL DEFAULT NOW() — immutable after insert |

`audit_type` values (extend as needed, keep additive): `TENANT_PROVISIONED`,
`USER_ROLE_CHANGED`, `DOCUMENT_UPLOADED`, `DOCUMENT_EXPIRY_ALERTED`, `WORKFLOW_STEP_APPROVED`,
`VENDOR_ASSIGNED`, `VENDOR_STATUS_UPDATED`, `AI_SUGGESTION_OVERRIDDEN` (BDR-AI-004),
`OWNER_REPORT_GENERATED`, `AUTH_EVENT`, `DATA_ERASURE`, `PASSWORD_RESET`, `SESSION_REVOKED`.

A database trigger rejects any `UPDATE` or `DELETE` on `trellis_audit.audit_record`, identical
in spirit to the reference architecture's append-only audit table.

### Retention & Archival (BDR-NFR-008, real as of BRD v0.4)

A handbook gap review flagged that "permanently append-only" was never paired with a retention
or archival plan — fine for a table with a few thousand rows, a real operational problem once
it has millions. Now folded into `Requirements/Trellis_BRD_v0.4.docx`: *"The system shall
support archiving audit log records older than a configured retention period to lower-cost
storage, without weakening the append-only guarantee on records within that period."*

**The retention period itself is a legal/compliance decision, not an engineering one** — POPIA
and the Kenya DPA are both data-minimisation-oriented rather than prescribing a fixed number of
years for this kind of processing log, and the right figure depends on which markets Trellis
actually operates in and what a data-protection advisor confirms is defensible there. *I'm not
a lawyer and this isn't legal advice.* A reasonable placeholder for planning purposes — common
in professional-services record-keeping — is **7 years**, but that number belongs in a tenant
or platform configuration value, never hardcoded, so it can be corrected without a migration
once confirmed.

**The architectural decision that *does* need making now, before Phase 1's first
migration**: `trellis_audit.audit_record` is created as a **range-partitioned table on
`recorded_at`** (one partition per quarter or year) from day one, even though no archival
automation runs yet. This is the one part of this plan that's much cheaper to decide upfront
than to retrofit — converting an existing, populated, non-partitioned table into a partitioned
one later is a real migration project; declaring it partitioned from the first `CREATE TABLE`
costs nothing today. The append-only trigger applies identically regardless of partitioning.

Actually detaching and archiving old partitions (exporting to compressed object storage,
keeping only recent partitions "hot" in the primary database) is deferred, same as
`devops_environments.md` defers custom backup tooling — revisit once real production data
volume, or a specific tenant's confirmed retention requirement, makes it worth building rather
than a table that just keeps growing unbothered.

---

## Row-Level Security

Every tenant-owned table gets a standard policy:

```sql
ALTER TABLE trellis_property.property ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON trellis_property.property
  USING (
    current_setting('app.is_system', true) = 'true'
    OR tenant_id::text = current_setting('app.current_tenant_id', true)
  );
```

**Corrected during Phase 1 implementation**: an earlier version of this policy cast the session
variable to `::uuid` (`tenant_id = current_setting(...)::uuid`) rather than casting the column to
text. That looked equivalent and even tested fine in isolation — but the *reason* it broke wasn't
obvious until it was hit for real: `set_config('app.current_tenant_id', NULL, false)` does **not**
unset the GUC to "never set" the way it reads. Postgres stores the NULL as an empty string, and
`current_setting(..., true)` still reports it as set (to `''`), not `NULL` — and `RESET` has the
same problem once a custom GUC has been set at all in that session. So any request with no tenant
context (a background job, a Vendor-scoped or platform-scope request) hit `''::uuid`, which is a
genuine SQL error ("invalid input syntax for type uuid"), not a filtered-out row — first
surfaced by `NotificationDispatchBackgroundService`, which legitimately has no tenant context at
all. Comparing as text instead means an empty string simply fails to equal any real `tenant_id`
— fail-closed, correctly, with no cast to throw. `Core/Data/TenantScopeConnectionInterceptor.cs`
sends an empty string (not attempting `DBNull`) for exactly this reason now.

`app.is_system` is the Super Admin / background-job bypass, set only by trusted internal code
paths — never by anything driven directly from a user-facing request.

**Vendor-visible tables** (`workflow_instance`, `inspection`, and their child tables) carry an
additional clause so a Vendor can see rows they've been assigned without being tenant-scoped
at all:

```sql
CREATE POLICY tenant_or_vendor_isolation ON trellis_workflow.workflow_instance AS w
  USING (
    current_setting('app.is_system', true) = 'true'
    OR w.tenant_id::text = current_setting('app.current_tenant_id', true)
    OR EXISTS (
      SELECT 1 FROM trellis_vendor.vendor_assignment va
      WHERE va.vendor_id::text = current_setting('app.current_vendor_id', true)
        AND va.tenant_id = w.tenant_id
        AND va.work_item_id = w.workflow_instance_id
    )
  );
```

(Not yet built — no Vendor module exists yet — but corrected here now so whoever builds it
doesn't rediscover the same bug independently.)

**RLS is active even for the application's own database user — it is defence in depth.**
Achieving that literally, though, needs one more piece this section didn't originally state:
`ENABLE ROW LEVEL SECURITY` alone does **not** apply to a table's owner, and does nothing at all
for a superuser — both bypass every policy outright, regardless of `FORCE ROW LEVEL SECURITY`
(which only extends RLS to a non-superuser owner, not a superuser). Found the hard way during
Phase 1 implementation: `docker-compose.yml`'s `POSTGRES_USER` is a superuser by default, and if
the running API connected as that same user, layer 1 would be silently inert in every
environment, and the isolation test suite's own "RLS holds even if the app-layer filter is
bypassed" case (`testing_strategy.md`) would pass without proving anything. The fix: every
environment provisions a **second, unprivileged role** (`trellis_app`, created via
`infrastructure/init-app-role.sql` locally) that the running API connects as — not the
migration-owner role, which stays privileged and is used only for `dotnet ef database update`.
Every table's migration grants that role exactly the DML it needs (no `UPDATE`/`DELETE` on
`audit_record` at all, for instance — belt-and-suspenders alongside its append-only trigger).
See `devops_environments.md`'s Configuration & Secrets section for the two-connection-string
pattern this implies, and `security_guide.md`'s Secrets Management table for where each
connection string's credential lives per environment.

The application layer also enforces the equivalent filter at the EF Core level (see
`multi_tenant_design.md`).

**`trellis_audit.audit_record` uses this same standard `tenant_isolation` policy**, despite
`tenant_id` being nullable there (platform-level Super Admin actions have no tenant) — worth
stating explicitly rather than leaving it to be rediscovered, since a nullable column in a
tenant-scoped policy looks like it should need a special case. It doesn't: SQL's three-valued
logic means `tenant_id::text = current_setting('app.current_tenant_id', true)` evaluates to
`NULL`, not `TRUE`, when `tenant_id IS NULL` — a `NULL` row is invisible to every ordinary
tenant request and visible only when `app.is_system = true`, exactly the behaviour a
platform-level audit row needs, with no policy variant required.

`IAuditService`/`AuditService` (the code that writes rows here) live in **`Core/`**, not a
`Reporting` module — `Reporting` itself isn't built until much later (`roadmap.md`), but every
module from Phase 1 onward needs to write an audit record. This is the same "shared capability
available from day one, moved into its own module's phase later without callers changing"
pattern `project_architecture.md`'s extraction seam already uses elsewhere — when `Reporting` is
eventually built, `IAuditService`'s home moves, not its interface.

---

## EF Core Conventions

**One `DbContext` per module, living in that module's `Infrastructure/Data/` folder**, each
mapped only to that module's schema — and, per `project_architecture.md`'s Clean Architecture
layering, mapped via `IEntityTypeConfiguration<T>` classes in `Infrastructure/Data/Configurations/`,
never via attributes on the Domain entity itself. The Domain entity (`Domain/Entities/Property.cs`)
has no idea a database exists; only the configuration class and the `DbContext` do.

```csharp
// Modules/PropertyAsset/Infrastructure/Data/PropertyDbContext.cs
public class PropertyDbContext : DbContext
{
    public DbSet<Property> Properties { get; set; }
    public DbSet<UnitAsset> UnitAssets { get; set; }
    public DbSet<Owner> Owners { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Scoped by namespace, not the bare typeof(PropertyDbContext).Assembly form an earlier
        // version of this example used — every module lives in this same single Trellis.Api
        // assembly (project_architecture.md's single-project decision), so an unscoped
        // ApplyConfigurationsFromAssembly(assembly) call would pick up every OTHER module's
        // IEntityTypeConfiguration<T> too and map their entities into this DbContext as a side
        // effect — exactly the cross-module leak this whole section exists to prevent. The
        // predicate keeps each DbContext scoped to its own module's configurations only.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PropertyDbContext).Assembly,
            t => t.Namespace?.StartsWith("Trellis.Api.Modules.PropertyAsset.", StringComparison.Ordinal) == true);
        // Each entity's table/schema/column mapping lives in its own
        // Infrastructure/Data/Configurations/{Entity}Configuration.cs — see project_architecture.md
        // RLS: app.current_tenant_id / app.is_system set by a DbConnectionInterceptor on every
        // connection open, not a SaveChangesInterceptor — RLS applies to reads too, not just
        // writes, so it has to run before any command, not just before SaveChanges (Core/Data/
        // TenantScopeConnectionInterceptor.cs)
    }
}
```

This is enforced even though every module currently runs in the same process: a
`PropertyDbContext` simply has no `DbSet` for `Vendor` or `Document`, so a compile-time
mistake ("just join across modules, it's the same database anyway") isn't possible by
accident — and the namespace-scoped configuration scan above means it can't happen by *runtime*
accident either, via a stray `IEntityTypeConfiguration<T>` for another module's entity getting
picked up. The Domain/Infrastructure split adds a second guarantee on top: even *within*
PropertyAsset, `Property` the class and `property` the table are defined in different files by
different layers, so a persistence detail (a renamed column, an added index) never requires
touching the entity that defines the business object.

Every mutable entity's configuration also calls `.UseXminAsConcurrencyToken()` — Postgres's
built-in `xmin` system column used as an optimistic-concurrency token, at zero schema cost. See
`coding_standards.md` for the full convention (BDR-NFR-006, real as of BRD v0.4); it's noted here because
it's an EF Core mapping detail, same category as everything else in this section.

### Migrations

EF Core migrations with `Npgsql.EntityFrameworkCore.PostgreSQL`. Each module's migrations
live under its own `Data/Migrations/` folder and are applied to the same `trellis_platform`
database in dependency order:

```
Tenant → Notification → Auth → PropertyAsset → Vendor → Document → Workflow → Inspection → Ai → Billing → Reporting → Leasing → RentLedger
```

(`Support`'s migration runs whenever its Triggered, Not Scheduled trigger fires — see
`roadmap.md` — so it's deliberately left out of this sequence rather than given a placeholder
position it may never reach.)

Billing sits ahead of Reporting in this order (not just alphabetically last) because
`roadmap.md` sequences it as Phase 7, one phase before Reporting's Phase 8 — this list is the
literal migration application order, so it has to match build order, not table-creation
convenience. Leasing and RentLedger sit last, matching Phases 9–10. Notification now sits
second, right after Tenant and ahead of Auth, because Auth's own password-reset flow
(BDR-SEC-005) needs somewhere to send an email from Phase 1 onward — see `notifications_design.md`
for why this moved earlier than a first read of the module list would suggest.

```bash
dotnet ef migrations add AddVendorAssignment --project src/Trellis.Api --context VendorDbContext
dotnet ef database update --context VendorDbContext --connection "$TRELLIS_DB_URL"
```

Migration scripts must be idempotent, same rule as the reference architecture.

---

## Related Documentation

- [Project Architecture](./project_architecture.md) — module boundaries, layering
- [Multi-Tenant Design](./multi_tenant_design.md) — RLS rationale, Vendor scoping in depth
- [Security Guide](./security_guide.md) — PII encryption, secrets
- [Testing Strategy](./testing_strategy.md) — how RLS policies get verified with real Postgres, not mocks
- [DevOps & Environments](./devops_environments.md) — migration dependency order in the CI/CD pipeline
- [Coding Standards](./coding_standards.md) — why entity mapping lives in Infrastructure, never on the Domain entity itself
- [Billing Design](./billing_design.md) / [Leasing Design](./leasing_design.md) / [Rent Collection Design](./rent_collection_design.md) / [Notifications Design](./notifications_design.md) / [Support Design](./support_design.md) — full table definitions for the schemas summarised only briefly above
- [Security Guide](./security_guide.md) — the password reset, session revocation, and file-scanning flows `password_reset_token`, `session.revoked_at`, and `scan_status` support
- [Build Roadmap](./roadmap.md) — the phase sequence built on this document's migration order
