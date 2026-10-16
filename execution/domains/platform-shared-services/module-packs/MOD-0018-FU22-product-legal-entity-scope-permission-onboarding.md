---
id: MOD-0018-FU22
name: Product Legal Entity Scope Permission Onboarding
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: N/A
status: review
owner: auth-owner
branch: feature/mdm/mod-0290-product-scope-integration
started: 2026-08-26
target: 2026-09-04
form_field_count: 0
parent: MOD-0018
consumer_module: MOD-0290-FU03
---

# MOD-0018-FU22 — Product Legal Entity Scope Permission Onboarding

> **Code-start guard.** This review pack creates no permission, role, grant, role membership, entitlement, token, manifest,
> navigation, configuration, secret or tenant data. Runtime work requires `approved` or `ready-for-dev` status and a
> separate user code-start authorization for the exact Section 5 allow-list. Local Development reconciliation and every
> Production/Staging operation are separate gates.
> The user accepted the runtime implementation result on 2026-08-26. `status: review` is retained because FU03
> manifest/catalog declaration, Local Development reconciliation, explicit role membership and token/live smoke are
> separate operational gates.
>
> **DCP-002 preflight.** Master 8.1 identifies parent `MOD-0018` as `RBAC / ABAC Authorization`. Before this file was
> created, the exact command
> `verify_module_id.py . --check-id MOD-0018-FU22 --name "Product Legal Entity Scope Permission Onboarding" --parent MOD-0018`
> returned `OK MOD-0018-FU22: proven against Blueprint/registry`. No conflicting pack or registry row existed. This is
> mechanical identity evidence only; Master 8.1 remains the business/model authority. The canonical collision-free
> registry row is reconciled by the separate governance change in this bounded integration; current-main validation is
> recorded with the implementation handoff rather than inferred from historical local evidence.

## 1. Module Summary

FU22 owns Auth onboarding for exactly the six management and rollout permission keys approved by
MOD-0290-FU03 Product Legal Entity Scope Assignment:

- `mdm.product-legal-entity-scopes.read`
- `mdm.product-legal-entity-scopes.configure`
- `mdm.product-legal-entity-scopes.replace`
- `mdm.product-legal-entity-scopes.end`
- `mdm.product-legal-entity-scope-rollout.activate`
- `mdm.product-legal-entity-scope-rollout.rollback`

All six keys use canonical `ModuleCode = product-item-sku-master`. They do not create a new entitlement. FU22 adds a
special exact grant profile because the generic shared-module rule would otherwise grant all six keys to Tenant Admin
and the `read` key to Tenant Viewer, contrary to the approved least-privilege matrix.

This pack is backend-only authorization onboarding: `shell: none`, `golden_reference: none`, `form_field_count: 0` and
`entity_base: N/A` are intentional. No new persisted entity is owned.

## 2. Ownership and Boundaries

**FU22 / Auth owner:**

- Exact global catalog identity and entitlement-only baseline exclusion for the six keys.
- One pure `ProductLegalEntityScopeEntitlementGrantProfile` with exact key and role templates.
- Composite reconciliation that separates ABB, FU03 and generic residual permissions before any grant decision.
- Tenant/source-scoped idempotent grant and revoke behavior for three dedicated FU03 system roles.
- Fail-before-mutation role-name collision validation across every applicable dedicated ABB and FU03 role.

**MOD-0290-FU03 / MDM owner:**

- Nav-hidden page/action manifest declaration, controller enforcement, row-scope policy and rollout semantics.
- Management, completeness and rollout business rules. FU22 cannot declare or implement them in MDM.

**Existing owners retained:**

- FU16-FU19 retain Global Product, Finished Good, GSKU and LSKU two-key onboarding.
- FU20 retains ABB's exact eight-key profile and four dedicated responsibility roles.
- Platform retains the unchanged generic manifest, global catalog and effective entitlement transport.

**Out of scope:** MDM manifest/runtime, Platform runtime, Gateway, frontend, navigation, user-role assignment, JWT/token
issuance, tenant/entitlement/data mutation, WorkCenter, secrets/configuration and Production/Staging enablement.

## 3. Owned Objects

| Object | Exact invariant |
|---|---|
| Six FU03 `Permission` definitions | Global catalog rows; one active row per exact key; no `TenantId` |
| Module attribution | `Permission.Module = product-item-sku-master` |
| FU03 special profile | Exact six-key set; partial, extra-prefix or catalog-missing set fails before mutation |
| `ProductLegalEntityScopeSteward` | System role; `read`, `configure`, `replace`, `end` |
| `ProductLegalEntityScopeAuditor` | System role; `read` only; completeness uses the same read key |
| `ProductLegalEntityScopeRolloutOperator` | System role; `read`, `activate`, `rollback` |
| Tenant Admin FU03 default | `read` only |
| Tenant Viewer FU03 default | No FU03 permission |
| Module-sourced grants | Tenant-scoped; exact `SourceModuleCode = product-item-sku-master` |

