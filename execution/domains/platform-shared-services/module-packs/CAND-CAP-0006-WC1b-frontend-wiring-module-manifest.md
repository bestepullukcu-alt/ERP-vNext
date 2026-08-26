---
id: CAND-CAP-0006
name: WC-1b — Frontend Wiring & Tenant Module Manifest (Görev Merkezi)
governance_identity: CAND-CAP-0006
charter: DCP-004
slice: WC-1b
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: none
entity_base: BaseEntity
status: ready-for-dev
status_changed: draft -> ready-for-dev on 2026-07-25 (EA/user-approved; condition 2 of CAP-001 §7)
owner: platform-team
branch: feature/pss/candcap0006-wc1-work-item-projection
started: 2026-07-25
target: TBD
form_field_count: 0
depends_on: WC-1 (commit 866bcbf3)
---

# WC-1b — Frontend Wiring & Tenant Module Manifest (Görev Merkezi / Task Center)

> **Identity (DCP-002):** governance identity `CAND-CAP-0006`, charter
> [DCP-004](../../../portfolio/delivery-capability-packs/DCP-004-work-aggregation-task-center.md) §8 row **1b**.
> No Blueprint `MOD-xxxx` is invented; `CAND-CAP-0006` **never** enters runtime code (DCP-002 candidate gate
> `verify_module_id.py --candidate CAND-CAP-0006` → exit 0, 2026-07-25). Runtime slug stays
> `work-aggregation` / `Features/WorkAggregation`.
>
> **This pack is `ready-for-dev`.** Per CAP-001 §7, DCP-004 is `approved` (condition 1) **and** this pack is
> `ready-for-dev` (condition 2, EA/user 2026-07-25) — implementation of the authorized scope (§5) may begin.
> Two external blockers remain outside this slice: the Ocelot route (§15) and the MOD-0018 grant/scope
> repair (§14 B1/B2). The surface cannot be declared live until both land.
>
> **Executable authority:** `frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/fixture-contract.js`. The WC-1
> payload must satisfy `validateWorkItem`; on any conflict **the contract wins**.
>
> **Branch:** same branch as WC-1 (`feature/pss/candcap0006-wc1-work-item-projection`) — one PR at the end.

## 1. Module Summary

WC-1 (commit `866bcbf3`) built the read-only projection and endpoint `GET api/v1/work-items/mine`, but nothing
consumes it and the module does not exist in the tenant catalog. **WC-1b makes Görev Merkezi visible and real**:

1. **Tenant module manifest / self-registration** (BL-022) — `work-aggregation` enters the module catalog, the
   tenant sidebar, and the entitlement/permission chain.
2. **Frontend wiring** — `/WorkCenterNext` switches from `mock-data.js` fixtures to the real API through a
   same-origin proxy (browser never touches a service port).
3. **7-language localization** — the resource keys WC-1 emits get real values in all seven tenant languages,
   plus stable-code nav keys.

Target user: any authenticated tenant user with `platform.work-aggregation.inbox.view`. After WC-1b, the live
surface shows **only workflow approvals** (the single bound provider) — every other tab is legitimately empty
until further providers land (§12 DEC-1).

This is not a CRUD/DataTable module: `golden_reference: none`, `form_field_count: 0`. No persisted entity is
created (`entity_base: BaseEntity` records posture only).

### Delivery slice

| Item | Included |
|---|---|
| `WorkAggregationManifestProvider` + DI registration + provider unit test | Yes |
| Same-origin proxy action on `WorkCenterNextController` | Yes |
| `app.js` mock → real-API seam + dev/QA showcase toggle | Yes |
| 7-language resx values for WC-1 keys + `Nav.*` stable-code keys | Yes |
| Removal of the hardcoded tenant-shell nav `<li>` (duplicate-nav fix) | Yes |
| Empty / 401 / 403 / unavailable UX states | Yes |
| **Any change to WC-1 backend (`Features/WorkAggregation`)** | **No** — consumed as-is |
| **AuthService permission grant/seed** | **No** — protected path; separate MOD-0018 task (§14) |
| **Ocelot route** | **No** — protected path; separate integration-agent task (§15) |
| New providers (Enterprise Strategy etc.) | No — BL-018 |
| Command execution (approve/reject from WorkCenter) | No — stays on MOD-0023 endpoints |

## 2. Ownership and Boundaries

### WC-1b owns

- The `work-aggregation` **manifest document** (module identity, pages, nav visibility, required permission).
- The **frontend data source seam**: proxy action, fetch, projection→presentation mapping, load/empty/error UX.
- The **tenant-facing localization** of WC-1's resource keys and the module/page/domain nav names.

### WC-1b does NOT own (consume, never re-implement)

