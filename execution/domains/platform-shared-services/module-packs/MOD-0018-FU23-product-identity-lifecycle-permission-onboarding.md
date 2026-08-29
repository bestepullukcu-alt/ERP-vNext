---
id: MOD-0018-FU23
name: Product Identity Lifecycle Permission Onboarding
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: GlobalEntityBase
status: review
owner: auth-owner / product-data-owner
branch: feature/pss/mod-0018-fu23-product-identity-lifecycle-permissions
started: 2026-08-29
target: ""
form_field_count: 0
parent_module: MOD-0018
consumer_module: MOD-0290
---

# MOD-0018-FU23 — Product Identity Lifecycle Permission Onboarding

> **Implementation state.** The Section 5 Auth runtime/test slice is implemented and under review on the current
> `origin/main` base. Tenant grant/assignment, credential, operational data, Production/Staging and push remain
> separate operations. Completion is deliberately not claimed until the owning MDM manifest publishes the exact
> eight lifecycle definitions and the resulting catalog/profile path is verified.
>
> **Identity proof.** Master 8.1 `Blueprint_Data!A19:AG19` assigns roles, entitlements and policies to canonical parent
> `MOD-0018 — RBAC / ABAC Authorization`. Registry inspection found no `MOD-0018-FU23` collision. The fail-closed DCP-002
> preflight passed on 2026-08-29:
> `verify_module_id.py --check-id MOD-0018-FU23 --name "Product Identity Lifecycle Permission Onboarding" --parent MOD-0018`
> → exit `0`, `OK MOD-0018-FU23: proven against Blueprint/registry`.
>
> **Golden Reference decision.** This is backend-only Auth catalog/grant onboarding. It has no CRUD page, Razor shell,
> DataTable or user form; therefore `shell: none`, `golden_reference: none` and `form_field_count: 0` are intentional.

## 1. Module Summary

This follow-up onboards exactly eight Product Identity lifecycle permission keys for the existing MOD-0290 Product /
Item / SKU Master module and shared `product-item-sku-master` entitlement. Four resources each receive exactly
`submit` and `retire`:

| Resource | Exact keys |
|---|---|
| Global Product | `mdm.global-products.submit`, `mdm.global-products.retire` |
| GSKU | `mdm.gskus.submit`, `mdm.gskus.retire` |
| LSKU | `mdm.lskus.submit`, `mdm.lskus.retire` |
| Finished Good | `mdm.finished-goods.submit`, `mdm.finished-goods.retire` |

No Product Definition Revision permission is introduced. The first Revision is governed by the paired first-GSKU
business outcome, and later Revision semantics do not have an approved independent lifecycle surface.

FU16-FU19 own the four resources' existing read/create onboarding. Native MOD-0023 Workflow owns approval/rejection and
WorkCenter dispatch through its existing provider/actions. FU23 adds no product-specific approve/reject key or endpoint.
It adds a composable special grant profile because generic Admin-full entitlement behavior would otherwise grant maker
and retirement powers to the same default role, while a Product Identity approver also needs a bounded set of existing
shared Workflow permissions.

## 2. Ownership and Boundaries

**In scope:**

- Auth-owned global catalog definitions for the exact eight Product Identity-owned keys.
- One dedicated `ProductIdentityLifecycleEntitlementGrantProfile` composed with the existing Product Abbreviation
  profile under the shared `product-item-sku-master` ModuleCode.
- Entitlement-aware reconciliation for Admin, Viewer and three exact Product Identity responsibility roles.
- Idempotent system-role creation, exact module-sourced grants, source-scoped revoke and restore.
- Active, missing, disabled, expired and uncertain entitlement behavior.
- Tenant isolation, non-system role-name collision protection and no automatic user-role assignment.
- Regression protection for FU16-FU20 and all adjacent non-lifecycle permissions.

**Out of scope:**

