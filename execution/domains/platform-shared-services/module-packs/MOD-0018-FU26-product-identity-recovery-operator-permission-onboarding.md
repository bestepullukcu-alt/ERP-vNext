---
id: MOD-0018-FU26
name: Product Identity Recovery Operator Permission Onboarding
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: EntityBase
status: review
owner: auth-owner
branch: feature/pss/mod-0018-fu26-product-identity-recovery-operator-permission
started: 2026-09-02
target: 2026-09-02
form_field_count: 0
parent_module: MOD-0018
consumer_module: MOD-0290
---

# MOD-0018-FU26 — Product Identity Recovery Operator Permission Onboarding

> **Code-start authorization.** On 2026-09-02 the user approved this Phase 1.5 plan, promoted the pack to
> `ready-for-dev`, and authorized only the exact Section 5 Auth runtime/test allow-list. Automatic user assignment,
> MOD-0290 D, configuration/data mutation, Local Development operation, Production/Staging and push remain prohibited.
>
> **DCP-002 identity proof.** Master 8.1 assigns authorization, role and entitlement policy to canonical parent
> `MOD-0018 — RBAC / ABAC Authorization`. Registry, pack and repo-wide code-truth inspection found FU01 and FU09–FU25,
> but no FU26 collision. On 2026-09-02 the fail-closed command
> `verify_module_id.py . --check-id MOD-0018-FU26 --name "Product Identity Recovery Operator Permission Onboarding" --parent MOD-0018`
> returned `OK MOD-0018-FU26: proven against Blueprint/registry`.
>
> **Golden Reference decision.** This is backend-only Auth permission onboarding. It owns no Razor, DataTable or form;
> therefore `shell: none`, `golden_reference: none` and `form_field_count: 0` are intentional.

## 0. MOD-0290 Recovery D Dependency Closure — 2026-09-02

The previously future MOD-0290 D prerequisite is now implemented and verified. The exact operator surface is
`POST /api/product-identity-workflow-operations/{operationId:guid}/recover-before-start`, protected by
`mdm.product-identity.lifecycle-operations.recover`. The existing `GLOBAL_PRODUCTS` manifest page publishes exactly
one dangerous, non-toolbar, non-row `System` action `RECOVER_ORPHANED_LIFECYCLE_OPERATION`; no new page, navigation,
Gateway, frontend or WorkCenter action was introduced.

Saved MDM evidence is `89/89` focused D API/authorization/manifest tests and `1162/1162` non-transaction recovery
regressions, both with zero skipped tests; MDM API Release build passed with zero warnings and zero errors. The combined
A–D recovery selection was `111/127`; all 16 failures were the documented standalone-Mongo transaction precondition,
so this pack does not re-claim the B transaction evidence. FU26 Auth evidence remains `159/159` focused and `105/105`
FU20–FU25 regression, with Auth API Release build clean. No runtime test was rerun during this governance-only
reconciliation.

## 1. Module Summary

FU26 governs least-privilege onboarding of exactly one future MOD-0290 operator permission:

- `mdm.product-identity.lifecycle-operations.recover`

The permission is reserved for a dedicated tenant system role named `ProductIdentityRecoveryOperator`. After an active
`product-item-sku-master` entitlement and authoritative descriptor reconciliation, that role receives exactly the one
permission as a module-sourced grant. The role receives no user automatically. Tenant Admin, Tenant Viewer and every
existing Product Identity responsibility role receive zero recovery permissions.

MOD-0290 D now owns and enforces the operator API and publishes the permission through the existing Product Item SKU
Master manifest. FU26 does not expose or modify that surface; it supplies the separate Auth least-privilege onboarding
contract. Operational reconciliation and explicit human membership remain separate gates.

## 2. Ownership and Boundaries

**Auth owner:**

- One exact global permission definition transported from the authoritative MDM manifest.
- One tenant-scoped system-role template, exact one-key grant plan and source-scoped reconciliation.
- Provisioning-only guards that reject the recovery permission through generic manual assignment in both tenant and
  platform contexts and reject every generic permission assignment to the reserved recovery role.
- Fail-before-mutation descriptor/catalog validation, tenant isolation, replay, revoke and restore behavior.
- Explicit preservation of Admin, Viewer and existing dedicated-role least privilege.

**Read-only MOD-0290 D dependency (implemented; not FU26 ownership):**

- Implemented D operator API authorization with the exact permission key; FU26 does not create that API.
- Implemented D manifest declaration anchored to existing `GLOBAL_PRODUCTS` after endpoint enforcement.
- Recovery domain/application behavior, authoritative Workflow NotFound proof, deterministic audit and recovery safety.

These remain exclusively MOD-0290 D-owned. FU26 reads their eventual code truth as prerequisite evidence and does not
share, amend or implement MDM ownership.

**Out of scope:**

- Any MOD-0290 D controller, endpoint, request/response model, handler or recovery implementation.
- Automatic user assignment, startup operator creation, Tenant Admin/Viewer grant or platform-admin acceptance bypass.
- Manual recovery-permission assignment to any role and generic manual permission assignment to the reserved recovery
  role. Explicit human-to-role membership remains a separate supported operation.
