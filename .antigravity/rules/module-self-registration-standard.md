# Module Self-Registration Standard (ERP-vNext)

> **MANDATORY for every tenant-assignable module.** A module is not "done" until it self-registers.
> The catalog is populated FROM CODE, never by hand. Manual "Add Module" is only for placeholder
> (not-yet-built / third-party) modules. See [[module-pack-standard]], [[permission-key-standard]].

## 0. Why
A module's identity, pages, and permissions live in code. The platform catalog (`platform_module_catalog`
+ page/action descriptors) must mirror that code with **zero drift**. So every module ships a
**ModuleManifestProvider** that declares itself; at startup it is reconciled into the catalog. Operators
never hand-create code-backed modules — they would drift and conflict.

## 1. Every module MUST ship a ModuleManifestProvider
Reference implementation: `GoldenSlimManifestProvider` (DevEnablement) and `WorkflowManifestProvider` (Platform).
The provider returns a `ModuleManifestDocument`:
- **ModuleCode** = clean, lowercase, slug (`workflow`, `document-management`, `legal-entity`). NOT the service
  (`platform` belongs in the `Service` field), NOT an infra namespace (`auth`/`platform`/`mdm` umbrella).
  ModuleCode and permission keys are INDEPENDENT — `workflow` may declare permissions `platform.workflow.*`.
  No permission rename is required to adopt a clean slug.
- **DisplayName / Domain / Service / ModuleVersion / SortOrder** = SOFT metadata (seed-once, then operator-owned).
- **IsTenantAssignable** = true for products tenants can subscribe to.
- **Pages[]** + each page's **Actions[]** (see §2).

## 2. ⭐ Mirror the FRONTEND, not just the API (the #1 drift trap)
The manifest must mirror the real **frontend controller view-routes AND the actual UI (row-action / toolbar
menus)** — NOT only the API controller. Mirroring the API alone misses UI-only pages (Designer, VisualDesigner,
Versions) and misplaces actions (e.g. "Start Instance" is a row-action on the *Definitions* list, even though
the API endpoint is `POST instances`).

For EACH frontend view-route → one **page** with:
`PageCode, DisplayName, RoutePath (verbatim), RequiredPermission (real [HasPermission] key), ParentPageCode,
IsNavigationVisible (true ONLY for the module's top-level nav entry; sub-pages reached from a parent = false),
PageType, SortOrder`.

For EACH button the UI actually shows (toolbar + row menu) → one **action** with:
`ActionCode, DisplayName, PermissionKey (real), placement (Toolbar | RowAction), IsDangerous`.
Place the action on the page **where the button actually is in the UI**, not where the API groups it.

Do NOT invent pages/actions/permissions: every `RequiredPermission`/`PermissionKey` must be a real
`*Permissions` constant the controller enforces (`[HasPermission(...)]`). If an operation has no endpoint
(e.g. workflow definitions have no delete/update), do NOT add that action.