- MDM lifecycle entities, controllers, handlers, manifest, WorkCenter provider/actions, workflow, maker-checker domain
  implementation or Product Definition Revision behavior.
- Platform module catalog, entitlement engine or remote-provider runtime changes.
- Gateway, frontend, navigation, configuration, secrets, tenant data, token issuance or operational reconciliation.
- Any key outside the exact eight, including product-specific `approve`/`reject`, wildcard/manage/delete/bulk and every
  `mdm.product-definition-revisions.*` key.
- Re-owning, redefining or renaming the existing shared `platform.work-aggregation.inbox.view`,
  `platform.workflow.tasks.approve` or `platform.workflow.tasks.reject` catalog definitions.
- Automatic responsibility-role membership, default Admin/Viewer lifecycle access, SuperAdmin policy redesign or a
  permission bypass for platform actors.
- Production/Staging enablement.

MDM remains the record-level authorization and maker-checker owner. Auth permission possession never proves that a
specific actor may approve their own request or retire a record with active children.

## 3. Owned Objects

| Object / contract | Ownership and exact boundary |
|---|---|
| Eight Product Identity `Permission` definitions | Global Auth catalog records for submit/retire; Module=`product-item-sku-master`; tenant-assignable; no catalog `TenantId` |
| `ProductIdentityLifecycleEntitlementGrantProfile` | Exact key subsets, three dedicated role templates and composable selection rules |
| Tenant Admin desired grants | Existing four read/create pairs only; zero FU23 or shared-Workflow profile grants |
| Tenant Viewer desired grants | Existing four reads only; zero create, FU23 or shared-Workflow profile grants |
| `ProductDataSteward` | System role; four resources' exact `read + create + submit` matrix |
| `ProductIdentityApprover` | System role; four existing product reads plus three existing shared WorkCenter/Workflow permissions; no product approve/reject key |
| `ProductIdentityRetirementSteward` | System role; four resources' exact `read + retire` matrix |
| Role membership | Explicit operator-owned assignment through existing Auth flows; FU23 creates none |
| Entitlement revoke/restore | Matching `GrantSource.Module` and `SourceModuleCode=product-item-sku-master` only; manual/system/other-module grants preserved |

No new collection or index is introduced. Existing `Permission`, `Role` and `RolePermission` contracts are reused.
The existing `ProcessedIntegrationEvent` document gains a completion-protocol marker so repository/profile failures
cannot poison an EventId as falsely complete. Legacy documents without the marker are unconfirmed and lazily replayed.

## 4. Entity Fields

| Existing object / profile value | Exact rule |
|---|---|
| `Permission.Key` | One of the exact eight lowercase dotted Product Identity keys; global uniqueness; no alias/wildcard |
| `Permission.Module` | Exactly `product-item-sku-master` |
| `Permission.Resource` | Exactly `global-products`, `gskus`, `lskus` or `finished-goods` |
| `Permission.Action` | Exactly `submit` or `retire` |
| `Permission.Scope` | `Tenant`; catalog definition remains global and has no `TenantId` |
| `Role.Name` | Exact ordinal names `ProductDataSteward`, `ProductIdentityApprover`, `ProductIdentityRetirementSteward` |
| `Role.IsSystem` | `true`; an existing same-name non-system role is a conflict before any role/grant mutation |
| `RolePermission.TenantId` | Server-bound current tenant; never supplied from a grant request payload |
| `RolePermission.GrantSource` | `Module` for profile-created grants |
| `RolePermission.SourceModuleCode` | Exactly `product-item-sku-master` |
| User-role link | Not created by this profile; zero automatic assignments |
| `ProcessedIntegrationEvent.CompletionProtocolVersion` | Nullable integer; exact value `1` proves completed reconciliation. Missing/other value is unconfirmed and replayable |
| Inbox fact identity | Existing exact `EventId + EventName + TenantId`; any same-EventId fact drift fails closed before grant/revoke |

