# Frontend Architecture

The OMS reference has no frontend document at all — it's a backend notification platform with
no end-user UI of its own. Trellis is the opposite: the Vendor cross-tenant login and the
Tenant Admin experience are the product, so this document has no reference equivalent to
adapt from and is written from scratch, following the same reasoning style as the rest of the
handbook (a stated decision, the trade-off, and what would trigger revisiting it).

**Stack decision: React 18 + TypeScript.** Chosen over Blazor for ecosystem size and hiring
flexibility if Trellis ever grows past a solo build, and over Next.js because Trellis's app is
entirely behind a login — there's no public, SEO-sensitive surface yet that server-side
rendering would help with (a future marketing site is a separate, much simpler static site,
not part of this app).

---

## One SPA, Role-Based Routing — Not Separate Apps Per Persona

A single React SPA serves all three personas (Tenant Admin / Department Staff, Vendor, Super
Admin), routed by the role claim already present in the JWT (see `security_guide.md`), rather
than three separately built and deployed frontends:

```
/                      → redirect based on role
/admin/*               → TENANT_ADMIN, DEPARTMENT_STAFF (department-filtered)
/vendor/*              → VENDOR_USER
/platform/*            → SUPER_ADMIN
```

One codebase is the right call for a solo build — three separately maintained SPAs would
triple the surface area for the same amount of building capacity, for personas that share a
design system and, in the Vendor case, a narrow feature set. If a persona's needs diverge
enough to justify a genuinely separate product (e.g., a Vendor mobile app — see below), that's
a new decision made at that point, not a default multiplied by three today.

**UI-layer scoping is UX, not security.** The Vendor route tree simply never renders a
property list, tenant list, or any navigation implying broader access — but this is a
usability choice, not a boundary Trellis relies on. The actual enforcement is server-side, at
the three layers described in `multi_tenant_design.md`; a Vendor user manually navigating to
an admin URL gets a 401/403/404 from the API exactly as if the UI didn't exist. Never treat a
hidden button or an unrendered route as a substitute for a server-side check.

---

## Repository & Deployment

**`trellis-web` lives at `src/frontend/` in the same repository as the backend** —
`project_architecture.md`'s "Repository Structure" has the full reasoning and the trigger for
reverting to two repos. In short: this used to be a separate repository alongside `trellis-api`,
but that split bought nothing a monorepo with path-filtered CI can't also provide (independent
deploy lifecycles, independent pipelines, no accidental coupling), while costing real
coordination overhead — two PRs per full-stack feature, a generated OpenAPI client needing its
own versioning between repos — that isn't worth paying for pre-revenue, on a single-person
build. The API's own OpenAPI spec remains the actual integration contract between the two
folders — see below — enforced by a CI lint rule against cross-imports between `src/backend`
and `src/frontend`, not by physical repository separation.

```
src/frontend/                 # trellis-web
├── src/
│   ├── api/                  # Generated typed client from the API's OpenAPI spec
│   ├── features/
│   │   ├── admin/            # Tenant Admin & Department Staff views
│   │   │   ├── leasing/      # One subfolder per backend module the persona can reach —
│   │   │   ├── vendor/       # matching project_architecture.md's Module Catalogue keys,
│   │   │   ├── billing/      # so a module's frontend slice and backend slice share a name
│   │   │   └── ...           # and ship together (roadmap.md's "which frontend slice ships
│   │   │                     # with which backend phase")
│   │   ├── vendor/           # Vendor portal views
│   │   └── platform/         # Super Admin views
│   ├── components/           # Shared, persona-agnostic UI components
│   ├── theme/                # Design tokens (see Design System below)
│   ├── auth/                 # Token storage, refresh flow, route guards
│   └── App.tsx
├── tests/
│   ├── unit/                 # Vitest + React Testing Library
│   └── e2e/                  # Playwright, tagged by BDR ID
├── vite.config.ts
└── package.json
```

