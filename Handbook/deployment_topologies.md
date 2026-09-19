# Deployment Topologies

Everything else in this handbook assumes Trellis runs as a multi-tenant SaaS platform we
operate. This document specifies the second topology — a single customer's own on-premises
deployment — as a deliberately deferred capability: fully designed here so it's ready to
execute, not built or tested until a real prospect makes it a deal condition. That trigger
condition matters as much as the design itself, for the same reason Vault and per-tenant
encryption keys were deferred: maintaining a second deployment path is real ongoing overhead
(a version compatibility matrix, license infrastructure, customer-facing runbooks) that isn't
worth paying for against a hypothetical customer.

**Traceability note**: unlike every other BDR set in this handbook, BDR-LIC has never been
folded into a BRD, and deliberately stays that way — it's not in `Requirements/Trellis_BRD_v0.4.docx`
(the current version) or any version before it. The BDR-LIC IDs below remain proposed, not real
requirement IDs, until an actual qualifying enterprise deal triggers this capability (see
"Trigger to Actually Build This" below) — at that point they'd be folded into whichever BRD
version is current then, not retroactively into v0.2 or any other past version.

**Proposed BDR-LIC requirements** (not tied to any BRD version — deferred until triggered):

| ID | Requirement |
|----|-------------|
| BDR-LIC-001 | The platform shall support two deployment topologies — multi-tenant SaaS (default) and single-tenant on-premises — from the same codebase, selected by configuration |
| BDR-LIC-002 | An on-premises deployment shall enforce a cryptographically signed license file (expiry, unit cap, feature flags) in place of the SaaS billing flow |
| BDR-LIC-003 | An expired license shall trigger a stated grace period with a warning banner before the deployment degrades to read-only — never an immediate hard outage |
| BDR-LIC-004 | The platform shall enforce a license's unit/asset cap at the point of creation, returning a clear, actionable error rather than a silent failure |
| BDR-LIC-005 | An on-premises deployment shall default to no external telemetry or error-tracking egress unless explicitly enabled by the customer |
| BDR-LIC-006 | An on-premises deployment shall support disabling, or repointing to an approved endpoint, the Ai module's external provider dependency |
| BDR-LIC-007 | The platform shall provide a sanitized diagnostic bundle export for on-premises support use, without requiring direct database access |

---

## The Governing Principle

**One codebase, one config flag, no fork.** `Trellis:DeploymentMode = Cloud | OnPrem` is read
once at startup and drives which implementation gets registered behind interfaces that already
exist in the architecture for other reasons — this isn't new plumbing, it's the same seam used
for module extraction (`project_architecture.md`) and provider swapping (`billing_design.md`)
applied to one more axis:

| Concern | Interface | `Cloud` implementation | `OnPrem` implementation |
|---------|-----------|---------------------------|------------------------------|
| Payment / access enforcement | `IAccessGateway` | `PaystackBillingGateway` — usage metering, invoicing, dunning | `LicenseFileGateway` — signed file, no network required |
| Secrets | `ISecretProvider` | Cloud secrets manager | `FileSecretProvider` / `EnvSecretProvider`, or the customer's own vault |
| AI provider | `IAiProvider` | Default hosted extraction/routing provider | Configurable endpoint, or disabled entirely |
| Telemetry / error tracking | `ITelemetrySink` | Sentry | No-op by default; enabled only if the customer opts in |

Nothing about the module structure, the schema, or Row-Level Security changes between the two
columns. This is the direct payoff of building multi-tenancy as a first-class concern from the
start: an on-premises deployment is simply a Trellis instance with one row in
`trellis_tenant.tenant` — the RLS policies and `TenantAwareRepository<T>` from
`multi_tenant_design.md` don't need to know or care that they're the only tenant.

---

## Licensing (Replacing Billing On-Prem)

Paystack-based recurring billing assumes two things an on-premises deployment can't guarantee:
outbound internet access, and a relationship where non-payment means suspending a service we
host. Neither holds once the customer's own infrastructure runs the app — and enterprise
on-prem deals are, in practice, an annual contract invoiced outside the application entirely,
not a metered subscription.

### License File