The lifecycle profile validates three explicit dependency groups from the authoritative catalog: the eight FU23-owned
submit/retire keys, the existing eight Product Identity read/create keys and the three existing shared dependencies
`platform.work-aggregation.inbox.view`, `platform.workflow.tasks.approve` and `platform.workflow.tasks.reject`. The
profile consumes the shared dependencies for its approver role but does not define, rename or claim ownership of them.
ABB keys are neither required nor consumed by this profile; they remain owned by
`ProductAbbreviationEntitlementGrantProfile`.

## 5. Repo Scope

The completed planning step changed only this pack and the canonical registry row. The authorized runtime code-start
is restricted to the exact list below; no directory wildcard or adjacent runtime file is implied.

**Runtime allow-list:**

- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/ProcessedIntegrationEvent.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IIntegrationEventInboxRepository.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/ProductIdentityLifecycleEntitlementGrantProfile.cs` (new)
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Eventing/EntitlementSyncConsumer.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/IntegrationEventInboxRepository.cs`

**Test allow-list:**

- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecycleEntitlementGrantProfileTests.cs` (new)
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementSyncConsumerTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/IntegrationEventInboxRepositoryMongoTests.cs` (new)
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecyclePermissionOnboardingMongoTests.cs` (new)

`EntitlementPermissionSyncService` may be changed only to compose the lifecycle profile with the existing ABB profile
and generic non-special permission selection. A mutually exclusive `if/else` in which either profile suppresses the
other is forbidden.

## 6. Protected Paths

- `services/Diten.MdmService/**`
- `services/Diten.Platform/**`
- `services/Diten.Platform.Common/**`
- `gateway/**`
- `frontend/**`
- every Auth file not listed in Section 5, including index configuration, seeders, controllers, token services and
  role-assignment handlers
- appsettings, environment files, secrets, data migrations and operational helpers
- FU16-FU20 pack files and implementation tracker
- `.antigravity/**`, DCPs, Blueprint and backlog

The MOD-0290 manifest and lifecycle controllers are evidence dependencies only; this pack cannot modify them.

## 7. Dependencies

| Dependency | Required evidence |
|---|---|
| MOD-0018 parent | Existing permission catalog, roles, module grant attribution and entitlement synchronization seams |
| MOD-0290 Phase 1.5 | Frozen exact eight submit/retire keys, native Workflow approve/reject topology, zero Revision keys and role separation |
| FU16-FU19 | Existing exact read/create keys and Admin/Viewer matrices remain unchanged |
| FU20 | ABB special profile remains independently composable; its roles/grants/revoke behavior does not drift |
| Platform entitlement | Authoritative active/disabled/expired/uncertain state; no Platform runtime modification |
| MOD-0023 / WorkCenter | Existing inbox/approve/reject definitions and native provider/actions; Workflow template candidate assignment remains authoritative |
| MDM enforcement | Consumer later enforces submit/retire plus record-level maker-checker/parent/version reconciliation rules |

Auth and Product Data decisions, `ready-for-dev` promotion and the exact Section 5 code-start are approved under the
user's standing no-push authorization. Operational tenant reconciliation and Production/Staging remain separate.

## 8. Runtime Constraints

- Permission catalog definitions are global and idempotent; tenant role grants remain tenant-scoped.
- The FU23-owned subset must be exactly eight. A missing, extra, malformed, product approve/reject or Revision key fails before lifecycle
  role/grant mutation.
- The responsibility-role profile also requires all eight existing read/create keys; a partial base set cannot produce
  a partially privileged role.
- Admin and Viewer receive no FU23 key and no shared Workflow dependency through this profile. Generic Admin-full
  selection must exclude the submit/retire subset.
- `ProductIdentityApprover` receives the three existing shared permissions only after their exact catalog definitions
  are present and tenant-assignable. FU23 never creates fallback copies under the product module.
- Auth role membership is not Workflow assignment. MOD-0023 template user/position candidates and the actual bound task
  assignee remain mandatory record-level gates even when the actor holds `ProductIdentityApprover`.
- Dedicated roles are system roles but receive no user assignment automatically.
- All three role-name collisions are checked before any missing role or grant is created.
- Existing ABB profile, ABB roles and ABB grants are composed in the same reconciliation and cannot be replaced,
  reclassified or revoked by lifecycle subset selection.
- Reconciliation is exact and idempotent. It adds missing desired module grants and removes stale matching lifecycle
  module grants without touching manual/system/other-module grants.
- Missing, disabled or expired entitlement results in zero `product-item-sku-master` module-sourced grants for Admin,
  Viewer and all dedicated Product Identity/ABB roles through the existing authoritative revoke path. Restore produces
  the exact complete matrices again without duplicate role/grant rows.
- Entitlement transport uncertainty and profile preflight failure mutate nothing. A repository failure may leave a
  bounded partial role/grant reconciliation, but it never writes a completion marker or returns consumer success. The
  same authoritative event remains replayable and converges the tenant to the exact desired matrices. Best-effort
  per-module processing may continue, but any accumulated failure is surfaced after the pass.
- Cancellation propagates; it is never swallowed by best-effort reconciliation logging.
- Tokens remain bounded by the existing refresh/staleness contract; FU23 adds no token mechanism.

## 9. Layout & Shell Contract

`shell: none`. This backend-only follow-up has no Razor route, layout, DataTable, navigation or browser surface.
`_LayoutPlatformAdmin`, `_LayoutTenantShell` and frozen `_Layout.cshtml` are all outside scope.

## 10. Backend File Convention

No CRUD/CQRS feature is introduced. The new profile is one focused stateless policy class in the existing
`Application/Common/Services` convention and contains the exact constants, role templates, set validation and selection
functions. `EntitlementPermissionSyncService` remains the sole repository-backed reconciler; profile classes do not
access repositories, create users or assign role membership.

Each public type has its own file. Tests mirror the production type names. No controller, handler, command, query,
repository, entity, DTO or persistence implementation is added.

## 11. Frontend File Contract

No frontend file is in scope. Existing Global Product, GSKU, LSKU and Finished Good pages remain MOD-0290 consumers.
WorkCenter buttons, navigation and browser permission rendering are not FU23 acceptance evidence.

## 12. Validation Rules

| Input / decision | Required | Exact validation | Failure result |
|---|---:|---|---|
| ModuleCode | Yes | `product-item-sku-master` after existing normalization | Reject/no mutation |
| FU23-owned catalog | Yes | Exact eight submit/retire keys | Missing/extra/alias/product approve-reject/Revision key rejects before mutation |
| Base identity catalog | Yes | Exact four read/create pairs | Partial responsibility matrices forbidden |
| Shared Workflow dependencies | Yes for Approver role | Exact existing inbox-view, workflow-approve and workflow-reject keys | Missing/malformed dependency rejects profile; no replacement definition |
| Revision keys | Yes | Zero `mdm.product-definition-revisions.*` keys introduced/consumed | Reject lifecycle profile |
| Admin desired set | Yes | Existing four read/create pairs; lifecycle intersection empty | Any lifecycle key fails |
| Viewer desired set | Yes | Existing four reads; create/lifecycle intersection empty | Any extra key fails |
| Steward desired set | Yes | Four exact `read/create/submit` triples | Any extra/missing key fails |
| Approver desired set | Yes | Four product reads + three shared Workflow dependencies = 7 | Product submit/create/retire and product approve/reject keys fail |
| Workflow assignment | Yes for an actual decision | MOD-0023 template candidate and bound user/position assignment | Auth role alone never authorizes a record decision |
| Retirement Steward set | Yes | Four exact `read/retire` pairs | Submit/create and shared approve/reject dependencies fail |
| Role identity | Yes | Three exact system role names; no non-system collision | Fail before all role/grant mutation |
| Entitlement | Yes | Active/effective for tenant | Missing/disabled/expired revokes matching module grants |
| Tenant | Yes | Non-empty server-bound tenant | No grant/disclosure |
| User assignment | Yes | No profile assignment operation invoked | Any automatic assignment fails |

## 13. Failure Path to Verify

- One FU23 key missing, one extra/product-approve/product-reject/Revision key declared: fail before role/grant mutation.
- One required read/create key missing: no partial responsibility-role grant.
- One shared Workflow dependency missing or divergent: no partial Approver grant and no replacement permission definition.
- Generic Admin path attempts to grant submit/retire: fail and leave Admin at its existing read/create matrix.
- Viewer receives create or lifecycle: fail.
- Existing same-name non-system role for any of the three names: detect all collisions before creating a role/grant.
- Active entitlement replay: same role IDs and grant cardinality; no duplicate or automatic user assignment.
- An actor holds `ProductIdentityApprover` but is not a MOD-0023 candidate/assignee: Workflow/MDM record decision remains denied.
- Disabled/expired/missing entitlement: matching module grants are absent; manual/system/other-source grants survive.
- Entitlement uncertainty/profile preflight failure: no mutation or completion marker. Repository failure after a
  bounded partial write: no completion marker or false success; exact EventId replay converges the same desired state.
- Exact completed EventId replay skips reconciliation; same EventId with different tenant/event facts fails closed.
- Legacy inbox row without completion protocol is replayed and lazily upgraded after a successful reconciliation.
- Lifecycle profile plus ABB profile in the same declared module: both exact matrices survive in one reconciliation.
- Tenant A grant/revoke/restore never creates, removes or discloses Tenant B roles/grants.
- Cancellation during grant/revoke propagates and does not continue best-effort mutation.

## 14. Authorization Convention

The exact active-entitlement matrices are:

| Role | Exact Product Identity grants | Automatic assignment |
|---|---|---:|
| Tenant Admin | Four resources × `read + create` = 8; FU23/shared-Workflow profile keys = 0 | Existing default role only |
| Tenant Viewer | Four resources × `read` = 4; create/FU23/shared-Workflow profile keys = 0 | Existing default role only |
| `ProductDataSteward` | Four resources × `read + create + submit` = 12 | No |
| `ProductIdentityApprover` | Four product reads + `platform.work-aggregation.inbox.view` + `platform.workflow.tasks.approve` + `platform.workflow.tasks.reject` = 7 | No; MOD-0023 template candidate/assignment is separate |
| `ProductIdentityRetirementSteward` | Four resources × `read + retire` = 8 | No |

Holding multiple roles unions permissions but never bypasses MDM canonical-human maker-checker or child-admission
fences. A platform actor or SuperAdmin permission bypass cannot be treated as Product Identity approval evidence.

## 15. Gateway / API Routing Decision

No Gateway or API route change is required or authorized. FU23 is Auth catalog/grant onboarding only. MDM lifecycle and
WorkCenter routes belong to the separately gated MOD-0290 Phase 1.5 steps.

## 16. Acceptance Criteria

- [ ] DCP-002 exact ID/name/parent preflight remains green after registry/pack creation.
- [ ] Catalog contains exactly eight new submit/retire definitions under `product-item-sku-master`; no product approve/reject or Revision key.
- [ ] The three shared Workflow dependencies are consumed by exact existing key/ID and are not recreated/re-owned.
- [ ] Admin matrix is exact eight read/create keys with zero FU23/shared-profile grant; Viewer exact four reads with zero FU23/shared-profile grant.
- [ ] Three exact system roles receive `12/7/8` grants and zero user assignments.
- [ ] Approver permission does not replace MOD-0023 candidate/assignee enforcement.
- [ ] Lifecycle and ABB profiles compose; neither drops or widens the other's grants.
- [ ] Active replay preserves exact role IDs/cardinality and produces no duplicate grant.
- [ ] Missing/disabled/expired entitlement removes matching module grants; restore returns exact matrices.
- [ ] Manual, system and other-module grants are preserved during stale/revoke reconciliation.
- [ ] Partial/extra/Revision key sets and non-system role collision fail before mutation.
- [ ] Tenant isolation, cancellation propagation and entitlement uncertainty fail-closed behavior pass.
- [ ] Completion-only inbox replay, exact-facts conflict, partial-failure retry and legacy lazy-upgrade tests pass.
- [ ] FU16-FU20 permission/grant behavior remains green.
- [ ] MDM, Platform, Gateway, frontend, config/data and Production/Staging remain unchanged.

## 17. Test Expectations

| Test area | Required proof |
|---|---|
| Catalog/template | Exact eight submit/retire set; four resources/two owned actions; tenant scope; zero product approve/reject/Revision keys |
| Shared dependencies | Existing inbox-view/approve/reject resolved exactly; missing dependency fails; no duplicate definition |
| Profile unit | Exact subset validation; `12/7/8` role matrices; Admin/Viewer exclusion; adjacent ABB keys ignored safely |
| Workflow boundary | Approver role alone does not satisfy template candidate/bound assignment; read-only contract evidence |
| Composition | One declared module containing base Product Identity, lifecycle and ABB subsets reconciles both special profiles |
| Collision | Any non-system dedicated role collision rejects before role/grant mutation |
| Entitlement | Active/missing/disabled/expired/uncertain; exact revoke and restore |
| Source safety | Only matching module grants removed; manual/system/other-module preserved |
| Replay | Same roles, grants and assignments after repeated reconciliation |
| Tenant | Tenant A/B isolation for role creation, grants, revoke and read-back |
| Cancellation | Grant/revoke cancellation propagates; no swallowed continuation |
| Consumer completion | Marker only after the whole pass succeeds; accumulated module failures throw; marker failure remains replayable |
| Consumer replay | Exact completed replay skips; legacy/unconfirmed replay upgrades; EventId fact drift fails closed |
| Regression | FU16-FU20 focused tests and generic non-special module behavior |
| Real Mongo | `localhost:27017`, fixed shared Auth integration database + per-test fresh TenantId; no per-run database, production-wide index bootstrap, fake/in-memory or skip |

Runtime delivery must run focused profile/sync/Mongo tests, the full AuthService suite and Auth API Release build. No
MDM/Platform source test is authorized; their manifests/controllers may be inspected read-only for contract evidence.

## 18. Ready-for-dev Checklist

- [x] Master 8.1 parent and registry collision preflight passed.
- [x] Separate FU decision is justified: FU16-FU19 intentionally exclude lifecycle; FU20 owns ABB's different profile.
- [x] Exact eight submit/retire keys, zero product approve/reject and zero Revision keys are frozen.
- [x] Three existing shared WorkCenter/Workflow dependencies are consume-only and exact.
- [x] Admin/Viewer and three dedicated role matrices are frozen.
- [x] Dedicated special-profile composition with ABB is explicit.
- [x] No automatic user assignment and non-system collision behavior are explicit.
- [x] Active/disabled/expired/uncertain replay/revoke contracts are explicit.
- [x] Exact runtime/test allow-list and protected paths are documented.
- [x] Auth and Product Data owner decisions are accepted under the user's standing authorization.
- [x] Pack is promoted to `ready-for-dev`.
- [x] Section 5 code-start is authorized under the user's standing no-push instruction.
- [ ] Local Development and Production/Staging operational runs remain separate gates.

## 19. Implementation Notes

### 2026-08-29 implementation evidence

- The exact eight submit/retire keys and the `12/7/8` Steward/Approver/Retirement role matrices are implemented.
- ABB, Product Legal Entity Scope and Product Identity lifecycle profiles compose under the same module; inactive
  profile cleanup is source-safe and does not create missing roles.
- Entitlement reconciliation accumulates module failures and throws after the bounded pass, preventing a false
  integration-event completion marker.
- The inbox now reserves globally unique EventId with exact `EventName + TenantId` facts before reconciliation,
  reads only through a tenant-bound exact filter and publishes `CompletionProtocolVersion=1` only after successful
  reconciliation. Transport tenant is authoritative; payload/envelope mismatch fails before reservation or mutation.
- Focused profile/sync/inbox plus real-Mongo tests after rebase: `120/120` passed, zero skipped.
- Auth API Release build after rebase: zero errors and zero warnings.
- Full Auth suite after rebase: `589/591` passed. The two failures are the current `origin/main` User Lookup contract
  tests expecting only `UserId/Referenceable` while the byte-identical current DTO also exposes privacy-masked
  `MaskedName/MaskedEmail`; neither failing file is changed by FU23.
- MDM API Release build after resolving the `origin/main` Brand-page/JWT integration conflicts: zero errors, five
  pre-existing persistence warnings. Focused manifest/DI tests passed `8/8` and the full MDM suite passed `773/773`,
  zero skipped. Both Brand and Product Legal Entity Scope manifest pages are retained.
- Repo architecture guard remains `5/7`; both failures are pre-existing MDM DB-010 inventory drift and no Auth FU23
  test is reported as an offender.
- No push, operational reconciliation, user assignment, credential, configuration or business-data mutation ran.

**Open completion dependency:** code-truth inspection found no production MDM manifest declaration for the eight
submit/retire keys yet. `DefaultRolePermissionTemplate` only prevents default Viewer leakage; it does not create
catalog entries. FU23 therefore remains `review` until the separately governed MDM lifecycle delivery publishes the
exact definitions and live catalog/grant reconciliation is proven. Concurrent delivery is covered by consumer unit
tests and atomic real-Mongo completion-upsert tests; full consumer-plus-role-repository concurrent delivery remains a
live integration acceptance item rather than an overclaimed real-Mongo proof.

FU23 is separate because submit and retirement authorization are not a fourth simple read/create onboarding. Reusing
generic Admin-full reconciliation would silently combine maker and retirement authority. Approval/rejection remains on
the native Workflow/WorkCenter topology; inventing product-specific approve/reject keys would create dead duplicate
authorization paths. Folding FU23 into FU16-FU19 would also rewrite completed scopes and hide the fact that Product
Definition Revision has intentionally zero independent lifecycle keys.

The implementation must compose special profiles by subset under one module, not select a single winning profile for
`product-item-sku-master`:

1. Resolve the authoritative declared catalog once.
2. Validate the Product Identity base eight and FU23-owned submit/retire eight subsets exactly.
3. Resolve the three shared WorkCenter/Workflow dependencies exactly without defining fallback copies.
4. Validate ABB's exact subset independently when present.
5. Build desired plans for Admin/Viewer, the three Product Identity roles and four ABB roles.
6. Detect every system-role collision before creating any missing role/grant.
7. Reconcile only matching module-sourced desired/stale grants, then return one completion result.

This pack does not approve a generic profile framework or refactor unrelated modules. The minimum exact change remains
the three runtime and four test files in Section 5.

## 20. Follow-up Items

1. Auth/Product Data owner review of exact role names and matrices.
2. Promotion to `approved` or `ready-for-dev`, followed by separate Section 5 code-start.
3. After green code evidence, separately authorized Local Development catalog/entitlement/role reconciliation.
4. Explicit user membership assignment to responsibility roles through supported Auth operations; no automatic assign.
5. Token refresh/new login plus Admin/Viewer/Steward/Approver/Retirement allow-deny smoke when MOD-0290 submit/retire
   and native MOD-0023 approval paths exist; Approver must also prove template candidate/assignment enforcement.
6. Disabled/expired/restore live replay and tenant-isolation read-back.
7. Production/Staging enablement remains separately prohibited until an explicit operational gate closes.