| Concern | Owner |
|---|---|
| Work-item projection, status normalization, `actions[]` eligibility | **WC-1** (`Features/WorkAggregation`) — consumed unchanged |
| Approval semantics / command execution | **MOD-0023** existing endpoints |
| Permission grant to tenant roles, role templates | **MOD-0018 / `Diten.AuthService`** (protected) |
| Gateway routing | **integration-agent** (`ocelot.json`, protected) |
| Catalog reconcile / HARD-SOFT field policy | Module self-registration system |
| Entitlement decisions (which tenant gets the module) | Operator / subscription plan |

## 3. Owned Objects

| Object | Kind | Purpose |
|---|---|---|
| `WorkAggregationManifestProvider` | sealed class, `IModuleManifestProvider` | Declares module `work-aggregation` + its tenant page |
| DI line in `Application/DependencyInjection.cs` | `AddSingleton<IModuleManifestProvider, …>` | 7th provider alongside the existing 6 |
| `WorkCenterNextController.WorkItems()` | proxy action | `GET /WorkCenterNext/api/work-items` → gateway |
| `work-items-api.js` (or equivalent seam module) | JS | fetch + map API payload → presentation items |
| Resx values (7 langs) | `WorkCenterNextIndex.{lang}.resx` | WC-1 resource keys |
| Nav keys (7 langs) | `SharedResource.{lang}.resx` | `Nav.Domain/Module/Page.*` |
| `WorkAggregationManifestProviderTests` | xUnit | Zero-drift manifest test |

### Manifest specification (exact)

Record shape (verified — `Diten.BuildingBlocks.ModuleRegistration.Abstractions/ModuleManifestDocument.cs:15-31`):
`ModuleCode, ModuleName, DisplayName, Domain, Service, ModuleVersion, IsTenantAssignable, SortOrder, Pages, Icon = null, IsBaseline = false, NotificationEvents = null`