A signed file (JWT structure, `RS256`, verified against a public key baked into the binary —
the private signing key never leaves Trellis's own systems):

```json
{
  "license_id": "uuid",
  "customer_name": "Acme Property Holdings",
  "issued_at": "2026-09-18T00:00:00Z",
  "expires_at": "2027-09-18T00:00:00Z",
  "unit_cap": 5000,
  "feature_flags": { "ai_module": true, "inspections_offline": false },
  "signature": "..."
}
```

No phone-home requirement — verification is a local signature check, so this works in a fully
airgapped environment (BDR-LIC-002).

```csharp
public interface IAccessGateway
{
    Task<AccessStatus> CheckAsync(Guid tenantId);
}

public class LicenseFileGateway : IAccessGateway
{
    public async Task<AccessStatus> CheckAsync(Guid tenantId)
    {
        var license = await _licenseReader.ReadAndVerifyAsync();  // signature check happens here
        if (license.ExpiresAt < DateTime.UtcNow)
            return license.ExpiresAt.AddDays(GracePeriodDays) < DateTime.UtcNow
                ? AccessStatus.ReadOnly     // grace period elapsed
                : AccessStatus.ActiveWithWarning;  // BDR-LIC-003

        return AccessStatus.Active;
    }
}
```

**Expiry never means a hard outage.** A 14-day grace period with a persistent warning banner
runs before the deployment degrades to read-only — an admin's renewal delay shouldn't lock a PM
company out of their own tenant data on a Friday afternoon. This mirrors the dunning
grace-period reasoning in `billing_design.md`, applied to license expiry instead of a failed
card charge.

### Unit Cap Enforcement

`unit_cap` is checked at the exact point `PropertyAsset` module creates a new `unit_asset` row
— the same enforcement point `billing_design.md`'s usage metering reads from, just gating
creation instead of driving an invoice:

```csharp
public async Task<ServiceResult<UnitAsset>> CreateUnitAssetAsync(CreateUnitAssetRequest request)
{
    if (!await _accessGateway.HasCapacityForAsync(request.TenantId))
        return ServiceResult<UnitAsset>.BusinessRuleViolation(
            ErrorCodeRegistry.PropertyAsset.LicenseCapExceeded);   // 422, per error_handling.md's table

    // ...
}
```

Reaching the cap is a normal, expected outcome, not an exceptional one — it returns the same
`BusinessRuleViolation` shape everything else in `error_handling.md` uses, with a message
pointing the admin to contact Trellis about a license upgrade, not a generic failure.

---

## What Else Changes

| Concern | SaaS (default) | On-Premises |
|---------|-------------------|----------------|
| Packaging | CI/CD auto-deploys to a managed PaaS (`devops_environments.md`) | Versioned Docker Compose bundle (app + Postgres) — customer's IT team (or our professional services) runs `docker compose pull && up -d` on their own schedule |
| Updates | Continuous, our pipeline | Point releases; migrations ship bundled and are applied by the customer, same idempotency rule as `data_model.md` |
| Backups / DR | Managed Postgres add-on, automatic | Customer's responsibility — Trellis provides a documented `pg_dump` + retention runbook, doesn't operate it |
| Observability | Sentry + structured logs we can query | Structured logs stay local by default; a **diagnostic bundle export** (sanitized logs, schema version, license status — BDR-LIC-007) is how support gets visibility without direct DB access |
| Frontend | Billing settings page (`billing_design.md`) | Replaced by a **License Status** view — expiry, unit usage vs. cap, feature flags — same route slot, gated by `DeploymentMode` the same way routes are gated by role in `frontend_architecture.md` |

---

## Trigger to Actually Build This

Not before an ENTERPRISE-tier prospect makes on-premises an explicit deal condition — most
plausibly a data-residency requirement, an airgapped network policy, or a regulatory
requirement none of our SaaS regions satisfy. When that happens, the build order is: the
`IAccessGateway`/`LicenseFileGateway` pair and license-signing tooling first (it's the one
piece with no partial version — a license either verifies or it doesn't), then the Docker
Compose packaging, then the diagnostic bundle export. The AI provider toggle and telemetry
no-op are cheap enough to build proactively as part of general seam hygiene, alongside the
`IPaymentGateway`/`ISecretProvider` abstractions already in place.

---

## Related Documentation

- [Project Architecture](./project_architecture.md) — the interface-and-DI pattern this document reuses for a third axis (topology, alongside module extraction and provider swapping)
- [Billing Design](./billing_design.md) — the SaaS-side `IPaymentGateway` this document's `IAccessGateway` parallels
- [Security Guide](./security_guide.md) — `ISecretProvider`, the abstraction the on-prem secret implementation plugs into
- [Error Handling & Resilience](./error_handling.md) — the `BusinessRuleViolation` shape used for license cap enforcement
- [DevOps & Environments](./devops_environments.md) — the CI/CD pipeline this document's release-artifact process departs from
- [Testing Strategy](./testing_strategy.md) — license expiry, grace period, and unit-cap enforcement all need explicit test coverage before this ships
- [Build Roadmap](./roadmap.md) — where this sits as a triggered, not scheduled, capability