Dedicated roles may be provisioned idempotently, but no user is automatically assigned. FU22 deliberately excludes a
seventh `completeness` key: completeness is a read projection protected by
`mdm.product-legal-entity-scopes.read` plus the server-side policy boundary.

## 4. Entity Fields

No entity, collection or DTO is introduced.

| Existing object/field | Exact value or rule |
|---|---|
| `Permission.Key` | One of the six exact Section 1 keys |
| `Permission.Module` | `product-item-sku-master` |
| `Permission.Resource` | `product-legal-entity-scopes` or `product-legal-entity-scope-rollout` according to the key |
| `Permission.Action` | `read`, `configure`, `replace`, `end`, `activate` or `rollback` exactly |
| `Permission.Scope` | Tenant-assignable; catalog row remains global |
| `Permission.TenantId` | Prohibited |
| `Role.Name` | One of the three exact FU03 role names; tenant-scoped system role |
| `RolePermission.TenantId` | Required and server-derived |
| `RolePermission.GrantSource` | `Module` for entitlement reconciliation |
| `RolePermission.SourceModuleCode` | `product-item-sku-master` |

## 5. Repo Scope

This planning task may create only this pack. A later separately authorized runtime delivery is limited to the exact
no-glob allow-list below.

**Auth runtime:**

- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/ProductLegalEntityScopeEntitlementGrantProfile.cs` — new
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IIntegrationEventInboxRepository.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/ProcessedIntegrationEvent.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/IntegrationEventInboxRepository.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Eventing/EntitlementSyncConsumer.cs`

**Auth tests:**

- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductLegalEntityScopeEntitlementGrantProfileTests.cs` — new
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementSyncConsumerTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductLegalEntityScopePermissionOnboardingMongoTests.cs` — new; real `localhost:27017`

`ProductAbbreviationEntitlementGrantProfile.cs` is read-only code truth and is not writable under FU22. Its existing
tests and FU16-FU20 suites are required regression execution, not additional file-write authority.

MDM manifest runtime/test allow-list under FU22 is `none`. The separately approved FU03 manifest named step must declare
the nav-hidden page and all six exact descriptors before catalog reconciliation. Platform runtime/test allow-list under
FU22 is also `none`; the generic chain is consumed unchanged.

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, Blueprint workbooks, DCPs, backlog and unrelated governance files.
- `execution/registries/module-id-registry.md` under this task; its eventual canonical FU22 row requires separate scope.
- MOD-0018-FU16 through FU20 and every other Module Pack.
- All Auth files outside the exact Section 5 allow-list, including ABB profile production code, repositories, entities,
  consumers, token/JWT code, user-role assignment, configuration, appsettings and secrets.
- All `services/Diten.MdmService/**`, including `ProductItemSkuMasterManifestProvider.cs` and its tests.
- All `services/Diten.Platform/**`, `gateway/**`, `frontend/**`, WorkCenter and navigation/menu paths.
- Permission/catalog reconciliation execution, role/grant/entitlement/user data and Production/Staging operations.

## 7. Dependencies

- Master 8.1 parent MOD-0018 and current Auth global-catalog/tenant-grant contracts.
- Ready-for-dev MOD-0290-FU03 permission names, role matrix and nav-hidden management surface.
- Existing `DefaultRolePermissionTemplate.EntitlementOnlyViewerPermissions` baseline guard.
- Existing `EntitlementPermissionSyncService`, module-sourced grants and authoritative entitlement consumer behavior.
- FU20 `ProductAbbreviationEntitlementGrantProfile` and its four-role maker-checker boundary, unchanged.
- FU16-FU19 shared `product-item-sku-master` Admin-full/Viewer-read generic residual behavior, unchanged.
- A separately authorized FU03 MDM manifest declaration and later supported catalog reconciliation.
- MOD-0018-FU13 token/cache bounded-staleness behavior, unchanged.

## 8. Runtime Constraints

The combined `product-item-sku-master` descriptor set must be reconciled in this fail-closed order:

1. Normalize ModuleCode and descriptor keys; blank/duplicate/unknown inputs cannot create aliases.
2. Detect ABB and FU03 subsets independently by exact prefixes.
3. If either subset is present, validate it equals its complete approved set before any role or grant mutation.
4. Resolve the catalog and repeat exact-set validation; missing, deleted or divergent definitions fail before mutation.
5. Preflight every applicable dedicated ABB and FU03 role name. A visible non-system collision in any one role aborts
   before every role upsert, grant or revoke. A concurrent post-preflight role-name race remains fail-closed before
   grants but may leave an unassigned system-role skeleton; distributed single-writer reconciliation is a follow-up.
6. Compute generic residual as the complete module set minus both exact ABB and FU03 subsets.
7. Apply existing Admin-full/Viewer-read behavior only to that generic residual.
8. Apply FU20's unchanged ABB profile and this pack's exact FU03 matrix independently.
9. Reconcile only matching tenant/module-sourced rows; preserve Manual, System, other-module and other-tenant grants.

An active entitlement creates no user-role membership. Missing, disabled or expired entitlement removes matching
module-sourced grants for Admin, Viewer and all applicable dedicated roles. Unavailable, malformed, timeout, null or
ambiguous entitlement state grants nothing, revokes nothing and remains retryable. Cancellation propagates.

## 9. Layout & Shell Contract

Not applicable. `shell: none`; FU22 owns no Razor view, route, DataTable, localization resource or navigation item.

## 10. Backend File Convention

This is not a CRUD feature and creates no CQRS folder, controller, entity or repository.

- The new profile is one pure static exact mapping under existing Auth `Application/Common/Services` conventions.
- Persistence orchestration remains in the existing `EntitlementPermissionSyncService`.
- The composite path does not merge ABB and FU03 permission sets into one semantic profile; it validates and reconciles
  them independently while sharing one module entitlement.
- Existing repositories and cancellation-token propagation remain unchanged.

## 11. Frontend File Contract

Not applicable. FU22 owns no frontend file. The FU03 page remains nav-hidden and unavailable until its separate MDM,
permission reconciliation and operational gates close.

## 12. Validation Rules

| Input/fact | Required rule | Failure result |
|---|---|---|
| FU03 descriptor subset | Exactly six keys when any FU03 prefix is present | Abort before role/grant mutation |
| ABB descriptor subset | Existing exact eight keys when any ABB prefix is present | Preserve FU20 fail-before-mutation behavior |
| ModuleCode | `product-item-sku-master` | No alias/new entitlement |
| Catalog | Six active exact definitions, global and tenant-assignable | Missing/divergent/deleted definition blocks profile |
| Admin | FU03 read only | Any configure/replace/end/rollout automatic grant fails |
| Viewer | No FU03 key | Any automatic FU03 grant fails |
| Steward | read/configure/replace/end exactly | Extra/missing key fails |
| Auditor | read only | Mutation/rollout key fails |
| RolloutOperator | read/activate/rollback exactly | Configure/replace/end fails |
| Role names | All applicable dedicated names are system roles or absent | Any non-system collision aborts all profile mutation |
| Entitlement | Authoritative and active for grant | Otherwise no new grant |
| Revocation | Authoritative missing/disabled/expired | Remove only exact matching module-source rows |
| User assignment | Never automatic | Any `UserRole` mutation fails acceptance |

## 13. Failure Path to Verify

- Partial five-key or extra prefixed FU03 descriptor set: deterministic profile failure and zero role/grant mutation.
- Complete FU03 set with one catalog definition missing/deleted: no role upsert and no grant.
- Non-system collision on the first, middle or last applicable ABB/FU03 dedicated role: zero new role and zero grant for
  every profile; pre-existing tenant state remains unchanged.
- Active entitlement: exact Admin, Viewer, Steward, Auditor and RolloutOperator matrices; replay creates no duplicate.
- Missing/disabled/expired entitlement: matching FU03 grants are removed from all five relevant role targets; ABB and
  generic residual cleanup remains correct.
- Unavailable/ambiguous entitlement: neither grant nor revoke and no false-success consumption.
- Tenant A reconciliation never creates, removes or discloses Tenant B role/grant state.
- Manual/System/other-module grants survive exact module-profile reconcile and revoke.
- ABB's `1/1/3/5/3/2` role matrix and role-collision fail-before-mutation behavior remain unchanged.
- Global Product, GSKU, LSKU and Finished Good generic Admin/Viewer matrices remain unchanged.
- No dedicated user membership is created during grant, replay, revoke or restore.

## 14. Authorization Convention

Exact `ModuleCode`: `product-item-sku-master`.

