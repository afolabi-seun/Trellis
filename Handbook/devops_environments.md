# DevOps & Environments

The OMS reference assumes a Kubernetes cluster, Istio, ArgoCD, and a dedicated Management
zone — appropriate for 6 independently deployed services at real scale. None of that is
justified for a modular monolith with no customers yet. This document picks the smallest setup
that's still disciplined: environment separation, migrations that don't require a human to run
them by hand, and enough observability to debug a production issue without SSH-ing in and
reading log files.

---

## Environments

| Environment | Purpose | Data | Deploys from |
|-------------|---------|------|----------------|
| **Local** | Individual development | `docker-compose` Postgres, seeded fake data | Manual (`dotnet run`) |
| **Development** | Shared integration point, always-on | Ephemeral, reset freely, seeded demo tenants | Auto-deploy on merge to `main` |
| **Staging** | Pre-release validation against BDR acceptance criteria; where design partners could eventually get a preview | Controlled, hand-built dataset (see `testing_strategy.md`) | Manual promotion from a tagged `main` commit |
| **Production** | Live tenants | Real tenant data | Manual promotion from a Staging-validated tag, approval-gated |

Four environments, not the reference's larger set, because Trellis doesn't yet have distinct
teams or compliance zones that would justify more separation. Staging and Production being
manually promoted (not auto-deployed) is the one deliberately conservative choice here — with
one person building this, an unreviewed auto-deploy to Production is pure risk with no
offsetting benefit.

---

## Hosting Recommendation

**Recommended for Phase 1: a managed PaaS with a managed Postgres add-on** (Railway, Render, or
Azure App Service + Azure Database for PostgreSQL are all reasonable candidates) rather than
self-managed Kubernetes. Reasoning:

- No paying tenants yet — infrastructure cost and operational time should track that, not the
  reference architecture's assumption of an already-scaled platform.
- A modular monolith is one deployable unit; the reference's Kubernetes/Istio investment exists
  specifically to orchestrate *many* independently deployed units, which Trellis doesn't have.
- All three candidate PaaS options support the one Phase 1 requirement that matters most: a
  managed Postgres instance with automated backups, so disaster recovery isn't a Phase 1
  build item (see Backups below).
- Migrating off a PaaS to a more controlled setup later, once there's real revenue and a
  reason to control infrastructure more precisely, is a well-understood move — this is a
  reversible decision, unlike the Vault/per-tenant-key deferrals which touch the data model.

Azure App Service is worth a slight preference if Trellis ever wants Azure AD B2C or other
Microsoft-ecosystem integrations, given the .NET 8 stack; otherwise Railway or Render are
faster to get running from zero. This is a call to make once ready to deploy anything — it
doesn't block scaffolding the solution locally.

---

## Branching Strategy

Trunk-based, with short-lived feature branches:

```
main                    — always deployable to Development
  └── feature/BDR-VEN-003-vendor-assignment-close
  └── fix/rls-policy-missing-on-document-version
```

- Every change lands via PR into `main`, even solo — a PR is where the Security Checklist in
  `security_guide.md` gets actually checked, not just referenced.
- No long-lived `develop` or per-environment branches. Staging and Production deploy from
  tagged commits on `main` (`v0.3.0`), not from separate branches, so there's one linear
  history to reason about.

---

## CI/CD Pipeline

**Two path-filtered pipelines, one repository.** `backend/` and `frontend/`
(`project_architecture.md`, `frontend_architecture.md`) are built, tested, and deployed by
separate CI jobs gated on which paths a PR actually touches — a frontend-only change never
triggers the backend pipeline below, and vice versa. This is what makes the monorepo decision
free: each app keeps its own pipeline and deploy target exactly as if it were a separate repo,
without the coordination cost two repos would add. The pipeline below is the backend
(`backend/`) one; the frontend's is a conventional Vite build (`vitest` unit tests, a
`playwright` e2e job, `eslint`/`prettier` lint) deploying to its own static host/CDN target
(`frontend_architecture.md`), on the same branch/tag triggers.

The `.github/` these workflows live in, and every path below, is relative to `src/` — the
workspace's `src/` subtree *is* this repository, per `project_architecture.md`'s Repository
Structure; `backend/**` here is `src/backend/` from the wider workspace's point of view.

```
PR opened/updated (paths: backend/**)   — Trellis.Tests lives under backend/, so this
                                          one filter already covers it
  → Restore + Build
  → Dependency/vulnerability scan (GitHub Dependabot alerts + CodeQL — gate on high/critical)
  → Unit tests
  → Integration tests (Testcontainers Postgres — see testing_strategy.md)
  → API/contract tests
  → Lint (dotnet format --verify-no-changes)
  → [merge gate: all green + 1 review, even self-review against the Security Checklist]

Merge to main
  → Same pipeline, plus:
  → EF Core migrations applied to Development automatically
  → Deploy to Development

Tag created (v0.x.0)
  → Manual approval step
  → EF Core migrations applied to Staging
  → Deploy to Staging
  → (Manual approval step)
  → EF Core migrations applied to Production
  → Deploy to Production
```