Deploys to a static host/CDN (Vercel, Netlify, or the chosen PaaS's static hosting) — a
separate CI job and deploy target from the API's, path-filtered so a frontend-only change never
triggers a backend build or vice versa (`devops_environments.md`) — with its own environment
variable for the API base URL per environment (Local/Development/Staging/Production).

---

## API Integration

The frontend's types are **generated from the API's OpenAPI 3.1 spec** (`openapi-typescript` or
equivalent), not hand-maintained. This is the direct payoff of the OpenAPI commitment already
made in `api_standards.md`: a DTO change on the backend produces a type error in the frontend
build the moment the client is regenerated, instead of a silent runtime mismatch discovered by
a user.

- **Server state**: TanStack Query — handles caching, refetching, and loading/error states
  against the `ApiResponse<T>` envelope, rather than hand-rolled `useEffect` data fetching.
- **Client state**: kept minimal (React context or Zustand) for UI-only concerns — sidebar
  state, theme — deliberately avoiding a heavier state library, since most of what an admin
  portal like this needs is already server state.
- **Forms**: `react-hook-form` + `zod`, with validation schemas mirrored from the backend's
  request DTOs where practical, so client-side and server-side validation stay in sync.

---

## Authentication Flow

Matches the JWT design in `security_guide.md` exactly — the frontend doesn't invent its own
session model:

1. Login posts to `/api/v1/auth/login`; on success, the access token (15-min expiry) is held
   in memory (not `localStorage`, to reduce XSS exposure), and the refresh token is stored as
   an `httpOnly` cookie set by the API.
2. An axios/fetch interceptor attaches `Authorization: Bearer {access_token}` to every request.
3. On a 401, the interceptor attempts a silent refresh once before redirecting to login.
4. Route guards read the role claim to gate the `/admin`, `/vendor`, `/platform` trees — again,
   a UX convenience, not the actual security boundary.

---

## Module Entitlement Gating (per `billing_design.md`)

The login response's capability list (mentioned in `api_standards.md`'s Authentication section —
the JWT itself carries no `permissions[]` array) is extended with the tenant's currently `ACTIVE`
module keys from `trellis_billing.module_entitlement`. Route guards and nav rendering check
**both** the role claim and this list, same UX-not-security caveat as above — the real
enforcement is `RequiresModuleEntitlement` on the API side (`billing_design.md`).

- A module the tenant hasn't enabled: its nav item is hidden entirely, not just disabled — same
  reasoning as hiding admin nav from a Vendor.
- A direct hit on a disabled module's URL (bookmarked, or shared before the module was disabled)
  renders an "enable this module" upsell state, not a blank 404 — this is the one place the
  frontend deliberately treats a module-entitlement `403` differently from every other error in
  `error_handling.md`'s mapping, since it's actionable (Tenant Admin can enable it) rather than a
  fault.

---

## Design System

The pitch deck's brand palette becomes the app's starting theme tokens, so the product and the
sales materials feel like one company rather than two:

| Token | Value |
|-------|-------|
| `--color-navy` | `#1E293B` |
| `--color-paper` | `#FAF9F5` |
| `--color-sage` (accent) | `#5B8A72` |
| Fonts | DM Sans (headings), IBM Plex Sans (body) |

A component library (Mantine or a shadcn/ui-based setup) supplies the underlying primitives
(inputs, tables, modals) themed against these tokens, rather than building every control from
scratch — appropriate for a solo build that needs to look professional without spending weeks
on component-level polish.

### Accessibility (BDR-NFR-007, real as of BRD v0.4)

A handbook gap review found a real design system specified here with no stated accessibility
bar — worth closing before component-level decisions (which library, which tokens) get locked
in, since accessibility is far cheaper to build in than to retrofit onto an existing component
set. Now folded into `Requirements/Trellis_BRD_v0.4.docx`: *"The system's web interface shall
conform to WCAG 2.1 Level AA across the Tenant Admin, Department Staff, and Vendor
experiences."*