| Role / effective entitlement | read | configure | replace | end | activate | rollback |
|---|---:|---:|---:|---:|---:|---:|
| Tenant Admin + active entitlement | allow | deny | deny | deny | deny | deny |
| Tenant Viewer + active entitlement | deny | deny | deny | deny | deny | deny |
| `ProductLegalEntityScopeSteward` + active entitlement | allow | allow | allow | allow | deny | deny |
| `ProductLegalEntityScopeAuditor` + active entitlement | allow | deny | deny | deny | deny | deny |
| `ProductLegalEntityScopeRolloutOperator` + active entitlement | allow | deny | deny | deny | allow | allow |
| Missing / disabled / expired entitlement | deny | deny | deny | deny | deny | deny |

Completeness uses the read key and an internal server-side policy check; no seventh permission is inferred. Permission
possession does not bypass FU03 product row-scope, expected-version, audit, completeness or rollout-state rules.

## 15. Gateway / API Routing Decision

No Gateway or API route is owned. FU22 adds no endpoint, method, header, proxy or navigation item. MDM exposure and
integration-agent routing remain separately approved FU03 named steps.

## 16. Acceptance Criteria

- [ ] The exact six keys exist once each in the global Auth catalog with exact resource/action/module attribution and no
      `TenantId`.
- [ ] All six keys are excluded from unconditional default-role baseline grants.
- [ ] The FU03 and ABB subsets are independently exact-validated before mutation.
- [ ] A composite descriptor set removes both special subsets from generic residual Admin/Viewer selection.
- [ ] Active entitlement yields the exact Section 14 matrix without automatic user-role assignment.
- [ ] Missing/disabled/expired entitlement removes only matching tenant/module-source grants.
- [ ] Unavailable/ambiguous entitlement state mutates neither direction.
- [ ] Every applicable dedicated role-name collision is checked before any role/grant mutation.
- [ ] Replay is idempotent; Tenant A/B and grant-source isolation hold.
- [ ] FU20 ABB and FU16-FU19 generic Product Item SKU Master matrices are unchanged.
- [ ] MDM manifest, Platform runtime, JWT/token, user membership, Gateway/frontend/navigation/config/data remain unchanged
      during FU22 runtime delivery.
- [ ] Local Development catalog/grant/token smoke runs only after separate explicit operational approval.

## 17. Test Expectations

| Test area | Required evidence |
|---|---|
| Profile unit | Exact six keys, three role templates, matrices, prefix detection, partial/extra rejection |
| Baseline | Admin/Viewer receive no FU03 key without entitlement; existing entitlement-only set remains exact |
| Composite sync | Generic residual excludes ABB and FU03; special profiles independently receive exact subsets |
| Preflight atomicity | Parametric non-system collision across seven dedicated ABB+FU03 role names produces zero role/grant mutation |
| Active entitlement | Exact Section 14 grants, source attribution, no `UserRole`, idempotent replay |
| Revoke/restore | Missing/disabled/expired cleanup; restore exactness; unavailable result no mutation |
| Isolation | Tenant A/B, Manual/System/other-module preservation and catalog-missing behavior |
| ABB regression | Existing exact profile, `1/1/3/5/3/2` grants, maker-checker role separation and no auto assignment |
| Product regression | Global Product/GSKU/LSKU/Finished Good Admin full and Viewer read-only remain unchanged |
| Real Mongo | `localhost:27017`, fixed dedicated `diten_auth_fu22_itest` database, idempotent shared test catalog, per-run tenant identities and tenant-scoped role/grant/inbox cleanup; no fake/in-memory/skip; roles/grants/replay/revoke/collision cardinality, atomic cross-tenant EventId claim, lease recovery and release/reclaim |
| Build/suites | Focused Auth tests, full AuthService suite and Auth API Release build with real counts |

Existing FU20 tests and consumer failure tests must run unchanged as read-only regression evidence. No MDM/Platform file
is edited to manufacture test success.

## 18. Ready-for-dev Checklist

- [x] Exact DCP-002 preflight passed for ID/name/parent before file creation.
- [x] FU22 is a MOD-0018 authorization child, not a new MDM capability.
- [x] Six keys, ModuleCode, roles and least-privilege matrix match approved FU03 architecture.
- [x] Current ABB-special/generic-residual overgrant risk is evidenced from code truth.
- [x] Exact no-glob Auth runtime/test allow-list and protected paths are documented.
- [x] No MDM or Platform runtime is invented; separate FU03 manifest/catalog dependency is explicit.
- [x] Canonical FU22 registry row is present and collision-free in the separate bounded-integration governance change;
  current-main validation remains an implementation gate.
