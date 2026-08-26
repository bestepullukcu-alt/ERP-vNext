---
id: MOD-0018-FU17
name: Finished Good Permission Onboarding
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: EntityBase
status: review
owner: auth-owner
branch: feature/pss/mod-0018-fu17-finished-good-permission-onboarding
started: 2026-08-06
target: ""
form_field_count: 0
parent_module: MOD-0018
consumer_module: MOD-0290
---

# MOD-0018-FU17 — Finished Good Permission Onboarding

> **Review/code-truth guard (2026-08-09).** The implementation evidence below and the separately authorized Local
> Development pilot reconciliation close the Finished Good permission/live-smoke gate for Development only. Navigation
> and Production enablement remain open.

> **Identity evidence.** The fail-closed DCP-002 preflight completed with exit `0`:
> `verify_module_id.py . --check-id MOD-0018-FU17 --name "Finished Good Permission Onboarding" --parent MOD-0018`
> → `OK MOD-0018-FU17: proven against Blueprint/registry.`

## 1. Module Summary

This follow-up governs onboarding of exactly two already-declared MOD-0290 Finished Good permissions:

- `mdm.finished-goods.read`
- `mdm.finished-goods.create`

Their canonical module attribution is `product-item-sku-master`; `mdm` remains only the permission-key namespace.
Finished Good shares the same tenant module entitlement as Global Product. This pack does not create a Finished Good
specific entitlement, reopen MOD-0018-FU16, or broaden Finished Good runtime behavior.

## 2. Ownership and Boundaries

**In scope**

- Auth baseline exclusion so neither Finished Good key is silently granted without an active module entitlement.
- Entitlement-aware default-role evidence: Tenant Admin receives Finished Good read/create; Tenant Viewer receives read
  only.
- Exact module-sourced grant attribution using `SourceModuleCode = product-item-sku-master`.
- Fail-closed grant/revoke evidence for authoritative versus unavailable entitlement reads.
- Non-regression evidence that Global Product permissions continue to share the same module entitlement.

**Out of scope**

- Editing or reopening MOD-0018-FU16.
- Permission seed execution, role mutation, entitlement mutation, token issuance/refresh, live smoke or production
  enablement.
- MDM API, manifest, controller, frontend or Gateway changes.
- New permissions, roles, module codes, entitlements, pages or actions.
- Navigation visibility. Finished Good remains hidden until a separate production-readiness decision.

## 3. Owned Objects

| Object | Owner / invariant |
|---|---|
| `mdm.finished-goods.read` | Global Auth permission catalog record; no `TenantId`; globally unique; tenant-assignable |
| `mdm.finished-goods.create` | Global Auth permission catalog record; no `TenantId`; globally unique; tenant-assignable |
| Permission attribution | `Permission.Module = product-item-sku-master` for both exact keys |
| Tenant Admin grants | Both exact keys, tenant-scoped and module-sourced |
| Tenant Viewer grant | Read key only, tenant-scoped and module-sourced |
| Shared entitlement | Existing tenant entitlement for `product-item-sku-master`; no Finished Good-specific entitlement |

The existing MDM manifest already declares both keys and keeps the `FINISHED_GOODS` page non-navigation-visible. The
existing Platform manifest/catalog pipeline and Auth descriptor-key sync remain owners of their generic behavior; this
pack does not take ownership of or authorize edits to them.

## 4. Entity Fields

No new entity or DTO is introduced. `entity_base: EntityBase` satisfies pack metadata for the Auth tenant-owned grant
boundary; no new `EntityBase` subtype is authorized.

| Existing field | Locked value / rule |
|---|---|
| `Permission.Key` | Exactly one of the two Finished Good keys |
| `Permission.Module` | `product-item-sku-master` |
| `Permission.TenantId` | Prohibited; Permission is global catalog data |
| `Permission.Scope` | Tenant-assignable under the existing scope convention |
| `RolePermission.TenantId` | Required and server-derived |
| `RolePermission.GrantSource` | `Module` for automatic entitlement grants |
| `RolePermission.SourceModuleCode` | `product-item-sku-master` |
| `TenantModuleEntitlement.ModuleCode` | `product-item-sku-master` |

## 5. Repo Scope

This draft itself may change only this pack and its canonical registry row. A later, separately authorized runtime
implementation is limited to the following exact allow-list:

- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs`
  — add the two Finished Good keys to the exact entitlement-only baseline exclusion; do not alter role breadth or
  escalation boundaries.
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs`
  — exact baseline exclusion and Global Product non-regression evidence.
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
  — exact shared-entitlement grant, idempotency, tenant isolation and source-scoped revoke evidence.

### Finished Good Entitlement Safety Hardening — code-start authorized 2026-08-06