| Field | Value | HARD/SOFT |
|---|---|---|
| `ModuleCode` | **`work-aggregation`** — stable, never changes | **HARD** |
| `ModuleName` | `Work Aggregation` | HARD (refreshed each push) |
| `DisplayName` | `Görev Merkezi / Task Center` | SOFT (seeded once, operator-owned) |
| `Domain` | `Workspace` (§12 DEC-5) | SOFT |
| `Service` | `DitenPlatform` | SOFT |
| `ModuleVersion` | `1.0.0` | HARD |
| `IsTenantAssignable` | `true` | SOFT |
| `SortOrder` | `10` (top of sidebar — personal entry point) | SOFT |
| `Icon` | `bx-been-here` (matches today's hardcoded nav icon) | SOFT |
| `IsBaseline` | **`false`** (§12 DEC-4) | **HARD** (refreshed each push) |

Single page (`ModuleManifestPage`: `PageCode, DisplayName, RoutePath, RequiredPermission, ParentPageCode, IsNavigationVisible, PageType, SortOrder, Actions`):

| Field | Value |
|---|---|
| `PageCode` | `WORKCENTER` |
| `DisplayName` | `Görev Merkezi` |
| `RoutePath` | `/WorkCenterNext` |
| `RequiredPermission` | `WorkAggregationPermissions.InboxView` — the **WC-1 constant**, referenced, never re-typed |
| `ParentPageCode` | `null` |
| `IsNavigationVisible` | `true` |
| `PageType` | `List` |
| `SortOrder` | `10` |
| `Actions` | `[]` — WorkCenter executes **no** commands in this slice (approve/reject live on MOD-0023 pages) |

`/WorkCenterNext/Details/{id}` is **not** declared: it is a detail route reached from the list, and declaring a
templated route would add a nav-invisible descriptor with no permission distinction. Revisit only if
per-page permissions diverge.

## 4. Entity Fields

No entity. WC-1b consumes `WorkItemProjectionDto` (WC-1, unchanged). Field-by-field mapping to the presentation
model is in §12 DEC-2/DEC-3 and §16.

## 5. Repo Scope

### Backend (Platform)
- `services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/SelfRegistration/WorkAggregationManifestProvider.cs` **(new)**
- `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs` — one `AddSingleton` line appended to the existing manifest block (`:184-191`); extend, never fork
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/WorkAggregation/WorkAggregationManifestProviderTests.cs` **(new)**
- `services/Diten.Platform/src/Diten.Platform.Domain/Enums/ModuleCatalogDomain.cs` — add `Workspace` value (§12 DEC-5)
- `services/Diten.Platform/src/Diten.Platform.API/Program.cs` — **SCOPE ADDITION (EA-approved 2026-07-25)**: swap
  the registration order of the two hosted services at `:95`/`:97` so the module self-registration worker runs
  **before** the permission auto-registration worker (§12 **DEC-9**). Order swap only — no other edit to this file.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/ModuleDomainSeed.cs` — add the seed row (§12 DEC-5)

### Frontend (Diten.Web)
- `frontend/Diten.Web/Controllers/WorkCenterNextController.cs` — add `[Authorize]` + the proxy action
- `frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/app.js` — data-source seam only (`:95-98`, `:3204-3207`)
- `frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/work-items-api.js` **(new)** — fetch + payload→presentation mapping
- `frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/mock-data.js` — split the presentation mapper from the fixture source (§12 DEC-1)
- `frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/l10n.js` — additive named-token substitution (§12 DEC-3)
- `frontend/Diten.Web/Views/WorkCenterNext/Index.cshtml` / `Details.cshtml` — script registration for the new file
- `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` — **remove** the hardcoded WorkCenterNext `<li>` (`:195-202`) (§12 DEC-7)
- `frontend/Diten.Web/Resources/Views/WorkCenterNext/WorkCenterNextIndex.{en,tr,fr,es,zh,ar,ru}.resx` — new keys ×7
- `frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx` — `Nav.*` keys ×7
- `frontend/Diten.Web/wwwroot/assets/css/backbone-custom.css` — only if new state styling is needed, `.wcn-*` scoped classes only (FG-003)
- `frontend/Diten.Web/tests/workcenter-next-*.test.js` — extend (Vitest)

## 6. Protected Paths

- `services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/**` **except** the new
  `SelfRegistration/` folder — the WC-1 projection/provider/handler/controller is **consumed unchanged**. If a
  backend fix appears necessary: **STOP and report** (do not edit).
- `services/Diten.AuthService/**` — permission grant/seed is a separate MOD-0018 task (§14).
- `gateway/Diten.ApiGateway/**/ocelot.json` — integration-agent only (§15).
- MOD-0023 files (`Features/Workflow/**`, `Entities/Workflow/**`).
- Legacy `/WorkCenter`: `Controllers/WorkCenterController.cs`, `Views/WorkCenter/**`,
  `wwwroot/assets/js/WorkCenter/**`, `Services/WorkCenter/**`, `Models/WorkCenter/**` — frozen.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` — frozen archive layout (**not** `_LayoutTenantShell.cshtml`,
  which is in scope for the nav removal only).
- `.antigravity/**`, Blueprint `.xlsx`, `execution/registries/**`, `execution/portfolio/**`.
- Other domain services (`Diten.MdmService`, `Diten.EnterpriseStrategyService`, `Diten.DevEnablementService`).
- `services/Diten.Platform.Common/**`.

## 7. Dependencies

| Dependency | Use | Boundary |
|---|---|---|
| WC-1 (`866bcbf3`) | Projection DTO + `GET api/v1/work-items/mine` | Consumed unchanged |
| Module self-registration (`IModuleManifestProvider`, `PlatformModuleSelfRegistrationWorker`) | Catalog entry + nav + permission sync | In-process MediatR at startup; no env gate (`Program.cs:97`) |
| Catalog→Auth permission sync | Ensures the permission KEY exists | Grant is **not** covered (§14) |
| Entitlement→permission bridge | Grants the key to tenant Admin | Requires an entitlement + eventing transport (§14) |
| `NavNameLocalizer` | Stable-code nav localization | `SharedResource`, `Nav.{Domain,Module,Page}.{UPPER}` |
| Gateway (`GatewayUrl`, `http://localhost:5000`) | Proxy target | New route required (§15) |
| MOD-0018 | Effective permission for `actions[]` | Consumed; no computation in browser |

## 8. Runtime Constraints

1. **Browser never calls a service port.** All traffic goes browser → same-origin `/WorkCenterNext/api/*` →
   `GatewayUrl` (5000) → Platform. Ports `5056`/`5057` must not appear in any JS.
2. Read-only slice: no command endpoint, no state write. Approve/reject remain MOD-0023's.
3. The browser renders only the `actions[]` the projection supplies; it never invents or re-derives eligibility.
4. Payload must pass `fixture-contract.js` `validateWorkItem` (dev/test assertion — §17).
5. `TenantId`, actor and permissions are resolved server-side; the proxy forwards the JWT from the HTTP-only
   cookie, never from JS.
6. No inline CSS (FG-003) — `.wcn-*` classes in `backbone-custom.css` only.
7. `CAND-CAP-0006` and Blueprint `MOD-` literals never appear in code.
8. Legacy `/WorkCenter` stays byte-for-byte untouched and reachable.

## 9. Layout & Shell Contract

- `shell: tenant` → `Layout = "_LayoutTenantShell";` stated **explicitly** in `Index.cshtml` and `Details.cshtml`
  (already the case; must remain).
- View folder `Views/WorkCenterNext/`; routes `/WorkCenterNext` and `/WorkCenterNext/Details/{id}` (unchanged).
- After WC-1b the sidebar entry is rendered **data-driven** by `DynamicModuleMenuViewComponent` from the catalog,
  not by the hardcoded `<li>` (§12 DEC-7).

## 10. Backend File Convention

Mirrors the six existing providers exactly:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/
└── SelfRegistration/
    └── WorkAggregationManifestProvider.cs     (sealed class : IModuleManifestProvider)
```

- Namespace `Diten.Platform.Application.Features.WorkAggregation.SelfRegistration`.
- `GetManifest()` returns one `ModuleManifestDocument` literal; **every** permission value references a
  `WorkAggregationPermissions` constant (zero-drift, enforced by the test in §17).
- DI: append to the existing block in `Application/DependencyInjection.cs:184-191`:
  `services.AddSingleton<Contracts.IModuleManifestProvider, Features.WorkAggregation.SelfRegistration.WorkAggregationManifestProvider>();`

### Frontend proxy convention (mirrors `Platform/PersonReferencesController.cs`)

- `[Authorize]` on the controller class (WorkCenterNext currently has none — must be added).
- Inject `IHttpClientFactory` (unnamed default client), `IConfiguration`, `ILogger<T>`;
  `_gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/') ?? "http://localhost:5000"`.
- Same-origin route: `[HttpGet("/WorkCenterNext/api/work-items")]` → `{_gatewayUrl}/api/v1/work-items/mine`.
- Forward from the auth cookie: `Authorization: Bearer` (via `AuthTokenCookies.GetAccessToken(Request)`),
  `X-Tenant-Id`, `X-Correlation-Id`, `Accept-Language`.
- Return the upstream status/content verbatim (`ContentResult`); no token, no permission logic in JS.
- Failure mapping: no token → `401`; upstream unreachable → `503` with a controlled message.

## 11. Frontend File Contract

`golden_reference: none` (non-CRUD aggregation surface). Script order in both views stays:
`_L10n` → `l10n.js` → `fixture-contract.js` → fixtures → adapters/resolvers → `mock-data.js` →
**`work-items-api.js` (new)** → `app.js`.

### The seam (minimal, surgical)

| Location | Today | After |
|---|---|---|
| `app.js:95-98` | `items: data.buildItems()` etc. (synchronous mock load) | initialize empty; populated by the fetch |
| `app.js:3204-3207` | fake `setTimeout` flipping `loadState='ready'` | `await` the API, populate state, then `ready` / `error` |
| `app.js:2874-2876` | retry re-runs the fake success | retry re-issues the fetch |

`boot()` is already `async` and the `loading`/`ready`/`error` state machine, the error renderer and the retry
affordance already exist — no new scaffolding. Nothing downstream of `state.items` changes.

**Presentation mapper must be preserved.** `mock-data.js` currently owns both the *fixture source*
(`allFixtureGroups()`) and the *presentation mapper* (`toPresentation` + `tabFor`, `segmentFor`, `computeSla`,
`computeBlocked`, `getActions`, `resolveLabel`). Only the **source** is mock-specific. The mapper must be
retained and shared by the real path (extracted to a neutral module or re-exported), because the API returns
canonical work items in exactly the shape `toPresentation` consumes.

## 12. Design Decisions (locked by this pack)

**DEC-1 — Real data is canonical; showcase fixtures survive behind a dev/QA toggle.**
The API is the only source in normal operation. Showcase/canonical fixtures are **not deleted** (they are the
non-normative UX showcase and the resolver tests' input) but are loaded only when an explicit dev toggle is on.
No such toggle exists today (`hydrateStateFromUrl` whitelists params strictly), so one must be added: a
query param (e.g. `?fixtures=showcase`) accepted **only** in the Development environment, surfaced through the
view (a `data-` attribute set server-side from `IWebHostEnvironment`), never a client-only switch. Production
must have no path to fixture data.

**DEC-2 — `escalation`: no contract change; map to the existing `escalated` signal.**
`fixture-contract.js` does not read `escalation` (nor `title`, nor `dueAt`) — it is an unvalidated additive
field, so WC-1's payload violates nothing. The UI already consumes a boolean `escalated` (`mock-data.js:156`,
`app.js:45,61,62,171,318,319,504`) and renders the "Eskale" signal chip. The mapper therefore sets
`escalated = !!dto.escalation?.escalated` and may surface `escalation.level`. **No fixture-contract.js edit and
no WC-1 backend edit.** A formal contract field remains a contract-owner decision, deliberately not taken here.

**DEC-3 — Label args: extend the JS l10n helper (named tokens), not the backend.**
WC-1 emits `title` as `{kind:'resource', key:'WorkAggregation_Title_Approval', args:{objectType, objectId}}`
(a **named** dictionary), but `WCN.tf` does **positional** `{0}`/`{1}` substitution and `resolveLabel`
(`mock-data.js:47-51`) ignores `args` entirely — so the title would render with literal placeholders.
Resolution: add **named-token** substitution (`{objectType}` → value) to `l10n.js` and have `resolveLabel` pass
`label.args` through. Additive, backwards-compatible with positional `tf`, and requires no backend change.
The resx value is authored with named tokens accordingly.

**DEC-4 — `IsBaseline: false` + `IsTenantAssignable: true` (entitlement-gated).**
Baseline and assignable are mutually exclusive in the live code: baseline bypasses the entitlement wall
(`TenantModuleAccessService:53`) but baseline modules are excluded from the assignment list
(`GetTenantAvailableModulesForAssignmentQueryHandler:72`) and rejected for entitlement rows
(`TenantModuleEntitlementCommandSupport:26`). Non-baseline is chosen because it (a) preserves commercial
optionality, and (b) is the **only** option whose permission grant works without editing the protected
AuthService: the entitlement→permission bridge grants the module's catalog keys to the tenant Admin role,
whereas a baseline module would have to be hand-added to `DefaultRolePermissionTemplate` inside AuthService
(as `access-governance` and `tenant-settings` were). Consequence, stated plainly: **the module is invisible
until an operator entitles it to the tenant** (plan or manual). `IsBaseline` is HARD and refreshed on every
push, so flipping later is a one-line change.

**DEC-5 — New `Workspace` domain for nav grouping.**
No existing domain fits a personal work surface (`Administration`, `PlatformSharedServices`,
`DocumentManagement`, `MasterDataManagement`, `DevEnablement`); grouping Görev Merkezi under "Administration"
or "Platform Shared Services" would misinform tenant users. Cost is bounded and additive: one
`ModuleCatalogDomain` enum value, one `ModuleDomainSeed` row, and `Nav.Domain.WORKSPACE` in 7 languages.
`Domain` is SOFT, so an operator can regroup later without a code change.

**DEC-6 — Backend resource keys are added verbatim; the underscore style is deliberate.**
Existing WorkCenterNext keys are flat PascalCase (`StatusPending`), while WC-1 emits
`WorkAggregation_*`. The keys are added **as-is** rather than remapped, because they are contract values
supplied by the provider and the `WorkAggregation_` prefix namespaces them against UI-owned keys. No backend
change; the 7-language parity test governs them like any other key.

**DEC-7 — The hardcoded nav entry must be removed in the same change.**
`_LayoutTenantShell.cshtml:195-202` renders a static `<li>` for `/WorkCenterNext`
(`SharedLocalizer["WorkCenterNextMenu"]`). Once the manifest lands, `DynamicModuleMenuViewComponent` renders a
second entry → **duplicate nav**. The static block is deleted in the same commit, exactly as was done for
Access Governance and Tenant Settings. The now-unused `WorkCenterNextMenu` key (present in all 7 langs) is left
in place (harmless, and the legacy `WorkCenterMenu` entry beside it is untouched).

**DEC-8 — Explicit UX for every degraded state** (§13).

**DEC-9 — Fix the worker order in `Program.cs` (root-cause fix for B2). EA-approved 2026-07-25; scope addition.**
Verified: `Program.cs:95` registers `PlatformPermissionAutoRegistrationWorker` (A1) and `:97` registers
`PlatformModuleSelfRegistrationWorker`; both are `BackgroundService` with no start delay, so A1 wins the race. A1
syncs `moduleCode: null, scope: null`, and a **new** key created that way falls back to
`Module = key prefix ("platform")` + ctor-derived `Scope = PlatformAdmin`
(`InternalPermissionsController.cs:73-85`), which `SetScope` can never downgrade to `Tenant`
(`:146-152`, "most restrictive wins"). The manifest path computes the correct values
(`ScopeFromRoute("/WorkCenterNext")` → `Tenant`, `moduleCode: work-aggregation`), so **this is purely an
ordering defect**.

Resolution: swap the two registrations so the manifest worker runs first. Rationale for accepting the scope
addition instead of a one-off data fix: it repairs the **root cause** for every future module rather than this
one key; it is a one-line, additive change (the six existing modules are already registered, so A1's
`null/null` update path touches neither `Module` nor `Scope`); and the alternative — letting the key be created
wrong — is **irreversible** (manual role assignment stays permanently blocked because
`DefaultRolePermissionTemplate:117` requires `Scope == Tenant`).

Baseline evidence captured before any restart: `platform.work-aggregation.inbox.view` is **NOT PRESENT** in
AuthService, so the outcome is still determinable. Verification after the swap + restart is a hard acceptance
criterion (§16): stored `Module` must be `work-aggregation` and `Scope` must be `Tenant`.

## 13. Validation Rules / Failure Paths to Verify

| Scenario | Expected UX |
|---|---|
| **403** (permission not granted — the expected state before the MOD-0018 grant, §14) | Localized "no access" state; no raw JSON, no console error; nav entry hidden by the page permission gate |
| **401** (missing/expired token) | Redirect to login (existing tenant behavior); proxy returns 401 without calling upstream |
| **503 / gateway or Platform down** | `loadState='error'` + existing error panel + working Retry; never an infinite spinner |
| **Empty list** (user has zero approvals) | Meaningful empty state, not a blank grid |
| **Empty tabs** (İşlerim / Havuz / Geçmiş) | Localized "no provider yet produces this work type" message — an expected state, not an error (DEC-1) |
| **Payload fails `validateWorkItem`** | Dev/test build fails loudly with fixture id + rule; production drops the item rather than rendering a broken row |
| **Duplicate nav entry** | Must not occur — static `<li>` removed (DEC-7) |
| **Raw resource key visible in UI** | Test failure (`t()` falls back to the key, so this is detectable) |
| **Module not entitled** | Nav entry absent; direct `/WorkCenterNext` navigation shows the no-access state |

## 14. Authorization Convention

```text
Frontend controller: [Authorize]                            (tenant user; currently MISSING — must be added)
Backend endpoint:    [HasPermission("platform.work-aggregation.inbox.view")]   (WC-1, unchanged)
Manifest page:       RequiredPermission = WorkAggregationPermissions.InboxView
```

### Permission-seed verdict — **verified from code, not assumed**

**(a) Does the key exist in AuthService? YES — automatically, via two independent paths.**
1. `PlatformPermissionAutoRegistrationWorker` (`Program.cs:95`) reflects every `[HasPermission]` attribute and
   syncs it at every Platform startup — so WC-1 alone already pushes this key.
2. The manifest sync: `RegisterModuleManifestCommandHandler:141-145` → `CatalogPermissionSyncService:61-72` →
   AuthService `InternalPermissionsController:85-91`.
> The WC-1 comment "seed/grant lives in MOD-0018 … nothing here writes to AuthService"
> (`WorkAggregationModels.cs:11-12`) is **stale as to key existence** and should be corrected when that file is
> next legitimately edited (not in this slice — WC-1 is protected here).

**(b) Is it granted to a tenant user? NO. A separate MOD-0018 task IS required.**
The only automatic grant on creation is `FullCatalogPermissionGrantService` → **default-tenant SuperAdmin only**.
The tenant-Admin baseline (`DefaultRolePermissionTemplate.AdminModules`) is a curated allow-list
(`access-governance`, `legal-entity`) that does not include `work-aggregation`, and self-registration never
creates an entitlement row or publishes an entitlement event.

**Two blocking items for the MOD-0018 task (AuthService is protected here):**

- **B1 — Grant.** With DEC-4 (non-baseline), the entitlement→permission bridge
  (`EntitlementPermissionSyncService.GrantModuleWithKeysAsync`) grants the module's catalog keys to the tenant
  **Admin** role when the module is entitled. Note `Viewer` receives only `Action == "read"` keys, so an
  `…​.view` action does **not** reach Viewer — if Viewer access is wanted, that is an explicit MOD-0018 decision.
  In dev this path is gated on the eventing transport (RabbitMQ) or the provisioning path.
- **B2 — Scope-poisoning hazard (must be verified before declaring success).** The A1 worker syncs with
  `moduleCode: null, scope: null`, so if it creates the key first it lands as `Module="platform"`,
  `Scope=PlatformAdmin`. The later manifest sync **can fix `Module` but can never downgrade `Scope`** —
  `InternalPermissionsController:146-152` applies "most restrictive wins" and `SetScope` has no downgrade path.
  A `PlatformAdmin`-scoped key can never be assigned to a tenant role from the RoleAssignments UI
  (`DefaultRolePermissionTemplate:117` requires `Scope == Tenant`). Because WC-1 already shipped the
  `[HasPermission]` attribute, **the key may already be poisoned in any environment where Platform.API has
  started since `866bcbf3`.** WC-1b must therefore *verify* the stored `Module`/`Scope` and, if wrong, hand the
  repair to the MOD-0018 task. (The entitlement bridge filters by key set and does not check `Scope`, so B1 can
  still succeed while manual assignment stays blocked — verify both.)

## 15. Gateway / API Routing Decision

```text
Karar: Gateway değişikliği GEREKLİ → ayrı integration-agent task'i.
```

**Verified:** `gateway/Diten.ApiGateway/ocelot.json` contains **93 explicit routes and no generic catch-all**
(no `/{everything}`, `/api/{everything}`, or `/api/v1/{everything}`); `api/v1/work-items/**` is absent. The
sibling precedent is the explicit pair `/api/v1/workflow` + `/api/v1/workflow/{everything}` → `localhost:5057`.

Required route (authored by integration-agent, **not** by this pack):
`/api/v1/work-items/{everything}` → `/api/v1/work-items/{everything}`, host `localhost`, port `5057`,
methods `GET, OPTIONS`. Until it exists the proxy returns 503 — a release blocker, recorded as such.

## 16. Acceptance Criteria

### Manifest & catalog
- [ ] `WorkAggregationManifestProvider` matches the six existing providers' shape; `ModuleCode` is exactly
  `work-aggregation`; every permission value is a `WorkAggregationPermissions` constant (no string literal).
- [ ] Registered in `Application/DependencyInjection.cs` (7th `AddSingleton<IModuleManifestProvider,…>`).
- [ ] After Platform startup the catalog contains `work-aggregation` with one page `WORKCENTER` →
  `/WorkCenterNext`, `IsNavigationVisible: true`, `RequiredPermission = platform.work-aggregation.inbox.view`.
- [ ] Module is tenant-assignable and appears in the tenant assignment list (`IsBaseline: false`).
- [ ] After entitling the module to a test tenant, the sidebar shows **exactly one** Görev Merkezi entry
  (no duplicate — DEC-7).

### Permission chain (evidence-based, not assumed)
- [ ] The key exists in AuthService (cite the sync log/row).
- [ ] Stored `Module` = `work-aggregation` and `Scope` = `Tenant` — **explicitly verified** (B2). If it is
  `platform`/`PlatformAdmin`, the finding is recorded and handed to the MOD-0018 task; WC-1b is not declared
  complete on the basis of an assumption.
- [ ] A tenant Admin of an entitled tenant receives the permission claim and loads `/WorkCenterNext` (200).
- [ ] A tenant user without the permission gets the localized no-access state (not a raw 403 body).

### Frontend wiring
- [ ] `/WorkCenterNext` renders from `GET api/v1/work-items/mine` via the same-origin proxy; no service port
  (`5056`/`5057`) appears in any JS or network call from the browser.
- [ ] `WorkCenterNextController` carries `[Authorize]`; the JWT is read from the auth cookie server-side and
  never exposed to JS; `X-Tenant-Id` / `X-Correlation-Id` / `Accept-Language` are forwarded.
- [ ] Every returned item passes `fixture-contract.js` `validateWorkItem`; the browser adds no action.
- [ ] Backend DTO ↔ contract field names verified (camelCase serialization asserted by test, not assumed).
- [ ] `escalation` → `escalated` signal chip renders (DEC-2) with no contract file change.
- [ ] Titles render with substituted values, not literal `{objectType}` (DEC-3).
- [ ] Showcase fixtures reachable only via the Development-gated toggle; production has no path to them (DEC-1).

### Localization
- [ ] All 7 resx files (`en,tr,fr,es,zh,ar,ru`) contain an identical key set including every new
  `WorkAggregation_*` key; parity test green.
- [ ] Non-English values are real translations — no English placeholders (localization-standard).
- [ ] `Nav.Domain.WORKSPACE`, `Nav.Module.WORKAGGREGATION`, `Nav.Page.WORKCENTER` exist in all 7
  `SharedResource.*.resx`; the sidebar and Ctrl+K show the localized name, never a raw key.
- [ ] No raw resource key is visible anywhere in the UI.

### Gates & non-regression
- [ ] No inline CSS; new styling only via `.wcn-*` classes in `backbone-custom.css` (FG-003).
- [ ] `Features/WorkAggregation` (WC-1) unchanged apart from the new `SelfRegistration/` file.
- [ ] `ocelot.json`, `Diten.AuthService`, MOD-0023 files, legacy `/WorkCenter`, Blueprint, registry/ledger all
  unchanged.
- [ ] Legacy `/WorkCenter` still loads and behaves identically.
- [ ] `status: draft` until explicit approval; `CAND-CAP-0006` absent from all runtime code.

## 17. Test Expectations

### Backend (xUnit)
- `WorkAggregationManifestProviderTests` following the existing pattern: zero-drift permission oracle (reflect
  `WorkAggregationPermissions`, assert every `RequiredPermission`/`PermissionKey` is a real constant), identity
  asserts (`ModuleCode`, `DisplayName`, `Domain`, `Service`, `IsTenantAssignable`, `IsBaseline`, `Icon`),
  internal consistency (unique page codes/routes), and route↔permission pairing in both directions.
- Serialization test: `WorkItemProjectionDto` serializes to **camelCase** field names matching the contract
  (`workIntent`, `normalizedStatus`, `nativeStatus`, `workItemCapabilities`, `actionDepth`, …).
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug` → 0 errors.
- Existing WorkAggregation (23) + Workflow (109) suites stay green.

### Frontend (Vitest — `npm test` in `frontend/Diten.Web`, `tests/**/*.test.js`, `load-script.js` pattern)
- `workcenter-next-localization.test.js` already asserts exact 7-language parity — the new keys are covered
  automatically; extend its canonical-key list with the `WorkAggregation_*` keys.
- New test for the API→presentation mapper: a representative WC-1 payload maps to a presentation item with the
  correct tab/segment/SLA/`escalated`, and passes `validateWorkItem`.
- New test for named-token substitution in `l10n.js` (DEC-3), including the positional `tf` regression.
- Note: no existing test loads `app.js` (3215 lines, untested) — the seam change therefore has **no** existing
  safety net; the mapper and fetch logic must live in the separately testable `work-items-api.js`.

### Browser smoke (authenticated tenant session)
- `/WorkCenterNext` loads with real data, console clean, no service-port requests in the network tab.
- 403 path, empty-list path, and gateway-down (503 + Retry) path each verified.
- Sidebar shows one localized entry; Arabic RTL check on the surface.

## 18. Ready-for-dev Checklist

- [x] DCP-004 (approved) §8 row 1b, §4, §15 read.
- [x] WC-1 pack + shipped WC-1 code (`866bcbf3`) read; contract fields and resource keys extracted verbatim.
- [x] `fixture-contract.js` read; validated vs ignored fields established (`escalation`/`title`/`dueAt` ignored).
- [x] Existing manifest pattern read (all 6 providers + record shapes + reconcile HARD/SOFT policy).
- [x] Permission seed path traced end-to-end in code; verdict + two blocking items recorded (§14).
- [x] Gateway checked against real `ocelot.json`; verdict recorded (§15).
- [x] Frontend seam located precisely (`app.js:95-98`, `:3204-3207`) and proxy pattern captured.
- [x] l10n bridge, nav-localizer key format, and 7-language parity test located.
- [x] DCP-002 candidate preflight exit 0 (2026-07-25).
- [x] Design decisions DEC-1…DEC-8 resolved, not deferred.
- [x] User reviewed and set `status: ready-for-dev` (condition 2 of CAP-001 §7) — 2026-07-25.

## 19. Implementation Notes

Suggested order (each step independently verifiable):
1. Manifest provider + DI + provider test (backend only; no UI impact yet).
2. Start Platform; verify catalog row, page descriptor, and **permission `Module`/`Scope`** (B2). Stop here and
   report if the scope is poisoned.
3. Entitle the module to a test tenant; verify the Admin role receives the claim.
4. Proxy action on `WorkCenterNextController` (+ `[Authorize]`); verify with the real endpoint (503 until the
   gateway route exists — expected).
5. `work-items-api.js`: fetch + map + validate; unit-test it before touching `app.js`.
6. `app.js` seam (4 lines of state init + the boot block) + retry wiring.
7. Split the fixture source from the presentation mapper; add the Development-only showcase toggle.
8. `l10n.js` named tokens; resx keys ×7; `Nav.*` keys ×7; remove the hardcoded `<li>`.
9. Build, Vitest, browser smoke; then the audit note.

**Charter/backlog correction to raise separately** (governance edit, not this slice): DCP-004 §8 row 1b and
BL-022 both state the permission is "auto-seeded via catalog→auth sync". Per §14 that is true only of the
*key's existence*; the *grant* is not automatic. Those two documents should be amended to match the verified
behavior.

## 20. Follow-up Items

Not authorized by this pack:
1. **MOD-0018 task** — tenant-Admin grant policy for `work-aggregation` + scope repair if poisoned (§14 B1/B2).
2. **integration-agent task** — the `/api/v1/work-items/{everything}` Ocelot route (§15).
3. **BL-018** — Enterprise Strategy as a second provider (Binding A).
4. **WC-5 / WC-3 / WC-2 / WC-4** — provider registry, assignee resolver, working-time seam, notification seam.
5. **BL-019** — Blueprint canonical `MOD-xxxx` allocation (after WC-1 is proven).
6. **BL-020 / BL-021** — MOD-0023 pack reconciliation; Enterprise Strategy fixture-truth cleanup.
7. **Command execution from WorkCenter** — a separate approved slice with full authorization, concurrency and
   idempotency.
8. **Formal `escalation` field in `fixture-contract.js`** — contract-owner decision (DEC-2).
9. **Retiring the legacy `/WorkCenter`** — separate decision; it stays untouched here.