Concretely, this is a factor in the two open decisions already on the table, not a separate
workstream:

- **Component library choice** — both candidates (Mantine, shadcn/ui built on Radix primitives)
  have real accessibility support out of the box (keyboard navigation, ARIA roles, focus
  management on modals/menus) rather than needing it bolted on; this becomes a stated
  requirement of that choice, not just a nice property if it happens to be there.
- **Design tokens** — `--color-navy` on `--color-paper` and `--color-sage` as an accent both
  need their contrast ratios checked against WCAG AA's 4.5:1 (body text) / 3:1 (large text,
  UI components) thresholds before they're treated as final, not after a design partner is
  looking at a live screen.

Baseline expectations that apply regardless of library: every interactive element reachable and
operable by keyboard alone (this matters concretely for the Vendor portal's close-out flow,
which shouldn't require a mouse in the field); visible focus indicators, never suppressed for
aesthetics; every form input has a programmatically associated label (`react-hook-form`'s
`register` pattern already supports this, it just has to actually be used); and images/icons
that convey meaning (a status icon, not decorative flourish) carry accessible text.

Automated coverage lives in the Testing section below, not as a separate, easily-skipped
checklist — see `axe-core` in Testing.

---

## Responsive Behavior

Distinct from the native mobile app question below — this is about the SPA itself working
properly at phone and tablet widths, since the Vendor persona in particular is realistically
using a phone browser in the field, not a desktop.

| Breakpoint | Width | Priority |
|------------|-------|-----------|
| Mobile | < 640px | **Vendor portal (`/vendor/*`): full priority** — assignment list, work item detail, and close-out actions must be fully usable one-handed on a phone. Tenant Admin/Super Admin: usable, not optimized — core actions reachable, dense tables collapse to cards. |
| Tablet | 640–1024px | All three route trees fully usable — this is the realistic floor for Tenant Admin/Super Admin (a property manager walking a site with a tablet). |
| Desktop | > 1024px | Full layout — data tables, side-by-side panels, the primary experience for Tenant Admin and Super Admin. |

Mobile-first CSS (base styles target the smallest breakpoint, larger layouts are additive) —
whichever component library is chosen (Mantine or shadcn/ui, per the open decision above), its
own breakpoint tokens are mapped to this table rather than introducing a second, competing
breakpoint system.

---

## Application Shell: Footer

A persistent footer, present across all three route trees:

```
Trellis v{APP_VERSION} · © {YEAR} FusionAxis Integrated Services. All rights reserved.
```

- **`{APP_VERSION}`** is not hand-maintained — it's injected at build time from the git tag
  the CI pipeline is building (`v0.x.0`, same tag `devops_environments.md`'s pipeline promotes
  through Staging/Production), via a Vite `define` mapped to `VITE_APP_VERSION`. Local
  development falls back to `dev` or the current commit short-hash, never a hardcoded string
  that silently goes stale.
- **`{YEAR}`** is computed at render time (`new Date().getFullYear()`), not hardcoded, so it
  never needs a manual update.
- The version string doubles as a support diagnostic: paired with the correlation ID a user
  gets from a 500 error (`error_handling.md`), "which frontend build" plus "which request" is
  enough to reproduce almost any reported issue without guessing.
- Footer content is otherwise minimal by design for Phase 1 — no marketing links, no social
  icons. A Support/Status link is a reasonable Phase 2 addition once there's a real support
  channel to point to; nothing here should be built ahead of that being true.

---

## Offline Inspection Capture (PWA, Committed to Phase 1)

**Correction from an earlier draft of this document**: offline inspection capture was
previously deferred here as a triggered, future capability. That was a mistake to leave
unreconciled — BDR-INSP-003 and BDR-NFR-001 both mark offline data capture with sync-on-reconnect
as **Must-have** Phase 1 scope in the BRD, and the BRD's own Section 7.6 implementation
recommendation already anticipated exactly the right shape of solution ("a local-first data
model on the mobile client... local storage with queued background sync"). The deferral was the
engineering plan drifting from a decision already made, not a case where the BRD needed to
change — this section now builds it.

**The mechanism is a Progressive Web App, not a separate native app.** This is worth stating
explicitly because it wasn't the only option considered: a full React Native app (a second
codebase, app store distribution, a separate release process) would also satisfy the
requirement, but a PWA gets the same offline capability — installable, works without
connectivity, syncs on reconnect — from inside the existing `trellis-web` SPA, with no second
codebase to maintain. Since inspections are effectively single-writer (one Vendor or one staff
member completing one inspection at a time), the hard part of offline sync — concurrent
multi-user conflict resolution — doesn't apply here, which is what makes the PWA route
proportionate rather than a shortcut.

| Concern | Approach |
|---------|-----------|
| Offline shell & asset caching | A Service Worker, scoped to the `/vendor/*` and inspection routes, caches the app shell so it loads with no connectivity |
| Local-first data storage | IndexedDB (via Dexie.js) stores in-progress and completed-but-unsynced inspection records — checklist responses, notes, and captured photos (as blobs) — the moment they're entered, not just on submit |
| Sync on reconnect | A queued-submission pattern: each completed inspection is pushed to `POST /api/v1/inspections` with an `X-Idempotency-Key` (per `api_standards.md`) the moment connectivity returns, using the Background Sync API where supported and a connectivity-change listener with retry as the fallback everywhere else |
| Conflict resolution | Not a general CRDT/merge problem — one inspection has exactly one author, so a queued submission simply applies in full once it reaches the server; the only failure mode is a duplicate submission, which the idempotency key already handles |
| Photo capture while offline | Photos are captured and stored locally (IndexedDB, or the Cache API for larger blobs) and uploaded as part of the same queued sync, not held back for a separate flow |

This satisfies BDR-INSP-003 and BDR-NFR-001 as written, in Phase 5 of the build roadmap
(`roadmap.md`), not a later triggered phase.

**What's still a separate, genuinely deferred question**: a *native* app with app-store
presence, native push notifications, and deeper OS integration is a different thing from
offline capability, and nothing above requires it. That remains a real future option — see
`roadmap.md`'s "Triggered, Not Scheduled" table — worth revisiting only if app-store
distribution or native push specifically becomes a requirement, not because offline capture
does.

---

## Testing

| Layer | Tooling |
|-------|---------|
| Component/unit | Vitest + React Testing Library |
| End-to-end | Playwright, test names tagged with BDR IDs where a test verifies a specific requirement (mirrors the backend convention in `testing_strategy.md`) |
| Accessibility | `axe-core` (via `@axe-core/playwright`) run against the same Playwright suite above — an automated WCAG 2.1 AA scan on every page under test, not a separate manual pass that's easy to skip under deadline pressure (BDR-NFR-007, real as of BRD v0.4) |
| Visual regression | Deferred until the design system stabilises — premature before then |

---

## Related Documentation

- [API Standards](./api_standards.md) — the OpenAPI spec the frontend's typed client is generated from
- [Security Guide](./security_guide.md) — JWT structure and claims the auth flow depends on
- [Multi-Tenant Design](./multi_tenant_design.md) — why UI-layer role hiding is UX, not enforcement
- [DevOps & Environments](./devops_environments.md) — deployment targets and environment variables
- [Error Handling & Resilience](./error_handling.md) — how each error envelope shape is rendered in the SPA
- [Billing Design](./billing_design.md) — the Tenant Admin billing settings page, Super Admin revenue view, and the module entitlement list this SPA's nav/route guards read
- [Coding Standards](./coding_standards.md) — the `RowVersion`/concurrency-conflict pattern this SPA's forms need to handle
- [Build Roadmap](./roadmap.md) — which frontend slice ships with which backend phase