- Existing ProductDataSteward, ProductIdentityApprover, ProductIdentityRetirementSteward or ABB role expansion.
- New ModuleCode, entitlement, permission alias, wildcard, `manage` key or operator-configurable permission list.
- Gateway, frontend, navigation, WorkCenter, Workflow, Platform runtime, configuration, data migration, secrets,
  Production/Staging, stage, commit and push.

## 3. Owned Objects

| Object | Owner | Exact invariant |
|---|---|---|
| Recovery `Permission` definition | Auth global catalog | One active non-deleted exact definition; global, no `TenantId` |
| Future recovery manifest declaration | MOD-0290 D read-only dependency | One invisible `System` action on existing `GLOBAL_PRODUCTS`; no new page/route |
| Recovery grant profile | Auth | Applies only to the exact recovery key and exact module descriptor |
| `ProductIdentityRecoveryOperator` | Auth tenant system role | Exactly one module grant: recovery |
| Tenant Admin plan | Auth | Zero recovery grants |
| Tenant Viewer plan | Auth | Zero recovery grants |
| Existing Product Identity roles | Auth | Zero recovery grants; FU25 matrices remain otherwise unchanged |
| Module-sourced recovery grant | Auth | Tenant-bound; `SourceModuleCode=product-item-sku-master` |

No new persisted entity, collection, repository, endpoint or user assignment is introduced. Existing `Permission`,
`Role`, `RolePermission`, `RoleAssignmentVersion`, entitlement and module manifest models are reused.

## 4. Entity Fields

| Existing object | Field/value | Rule |
|---|---|---|
| `Permission.Key` | `mdm.product-identity.lifecycle-operations.recover` | Ordinal exact; no alias/wildcard |
| `Permission.Module` | `product-item-sku-master` | Existing entitlement/catalog owner |
| `Permission.Resource` | `product-identity.lifecycle-operations` | Exact nested PKS-001 resource |
| `Permission.Action` | `recover` | Exact operator action |
| `Permission.Scope` | `Tenant` | Tenant-scoped; generic/manual assignment forbidden; authoritative module profile only |
| `Permission.IsDeleted` | `false` | Deleted or duplicate definition fails closed |
| `Role.Name` | `ProductIdentityRecoveryOperator` | Tenant-scoped system role; non-system collision fails |
| `RolePermission.TenantId` | Server-bound current tenant | Empty/mismatched/cross-tenant value fails closed |
| `RolePermission.GrantSource` | `Module` | Manual/system grants are not rewritten |
| `RolePermission.SourceModuleCode` | `product-item-sku-master` | Exact source; no recovery alias module |
| Entitlement | `TenantId + product-item-sku-master` | Must be active and effective |

**Frozen future-D manifest shape:**

| Field | Exact value |
|---|---|
| Existing `PageCode` | `GLOBAL_PRODUCTS` |
| Existing `RoutePath` | `/MasterDataManagement/GlobalProducts` |
| Existing `RequiredPermission` | `mdm.global-products.read` |
| Existing page navigation | `IsNavigationVisible: true` remains unchanged; no new navigation descriptor is created |
| New `ActionCode` | `RECOVER_ORPHANED_LIFECYCLE_OPERATION` |
| New action `DisplayName` | `Recover Orphaned Lifecycle Operation` |
| New action `PermissionKey` | `mdm.product-identity.lifecycle-operations.recover` |
| New action `ActionType` | `System` (valid `ModulePageActionType`) |
| New action `SortOrder` | `50` |
| New action flags | `IsDangerous: true`, `IsToolbarAction: false`, `IsRowAction: false` |

The manifest model has no top-level permission or addressable action without a `ModuleManifestPage`; every action must
belong to a page and every page requires a `RoutePath`. Creating a synthetic page would invent a user-facing route,
while using the lower-case `/api/...` operator route would violate the page-route validator's UI-shaped canonical
grammar. FU26 therefore adds no page and uses the existing Product Identity root descriptor only as the permission
catalog anchor. The `System` action has neither toolbar nor row placement and carries no endpoint address; future D's
actual API remains the separately frozen `POST /api/product-identity-workflow-operations/{operationId:guid}/recover-before-start`.
For future D, the parent plan's earlier “navigation-hidden and action-only” phrase therefore means **no new navigation
node and no renderable action placement**; it does not authorize a synthetic `IsNavigationVisible: false` page.

`entity_base: EntityBase` reflects the reused tenant-scoped Auth role/grant aggregate. The global permission catalog is
also reused; FU26 creates no new entity type and no client-supplied `TenantId`.

## 5. Repo Scope

Planning writes are limited to this pack and its canonical registry row. A later separately approved runtime delivery
is limited to this exact allow-list.

**Auth runtime owner:**

- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Features/Roles/Handlers/CommandHandlers/AssignPermissionCommandHandler.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/FullCatalogPermissionGrantService.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/ProductIdentityRecoveryOperatorEntitlementGrantProfile.cs` — new, pure exact profile
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs`

**Auth tests:**

- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/AssignPermissionCommandHandlerTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Permissions/FullCatalogPermissionGrantServiceTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityRecoveryOperatorEntitlementGrantProfileTests.cs` — new
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityRecoveryOperatorPermissionOnboardingMongoTests.cs` — new, real `localhost:27017`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementSyncConsumerTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/InternalEventsControllerTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/IntegrationEventInboxRepositoryMongoTests.cs`

No MOD-0290 D API file is allow-listed here. Its controller/authorization and manifest implementation remain read-only
dependency evidence, not FU26 allow-list entries. Their completed evidence is recorded in Section 0; FU26 changes no
MDM file.

## 6. Protected Paths

- Every file outside Section 5, including Auth `DataSeeder.cs`, entities, repositories, controllers other than the exact
  assign-permission handler, token flows, user-role assignment handlers, configuration and appsettings.
- All `services/Diten.MdmService/**`, including manifest provider/tests, recovery runtime and future D API files.
- `services/Diten.Platform/**`, `services/Diten.Platform.Common/**`, `gateway/**`, `frontend/**` and navigation/menu.
- WorkCenter, Workflow, other domain services, config/data migrations, secrets and credentials.
- `.antigravity/**`, AGENTS.md, DCPs, Blueprint, tracker, delivery board and unrelated module packs.
- Local Development operational mutation, Production/Staging, stage, commit and push.

## 7. Dependencies

- Canonical parent MOD-0018 and existing Auth permission/role/entitlement infrastructure.
- MOD-0018-FU20 exact-profile precedent for dedicated roles, source-safe revoke/restore and no automatic membership.
- MOD-0018-FU23/FU25 Product Identity lifecycle profile and its current authoritative `13/7/8` role matrices.
- Existing `product-item-sku-master` manifest/catalog/entitlement chain and generic Platform transport, consumed unchanged.
- MOD-0290 orphaned lifecycle recovery A–C application/repository/runtime foundation.
- Separately approved future MOD-0290 D operator API with exact `[HasPermission]` enforcement.
- Existing Auth role assignment API for a later explicit human-to-role assignment operation.
- Existing `IRoleRepository.GetAllByTenantAsync` plus `IRolePermissionRepository.GetByRoleAsync` for a tenant-wide,
  active-role/active-grant scan; no repository/interface expansion is required.
- Existing `EntitlementSyncConsumer` and `InternalEventsController.TenantActivated` claim/release semantics: sync
  exception prevents `CompleteClaimAsync`, releases the claim and rethrows; release failure falls back to lease expiry.

## 8. Runtime Constraints

- Canonical ModuleCode remains `product-item-sku-master`; no recovery-specific entitlement is created.
- Permission catalog definition is global and unique. Tenant scope starts at entitlement, role and grant.
- Recovery profile cardinality is exactly one permission and one dedicated role.
- Admin, Viewer and all existing dedicated roles receive zero recovery permission, even with active entitlement.
- Provisioning the system role never creates user-role membership.
- The recovery key is provisioning-only: `DefaultRolePermissionTemplate.IsTenantAssignable` returns false for it, and
  `AssignPermissionCommandHandler` rejects it before the current platform-context exemption. The same handler rejects
  every generic permission assignment to `ProductIdentityRecoveryOperator`. All other permission/role assignment
  behavior remains byte-for-byte equivalent.
- `FullCatalogPermissionGrantService` resolves the registered permission through the existing
  `IPermissionRepository.GetByIdAsync` seam before any role/grant lookup. For the exact recovery key it returns without
  creating the default-tenant SuperAdmin `System` grant. Every other registered permission keeps the current
  full-catalog role lookup, idempotency, `SystemGrant`, logging and best-effort behavior. A missing permission or lookup
  failure creates no grant and preserves the service's non-throwing best-effort contract. Cancellation from permission,
  role or grant repository calls always propagates. The existing interface,
  controller call sites, repositories and DI registration do not change.
- Before active grant/restore mutates any role or grant, Auth resolves the unique active recovery `PermissionId`, reads
  every active tenant role through `GetAllByTenantAsync`, and reads every active grant through `GetByRoleAsync`. Across
  the entire tenant, the only allowed row for that PermissionId is:
  `Role.Name=ProductIdentityRecoveryOperator`, `Role.IsSystem=true`, `GrantSource=Module`,
  `SourceModuleCode=product-item-sku-master`. Any other role, source, missing/wrong module source, non-system reserved
  role or duplicate active recovery row is forbidden and fails before grant/restore mutation.
- Complete descriptor and catalog validation occurs before role creation or active-entitlement grant/restore.
- Grant/restore and revoke are deliberately asymmetric. Active-entitlement grant or restore on a contaminated reserved
  role fails before role/grant mutation.
- An authoritative missing, disabled or expired entitlement still enters the narrowing revoke path. It removes every
  exact recovery grant whose `GrantSource=Module` and `SourceModuleCode=product-item-sku-master`, even when the role also
  has contamination. Manual/System/foreign-module/wrong-source rows remain untouched and produce blocked/remediation
  evidence; contamination cannot prevent removal of the authoritative entitlement grant.
- Revoke performs the same tenant-wide PermissionId scan and removes every source-canonical recovery row — exact
  recovery `PermissionId` + `GrantSource=Module` + `SourceModuleCode=product-item-sku-master` — regardless of role. It
  then re-scans and blocks on every remaining forbidden row. If any
  forbidden recovery row remains, it logs event
  `entitlement.sync.product_identity_recovery_contamination` with only TenantId, RoleId, RoleName, GrantSource and
  normalized SourceModuleCode (no user/token/credential data), then throws
  `InvalidOperationException("PRODUCT_IDENTITY_RECOVERY_GRANT_CONTAMINATION")` **after** successful source-canonical removals.
  The same exception code is used by active grant/restore preflight before mutation. No new exception/evidence type is
  introduced. Existing repository deletes are individually persisted and non-transactional, so the post-delete
  exception does not roll them back.
- Narrowing revoke resolves the canonical recovery PermissionId by the exact key through the existing
  including-deleted repository lookup. Deleted or tuple-divergent catalog metadata cannot strand an already-effective
  source-canonical recovery grant; strict active/non-deleted tuple validation remains mandatory for grant/restore.
- The recovery role is excluded from generic module cleanup after the exact recovery narrowing pass. Same-source
  non-recovery rows on that role remain immutable contamination evidence and force the coded retryable failure.
- After revoke, authoritative product-module recovery grants are exactly zero. If contamination contains only
  non-recovery permissions, effective recovery permission is also zero. If a preserved Manual/System/foreign-module
  row itself grants the recovery key, the current positive-grant model has no deny overlay: total effective recovery
  permission cannot truthfully be claimed zero until separate remediation removes that row through its owning path.
  FU26 reports blocked/remediation-required and cannot report successful security closure.
- Uncertain entitlement state performs no grant or revoke and reports incomplete; uncertainty is not equivalent to an
  authoritative missing/disabled/expired snapshot.
- Existing event completion is the exact failure mechanism; no evidence entity is invented. The consumer and
  `tenant.activated` catch the post-delete exception, do not call `CompleteClaimAsync`, release the inbox claim and
  rethrow. Replay of the same EventId reclaims, observes every source-canonical grant already absent, re-detects forbidden rows and
  remains incomplete until separately authorized remediation makes the tenant-wide scan clean.
- Same-name non-system role collision fails before mutation; it is never adopted or converted.
- If an existing reserved recovery role has any active non-recovery grant, or has the recovery grant with `System`,
  `Manual`, missing/wrong `SourceModuleCode`, or another non-canonical source, active grant/restore fails before
  mutation. The authoritative revoke path is the sole exception: it removes only the exact product-module recovery
  row, preserves contamination and emits blocked/remediation evidence.
- Cross-tenant reads/mutations fail closed and disclose no role, grant or entitlement state.
- Token claims change only through supported refresh/new login after explicit assignment; tokens/credentials are not logged.
- D manifest/API operational publication, operator-role assignment, token issuance/refresh proof, smoke and acceptance
  are prohibited while any tenant-wide forbidden recovery row exists, even if Auth code tests and future D code pass.

## 9. Layout & Shell Contract

`shell: none`; FU26 owns no view.

- No Razor file, frontend route, DataTable, localization resource or layout is created or changed.
- No `Layout = "..."` applies because the pack is backend-only.
- `golden_reference: none` and `form_field_count: 0` are mandatory for this non-CRUD permission slice.

## 10. Backend File Convention

This is not a CRUD/CQRS API feature and does not create Golden Reference command/query folders.

- The new grant-profile file is a pure exact role/key contract under the existing Auth
  `Application/Common/Services` convention.
- It performs no persistence, transport, assignment, token or configuration work.
- `DefaultRolePermissionTemplate` owns the domain-safe provisioning-only permission key and reserved-role-name guards;
  `AssignPermissionCommandHandler` applies them before both tenant and platform-context generic assignment paths.
- `FullCatalogPermissionGrantService` uses the existing permission repository lookup to exclude only the exact
  provisioning-only recovery key from default-tenant SuperAdmin auto-grant; it changes no interface or controller.
- `EntitlementPermissionSyncService` remains the single orchestration boundary through existing repositories.
- MDM manifest/API work is read-only prerequisite evidence owned by a separate future-D amendment; FU26 changes none.
- Cancellation continues to flow to every existing repository operation.

## 11. Frontend File Contract

No frontend file, menu node, button, proxy, JavaScript asset, RESX or browser behavior is in scope. Operational API
acceptance, if later authorized, uses the future D server endpoint and exact authorization policy; frontend visibility is
neither required nor accepted as enforcement evidence.

## 12. Validation Rules

| Input/decision | Required | Exact rule | Failure |
|---|---:|---|---|
| Permission key | Yes | Exact singleton recovery key | Reject partial/extra/alias set |
| Catalog tuple | Yes | Exact module/resource/action/scope, active/non-deleted | Fail before mutation |
| ModuleCode | Yes | `product-item-sku-master` | Reject alias/new module |
| Descriptor | Yes | Complete authoritative set including exact recovery key | Reject missing/extra/duplicate/blank |
| Future D enforcement | Yes | Exact `[HasPermission]` key equals manifest key | Block publication/reconciliation |
| Manifest page | Yes | Existing `GLOBAL_PRODUCTS`; existing route/read permission unchanged | Reject new/synthetic route or page |
| Manifest action | Yes | Exact code, `System`, sort 50, dangerous true, toolbar/row false | Reject visible or address-bearing action |
| Role name | Yes | `ProductIdentityRecoveryOperator` system role | Non-system collision fails |
| Role matrix | Yes | Recovery key only | Extra/missing grant fails |
| Manual permission assignment | Yes | Recovery key denied for any role/context; all keys denied for reserved role | 403 before grant/version/audit mutation |
| Existing reserved-role grants | Yes | Empty, or exact recovery `Module/product-item-sku-master` only | Grant/restore blocks; no foreign cleanup |
| Tenant-wide PermissionId scan | Yes | All active tenant roles/grants; canonical reserved-role module row only | Forbidden row blocks grant/restore before mutation |
| Admin/Viewer | Yes | Zero recovery grants | Any recovery grant fails |
| Default-tenant SuperAdmin | Yes | Full catalog except exact recovery key | Recovery auto-grant fails; every other registered permission remains unchanged |
| Existing dedicated roles | Yes | Zero recovery grants | Any leakage fails |
| Entitlement | Yes | Active/effective, authoritative current tenant | Otherwise zero module grants |
| Authoritative revoke | Yes | Remove every source-canonical recovery row regardless of role | Re-scan all remaining forbidden rows; blocked evidence |
| Wrong-source recovery after revoke | Yes | Cannot claim total effective-zero in positive-grant model | Block closure pending remediation |
| Post-revoke failure | Yes | Re-scan, exact log event + coded `InvalidOperationException` after delete | Inbox not completed; claim released/retryable |
| TenantId | Yes | Server-bound non-empty current tenant | No grant or disclosure |

## 13. Failure Path to Verify

- Missing future D permission enforcement or manifest declaration: onboarding remains blocked; no Auth mutation.
- Duplicate, deleted or tuple-divergent catalog definition: fail before role/grant mutation.
- Blank, duplicate, missing or additional descriptor key: fail before mutation; no generic fallback.
- Same-name non-system role: conflict; no grant and no silent system-role conversion.
- Tenant-context and platform-context generic assignment of recovery to any role: 403 before grant/version/audit write.
- Generic assignment of any adjacent permission to the reserved recovery role: 403 before mutation.
- Recovery PermissionId exists on any other active tenant role, or more than once: active grant/restore fails before
  role creation, stale removal or grant insertion.
- Reserved role contains contamination during active grant/restore: fail before mutation and preserve every row.
- Admin or Viewer receives recovery through generic Admin-all/read behavior: regression test fails.
- Full-catalog sync creates or restores a recovery `System` grant on default-tenant SuperAdmin: regression test fails.
- Full-catalog sync of any other registered permission no longer grants once/idempotently or changes its best-effort
  missing-role behavior: regression test fails.
- Existing Product Identity role receives recovery: exact-matrix regression fails.
- Recovery operator receives any second permission: exact one-key test fails.
- Disabled/expired entitlement with a clean tenant-wide scan: every source-canonical recovery grant is revoked; effective recovery is zero.
- Disabled/expired entitlement with non-recovery contamination: exact product-module recovery grant is revoked despite
  contamination; foreign rows remain, effective recovery is zero, and blocked evidence records role impurity.
- Disabled/expired entitlement with Manual/System/foreign-module recovery contamination: exact authoritative row is
  removed if present, foreign recovery remains, and the run is blocked/remediation-required rather than falsely
  claiming effective-zero or successful closure.
- Post-delete forbidden-row exception is observed by entitlement-event and tenant-activation callers: inbox completion
  is absent, the claim is released (or lease-expires if release fails), and the same EventId replay repeats the scan
  without recreating the canonical grant or falsely completing.
- Re-enable after revoke: same canonical role is restored to exactly one grant without duplicates.
- Repeated identical reconcile: role/grant cardinality and assignment cardinality remain stable.
- Tenant A reconcile/revoke/restore never creates, removes or discloses Tenant B role/grant state.
- Cancellation or repository exception: operation does not report completion and replay remains possible.

## 14. Authorization Convention

Future D policy is `[Authorize]` plus `[HasPermission("mdm.product-identity.lifecycle-operations.recover")]` on the
operator endpoint. Permission possession does not bypass the MDM recovery handler's authoritative candidate, Workflow
NotFound, version, tenant, replay or audit checks.

The recovery key is not manually assignable. `DefaultRolePermissionTemplate.IsTenantAssignable` must exclude it, but
that alone is insufficient because the current handler exempts platform context; the exact handler guard must reject
this key before the exemption. The reserved role is also provisioning-managed for permissions: generic assignment of
any permission to it is denied in every context. Generic assignment of every other permission to every other role keeps
its current tenant/platform behavior.

| Role | Recovery grant after active entitlement | Membership |
|---|---:|---|
| Tenant Admin | 0 | Existing membership unchanged |
| Tenant Viewer | 0 | Existing membership unchanged |
| ProductDataSteward | 0 | Existing membership unchanged |
| ProductIdentityApprover | 0 | Existing membership unchanged |
| ProductIdentityRetirementSteward | 0 | Existing membership unchanged |
| ABB/scope responsibility roles | 0 | Existing membership unchanged |
| `ProductIdentityRecoveryOperator` | Exactly recovery | No automatic assignment |

An authorized operator may later assign a human through the existing Auth role-assignment contract under separate
operational approval. Platform-admin bypass is not acceptance evidence and this pack grants nothing to that actor type.

## 15. Gateway / API Routing Decision

Gateway change is unnecessary and prohibited in FU26.

- FU26 adds no endpoint or HTTP method.
- Future D owns any MDM API route and must separately prove the existing Gateway contract or obtain an
  integration-agent-owned route change.
- Browser/service direct-port behavior, frontend proxy and navigation are outside this pack.

## 16. Acceptance Criteria

- [ ] DCP-002 identity/collision proof remains green for exact ID, name and parent.
- [x] MOD-0290 D enforces the exact recovery key before the MDM manifest publishes it.
- [x] The separately approved MOD-0290 D amendment adds exactly one invisible `System` recovery action to existing
      `GLOBAL_PRODUCTS`; FU26 only reads exact page/action/API evidence and changes no MDM file.
- [ ] Generic Platform transport creates exactly one global active catalog definition with exact tuple and no Platform change.
- [ ] Active entitlement provisions one `ProductIdentityRecoveryOperator` system role with exactly one module grant.
- [ ] Active grant/restore first scans the recovery PermissionId across all active tenant roles/grants; only the exact
      canonical system-role/module-source row is allowed and every forbidden row fails before mutation.
- [ ] No user-role assignment is created by catalog, entitlement, startup or replay reconciliation.
- [ ] Tenant and platform-context generic permission assignment both reject recovery for every role, and reject every
      permission for the reserved recovery role, before grant/version/audit mutation.
- [ ] Admin, Viewer and every existing dedicated role receive zero recovery grants.
- [ ] First-create and reactivate catalog sync never auto-grant the exact recovery permission to default-tenant
      SuperAdmin, while every other registered permission preserves existing once-only/idempotent full-catalog grant
      behavior and missing-role/lookup-failure remains non-throwing with zero grant.
- [ ] Missing/disabled/expired entitlement removes every exact `Module/product-item-sku-master` recovery grant even on
      a contaminated role; foreign/manual/system/wrong-source rows remain and produce blocked remediation evidence.
- [ ] Authoritative revoke/restore preserves manual/system/other-module grants and restores the exact one-key plan.
- [ ] Replay is idempotent; role, grant and assignment cardinalities remain stable.
- [ ] Duplicate/deleted/divergent catalog, malformed descriptor and non-system role collision fail before mutation.
- [ ] Existing reserved-role contamination fails before mutation on grant/restore; revoke still removes the exact
      authoritative row and preserves contaminants.
- [ ] Effective recovery is proven zero after revoke for clean/non-recovery-contaminated roles. A preserved wrong-source
      recovery row blocks this claim and operational closure until separate remediation.
- [ ] Persist-then-fail replay proves canonical delete survives the dedicated exception, inbox is never completed, claim
      release/lease retry works, and the same EventId never recreates or re-deletes an already absent canonical row.
- [ ] Tenant A/B grant, revoke, restore and read-back isolation passes with cross-tenant not-found/non-disclosure behavior.
- [ ] Existing FU20–FU25 role/grant matrices remain unchanged except for the new isolated role cardinality.
- [ ] D manifest/API operational exposure, token proof, smoke and acceptance remain prohibited until the tenant-wide
      recovery PermissionId scan is clean.
- [ ] No runtime outside Section 5, config/data, Gateway/frontend, WorkCenter, Production/Staging or push occurs.

## 17. Test Expectations

| Area | Required evidence |
|---|---|
| Manifest/future D | Existing `GLOBAL_PRODUCTS` shape unchanged; exact invisible `System` action; API key equality; no new route/page |
| Profile unit | Exact role name, singleton key, tuple validation and all malformed-set negative cases |
| Generic assignment | Tenant/platform deny recovery on any role; deny any permission on reserved role; other behavior unchanged |
| Default-role deny | Admin/Viewer recovery grant count zero before and after active entitlement |
| Full-catalog deny/regression | Exact recovery key creates no SuperAdmin `System` grant on create/reactivate; another registered permission still grants once and replay is idempotent |
| Dedicated-role deny | Existing lifecycle/ABB/scope roles recovery grant count zero |
| Assignment safety | No `UserRole` row created on reconcile, replay, revoke or restore |
| Entitlement | Active, missing, disabled, expired and uncertain outcomes |
| Revoke/restore | Grant/restore contamination preflight; revoke despite contamination; exact source removal |
| Contamination | Foreign rows preserved; blocked evidence; wrong-source recovery prevents effective-zero claim |
| Tenant-wide scan | Recovery PermissionId on other role/source/duplicate; all forbidden grant/restore before mutation |
| Event completion | Persist-then-fail delete, no CompleteClaim, release/lease retry, same-EventId stable replay |
| Replay | Stable role ID, one active recovery grant, no duplicate/soft-delete drift |
| Tenant safety | Real Tenant A/B create/read/revoke/restore isolation and collision fail-before-mutation |
| Regression | FU20–FU25 profile/sync/real-Mongo suites and current `13/7/8` lifecycle matrices |
| Quality | Focused tests, full Auth suite, Auth Release build and `git diff --check` |

Persistence claims require a real `mongodb://localhost:27017` isolated disposable database; fake/in-memory tests cannot
close replay, tenant-isolation or revoke/restore acceptance. Operational catalog/entitlement/token/API evidence is a
separate gate after code tests and is not authorized by this draft.

## 18. Ready-for-dev Checklist

- [x] AGENTS.md, module-pack-author and prepare-module-pack instructions read.
- [x] PSS domain config, master plan, full registry and delivery board read.
- [x] Module-pack standard and backend-only `none` decision read and applied.
- [x] FU20/FU25 precedents and Auth role/grant sync plus MDM manifest code truth inspected.
- [x] DCP-002 preflight passed; FU26 registry/pack/code collision check is clear.
- [x] Exact permission, role, no-assignment rule and zero Admin/Viewer grant are frozen.
- [x] Tenant/platform manual-assignment bypass closure and reserved-role generic-assignment guard are frozen.
- [x] Existing reserved-role contamination fail-closed/no-cleanup rule is frozen.
- [x] Grant/restore contamination preflight is separated from narrowing revoke behavior.
- [x] Wrong-source recovery limitation and operational-remediation blocker are explicit.
- [x] Tenant-wide recovery PermissionId preflight and exact canonical-row predicate are frozen.
- [x] Existing persist-then-fail plus inbox release/retry mechanism is frozen; no evidence entity is introduced.
- [x] Future D manifest is frozen as one invisible action on existing `GLOBAL_PRODUCTS`; no invented route/page.
- [x] Role, revoke/restore, replay and tenant-isolation matrices are frozen.
- [x] Exact future runtime/test allow-list and protected paths are frozen.
- [x] Auth owner approves the pure recovery profile and sync composition.
- [x] Separately approved MOD-0290 D amendment implements/tests exact endpoint enforcement and frozen manifest shape.
- [x] User promotes this pack to `approved` or `ready-for-dev`.
- [x] User separately authorizes the exact Section 5 runtime/test code-start.
- [ ] After green code verification, user separately authorizes any Local Development operational reconciliation.
- [ ] Production/Staging and push remain separately prohibited until explicit later gates close.

## 19. Implementation Notes

### 2026-09-02 scoped implementation evidence

The authorized Section 5 Auth slice is implemented and its saved evidence remains under review:

- Exact singleton recovery profile and reserved system-role plan.
- Tenant and platform-context generic assignment guards before grant/version/audit mutation.
- Recovery exclusion from every default-role plan, including default-tenant SuperAdmin baseline selection.
- Tenant-wide active grant/restore contamination preflight plus an immediate second race-fence scan.
- Source-canonical tenant-wide narrowing revoke, foreign-row preservation, coded persist-then-fail behavior and
  retryable entitlement/TenantActivated inbox semantics.
- Real-Mongo replay, tenant isolation, no-membership, contamination, narrowing revoke and same-EventId recovery proof.

Measured results:

- FU26 + assign/sync/event/inbox/full-catalog focused tests: `159/159`, `0 skipped`.
- FU20–FU25 special-profile regression selection after final review remediation: `105/105`, `0 skipped`.
- Auth API Release build: `0 errors`, `0 warnings`.
- Full Auth suite: `744/746`; the two failures are pre-existing `UserLookupValidationContractTests` expecting the old
  two-field response while the dirty worktree already contains `MaskedName` and `MaskedEmail`.
- Mongo test-database architecture guard: FU26 introduced no violation; `4/5` passed and the sole remaining failure
  identifies two pre-existing Platform audit tests that create per-run databases.

The separately approved full-catalog amendment is closed: `FullCatalogPermissionGrantService` resolves the registered
permission and excludes only the exact provisioning-only recovery key before default-tenant SuperAdmin grant mutation.
Focused tests prove the recovery key is not auto-granted, ordinary full-catalog idempotency is unchanged and lookup
cancellation propagates. Independent review remediation additionally proves missing permission short-circuit, inner
role/grant cancellation propagation, duplicate canonical-row fencing, same-source non-recovery contamination
preservation and deleted/divergent catalog narrowing revoke in unit and real Mongo coverage. No operational
reconciliation was run; explicit user assignment and future MOD-0290 D remain separate gates.

Code truth measured on 2026-09-02:

- MOD-0290 recovery A–D exists as application orchestration, repository persistence, worker/runner support and the
  exact operator HTTP/manifest surface recorded in Section 0.
- The manifest publishes the exact recovery permission as one invisible System action on existing `GLOBAL_PRODUCTS`.
- Auth `ProductIdentityLifecycleEntitlementGrantProfile` owns existing Product Identity role templates; FU25 currently
  defines exact Steward/Approver/Retirement matrices `13/7/8` and shared Workflow dependencies.
- `EntitlementPermissionSyncService` already supports exact special profiles, tenant system-role provisioning,
  module-sourced authoritative revoke/restore and real-Mongo replay. Generic Admin-all behavior means the recovery key
  must be isolated by a dedicated special profile; merely adding the manifest key would be unsafe.
- `DefaultRolePermissionTemplate.IsTenantAssignable` currently accepts every Tenant-scoped permission, while
  `AssignPermissionCommandHandler` exempts platform context and writes a `Manual` grant. Without the two exact guards,
  either tenant or platform operators could bypass the dedicated profile.
- `FullCatalogPermissionGrantService` now uses the already registered `IPermissionRepository.GetByIdAsync` seam and
  skips only the exact provisioning-only recovery permission. `IFullCatalogPermissionGrantService`,
  `InternalPermissionsController`, persistence repositories and DI registration remain unchanged.
- Existing system-role protection prevents delete and protects System/Module grant revoke, but generic permission assign
  still accepts system roles. FU26 therefore reserves only the recovery role's permission plan while leaving explicit
  user-role membership assign/revoke behavior unchanged.
- `RoleRepository.GetAllByTenantAsync` returns all active tenant roles; `RolePermissionRepository.GetByRoleAsync`
  returns active tenant-bound grant rows with `GrantSource` and `SourceModuleCode`. These existing seams are sufficient
  for the required tenant-wide PermissionId preflight; no repository or evidence schema is added.
- `RolePermissionRepository.RemoveByIdAsync` is an immediately persisted Mongo delete and the sync service has no
  enclosing transaction. Therefore a deliberate exception after canonical revoke does not roll back the narrowing
  delete. This is required for contaminated disable/expiry handling.
- `EntitlementSyncConsumer` calls `CompleteClaimAsync` only after sync returns. Its catch releases and rethrows.
  `InternalEventsController.TenantActivated` has the same complete-after-sync/release-on-exception ordering. These are
  the exact existing incomplete/retry mechanisms; the pack does not invent a blocked-evidence entity or completed-with-
  warning state.
- `ModuleManifestAction` has no route/address and requires a parent `ModuleManifestPage`; `PageType=Custom` and
  `ActionType=System` are valid enums, but a new page still requires an artificial route. Anchoring the invisible
  action to existing `GLOBAL_PRODUCTS` is the only measured no-new-route shape and does not advertise a UI action.
- `DataSeeder.cs` is deliberately excluded. Runtime startup must not create an operator membership or tenant-specific
  recovery grant outside authoritative entitlement reconciliation.

The proposed profile is separate from the lifecycle Steward/Approver/Retirement profile because recovery is an
exception operator capability, not a normal maker/checker or retirement responsibility. It must not change the current
`13/7/8` matrices or turn `ProductDataSteward` into a recovery operator.

## 20. Follow-up Items

1. Obtain Auth owner review, then explicit user promotion of this draft.
2. Obtain separate user approval for the exact Section 5 runtime/test allow-list.
3. Preserve the implemented MOD-0290 D endpoint/manifest key equality as a read-only dependency contract.
4. If contamination exists, active grant/restore stops. Authoritative disable/expiry/missing revoke may still remove
   the exact product-module recovery row, then emits blocked/remediation evidence. FU26 never deletes, rewrites or
   source-converts Manual/System/foreign rows.
5. Run focused, real-Mongo, full Auth and Release-build verification; do not perform operational reconciliation yet.
6. If code verification is green, obtain a separate Local Development catalog/entitlement/replay/token/API approval.
7. Explicit human assignment to `ProductIdentityRecoveryOperator` requires a separate operational approval and must use
   the supported Auth assignment contract; no automatic assignment is ever permitted.
8. Production/Staging enablement, configuration/data changes and push remain separate user-controlled gates.
9. Operator assignment, token refresh/login, smoke and acceptance remain closed until
   the tenant-wide recovery PermissionId scan is clean; a blocked/retryable inbox is not acceptance evidence.