The user approved the Phase 1.5 design and explicitly authorized only the following hardening allow-list. This named
step preserves the completed entitlement/default-role implementation while closing authoritative-read failure
handling, effective `HasAccess` projection, `TenantEntitlementExpiryUpdatedV1` reconciliation and
`TenantEntitlementOverrideRemovedV1` reconciliation:

- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/QueryHandlers/GetTenantEntitledModulePermissionsQueryHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalTenantEntitlementsController.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/GetTenantEntitledModulePermissionsQueryHandlerTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/InternalTenantEntitlementsControllerTests.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Eventing/EntitlementSyncConsumer.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementSyncConsumerTests.cs`
- `execution/domains/platform-shared-services/module-packs/MOD-0018-FU17-finished-good-permission-onboarding.md`
- `execution/domains/master-data-management/module-packs/MOD-0290-product-item-sku-master.md`
- `execution/registries/module-implementation-status.md`

This step authorizes no navigation, tenant mutation, live grant/revoke, token issuance, smoke execution or production
configuration. No MDM runtime file is allow-listed.

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, Blueprint files, backlog and DCP files.
- MOD-0018-FU16 and every other Module Pack.
- MOD-0290 Domain Contract; the MOD-0290 Module Pack may change only under the hardening allow-list above.
- All Auth files outside the exact Section 5 allow-lists, including seeders, repositories, token issuance/refresh,
  entitlement client/service production code, configuration and secrets.
- All Platform files outside the exact hardening allow-list and all `services/Diten.MdmService/**` runtime/test files.
- `frontend/**` and `gateway/**`.
- Permission seed/grant execution, entitlement mutation and production configuration/data.
- Archive/frozen paths and unrelated user work.

## 7. Dependencies

- MOD-0018 parent authorization authority.
- MOD-0018-FU13 token/cache behavior; referenced unchanged.
- MOD-0018-FU16 Global Product onboarding boundary; referenced and protected, not expanded.
- MOD-0290 Finished Good API enforcement and its exact two permission declarations.
- Existing MDM `ProductItemSkuMasterManifestProvider` with `ModuleCode = product-item-sku-master`.
- Existing Platform manifest reconciliation, global permission-catalog sync and tenant entitlement projection.
- Existing Auth descriptor-key grant sync and module-source revoke behavior.

## 8. Runtime Constraints

- Permission catalog rows are global, have no `TenantId`, and retain existing unique-key/tuple constraints.
- Both permissions use `Permission.Module = product-item-sku-master`; no `finished-good`, `finished-goods`, `mdm` or
  MOD ID alias is accepted as ModuleCode.
- Tenant scope exists only through role assignments, `RolePermission` and the effective tenant module entitlement.
- Tenant Admin receives read/create and Tenant Viewer receives read only after authoritative active entitlement proof.
- Missing, disabled or expired entitlement creates no automatic grant.
- Confirmed missing/disabled/expired entitlement may remove only matching module-sourced grants.
- Unavailable, malformed, ambiguous or non-authoritative entitlement state grants nothing and revokes nothing.
- System/manual grants and grants sourced by another module are never removed by this reconciliation.
- Grant/revoke is idempotent and tenant-isolated.
- Navigation remains hidden until separate production readiness and enablement approval.
- Existing MOD-0018/FU13 token refresh, expiry and cache-invalidation semantics remain unchanged.
- A future-dated finite expiry is not automatically reconciled at the instant it becomes due by this hardening step;
  durable scheduled reconciliation remains an open production-readiness gate.

## 9. Layout & Shell Contract

Not applicable. `shell: none`, `golden_reference: none` and `form_field_count: 0`; no page, Razor layout or DataTable is
owned by this authorization follow-up.

## 10. Backend File Convention

No new feature folder, endpoint, entity or repository is authorized. Implementation is restricted to the exact Section
5 onboarding and hardening allow-lists. Existing naming and layering remain unchanged.

## 11. Frontend File Contract

Not applicable. No frontend file is authorized. Existing Finished Good navigation stays hidden; this pack neither
creates nor reveals a page.

## 12. Validation Rules

| Input/fact | Valid | Fail-closed behavior |
|---|---|---|
| Permission key | Exact two-key set | Reject/ignore additions and aliases |
| ModuleCode | `product-item-sku-master` | No grant or revoke under an alias |
| Role | Default tenant `Admin` or `Viewer` | No automatic grant to other/custom roles |
| Admin actions | `read`, `create` | No update/delete/lifecycle permission inferred |
| Viewer actions | `read` only | Create is denied |
| Entitlement read | Authoritative and active | Otherwise no grant |
| Revocation evidence | Authoritative absence/disable/expiry | Unavailable/ambiguous state cannot revoke |
| Tenant | Server-derived and exact | Cross-tenant mutation prohibited |

## 13. Failure Path to Verify

- No entitlement → neither default role receives a Finished Good module grant.
- Active entitlement → Admin receives exact read/create; Viewer receives exact read; replay creates no duplicate.
- Confirmed entitlement removal/disable/expiry → only `GrantSource.Module` rows with matching tenant and
  `SourceModuleCode` are removed.
- Platform unavailable, timeout, malformed response or ambiguous result → no grant and no revoke.
- Catalog missing one/both exact keys → missing keys are not invented; reconciliation is fail-closed.
- Duplicate/conflicting catalog identity → onboarding blocks; no second permission record is created.
- Token predates a permission change → existing FU13 bounded-staleness behavior applies; no token shortcut is added.

## 14. Authorization Convention

| Finished Good surface | Permission |
|---|---|
| list and detail | `mdm.finished-goods.read` |
| GSKU selector and draft create | `mdm.finished-goods.create` |

Default role proposal:

- Tenant Admin: `mdm.finished-goods.read` + `mdm.finished-goods.create`.
- Tenant Viewer: `mdm.finished-goods.read` only.
- Other/custom roles: no automatic grant from this pack.

Every automatic row is tenant-scoped, has `GrantSource = Module`, and has
`SourceModuleCode = product-item-sku-master`. This proposal does not itself grant a permission.

## 15. Gateway / API Routing Decision

No route change. Existing Finished Good API/Gateway contracts are consumers only. This pack adds no endpoint, header,
tenant injection, bypass or public entitlement surface.

## 16. Acceptance Criteria

- [x] `MOD-0018-FU17` identity preflight passes against parent MOD-0018.
- [x] Exact permission set is locked to read/create only.
- [x] Canonical ModuleCode is `product-item-sku-master`; namespace remains `mdm`.
- [x] Finished Good and Global Product share one module entitlement.
- [ ] Both Finished Good permissions exist once in the global catalog with no `TenantId`, exact module attribution and
      tenant-assignable scope.
- [x] Finished Good keys are excluded from the unconditional Viewer baseline.
- [x] Active authoritative entitlement grants Admin read/create and Viewer read, idempotently and tenant-isolated.
- [x] Missing/disabled/expired entitlement produces no automatic grant.
- [x] Confirmed absence may revoke only matching module-sourced grants; unavailable/ambiguous state mutates neither
      direction.
- [x] Global Product grants under the shared entitlement remain unchanged.
- [x] No additional permission, role, entitlement, page, action or navigation visibility is introduced.
- [x] No production seed/grant/entitlement mutation or enablement occurs under this pack alone.
- [x] Platform failure and null-data results remain unavailable failures rather than authoritative empty results.
- [x] Authoritative selection uses canonical `HasAccess`, including `EnabledByOverride` and excluding false-access
      outcomes.
- [x] Expiry-updated and override-removed events run authoritative reconciliation; unavailable reads do not mutate or
      consume the inbox event, while confirmed empty may remove stale module-source grants.

## 17. Test Expectations

- Exact-set test for `EntitlementOnlyViewerPermissions` contains both Global Product and Finished Good read/create keys
  and silently permits no adjacent key.
- Baseline selection test proves Viewer receives neither Finished Good key without entitlement; unrelated tenant reads
  remain unchanged.
- Descriptor-key sync test uses one `product-item-sku-master` entitlement containing Global Product plus Finished Good
  declarations and proves:
  - Admin receives all four exact keys;
  - Viewer receives both exact read keys and no create key;
  - all automatic rows carry the event tenant and exact `SourceModuleCode`;
  - replay is idempotent.
- Revoke test preserves System, Manual, other-module and other-tenant rows.
- Existing authoritative-empty/unavailable consumer tests remain green and prove the locked grant/revoke distinction.
- Platform handler/controller tests prove successful empty, failure/null propagation, canonical `HasAccess` selection,
  descriptor union/de-duplication, authorization and tenant/platform scope restoration.
- Auth consumer tests prove expiry/override reconciliation, fallback preservation, confirmed-empty cleanup,
  unavailable retryability, replay idempotency, preserved direct grant/revoke routes and tenant isolation.
- Existing MDM manifest/controller tests remain green and prove exact declaration/enforcement without editing MDM.
- Focused Auth tests and the full Auth suite/build must pass with real counts; relevant MDM and Platform contract suites
  are non-mutating regression evidence. No skipped/fake production enablement claim is accepted.

## 18. Ready-for-dev Checklist

- [x] Module identity and parent are verified.
- [x] Ownership, exact keys, ModuleCode and shared-entitlement decision are locked.
- [x] Minimum runtime allow-list and protected paths are explicit.
- [x] Existing generic Platform/MDM/Auth reuse points are evidenced.
- [x] Role proposal and fail-closed entitlement semantics are explicit.
- [x] Product Owner approves this draft or marks it `ready-for-dev`.
- [x] User gives separate explicit code-start authorization for Section 5.
- [ ] Runtime operator supplies a later production enablement plan and live entitlement targets.

## 19. Implementation Notes

- Do not append `product-item-sku-master` to the general Admin baseline. Entitlement sync, not default provisioning,
  owns these module grants.
- The existing MDM manifest already declares the Finished Good keys and keeps the page hidden; duplicate declaration or
  a second manifest is prohibited.
- The existing Auth sync accepts Platform-declared key sets and selects Admin full/Viewer read-only. It therefore needs
  focused proof, not a new Finished Good branch.
- The Platform pipeline remains module/descriptor-driven; the hardening adds generic authoritative failure propagation
  and canonical `HasAccess` evaluation without a Finished Good-specific branch.
- Catalog synchronization and production enablement are separate operational steps; passing tests is not proof that a
  live tenant is entitled or that a current token contains the keys.

### Runtime implementation evidence — 2026-08-06

- `EntitlementOnlyViewerPermissions` contains exactly the two Global Product and two Finished Good keys.
- One active `product-item-sku-master` descriptor set grants Admin all four exact keys and Viewer the two read keys;
  replay is idempotent and Tenant A produces no Tenant B row.
- Matching module-source revoke preserves Manual, System, other-module and other-tenant grants. Missing catalog keys are
  not invented. Existing consumer tests preserve confirmed-absence versus unavailable/ambiguous fail-closed behavior.
- Focused Auth tests: DefaultRolePermissionTemplate `20/20`; EntitlementPermissionSyncService `16/16`;
  EntitlementSyncConsumer `21/21` after safety hardening.
- Full AuthService suite: `319/319`; Auth API build: `0 warnings / 0 errors`.
- Read-only regressions: MDM manifest/controller permission contracts `9/9`; Platform manifest/catalog `28/28` and
  tenant commercial entitlement query `3/3`.
- No permission seed/grant, entitlement, token, navigation or live tenant mutation was executed.

### Entitlement safety hardening evidence — 2026-08-06

- Platform focused handler/controller hardening tests passed `17/17` with no skipped tests.
- Auth `EntitlementSyncConsumer` focused tests passed `21/21` with no skipped tests.
- Auth default-role plus entitlement-sync focused tests passed `36/36`; the full Auth suite passed `319/319`.
- Platform entitlement/catalog regressions passed `232/232`; the full Platform suite passed `1435/1435`.
- Platform and Auth API builds completed with `0 warnings / 0 errors`.
- Failure/null-data is distinct from confirmed empty; `EnabledByOverride` is included through `HasAccess=true`.
- Expiry-updated and override-removed events reconcile before inbox consumption; unavailable reads remain retryable,
  and confirmed empty performs authoritative source-scoped cleanup through the existing sync service.

### Local Development reconciliation evidence — 2026-08-09

- The pilot `product-item-sku-master` entitlement and refreshed sessions proved Admin read/create, Viewer read and
  Viewer create denial for Finished Good without broadening the exact two-key contract.
- `FG-000000000005` was created once and read back through the supported UI/API chain with tenant isolation. This is
  Local Development evidence, not Production catalog/grant or navigation readiness.

## 20. Follow-up Items

- Before Production enablement, identify the target Production environment/tenants, confirm the canonical module
  catalog owner and active entitlement, run Production catalog/grant reconciliation, refresh tokens under FU13
  behavior, and collect Production allow/deny evidence. Local Development evidence is already recorded.
- Decide separately when the already-declared Finished Good navigation page becomes production-visible. This pack keeps
  it hidden and does not authorize that decision.
- Any future Finished Good update/delete/lifecycle permission requires a separate approved authorization slice.
- **Production-readiness follow-up — scheduled finite-expiry reconciliation:** owner is Platform Commercial
  Entitlements, with Auth responsible for consumer/reconciliation closure evidence. When a finite entitlement reaches
  expiry, a durable scheduler and overdue recovery scan must request authoritative reconciliation rather than direct
  revoke. Failed, null, malformed or unavailable reads must mutate nothing and remain retryable; duplicate/stale
  schedules must be idempotent, and current expiry extension/removal or plan/add-on fallback must win over stale timers.
  Closure requires clock-driven expiry without a new mutation, restart/downtime recovery, expiry extension/removal,
  fallback preservation, confirmed-empty source-scoped cleanup, unavailable retry, replay idempotency, tenant isolation
  and durable production-topology scheduling tests.
- Until that scheduled-expiry gate closes, any pilot is limited to Development, a non-system tenant and a non-expiring
  entitlement. This is not production-readiness evidence and does not authorize navigation, live mutation, grant/revoke,
  token issuance or smoke without separate user approval.