GitHub Actions is the assumed CI runner (matches the rest of the FusionAxis toolchain);
nothing here is GitHub-specific in a way that would resist moving to another runner.

**Dependency/vulnerability scanning** (added by the same gap review as `security_guide.md`'s
Security Checklist item it satisfies — a CI process addition, not a BDR-numbered requirement,
the same way the Lint step above carries no BDR ID) deliberately reuses what GitHub Actions
already provides rather than adding a new tool to
operate: Dependabot for known-vulnerable NuGet/npm packages, CodeQL for a baseline SAST pass on
both the .NET and TypeScript code. The gate is high/critical severity only — the same
"worth having now, not worth the noise of blocking on every low-severity advisory" reasoning
`security_guide.md` uses for auth rate-limiting's own trade-offs. A dependency alert below that
threshold is tracked, not blocking, until it's re-assessed at the next dependency-bump pass.

**Migrations are never applied by hand against Staging or Production.** The pipeline runs
`dotnet ef database update` per module in the dependency order defined in `data_model.md`
(`Tenant → Notification → Auth → PropertyAsset → Vendor → Document → Workflow → Inspection →
Ai → Billing → Reporting → Leasing → RentLedger`; Support is excluded, being Triggered, Not
Scheduled — `roadmap.md`), and every migration must be written idempotently (same rule as the
OMS reference) so a re-run after a partial failure doesn't corrupt state.

---

## Configuration & Secrets Per Environment

**Revised from an earlier version of this document**, which split "config" into committed
`appsettings.{Environment}.json` files and "secrets" into a separate mechanism (User Secrets
locally, a cloud secrets manager elsewhere). That split was reasonable but meant two config
systems to keep straight — and it's how a real dev-only Postgres password ended up committed in
`appsettings.Development.json` during Phase 0 scaffolding: "no secrets in this file" was a
convention nothing enforced. **Environment variables, uniformly, everywhere** — one mechanism,
not two — closes that gap by construction rather than by remembering the rule:

| Environment | Config & secrets source |
|-------------|--------------------------|
| Local | `.env` (gitignored, real values), loaded into the process's environment variables at startup via `DotNetEnv` |
| Development / Staging / Production | Environment variables set directly in the hosting platform's dashboard (Railway/Render/Azure App Service all support this natively) — no file on disk in any of these environments |

`.env` is **never committed with real values** — `.env.example` (structure only, placeholder
values) is the one exception, committed so a new environment can be stood up by copying it and
filling in real values locally, or by reading it as the list of variables a deployed environment
needs set. Nested keys use ASP.NET Core's double-underscore convention
(`ConnectionStrings__Trellis`, `Modules__Vendor__MaxAssignmentsPerDay`) — `IConfiguration`
resolves these identically to a `ConnectionStrings:Trellis` JSON path, so no code downstream of
`IConfiguration` (including every `{Module}Options` binding — `coding_standards.md`) changes
based on where a value came from.

This also simplifies `security_guide.md`'s Secrets Management: `ISecretProvider` now has one
implementation across every environment (read the environment variable), not a
User-Secrets-locally/cloud-SDK-elsewhere split — see that document for the updated
`EnvironmentSecretProvider`.

---

## Observability

Deliberately minimal for Phase 1, but not absent — this is the one area worth not skipping
even pre-revenue, because debugging a modular monolith with zero logging is much harder than
debugging one microservice with zero logging (everything lives in one process's log stream,
so structure matters more, not less):

| Concern | Phase 1 approach |
|---------|---------------------|
| Structured logging | Serilog, JSON output, one `CorrelationId` per request set in `Core/Middleware/` and included on every log line |
| Health checks | `/health` endpoint (ASP.NET Core `HealthChecks`) verifying DB connectivity; used by the PaaS's own uptime monitoring |
| Error tracking | A hosted error-tracking service (e.g. Sentry) wired to catch unhandled exceptions — cheap to add, high value the first time something breaks in Staging |
| Metrics/APM | Deferred — revisit once there's real traffic and a specific question ("why is this endpoint slow") worth instrumenting for, rather than instrumenting speculatively |
| Audit log | Already specified in `data_model.md` (`trellis_audit.audit_record`) — this is business-event auditing, not operational observability, and stays in Postgres regardless of hosting choice |

---

## Backups & Disaster Recovery

Handled entirely by the managed Postgres add-on's automated backup feature (point-in-time
recovery, typically 7–30 day retention depending on plan) rather than a custom backup job —
this is exactly the kind of undifferentiated operational work a PaaS should absorb during
Phase 1. Revisit only if a specific tenant's compliance requirement (e.g. a longer retention
window than the platform default) demands it.

---

## Related Documentation

- [Testing Strategy](./testing_strategy.md) — what runs at each CI stage
- [Security Guide](./security_guide.md) — the `ISecretProvider` abstraction referenced above, the Security Checklist item the dependency-scan gate satisfies, and the Incident Response procedure this pipeline's monitoring alerts can trigger
- [Data Model](./data_model.md) — migration dependency order
- [Project Architecture](./project_architecture.md) — the single-deployable-unit assumption this whole document is built on
