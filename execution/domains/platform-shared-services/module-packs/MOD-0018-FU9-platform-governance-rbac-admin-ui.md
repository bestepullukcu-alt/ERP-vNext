---
id: MOD-0018-FU9
name: Platform Governance / RBAC Admin UI
slug: platform-governance-rbac-admin-ui
domain: platform-shared-services
parent_module: MOD-0018
status: ready-for-dev
owner: platform-team
branch: feature/governance/access-governance-execution
golden_reference: slim
form_field_count: 0
dates:
  started: 2026-06-11
---

# MOD-0018-FU9 — Platform Governance / RBAC Admin UI

> **Ready-for-dev note (Platform Governance / RBAC Admin UI V1).** Module-pack promoted `draft → ready-for-dev`. History:
> **DOC-FE0** authored the draft pack; **DOC-FE0-R1** locked the layout / tier / OD decisions; **DOC-FE0-R2** locked
> OD-FE9-03 to Option B (shared canonical default-role template) from the BE-B grant-source inventory; the promotion
> safety review **PASSED**. All required OD locks are complete (OD-FE9-01…06 **LOCKED**); **OD-FE9-07** (entitlement-aware
> MDM default grants) is a **deferred follow-up, not a V1 blocker**. Layout LOCK accepted. **No runtime, frontend,
> backend, gateway, or BOOT-FE work has started; no `.antigravity` change; no PR; no merge; `main` unchanged at
> `d3ab4a4`.** **`ready-for-dev` does NOT authorize runtime implementation — every atomic runtime group (BE-A, BE-B,
> GW-A, BOOT-FE, FE-A-core, FE-A-harden, FE-B, FE-C, FE-D, FE-E, FE-F) requires a separate user-approved step.** This is
> not `done`, not `implemented`, not `merged`.

> **Frontmatter convention note.** This pack follows the **frontend-pack frontmatter precedent** (`MOD-0009-FU03`
> Tenant Core UI, `MOD-0033-FU01` Tenant Quota Governance UI), which omits the backend-oriented `service` / `shell` /
> `entity_base` keys. This pack is intentionally **dual-shell** (tenant RBAC screens on `_LayoutTenantShell`; platform
> audit/self-explain on `_LayoutPlatformAdmin`), so a single frontmatter `shell` enum (`platform-admin | tenant | none`)
> cannot express it. The per-screen shell assignment is **locked in §3** and is authoritative. See OD-FE9-06.

---

## §1 Identity

- Canonical ID: **`MOD-0018-FU9`**
- Canonical name: **Platform Governance / RBAC Admin UI**
- Slug: `platform-governance-rbac-admin-ui`
- Canonical parent: **`MOD-0018`** (RBAC / ABAC Authorization — Blueprint-canonical foundation)
- Owner domain: `platform-shared-services`
- Registry reservation commit: **`7e09373`** (`docs(registry): reserve MOD-0018-FU9 platform governance UI`, pushed to origin)
- Identity basis: resolves the live FU9 prose references that reserve a separate RBAC Admin UI pack —
  `MOD-0018-FU10:206`, `MOD-0018-FU10:372`, `MOD-0018-FU12:231`. No new `MOD-xxxx`, no `MOD-0018-FU16`, no CAND-CAP, no
  EA identity decision required (FU child inherits the Blueprint-canonical parent `MOD-0018`).
- Pack status: **`draft`** — implementation **not started**; runtime not started; frontend not started; gateway not started.
- Merge state: **not merged to `main`** (`origin/main` `d3ab4a4`); **merge-freeze ongoing**; no PR.

## §2 Purpose and scope split

**Purpose:** deliver the Platform Governance / RBAC Admin UI **V1** over the existing access-governance backend
foundation (MOD-0018 family, `100%` backend permission foundation).

V1 surfaces, explicitly separated by audience:
- **Tenant-scoped RBAC management UI** — tenant administrators self-manage their own tenant's users/roles/permissions.
- **Platform-adjacent audit-labeling UI** — clarify the deny-only audit baseline for platform admins.
- **Platform self-effective-access UI** — authenticated subject inspects its own access (FU14 self-explain).
- **Shared status / error pages** — Error / Unauthorized / Forbidden / Not Found.

**This pack does NOT:**
- rewrite the backend permission engine (MOD-0018 family stays authoritative);
- replace backend `[HasPermission]` enforcement (frontend guard is UX-only — §10);
- make any frontend guard a security boundary;
- own Legal Entity implementation (MDM-owned — §6 / OD-FE9-04);
- write any business-domain UI.

## §3 Layout LOCK

**LOCKED. The platform and tenant shells stay separate. The per-screen assignment below is authoritative (overrides any
single frontmatter shell value).**