- [x] Composite profile, exact role names and Admin-read/Viewer-none defaults approved on 2026-08-26.
- [x] User approved the Phase 1.5 architecture on 2026-08-26; this approval does not grant runtime code-start.
- [x] Pack promoted to `ready-for-dev` after Auth-owner approval.
- [x] User authorized runtime code-start for the exact Section 5 paths on 2026-08-26; operational mutation remains separate.
- [x] Runtime/test implementation passed focused, FU16-FU20 regression, full AuthService and Release build gates.
- [x] User accepted the FU22 runtime implementation result on 2026-08-26.
- [ ] Separate Local Development target-tenant operational plan is approved after runtime tests pass.

## 19. Implementation Notes

### 2026-08-26 implementation evidence

- `DefaultRolePermissionTemplate` now excludes all six FU03 keys from the unconditional Viewer baseline.
- `ProductLegalEntityScopeEntitlementGrantProfile` owns exactly six canonical keys and three exact dedicated role
  matrices. Canonical casing, module/resource/action/scope attribution, partial/extra sets and catalog drift fail closed.
- `EntitlementPermissionSyncService` independently validates ABB and FU03 subsets, excludes both from the generic
  residual and preflights all seven dedicated role names before the first role/grant mutation. A special-profile module
  payload must equal the complete active catalog descriptor set; ABB-only, FU03-only and generic-only subsets fail before
  mutation. Duplicate/blank descriptors and cancellation fail closed without partial mutation.
- Admin receives only FU03 `read`; Viewer receives no FU03 key. Steward receives `read/configure/replace/end`, Auditor
  receives `read`, and RolloutOperator receives `read/activate/rollback`. No user-role assignment path was added.
- Active reconciliation removes stale/orphaned matching-module grants outside the desired authoritative matrix.
  Missing/disabled/expired cleanup and direct revoke remove only matching tenant/module-source grants; Manual, System,
  other-module and other-tenant rows are preserved.
- The original delivery evidence was `73/73` focused and `362/362` full AuthService. Current-main integration
  supersedes those counts: the combined FU22/default/sync/consumer/FU20 regression set passes `110/110`, zero skipped.
  FU22 adds 35 tests over the clean FU21 integration base. The full AuthService run is `566/568`; the exact same two
  `UserLookupValidationContractTests` failures occur on the clean FU21 base (`531/533`) and are outside FU22 paths.
  A focused parameterized test covers collisions on all seven ABB+FU03 role names.
- The original real-Mongo evidence used a unique disposable database. Current-main integration supersedes that
  DB-010-incompatible harness with fixed `diten_auth_fu22_itest`, an idempotent shared test catalog, per-run tenant
  identities and tenant-scoped role/grant cleanup. It proves nine
  system roles, 27 exact module grants, idempotent replay, revoke/restore, source preservation, Tenant A/B isolation,
  representative cross-profile collision atomicity and zero automatic `UserRole` rows.
- Auth API Release build passed with `1` pre-existing GUID-representation warning and `0` errors. The targeted DB-010
  architecture guard passed `5/5` and
  DCP-002 verification returned `OK MOD-0018-FU22`. `git diff --check` and file-hygiene evidence are recorded at handoff.
- Inbox processing now obtains an atomic, global-EventId, tenant-bound `Processing` claim before any authoritative
  mutation. A caught failure releases its claim; a process crash or release failure is recovered by the bounded
  30-second lease. Completion is recorded only after the idempotent grant/reconcile mutation succeeds. Concurrent first
  deliveries cannot both mutate under different tenants, completed deliveries retain duplicate suppression, and replay
  under a different tenant fails before mutation instead of treating `(EventId, TenantId)` as a new identity. Legacy
  inbox documents remain `Completed` by the enum default and are covered by raw BSON real-Mongo read-back. Payload and
  envelope tenant mismatch, event-name identity drift and malformed claimed-without-token results all fail before
  mutation.
- No MDM, Platform, Gateway, frontend, configuration, tenant, entitlement, token, role-membership or operational data
  mutation occurred. The separately approved FU03 manifest/catalog and Local Development reconciliation remain open.

## 20. Follow-up Items

- Implement the separately approved FU03 nav-hidden MDM manifest declaration and reconcile exact global catalog rows.
- After runtime tests pass, separately approve Local Development target tenant, entitlement/grant reconciliation, explicit
  role memberships, token refresh and allow/deny smoke. No role membership is automatic.
- Production/Staging reconciliation, secrets/configuration, observability/runbook, navigation and enablement remain
  separately prohibited until explicit gates close.