### 2a. TAB ≠ PAGE — one page per ROUTE (decision A)
A page in the manifest = one **route**. **Intra-module tabs** (Bootstrap `data-bs-toggle="tab"` sections of a single
route — e.g. the Tenant detail's Overview/Access/Commercial/SystemMonitoring tabs) are **presentation, NOT pages**:
they share one route, so they are NOT modeled as separate manifest pages. The permissions that gate each tab's DATA
are the API endpoints the tab calls (`[HasPermission]` on those endpoints) — already auto-registered via A1
(platform `[HasPermission]` auto-registration), so they exist system-wide; they are simply not catalog "pages".
- A multi-route detail (e.g. workflow `…/Designer`, `…/Versions` are distinct routes) → one page EACH (those are
  routes, not tabs).
- Future tab-level catalog fidelity is an OPTIONAL additive extension (B): a `PageType="Tab"` child entry under the
  page (reuses the existing `ParentPageCode`), routeless, with the tab's view-permission. A→B is additive — no data
  migration, no regression (reconcile re-populates on restart; permission keys upsert idempotently). Do NOT pre-build B.

### 2b. Cross-module aggregator screens (e.g. WorkCenter) — NOT tabs, NOT one module
An aggregator screen that composes content from SEPARATE modules (WorkCenter's Inbox = MOD-0023 workflow tasks,
Task = MOD-0024 task engine) is neither (A) nor (B). Each source module self-registers its OWN pages/permissions
independently. The aggregator is a thin shell; its tabs surface other modules' content **gated by the source
module's permission/entitlement** (Inbox tab shows iff the tenant is entitled to workflow; Task tab iff task-engine).
Do NOT model the aggregator's tabs as its own pages. The aggregator may be its own thin module (a `/WorkCenter` page)
or a platform-shell feature — wired by entitlement, not by self-registering the borrowed content.

### 2c. ⭐ RoutePath IS the escalation boundary — Permission Scope (security-load-bearing)
Since İŞ3 (2026-07), every permission carries an explicit **`PermissionScope` (Tenant | PlatformAdmin)** that is the
SINGLE authorization signal for the tenant-vs-platform-admin boundary — NOT the Module name. On self-registration the
scope is **derived from each page's `RoutePath` at sync time** (`ModulePageDescriptorNormalizer.ScopeFromRoute`):
`/Platform/…` → **PlatformAdmin** (tenants get 403 via the shell access filter, permission is NOT tenant-assignable);
any other route → **Tenant** (permission IS assignable to tenant roles). So a correctly self-registering module gets the
right boundary AUTOMATICALLY — no hand-classification. This makes **RoutePath security-load-bearing, not just navigation**:

- A platform-admin-only page MUST have a `/Platform/…` route; a tenant-facing page MUST NOT. A wrong route silently
  MIS-SCOPES the permission → either a platform-admin capability leaks as tenant-assignable, or a tenant capability
  gets locked behind platform-admin. Double-check the route of any page whose data must not cross the boundary.
- **Never hand-seed a permission with an implicit scope.** Self-registration is the correct path (scope auto-derives).
  If a permission MUST be seeded in `DataSeeder`, pass an **explicit `scope:`** — do not rely on the legacy
  name-based `ClassifyScope` fallback (a Faz-0 bridge being retired; it guesses from a hardcoded module-name list and
  will mis-scope any module not in it).
- **Self-service exception:** a handful of tenant-admin self-management permissions (tenant security settings, tenant
  navigation/menu settings) are platform-namespaced yet must be tenant-assignable. These live in the explicit
  `DefaultRolePermissionTemplate.TenantSelfServicePermissions` allow-list — the ONLY sanctioned way to make a
  PlatformAdmin-routed key tenant-assignable. Do not widen the boundary any other way.
- If a page's route genuinely can't express its scope (rare — e.g. a tenant-facing feature that must stay
  platform-admin), that is a deliberate boundary decision: flag it to the owner, don't paper over it with a route hack.

## 3. Completeness test (MANDATORY)
Tests must assert BOTH directions, else missing pages/actions slip through silently:
1. **Every manifest page/action permission is a real `*Permissions` constant** (reflected). [already common]
2. **Every frontend controller view-route has a manifest page** (route count / route set match).
3. **Every UI toolbar+row-action button has a manifest action** (mirror the Details/index JS action menu).
4. Unique PageCodes, RoutePaths, ActionCodes; the top-level nav page is correct.

## 4. Wiring
- **Platform-hosted modules** → in-process: register the `IModuleManifestProvider` in Application DI
  (one line per module); `PlatformModuleSelfRegistrationWorker` reconciles each at startup via
  `IMediator.Send(RegisterModuleManifestCommand)` under `SetPlatformContext(Guid.Empty)`. No HTTP.
- **Other services (MDM, DevEnablement, …)** → HTTP push: `ModuleRegistrationHostedService` POSTs to
  Platform `/api/internal/module-catalog/register-manifest` with `X-Internal-Api-Key`.
- Reconcile is idempotent (create → update on re-push) and best-effort (one module's failure never blocks others).
- Reconcile is **AUTHORITATIVE, not additive**: on every push it PRUNES (soft-deletes, module-scoped) any live
  page/action the manifest no longer declares — so a moved/renamed/removed page or action (e.g. an action moved
  between pages) never lingers as an orphan (DB == manifest, both adds AND removals). Prune-first so a moved
  descriptor's route frees up for the new page in the same push. AuthService permissions are NOT deleted on
  prune (additive by design; permission removal is separate/riskier). NOTE: manifest unit tests do NOT catch
  reconcile-level orphans — a reconcile-state test (push A → push B-with-removal → assert DB == B) is required.

## 5. HARD vs SOFT + Origin (governance)
- Self-registered items carry `Origin = SelfRegistered`. **HARD** (code-owned): ModuleCode, pages, actions,
  permissions, ModuleName, ModuleVersion — re-pushed every startup; operators CANNOT hand-edit/delete these
  (guard: `MODULE_MANAGED_BY_CODE` 409). **SOFT** (operator-owned after seed): Domain, Service, DisplayName,
  SortOrder, IsTenantAssignable, Status, Description.
- A manual placeholder whose code later self-registers is **adopted** by ModuleCode match: HARD fields refresh
  from code, SOFT preserved, `Origin` flips Manual→SelfRegistered. The operator's placeholder code MUST exactly
  match the eventual manifest ModuleCode, or two entries result.

## 6. Lifecycle
Module catalog status lifecycle (promotion-only): `Draft→Preview→Beta→Active→Inactive⇄Active`, `Active→Deprecated`,
plus forward-jumps (`Draft→Beta/Active`, `Preview→Active`). No demotion. Self-registered create lands `Active`.

## 7. Ctrl+K search (data-driven, automatic) — FEAT-CTRLK-DYNAMIC-SEARCH
- A module's **nav-visible self-registered page** (`IsNavigationVisible: true`) is **automatically included in the
  tenant Ctrl+K global search** — NO separate work, NO static-JSON edit. The tenant shell's search is fed live
  from the tenant's data-driven navigation (`/TenantSearch/data` → `GET /api/platform/navigation/menu`), the same
  entitlement- + permission-filtered menu the sidebar renders. So a user only ever searches pages they can reach.
- Therefore: give every nav-visible page a **meaningful `DisplayName`** and a **real `RoutePath`** — those become
  the search result label and target. The module `DisplayName` is the search section header; the module `Icon`
  (see §catalog Icon) is the result icon. `ModuleCode`, module/domain names and page name are folded into search
  keywords automatically (so "roles", "access governance" and "administration" all surface the Roles page).
- Pages with `IsNavigationVisible: false` (UI-only Designer/Versions/detail routes) are intentionally NOT searched.
- (Platform-admin shell still uses the curated `platform-search.{culture}.json`; this data-driven feed is tenant-only.)
- (Future/optional) a manifest page MAY declare explicit search synonyms via a `SearchKeywords` field when it is
  added; until then keywords are derived from the names above.

## 8. Tenant nav localization (MANDATORY for tenant modules) — FEAT-NAV-L10N
- The tenant sidebar **and** Ctrl+K localize every nav node **generically, by its stable code** — there is NO
  hardcoded per-module map. The frontend (`Services/Navigation/NavNameLocalizer`) resolves each node in the request
  culture from `IStringLocalizer<SharedResource>`:
  - **Domain header** → `Nav.Domain.{Domain}` (the manifest `Domain` string verbatim, e.g. `PlatformSharedServices`)
  - **Module** → `Nav.Module.{ModuleCode}`
  - **Page** → `Nav.Page.{PageCode}` (for each nav-visible page)
- A tenant **rename** (Menu Settings `DisplayNameOverride`) is rendered **as-typed**, never localized (the nav DTO
  flags `ModuleDisplayNameIsOverride` / `DomainDisplayNameIsOverride`); a **reset** reverts to the localized default.
- Therefore **every self-registered TENANT module MUST ship its nav l10n keys** — `Nav.Module.{ModuleCode}`,
  `Nav.Page.{PageCode}` for each nav-visible page, and `Nav.Domain.{Domain}` **if it introduces a new domain** — in
  **all 7 tenant languages** (`en, tr, fr, es, zh, ar, ru`) in `frontend/Diten.Web/Resources/SharedResource.{lang}.resx`,
  via the same **l10n gate**. This is what makes a new module localize automatically — the lookup stays generic.
- **Graceful fallback (but a defect):** a missing key degrades to the English server default (never a raw key), so
  the menu never breaks — but a tenant module shipping without its keys is incomplete. The guard test
  `NavL10nContractTests` fails the build if a **current** nav code lacks a key in any of the 7 files.

## Ready-for-dev checklist (manifest)
- [ ] ModuleManifestProvider exists, ModuleCode = clean slug.
- [ ] Every frontend view-route → a page (incl. UI-only Designer/Versions/etc.).
- [ ] Every toolbar+row button → an action, placed where the UI shows it, real permission key.
- [ ] Route ↔ scope correct (§2c): platform-admin pages on `/Platform/…`, tenant pages not; no hand-seeded implicit scope; any self-service exception via `TenantSelfServicePermissions` only.
- [ ] Wired (in-process for Platform, HTTP for other services).
- [ ] Completeness tests (both directions) green.
- [ ] Restart → module + pages + actions appear in catalog with no manual add; re-restart idempotent (no dup).
- [ ] Nav-visible pages have meaningful DisplayName + RoutePath → auto-searchable in tenant Ctrl+K (§7); no JSON edit.
- [ ] Tenant module: shipped `Nav.Module.{ModuleCode}` + `Nav.Page.{PageCode}` (+ `Nav.Domain.{Domain}` if new domain) in all 7 tenant resx (§8); `NavL10nContractTests` green.