### Tenant shell — `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml`
V1 tenant screens (tenant_user with the tenant "Admin" role; AuthService is strictly tenant-scoped, no platform bypass):
- Users
- Roles
- User-role assignment
- Role-permission assignment
- Permission catalog viewer

### Platform shell — `frontend/Diten.Web/Views/Shared/_LayoutPlatformAdmin.cshtml`
V1 platform screens (platform_admin / partner_admin):
- Audit Logs labeling
- Self Effective Access

### Shared views / partials (hostable by both shells)
- Error · Unauthorized · Forbidden · Not Found
- confirmation · notification · empty-state · pagination · bounded warning / badge partials
- Existing both-shell precedent: `_GlobalConfirmation.cshtml`, `_GlobalNotification.cshtml`.

### Frozen / protected — DO NOT TOUCH
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN, archive layout)
- `frontend/Diten.Web/Views/_ViewStart.cshtml` (PROD-001 protected)
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`

### Forbidden
- landing-shell unification
- moving platform navigation into the tenant shell
- moving tenant navigation into the platform shell
- editing the frozen `_Layout.cshtml`
- editing the root `_ViewStart.cshtml`
- fabricating the actor / tenant split at the view layer (the split is derived from the live backend actor model)

Menu insertion points (editable): `_LayoutPlatformAdmin.cshtml` sidebar `<ul>` (lines ~195–248);
`_LayoutTenantShell.cshtml` sidebar `<ul>` (lines ~169–197). Each new entry is a `<li class="menu-item">` inside the
existing `<ul>` — no layout restructure.

## §4 Protected paths and ownership

| Path | Status | Owner | Rule |
| ---- | ------ | ----- | ---- |
| `frontend/Diten.Web/Views/Shared/_Layout.cshtml` | **Frozen** | — | Never edit; archive layout (AGENTS.md §4 / PROD-001). |
| `frontend/Diten.Web/Views/_ViewStart.cshtml` | **Protected** | — | Never edit (PROD-001 layout & view-start freeze). |
| `frontend/Diten.Web/Views/Shared/_LayoutPlatformAdmin.cshtml` | **Editable** | frontend-ui-ux | Add platform menu entry in existing `<ul>` only. |
| `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` | **Editable** | frontend-ui-ux | Add tenant menu entry in existing `<ul>` only. |
| `frontend/Diten.Web/Views/Shared/_Global*.cshtml` (shared partials) | **Editable** | frontend-ui-ux | Shared error/empty-state/notification partials. |
| `frontend/Diten.Web/Controllers/**`, `Views/Platform/{Module}/`, tenant views, `wwwroot/assets/js/**`, `Resources/**/*.resx` | **Editable** | frontend-ui-ux | Normal screen work; Platform/admin DataTables use the same-origin proxy-profile (server-side cookie→Bearer). |
| `gateway/Diten.ApiGateway/ocelot.json` | **Protected** | **integration-agent** | Route additions only via integration-agent in a separate atomic GW commit. |
| `services/Diten.AuthService/**` | **Editable (backend)** | backend-architect | BE-A / BE-B only (§8); separate atomic groups. |
| `execution/registries/module-id-registry.md` | **Controlled** | lead-architect | `MOD-0018-FU9` row already reserved (`7e09373`); no further edit by this pack. |
| `.antigravity/**` | **Protected** | — | User approval required; standards reconciliation is a separate step (§20). |

## §5 V1 screens (mandatory)

**Shared:** Error · Unauthorized · Forbidden · Not Found

**Tenant shell (`_LayoutTenantShell`):** Users · Roles · User-role assignment · Role-permission assignment · Permission catalog viewer

**Platform shell (`_LayoutPlatformAdmin`):** Audit Logs labeling · Self Effective Access

**Both shells:** canonical permission UX guard (§10)

## §6 V1.1 screens (reference-only)

| Screen | Shell | Note |
|---|---|---|
| Legal Entity | tenant | **MDM-owned dependency** (OD-FE9-04); not implemented under this pack. |
| Org Units viewer | platform | backend exists; needs GW route. |
| Positions viewer | platform | backend exists; needs GW route. |
| Manager Chain viewer | platform | backend exists (`positions/{id}/manager-chain`); needs GW route. |
| Data-scope summary | platform | derived from org/position. |
| Entitlement visibility improvements | platform | extends existing tenant module-entitlements surface. |
| Tenant self-explain host | tenant | requires a separate route off `/api/platform` admin-path + security review (OD-FE9-01). |

**V1.1 implementation does NOT block this pack's V1 runtime-start gate (§22).**

## §7 Deferred screens

Groups · Access Packages · Temporary Access · Approvals · Delegation · Cross-user Effective Access ·
Permission Analytics · Territory Scope · Field / Row Masking · SoD · **tenant audit viewer** (separate milestone —
the platform Audit UI is platform_admin-only).

## §8 Backend dependencies

### BE-A — Role permission checked-state endpoint (V1 blocker for Role-permission assignment)
**Missing:** an endpoint to read a role's current permission set. `RolesController` exposes assign
(`POST /api/roles/{id}/permissions`) and revoke (`DELETE /api/roles/{id}/permissions/{permissionId}`), and `GET /api/roles/{id}`
returns `RoleDto` with **`PermissionCount` only** — there is no way to render which permissions are checked.

Proposed narrow contract:
- `GET /api/roles/{id}/permissions`
- tenant-scoped (from JWT `tenant_id`); authenticated; canonical permission `auth.roles.read`
- returns: role id + permission IDs + canonical permission keys
- no grant mutation; no raw internal metadata
- **separate AuthService backend atomic group (BE-A)**; must complete **before FE-C** (Role-permission screen).

### BE-B — Non-default tenant default-role grant wiring (V1 blocker for tenant RBAC E2E)
**`OD-FE9-03 — LOCKED: OPTION B — SHARED CANONICAL DEFAULT-ROLE TEMPLATE`** (resolved by the BE-B read-only backend grant-source inventory).

**Confirmed gap:** `RoleProvisioningService.EnsureDefaultRolesAsync` injects only `IRoleRepository` and calls
`UpsertSystemRoleAsync` for "Admin"/"Viewer" — it creates the role records but **attaches no `RolePermission` rows**.
The canonical Admin→`auth.*`/`mdm.*` and Viewer→`*.read` grants exist only in `DataSeeder` for the default tenant
(`…0001`). A freshly provisioned non-default tenant Admin therefore gets **zero permission claims** → tenant RBAC is
dead-on-arrival even when the screens are built.

**Inventory grounding (bounded):** non-default provisioning creates Admin + Viewer roles but no RolePermission; the
default seed grant set is the established V1 baseline (Admin = all permissions in modules `auth` + `mdm`; Viewer =
canonical `*.read` only); the Permission catalog is global; `platform.*` is not in the catalog and must not enter a
tenant grant; `mdm.legal-entities.*` matches canonically on both the catalog and the MDM controller enforcement (post
Slice-1A); the Platform entitlement model is not consulted by current MDM enforcement or the grant path.

**LOCKED V1 shape (Option B):**
- Extract a **shared canonical default-role grant template seam** in AuthService used by **both** `DataSeeder` and
  `RoleProvisioningService` — a **single source-of-truth**; do **not** create two hardcoded lists.
- **Admin template** = modules `auth` + `mdm`, resolved through the global Permission catalog (per module), granting all
  active canonical lowercase-dotted keys in those modules.
- **Viewer template** = canonical `*.read` only, resolved through the same shared seam.
- **Excluded:** `platform.*`, legacy alias keys, raw permission tokens, cross-tenant grants.
- **No migration · no new entity · no new pack · no new MOD.** Entitlement-aware MDM gating is **out of V1** (deferred
  follow-up — OD-FE9-07).

**Idempotency / retry contract (behavior locked, not a specific catch location):** RolePermission provisioning is
tenant-scoped, retry-safe, and idempotent — a duplicate grant is a no-op, a partial run is recoverable by re-run, the
unique index `(RoleId, PermissionId, TenantId)` is preserved, bulk-assign is not required, an enclosing transaction is
not required, and cancellation is propagated. The implementation audit chooses the **narrowest existing-layer seam**
(repository-level idempotent assign helper, or an existing-row pre-check, or duplicate-key-tolerant no-op handling). **The
pack locks retry-safe behavior, not an arbitrary catch location.**

**Platform boundary:** `platform.*` is never added to a tenant-Admin grant. Platform APIs gate on `platform_admin` /
`partner_admin` actor type (with bypass); a `tenant_user` cannot reach the platform admin-path even if granted a
`platform.*` key — granting one is inert today and a **latent privilege-escalation risk**, so it is excluded. The
frontend must not fabricate `platform.*` keys into a tenant RBAC grant list.

**Auditability:** provisioning grants must use a **deterministic actor marker** (recommended `System/provisioning`); no
raw user or secret is written; tenant isolation is preserved. *If an existing bounded AuthService audit seam is available
during BE-B implementation, reuse it; if absent, report the audit-event gap before adding new infrastructure.*
`AssignedBy` must remain deterministic.

- **Separate AuthService provisioning atomic group (BE-B)** under the FU9 dependency; tests mandatory; must complete
  **before FE-C** (tenant RBAC screens).

## §9 Gateway dependencies

Gateway owner: **`integration-agent`** (only; `ocelot.json` is protected). All route additions land as separate atomic
GW commits.

### GW-A (V1) — Self-explain route
**Missing route:** `GET /api/platform/access/explain/me`
- backend endpoint exists (`AccessExplainController`, DTO complete — §11);
- gateway route **absent** (ocelot has 0 mentions of `access/explain`);
- must complete **before FE-E** (Self Effective Access UI);
- gateway edit is a separate atomic commit by integration-agent.

AuthService RBAC routes (`/api/users|roles|permissions/{everything}`) already exist in ocelot — no GW work for FE-C
proxying (sub-routes `/{id}/roles`, `/{id}/permissions` are covered by the `{everything}` catch-all).

### V1.1 gateway dependencies (reference-only — do NOT block V1)
- MDM Legal Entity (`/api/legal-entities/*`) — absent; also needs an MDM service port.
- Org Units (`/api/platform/organization-units`)
- Positions (`/api/platform/positions`)
- Manager Chain (`/api/platform/positions/{id}/manager-chain`)
- Data-scope summary

## §10 Canonical permission UX guard

**LOCKED boundary:**
- the frontend guard is **for UX visibility only**;
- backend `[HasPermission]` is the **sole authoritative enforcement**;
- canonical lowercase-dotted keys only (e.g. `auth.users.read`, `auth.roles.read`, `platform.audit.read`);
- no legacy alias is carried into the frontend; **no frontend alias map** is written;
- raw claims are never dumped to a view; a **bounded** permission snapshot / helper is used.

UX use cases: navigation visibility · route visibility · button visibility.

Candidate shape (live seams): add a bounded `Permissions` set to `PlatformProfileSnapshot` (today carries Roles only) from
the validated JWT `permission` claim, plus a server-side Razor helper `HasUxPermission("auth.users.read")`; the existing
`window.APP_PERMISSIONS` consumer (`permissions.js`) is currently never injected — inject a bounded set in the layout.

**Note:** even if frontend visibility is bypassed, backend enforcement must still deny access. Today the frontend uses
exactly one canonical key (`platform.tenants.quotas.view`), zero legacy PascalCase keys, and no alias map — this baseline
must be preserved.

## §11 FU14 Self Effective Access DTO contract

- Backend: `GET /api/platform/access/explain/me` (`[Authorize]` only; self-only; query `permissionKey` + `moduleCode`
  required, `featureCode` optional; subject/tenant from authenticated context).
- Primary V1 host: `_LayoutPlatformAdmin.cshtml` (as wired, the `/api/platform` admin-path middleware 403s tenant_user;
  a tenant host is V1.1 per OD-FE9-01).

Fields to render (DTO `SelfAccessExplainResponse`):
`Mode` · `PermissionSatisfied` · `RequiredPermission` · `PermissionMatch` · `MatchedViaLegacyAlias` · `ActorType` ·
`TenantId` · `ScopeKinds` · `ScopeCounts` · `ScopeNotes` · `TokenExpiresAtUtc` · `FreshnessNotes` · `DiagnosticFailure`

**Forbidden in the UI:**
- combined `Allowed` verdict (does not exist in the DTO — do not synthesize one)
- fabricated scope-applicable / scope-denied verdict
- raw JWT · raw claims · raw alias value · role inventory · raw scope IDs · raw exception text

**Render rule:** the permission observation (`PermissionSatisfied` / `PermissionMatch`) is rendered **separately** from
the descriptive scope observation (`ScopeKinds` / `ScopeCounts` / `ScopeNotes`). Empty scope is **not** a deny.

## §12 Audit Logs labeling baseline

AG-STEP-012 baseline: **deny-only**.
- Not all allow decisions are logged (`IEntitlementAuditSink` exposes only `LogDeniedAsync`).
- An explain-request audit is different from a deny-decision audit.
- The UI must state this explicitly via help-text / warning.
- **No backend change required — UI-wording only.**

Discriminators:
- explain-request: `EntityType = AccessExplain`, `Operation = Execute`
- deny-decision: `RequestType = EntitlementAccessDenied`, `Operation = PermissionDenied`, `Outcome = Denied`

List-DTO limitation: the list endpoint exposes `EntityType` / `Operation` / `Outcome` for filtering; `RequestType` may be
**detail-only** — so list-level distinction must use `EntityType` / `Operation` / `Outcome`.

Files (UI-wording only): `Resources/Views/Platform/AuditLog/AuditLogIndex.{en,tr}.resx` +
`Views/Platform/AuditLog/Index.cshtml` (help line). The `PlatformAuditController` is a transparent proxy — untouched.

## §13 Shell hardening dependencies

| Issue | Severity | V1 blocker | Atomic group |
| ----- | -------- | ---------- | ------------ |
| **FE-A1** Broken `/Home/Error` handler — `UseExceptionHandler("/Home/Error")` (`Program.cs:215`) targets a non-existent `HomeController`/`Views/Home`; the shared `Views/Shared/Error.cshtml` exists but is unreachable. | High | **Yes** | **FE-A-core** |
| **FE-A2** No 401 / 403 / 404 status pages — no `UseStatusCodePages*`; `ShellAccessFilter` returns a bare `ForbidResult`. | High | **Yes** | **FE-A-core** |
| **FE-A3** Eager refresh has no single-flight lock — `Program.cs:157-198` (rotating refresh-token race risk). | Medium | No (rollout hardening) | **FE-A-harden** |
| **FE-A4** `UseForwardedHeaders` absent — `Secure` cookie may drop behind a TLS-terminating proxy. | Medium | No (rollout hardening) | **FE-A-harden** |
| **FE-A5** Six controllers mutate the shared `HttpClient.DefaultRequestHeaders.Authorization` (cross-request token-bleed) — GoldenReferenceCompact/Slim, SubscriptionFeatures, SubscriptionPlans, ModuleCatalog, InterfaceRegistry; convert to per-request `HttpRequestMessage` (precedent: `PlatformAuditController`). | Medium | No (rollout hardening) | **FE-A-harden** |

**FE-A split:** **FE-A-core** (FE-A1 + FE-A2 — error handler + 401/403/404 status pages) is a **V1 implementation blocker**;
**FE-A-harden** (FE-A3 + FE-A4 + FE-A5 — refresh single-flight, forwarded headers, shared-`HttpClient` mutation removal)
is a **rollout blocker**. The two groups are **independently revertable**.

Safe-fix note: all FE-A-core and FE-A-harden fixes are achievable **without** editing the frozen `_Layout.cshtml` or root `_ViewStart.cshtml`.

## §14 Bootstrap / restore plan

Bootstrap is a **separate, explicitly authorized, network-gated step** (`BOOT-FE`) run **before** implementation. It is
**not** run during DOC-FE0.

### .NET
```
dotnet restore frontend/Diten.Web
dotnet build frontend/Diten.Web
```
(`Diten.Web` is currently un-restored — `dotnet build --no-restore` fails NETSDK1004; `dotnet build` is the typecheck;
there is no separate lint script.)

### JS
```
cd frontend/Diten.Web
npm ci
npm test
```
(Both `package-lock.json` and `yarn.lock` are present; prefer `npm ci` for reproducibility. `npm test` runs vitest/jsdom
against `wwwroot` assets.)

Rules: network-gated; run before implementation; no package update; no lockfile change; no autofix; no snapshot update;
`bin/` / `obj/` / `node_modules/` are gitignored; generated artifacts are never committed.

## §15 Atomic implementation groups

| Group | Purpose | Dependency | Owner | Gate | Rollback |
| ----- | ------- | ---------- | ----- | ---- | -------- |
| **DOC-FE0** | Draft pack authoring (this step) | — | module-pack-author | identity gate + `git diff --check` | delete the draft pack file |
| **BE-A** | Role permissions read endpoint (`GET /api/roles/{id}/permissions`) | — | backend-architect | AuthService tests | revert commit |
| **BE-B** | Shared canonical default-role grant template (one source-of-truth for `DataSeeder` + `RoleProvisioningService`) + RolePermission wiring so non-default tenant Admin (`auth`+`mdm`) / Viewer (`*.read`) get usable grants; retry-safe idempotent assignment (OD-FE9-03 Option B — no migration/entity/pack) | OD-FE9-03 LOCKED | backend-architect | AuthService build + provisioning-grant / retry-duplicate / tenant-isolation / token-claim tests | independently revertable AuthService atomic commit |
| **BOOT-FE** | Restore / install / baseline build + test | explicit network authorization | frontend-ui-ux | restore+build+`npm ci`+test green | no tracked diff expected |
| **FE-A-core** | Shell hardening — `/Home/Error` handler fix + 401/403/404 status pages (FE-A1, FE-A2) — **V1 implementation blocker** | — | frontend-ui-ux | routing + status-page render tests; frozen layout untouched | per-sub-commit revert |
| **FE-A-harden** | Rollout hardening — refresh single-flight lock, `UseForwardedHeaders`, remove shared `HttpClient.DefaultRequestHeaders` mutation → per-request `HttpRequestMessage` (FE-A3, FE-A4, FE-A5) — **rollout blocker** | — | frontend-ui-ux | auth/session regression; proxy header isolation; reverse-proxy config review; frozen layout untouched | per-sub-commit revert |
| **GW-A** | Gateway route additions (FU14 explain; V1.1 org/position later) | — | **integration-agent** | gateway build | revert ocelot hunk |
| **FE-B** | Canonical permission UX helper (bounded snapshot + Razor/JS helper) | FE-A-core | frontend-ui-ux | build + vitest | revert helper |
| **FE-C** | Tenant RBAC screens (Users · Roles · User-role · Role-permission · Permission catalog) | BE-A + BE-B + FE-B | frontend-ui-ux | build + tests | per-screen revert |
| **FE-D** | Audit Logs labeling (platform shell) | — | frontend-ui-ux | build | revert wording |
| **FE-E** | Self Effective Access UI (platform shell; bounded observations) | GW-A + FE-B | frontend-ui-ux | build + render tests | revert screen |
| **FE-F** | Frontend integration audit (build + tests + security review) | all FE | read-only-auditor / security-agent | full build + tests | n/a |
| **DOC-FE1** | Governance reconciliation (pack runtime note; roadmap if needed; `.antigravity` propagation handoff) | all above | module-pack-author | — | n/a |

## §16 Acceptance criteria

- [ ] Pack identity proven (`verify_module_id.py --check-id MOD-0018-FU9 --parent MOD-0018` OK; `--check-all` HARD violations 0).
- [ ] Layout split preserved (tenant RBAC → `_LayoutTenantShell`; platform audit/self-explain → `_LayoutPlatformAdmin`).
- [ ] Frozen paths untouched (`_Layout.cshtml`, root `_ViewStart.cshtml`, Archive/**).
- [ ] V1 screen tier locked; V1.1 reference-only; deferred screens explicit.
- [ ] BE-A and BE-B dependencies explicit and sequenced before FE-C.
- [ ] OD-FE9-03 locked as Option B; shared canonical default-role template is the single source-of-truth (`DataSeeder` + `RoleProvisioningService` use the same seam).
- [ ] Admin template = `auth` + `mdm` only; Viewer template = canonical `*.read` only; `platform.*` and legacy alias keys excluded.
- [ ] Non-default tenant Admin receives the expected grants; non-default tenant Viewer receives the expected read-only grants.
- [ ] Duplicate provisioning retry does not fail and does not create duplicate RolePermission rows; a partial run is safely re-runnable.
- [ ] Tenant isolation preserved across provisioning; no migration; no new entity; no new MOD.
- [ ] Entitlement-aware MDM gating explicitly deferred (OD-FE9-07); auditability marker deterministic.
- [ ] No runtime implementation in DOC-FE0-R2.
- [ ] GW-A owner explicit (integration-agent); FU14 route sequenced before FE-E.
- [ ] UX guard documented as non-authoritative; backend `[HasPermission]` is the sole gate.
- [ ] Frontend uses canonical lowercase-dotted keys only; **no frontend alias map**.
- [ ] FU14 UI renders permission and scope as **separate** observations; **no combined `Allowed`**.
- [ ] Audit deny-only baseline wording explicit in the UI plan.
- [ ] Shell-hardening issues (FE-A1…A5) explicit.
- [ ] Bootstrap network-gated and run before implementation.
- [ ] Atomic groups independently revertable.
- [ ] **No runtime implementation in DOC-FE0.**
- [ ] **No frontend diff in DOC-FE0.**
- [ ] **No gateway diff in DOC-FE0.**
- [ ] **No registry diff in DOC-FE0.**
- [ ] **No `.antigravity/**` diff in DOC-FE0.**

## §17 Test plan

- Identity gate: `verify_module_id.py --check-id MOD-0018-FU9 --name "Platform Governance / RBAC Admin UI" --parent MOD-0018`; `--check-all`.
- `git diff --check` clean (DOC-FE0).
- Frontend bootstrap baseline (BOOT-FE): `dotnet restore` · `dotnet build` · `npm ci` · `npm test`.
- AuthService BE-A tests: role-permissions read endpoint (tenant-scoped, canonical keys).
- AuthService BE-B tests: shared template resolves Admin → `auth`+`mdm`; Viewer → canonical `*.read` only; `platform.*` excluded; legacy aliases excluded; non-default tenant provisioning creates Admin grant rows and Viewer read-only grant rows; retry → duplicate assignment no-op and second provisioning run succeeds; partial failure → rerun completes missing grants; tenant isolation → other tenant grants untouched; newly provisioned Admin token carries the expected claims; cancellation propagated; `DataSeeder` uses the same shared template seam.
- Regression: MDM canonical permission tests remain green.
- Gateway: GW-A route validation (FU14 explain reachable; methods incl. OPTIONS).
- Frontend: screen tests · UX-guard tests · FU14 DTO render tests · no-combined-`Allowed` regression · status-page tests · audit-labeling render test.
- Final: integration audit (FE-F) · security audit · working tree clean.

## §18 Security constraints

- Backend enforcement authoritative; tenant isolation mandatory; no cross-tenant subject selector.
- No raw JWT / claim dump; no token logging.
- No frontend alias map; no role-inventory leak; no raw scope-ID leak; no fabricated scope verdict.
- No frozen layout edit (`_Layout.cshtml`); root `_ViewStart.cshtml` untouched.
- Gateway route edits only by `integration-agent`.
- Shared `HttpClient` header mutation removed before rollout (FE-A5); refresh race hardened before rollout (FE-A3);
  forwarded headers configured before reverse-proxy deploy (FE-A4).
- BE-B grant boundary: no overgrant beyond the established V1 baseline; no `platform.*` tenant grant; no legacy alias
  grant; no cross-tenant RolePermission; no duplicate RolePermission row; no hardcoded template duplication (single
  shared seam); no entitlement-aware behavior fabricated in V1; no seed/runtime drift; no raw permission-inventory leak
  to the frontend; stale tokens follow existing refresh / TTL behavior; retry idempotency mandatory; partial failure
  recoverable by re-run; `AssignedBy` deterministic (`System/provisioning`).

## §19 Non-goals

Business-domain UI · MDM Legal Entity implementation · Groups · Access Packages · Temporary Access · Approvals ·
Delegation · Cross-user explain · Permission Analytics · Territory · Field/Row Masking · SoD · tenant audit viewer ·
allow-decision logging runtime · entitlement-aware MDM default-role gating · DB-persisted grant-template entity ·
new migration · new access-package model · new group model · `platform.*` tenant grants · broad cross-service
provisioning abstraction · production deploy · `main` merge.

## §20 `.antigravity/**` propagation handoff

No `.antigravity/**` edit is made in this step. Future standards-reconciliation candidates (separate, **user-approved**
step):
- `.antigravity/rules/frontend-standards.md`
- `.antigravity/rules/permission-key-standard.md`
- `.antigravity/rules/routes.md`
- `.antigravity/rules/ports.md`
- `.antigravity/agents/frontend-ui-ux.md`
- `.antigravity/agents/integration-agent.md`
- `.antigravity/rules/module-pack-standard.md`
- root `AGENTS.md`

**Frontend-pack frontmatter archetype reconciliation (future `.antigravity` audit).** `module-pack-standard.md` carries a
backend-oriented *required* frontmatter checklist (`service` / `shell` / `golden_reference` / `entity_base`), but the
frontend-pack precedents (`MOD-0009-FU03`, `MOD-0033-FU01`) already omit several of those keys. For this **dual-shell**
FU9 pack an artificial single `service` / `shell` / `entity_base` value was deliberately **not** forced — the §3
per-screen shell assignment is authoritative instead (OD-FE9-06 LOCKED). The final `.antigravity/**` reconciliation should
evaluate either a documented **frontend-pack archetype exemption** from the backend-oriented keys, or a `shell: multi`
standard for dual-shell packs. No `.antigravity/**` edit is made in this step.

Final standards reconciliation will be a separate user-approved step.

## §21 Open decisions

> Decision-lock pass (DOC-FE0-R1 + R2): OD-FE9-01/02/03/04/05/06 are **LOCKED**. OD-FE9-03 was resolved by the BE-B
> grant-source inventory (Option B). OD-FE9-07 is a **DEFERRED FOLLOW-UP** (not a V1 blocker).

### OD-FE9-01 — Self Effective Access tenant hosting — **LOCKED**
- Option A: platform-shell only in V1.
- Option B: tenant-shell host in V1.1 after a route change (off the `/api/platform` admin-path) + security review.
- **LOCKED:** Platform-shell-only V1 — primary host `_LayoutPlatformAdmin.cshtml`, route `GET /api/platform/access/explain/me`. Tenant-shell hosting is deferred to V1.1 pending route and security review.

### OD-FE9-02 — BE-A endpoint shape — **LOCKED**
- Narrow `GET /api/roles/{id}/permissions` returning permission IDs + canonical keys.
- **LOCKED:** Narrow tenant-scoped role-permission read endpoint (`auth.roles.read`; returns role ID + permission IDs + canonical keys; read-only; no grant mutation; no extra internal metadata). Pagination is optional for V1.

### OD-FE9-03 — BE-B grant source — **LOCKED: OPTION B — SHARED CANONICAL DEFAULT-ROLE TEMPLATE**
- Option A: copy the `DataSeeder` hardcoded Admin list into provisioning (rejected — two source-of-truths, drift risk).
- Option B: extract a shared canonical default-role template seam used by both `DataSeeder` and `RoleProvisioningService`.
- **LOCKED (Option B)** — resolved by the BE-B read-only grant-source inventory. Lock summary:
  - Admin template = modules `auth`, `mdm`; Viewer template = canonical `*.read`.
  - Single shared seam used by `DataSeeder` + `RoleProvisioningService` (one source-of-truth).
  - `platform.*` excluded; legacy aliases excluded; cross-tenant grants excluded.
  - Retry-safe / idempotent provisioning required (no-op on duplicate; partial run re-runnable).
  - Entitlement-aware MDM gating deferred via OD-FE9-07.
  - No new migration / entity / pack / MOD.
  - See §8 (BE-B) for the full model. Ready-for-dev promotion is no longer blocked by this OD.

### OD-FE9-04 — Legal Entity UI — **LOCKED**
- Option A: reference-only V1.1 dependency in this pack.
- Option B: a separate `MOD-0220-FUxx` pack after an MDM identity audit.
- **LOCKED:** Legal Entity UI remains a reference-only V1.1 dependency (no V1 implementation in FU9). MDM-owned implementation requires a separate identity audit before any `MOD-0220-FUxx` reservation.

### OD-FE9-05 — Audit Logs labeling ownership — **LOCKED**
- Option A: FU9 adjunct UI-wording fix.
- Option B: a `MOD-0021`-owned separate UI pack.
- **LOCKED:** FU9 adjunct UI-wording-only labeling (help-text / badge) — no backend behavior change, no audit-filter change, deny-only baseline preserved, explain-request vs deny-decision distinction shown. Escalate to MOD-0021 ownership review if behavior, filtering, or allow-audit runtime changes.

### OD-FE9-06 — Dual-shell pack structure — **LOCKED**
- Option A: keep one `MOD-0018-FU9` pack covering both shells, with the §3 per-screen shell LOCK authoritative
  (frontmatter follows the frontend-pack precedent and omits a single `shell` enum).
- Option B: split the tenant-RBAC surface into a sibling pack so each pack has a single `shell`.
- **LOCKED:** One FU9 pack with authoritative per-screen shell assignments in §3. No shell unification and no forced single-shell frontmatter value; frozen `_Layout.cshtml` and root `_ViewStart.cshtml` untouched.

### OD-FE9-07 — DEFERRED FOLLOW-UP: entitlement-aware MDM default grants
- Current V1: `mdm.*` stays in the default Admin template; this aligns with the current claim-only MDM enforcement
  (entitlement is not consulted by the grant path or MDM enforcement today).
- Deferred questions: should `mdm.*` be included in the grant template conditionally on tenant entitlement/subscription?
  What contract links the Platform entitlement seam to AuthService provisioning? How are grant removal / refresh-token
  invalidation handled on an entitlement change?
- **DEFERRED — NOT A V1 BLOCKER.** No entitlement-aware runtime, cross-service contract, or new architecture is written
  now.

## §22 Runtime-start gate

This pack is a **docs-only draft**. The start gates are layered:

**Docs-revision gate (DOC-FE0-R2) — MET.** This decision-lock revision may complete with no runtime started; pack status
stays `draft`.

**Ready-for-dev promotion — UNBLOCKED.** All open decisions are resolved: OD-FE9-01/02/03/04/05/06 are **LOCKED**
(OD-FE9-03 = Option B), and OD-FE9-07 is a deferred follow-up that is **not** a V1 blocker. Promotion to `ready-for-dev`
is a **separate step** — this revision does **not** flip the status.

**BE-B runtime gate.** BE-B requires separate authorization; it lands as an independently revertable AuthService atomic
commit under the FU9 dependency.

**FE-C blocker (tenant RBAC screens).** FE-C must not start until **BE-A** has landed, **BE-B** has landed, and **FE-B**
(UX guard) has landed.

**FE-E blocker (Self Effective Access UI).** FE-E must not start until **GW-A** (FU14 route) has landed and **FE-B** has
landed.

**Rollout blocker.** Rollout must not start until **FE-A-harden** is complete and the final build / test / security audit
(FE-F) passes.

**Independently startable after promotion** (each with separate authorization): **BE-A**, **BE-B**, **GW-A**, **BOOT-FE**,
**FE-A-core**. Each V1 runtime group requires independent authorization; runtime stays fail-closed throughout; no runtime
implementation begins from this pack until the relevant gate is met.
