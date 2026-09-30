---
id: MOD-0018-FU19
name: LSKU Permission Onboarding
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: EntityBase
status: review
owner: auth-owner
branch: feature/pss/mod-0018-fu19-lsku-permission-onboarding
started: 2026-08-08
target: ""
form_field_count: 0
parent_module: MOD-0018
consumer_module: MOD-0290
---

# MOD-0018-FU19 — LSKU Permission Onboarding

> **Review/code-truth guard (2026-08-09).** The Section 5 implementation and separately authorized Local Development
> pilot reconciliation are complete. Admin read/create, Viewer read-only and tenant isolation are evidenced for the
> exact LSKU keys. Navigation and Production enablement remain separate gates.
>
> **Identity proof.** Master 8.1 is the business/model authority: parent `MOD-0018` is `RBAC / ABAC Authorization`.
> `MOD-0018-FU19` is a child follow-up, not an invented product-module ID. The DCP-002 command
> `verify_module_id.py . --check-id MOD-0018-FU19 --name "LSKU Permission Onboarding" --parent MOD-0018` returned
> `OK MOD-0018-FU19: proven against Blueprint/registry` on 2026-08-08. This legacy verifier result is mechanical
> compatibility evidence only; it does not replace Master 8.1 authority.
>
> **Golden-reference decision.** This is backend-only authorization onboarding, not a Razor, CRUD or DataTable module:
> `shell: none`, `golden_reference: none`, `form_field_count: 0`.

## 1. Module Summary

FU19 prepares the least-privilege Auth catalog/grant onboarding contract for the existing MOD-0290 LSKU Register.
It owns exactly `mdm.lskus.read` and `mdm.lskus.create`. Both use `Permission.Module` and the shared tenant
entitlement `ModuleCode` `product-item-sku-master`. That one entitlement covers Global Product, GSKU, Finished Good
and LSKU; the `mdm` key namespace is not an entitlement alias.

## 2. Ownership and Boundaries

**In scope:** global Auth permission definitions, baseline exclusion, entitlement-aware default-role grants, and focused
Auth proof for the two LSKU keys.

**Out of scope:** MOD-0290 LSKU controller/manifest/CQRS/persistence/frontend work; Platform catalog or entitlement
mechanism changes; permission seed/grant execution; live entitlement mutation; token issuance/refresh; navigation; and
production enablement. FU16, FU17 and FU18 remain unchanged and retain ownership of their respective surfaces.

## 3. Owned Objects

| Object | Locked owner/invariant |
|---|---|
| `mdm.lskus.read` | One global Auth catalog definition; tenant-assignable, no `TenantId` |
| `mdm.lskus.create` | One global Auth catalog definition; tenant-assignable, no `TenantId` |
| Permission attribution | `Permission.Module = product-item-sku-master` |
| Tenant Admin grants | Both LSKU keys, only through active shared entitlement |
| Tenant Viewer grant | Read only, only through active shared entitlement |
| Shared entitlement | Existing `product-item-sku-master`; no LSKU-specific entitlement |

The MDM `ProductItemSkuMasterManifestProvider` already declares `LSKUS`, the two exact keys, two actions, and
`IsNavigationVisible: false`; FU19 does not own or edit it. Platform generic manifest/catalog/entitlement transport is
consumed unchanged unless an evidenced blocking defect requires a revised, approved pack.

## 4. Entity Fields

No entity or DTO is introduced. `entity_base: EntityBase` denotes tenant-owned `RolePermission` grants only.

| Existing field | Locked value/rule |
|---|---|
| `Permission.Key` | Exactly `mdm.lskus.read` or `mdm.lskus.create` |
| `Permission.Module` | Exactly `product-item-sku-master` |
| `Permission.Resource` / `Action` | `lskus` / `read` or `create` only |
| `Permission.TenantId` | Prohibited; catalog is global |
| `RolePermission.TenantId` | Required, server-derived, tenant-isolated |
| `RolePermission.GrantSource` | `Module` |
| `RolePermission.SourceModuleCode` | Exactly `product-item-sku-master` |
| `TenantModuleEntitlement.ModuleCode` | Exactly `product-item-sku-master` |

## 5. Repo Scope

This planning change owns only this pack and one canonical registry row. A later separately authorized implementation
has this exact runtime allow-list—no wildcard or adjacent file is implied:

- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`

The inspected generic `EntitlementPermissionSyncService` already provides Admin/full and Viewer/read-only selection,
tenant/source-scoped idempotent grants, and matching module-source-only revocation. Therefore it is deliberately not
allow-listed; neither are Platform files, seeds, consumers, clients, configuration, secrets or MDM files.

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, Blueprint workbooks, DCPs and backlog artifacts.
- MOD-0018-FU16, MOD-0018-FU17, MOD-0018-FU18 and all other module packs.
- All MDM runtime/tests including `ProductItemSkuMasterManifestProvider.cs`; all Platform, frontend and gateway files.
- Every Auth file outside the exact Section 5 allow-list, including permission seeders/repositories, role/user mutation
  endpoints, entitlement consumers/clients, token issuance/refresh, configuration and secrets.
- `gateway/Diten.ApiGateway/**/ocelot.json`, archive/frozen paths, and unrelated dirty worktree changes.

## 7. Dependencies

- Master 8.1 and canonical parent MOD-0018.
- MOD-0290 LSKU contract and its existing MDM manifest declaration/enforcement.
- FU16 Global Product, FU17 Finished Good and FU18 GSKU as unchanged shared-entitlement regressions.
- Existing `DefaultRolePermissionTemplate`, `EntitlementPermissionSyncService`, generic Platform manifest/catalog and
  authoritative tenant-entitlement chain.

## 8. Runtime Constraints

- The two catalog records are global and appear once each; no tenant-specific duplicate seed exists.
- Active authoritative entitlement: Tenant Admin allow/read+create; Tenant Viewer allow/read and deny/create.
- Missing, disabled or expired entitlement: both roles deny/read+create through this onboarding.
- Unavailable, timeout, malformed, null or ambiguous entitlement reads grant nothing and revoke nothing.
- Grants/revokes are idempotent, tenant-isolated, module-sourced and preserve Manual, System, other-module and
  other-tenant rows. ABB permissions remain untouched.
- Navigation enablement is a separate H-step and remains disabled/out of scope.

## 9. Layout & Shell Contract

Not applicable: no page, layout, DataTable, frontend route or navigation item is owned.

## 10. Backend File Convention

No feature folder, handler, controller, entity or repository is added. A future change may alter only the three exact
Auth files in Section 5 and must retain their existing naming and generic service design.

## 11. Frontend File Contract

Not applicable. `LSKUS` manifest navigation remains hidden; no frontend file is authorized.

## 12. Validation Rules

| Input/fact | Required rule | Fail-closed result |
|---|---|---|
| Permission set | Exact two-key allow-list | No alias, wildcard or adjacent action |
| ModuleCode | `product-item-sku-master` | No `lsku`, `lskus`, `mdm` or MOD-ID alias grant |
| Catalog tenancy | Global permission, no `TenantId` | Reject duplicate tenant catalog definition |
| Role/action | Admin full; Viewer read only | Viewer create denied |
| Entitlement | Authoritative and active for grant | Otherwise no grant |
| Tenant/source | Server-derived + exact source module | Cross-tenant/other-source mutation prohibited |

## 13. Failure Path to Verify

- Missing, disabled or expired entitlement: neither role receives either LSKU key.
- Active entitlement: Admin receives exact read/create; Viewer receives exact read; Viewer create is denied.
- Unavailable/ambiguous entitlement result: no grant and no revoke.
- Replay: no duplicate `RolePermission`; Tenant A never changes Tenant B.
- Confirmed removal: only matching Module/source-module rows are removed; Manual/System/other-module survive.
- Missing catalog key: no replacement or invented permission.
- Global Product, GSKU, Finished Good matrices remain unchanged; ABB keys are never granted, revoked, renamed or attributed.

## 14. Authorization Convention

| Role / entitlement state | `mdm.lskus.read` | `mdm.lskus.create` |
|---|---|---|
| Tenant Admin + active entitlement | allow | allow |
| Tenant Viewer + active entitlement | allow | deny |
| Missing / disabled / expired entitlement | deny | deny |

Automatic rows use `GrantSource = Module` and `SourceModuleCode = product-item-sku-master`. This is a design contract,
not authorization to mutate roles, users or data.

## 15. Gateway / API Routing Decision

No route change. MOD-0290 owns LSKU API exposure; `ocelot.json` stays protected.

## 16. Acceptance Criteria

- [x] Parent/FU decision, Master 8.1 authority and successful DCP-002 preflight are recorded.
- [x] Exact keys, ModuleCode, shared entitlement, shell and backend-only boundary are locked.
- [x] Exact three-file Auth runtime allow-list and protected paths are recorded.
- [ ] Each LSKU key exists once in the global Auth catalog with exact attribution and no `TenantId`.
- [x] Active entitlement produces the Section 14 role matrix, idempotently and tenant-isolated (focused Auth proof).
- [x] Missing/disabled/expired entitlement produces no LSKU grant; unavailable results mutate nothing (focused Auth proof).
- [x] Global Product, GSKU, Finished Good and ABB regression evidence passes (focused Auth proof).
- [ ] No runtime seed/grant/entitlement/token/navigation/production mutation occurs under this planning draft.

## 17. Test Expectations

| Test area | Required evidence |
|---|---|
| Baseline | Exact eight-key entitlement-only set; LSKU keys excluded from Viewer baseline |
| Active entitlement | One shared descriptor set grants Admin all eight surface keys and Viewer four read keys only |
| Isolation/idempotency | Tenant, source and replay behavior as Section 13 |
| Entitlement failure | Missing/disabled/expired deny; unavailable/ambiguous causes no mutation |
| Regressions | Global Product, GSKU, Finished Good and `mdm.product-abbreviations.*` unchanged |
| Build | Focused Auth tests, full Auth suite and Auth API build pass with reported counts |

No live seed, grant/revoke, entitlement, token or production smoke is planning verification.

## 18. Ready-for-dev Checklist

- [x] Identity and parent preflight pass; legacy result is explicitly non-authoritative.
- [x] Existing MDM manifest and generic Auth/Platform behavior have been inspected.
- [x] Exact keys, role matrix, shared entitlement, allow-list and regressions are explicit.
- [x] User approved this draft and separately authorized code start within Section 5 only (2026-08-08).
- [ ] Runtime operator supplies target environment/tenant reconciliation and production-enablement plan.

## 19. Implementation Notes

FU19 is separate because the common entitlement does not authorize implicit onboarding of a fourth resource. It avoids
expanding FU16/FU17/FU18 and preserves one owner/auditable scope per Global Product, Finished Good, GSKU and LSKU.
Current repo evidence supports no new Platform code: the manifest already carries LSKU descriptors and generic Auth sync
already supplies the required role/source/tenant semantics. A reproducible blocking defect requires pack revision and
fresh approval.

### Review and Local Development evidence — 2026-08-09

- `DefaultRolePermissionTemplate` and `EntitlementPermissionSyncService` focused tests passed **39/39**; full
  `Diten.AuthService` passed **322/322**; the MDM manifest regression passed **3/3**.
- The active pilot entitlement and refreshed sessions proved Admin read/create, Viewer read and Viewer
  create/create-options denial for LSKU. `LS-000000000004` / `TR` was read back through UI/API/Mongo and tenant
  isolation held. No second LSKU or consumer market assignment was created.
- This closes Local Development permission/smoke drift only. Navigation and Production reconciliation/readiness remain
  open.

## 20. Follow-up Items

### LSKU lifecycle owner amendment — approved 2026-09-09

The current user approval and protected MOD-0290 19.18.5 E supersede the original two-key-only scope for this
bounded lifecycle integration. Exact runtime/test paths are:

- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/ProductIdentityLifecycleEntitlementGrantProfile.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecycleEntitlementGrantProfileTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecyclePermissionOnboardingMongoTests.cs`

Add only `mdm.lskus.withdraw` to ProductDataSteward and replace the module-sourced `mdm.lskus.retire`
with `mdm.lskus.request-retirement` on ProductIdentityRetirementSteward. Retire remains catalogued/system-only;
manual, system and other-module grants are preserved. No user assignment or operational grant is performed.
Admin/Viewer gain neither action. Existing Global Product, GSKU, Finished Good and ABB grants are unchanged.
The current branch's GSKU direct-retire grant is deliberately preserved; the protected final's unrelated removal
is not copied. The actual resulting dedicated-role sizes are 20 / 7 / 11, distinct from 18 lifecycle keys.
The existing source-aware generic sync is reused without importing the final branch's recovery-role feature.

Verification: focused profile/default-role/sync/Mongo tests passed 121 / failed 0 / skipped 0.
The separate overlapping real-Mongo replay/revoke/restore/source-preservation/isolation test passed 1/0/0
in the fixed test-owned database. Earlier runs failed on stale expected catalog/grant cardinalities and
the old four-key preservation assertion; expectations now explicitly account for the two LSKU keys.
Auth Release build passed with 0 warnings and 0 errors. No live tenant reconciliation or user assignment ran.

- Prepare a separate Production-enable runbook: Production catalog reconciliation, active entitlement verification,
  grant reconciliation, token refresh and allow/deny smoke for target tenants. Section 5 implementation and Local
  Development evidence are already recorded.
- Navigation enablement remains a separate H-step.
- Any LSKU update/delete/submit/approve/retire permission requires a separate approved authorization scope.

## Product-five entitlement contract fix amendment — approved 2026-09-29

This amendment records only `PRODUCT-FIVE-ENTITLEMENT-CONTRACT-FIX-01`. It preserves this pack's frontmatter,
review status and historical validation. For this narrow fix it supersedes only earlier scope text that would block
the exact manifest/profile/sync reconciliation and proof allow-list below.

### Bounded contract

- The MDM descriptor, Auth catalog and lifecycle profile must agree on exact canonical permission keys and exact
  `Module`, `Resource`, `Action` and tenant `Scope` metadata before any role mutation.
- `mdm.product-identity.lifecycle-operations.recover` remains catalogued as a non-human descriptor but produces
  no grant for Admin, Viewer, ProductDataSteward, ProductIdentityApprover,
  ProductIdentityRetirementSteward or any other human role. No recovery-role provisioning, credential-management
  API or audit-selector work is included.
- `mdm.brands.read` is consumed through the existing cross-module dependency contract with its existing
  `brand-product-master` catalog ownership. Brand/Product behavior, authority and grants remain unchanged.
- Existing approved Finished Good keys and role semantics are preserved. Their descriptor alignment is not a new
  Finished Good lifecycle implementation or acceptance claim.
- The exact grant-add delta jointly owned by FU18/FU19 is six tenant-scoped `Module` grants with
  `SourceModuleCode = product-item-sku-master`. FU19 owns only these two LSKU additions:

| Human role | Exact LSKU grant-add delta |
|---|---|
| `ProductDataSteward` | `mdm.lskus.withdraw` |
| `ProductIdentityRetirementSteward` | `mdm.lskus.request-retirement` |

- The other four additions are the FU18 GSKU rows: ProductDataSteward receives
  `mdm.gskus.request-correction`, `mdm.gskus.update` and `mdm.gskus.withdraw`; ProductIdentityRetirementSteward
  receives `mdm.gskus.request-retirement`. No other grant-add row is authorized.
- The expected live reconciliation delta exposes the legacy LSKU direct-retire row separately: remove
  `ProductIdentityRetirementSteward -> mdm.lskus.retire` only when the existing row has
  `GrantSource = Module`, the target tenant and `SourceModuleCode = product-item-sku-master`. This amendment does
  not perform that live deletion. Manual, System, other-module, other-tenant and unrelated grants are preserved.
- The existing GSKU direct-retire and Finished Good contracts remain unchanged. Reconciliation and replay are
  idempotent; consumer failure remains retryable and does not complete the event, while successful retry completes
  once.
- No provisioning adapter, Manual-grant substitute, direct Mongo correction, live manifest POST, catalog/role
  mutation, token refresh, service restart or production enablement is authorized.

### Exact implementation and proof allow-list

Runtime files:

- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/ProductIdentityLifecycleEntitlementGrantProfile.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs`

Test files:

- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecycleEntitlementGrantProfileTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecyclePermissionOnboardingMongoTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementSyncConsumerTests.cs`

Every runtime/test path outside this exact set remains out of scope for this amendment.

### Evidence required before checkpoint

- Manifest/profile tests prove exact descriptor/catalog/profile equality, canonical metadata, recovery's non-human
  classification, Brand dependency ownership and unchanged Finished Good/GSKU contracts.
- Sync tests prove the joint six-row grant-add delta and the separate, provenance-bounded LSKU direct-retire revoke;
  recovery has zero grants across every human role and unrelated/Manual grants survive.
- The test-owned Mongo regression proves replay idempotency, tenant/source isolation, revoke/restore and preservation
  of Manual, System, other-module and unrelated rows without direct application-data edits.
- Consumer regression proves failed reconciliation remains retryable/uncompleted and successful retry completes
  exactly once.
- Focused tests and affected Auth/MDM Release builds must pass. This evidence does not authorize or claim live
  reconciliation, catalog/role mutation or Finished Good lifecycle acceptance.

## Product-five entitlement end-to-end fix amendment — approved 2026-09-29

This amendment records only `PRODUCT-FIVE-ENTITLEMENT-END-TO-END-FIX-02`. It preserves this pack's frontmatter,
review status, prior dirty amendment and historical evidence. It supersedes only earlier scope text that would block
the exact bounded checkpoint below.

### Bounded contract

- `mdm.product-identity.lifecycle-operations.recover` remains in the permission catalog, but no new human-role grant
  may be created through default-role selection (including Admin, Viewer and SuperAdmin), tenant-assignability,
  catalog create/reactivate full-catalog auto-grant, or manual assignment in tenant or platform-admin context.
  Existing historical grants are not scanned or automatically deleted; any remediation/revocation is separate.
- `mdm.brands.read` remains owned by `brand-product-master`. The `product-item-sku-master` manifest is only a
  consumer and carries the owner via explicit `PermissionOwnerModuleCode`; prefix inference is forbidden.
  Registration order and replay cannot change ownership.
- The deny-new-grant rule is exact-key bounded and does not widen general authorization or change adjacent
  permissions. Existing Finished Good behavior, the previously approved six-row GSKU/LSKU add delta, the existing
  GSKU direct-retire grant and the provenance-bounded stale LSKU direct-retire removal remain unchanged.
- No live catalog/role mutation, automatic historical-grant deletion, token refresh, service restart or Production
  enablement is authorized.

### Exact 24-path checkpoint allow-list

1. `execution/domains/platform-shared-services/module-packs/MOD-0018-FU18-gsku-permission-onboarding.md`
2. `execution/domains/platform-shared-services/module-packs/MOD-0018-FU19-lsku-permission-onboarding.md`
3. `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs`
4. `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/ProductIdentityLifecycleEntitlementGrantProfile.cs`
5. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
6. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementSyncConsumerTests.cs`
7. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecycleEntitlementGrantProfileTests.cs`
8. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecyclePermissionOnboardingMongoTests.cs`
9. `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs`
10. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs`
11. `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs`
12. `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IFullCatalogPermissionGrantService.cs`
13. `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/FullCatalogPermissionGrantService.cs`
14. `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs`
15. `services/Diten.AuthService/src/Diten.AuthService.Application/Features/Roles/Handlers/CommandHandlers/AssignPermissionCommandHandler.cs`
16. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs`
17. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Permissions/FullCatalogPermissionGrantServiceTests.cs`
18. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Permissions/InternalPermissionsControllerTests.cs`
19. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/AssignPermissionCommandHandlerTests.cs`
20. `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.ModuleRegistration.Abstractions/ModuleManifestDocument.cs`
21. `services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommandHandler.cs`
22. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/ModuleRegistration/RegisterModuleManifestCommandHandlerTests.cs`
23. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/ModuleCatalog/CatalogPermissionSyncServiceTests.cs`
24. `execution/domains/master-data-management/module-packs/MOD-0290-FU01-brand-product-master-boundary.md`

Every other tracked path is out of scope; `.local/**` and `.testoutput/**` remain non-checkpoint artifacts.

### Test gates before checkpoint

- MDM manifest tests prove exact unique canonical descriptors, recovery as non-interactive System-only metadata,
  unchanged Finished Good contract, and explicit `brand-product-master` ownership for `mdm.brands.read`.
- Platform registration/catalog tests prove page/action owner propagation, no prefix inference, descriptor-order
  independence, replay convergence and the generic Auth payload retaining `brand-product-master`.
- Auth tests prove recovery stays catalogued while every default/SuperAdmin/full-catalog/manual human grant-creation
  path rejects it in tenant and platform-admin contexts; adjacent permission behavior and historical grants remain
  unchanged.
- Profile/sync/Mongo/consumer tests retain the six-row delta, zero recovery grants, provenance-bounded LSKU revoke,
  replay idempotency, tenant/source isolation, Manual/System/other-source preservation and failure/retry semantics.
- Focused affected suites plus Auth, MDM, Platform and Building Blocks Release builds must pass. Test evidence does
  not claim live reconciliation, general authorization expansion, Finished Good acceptance or Production enablement.

## Product-five entitlement reconciliation command participation — approved code/test start 2026-09-29

**Superseding approval record (2026-09-29):** the user's explicit `onaylıyorum` to the consolidated three-step code/test
approval includes FU19's bounded participation in the shared **14 runtime + 7 test** FU18 allow-list; no second runtime
allow-list or runner is created. The operator permission `auth.roles.assign-permission` and documented observed-quiescence
residual, including independent/versionless writers, are accepted. General frontmatter/status and previous work remain
unchanged. This amendment records FU19's bounded participation in
`PRODUCT-FIVE-ENTITLEMENT-RECONCILIATION-COMMAND-CONTRACT-01`. The complete command, authority, concurrency,
provenance, allow-list and test contract is the same-dated section in
`MOD-0018-FU18-gsku-permission-onboarding.md`; FU19 does not define a second runner, API, event or rule copy.

For tenant `74355e70-4c7d-410c-8cf6-db5fe3b9547f` and module `product-item-sku-master`, FU19 contributes exactly:

- add `mdm.lskus.withdraw` to ProductDataSteward with `GrantSource=Module` and
  `SourceModuleCode=product-item-sku-master`;
- add `mdm.lskus.request-retirement` to ProductIdentityRetirementSteward with the same provenance; and
- remove only grant `dc241b94-3a12-4825-bcb0-b66a7189124f` after re-proving its tenant, retirement-steward role,
  `mdm.lskus.retire`, Module source and `product-item-sku-master` source module.

Every Manual/System/other-module grant, User/UserRole/membership row and unrelated LSKU permission remains outside
the write set. Plan and apply must use the shared authoritative entitlement profile and the single transaction,
role-assignment-version, affected-holder refresh-token revocation, audit receipt, replay and ambiguous-outcome
contract recorded in FU18. That transaction is not represented as a fence against sessionless UserRole,
RolePermission or refresh-token writers. FU18's observed-quiescence maintenance window and its residual-risk decision
apply equally to these LSKU rows. A local-commit/pending or manual receipt can never become success merely because
the LSKU post-state matches on replay; fresh operator/Platform/local verification and the immutable manual-state rules
remain mandatory. Only bounded implementation, build and test-owned verification are authorized. Live DB writes,
service start, stage, checkpoint, push and merge remain unauthorized; live plan/apply requires its separate approval.

### Shared writer-inventory correction participation — 2026-09-30

FU18's same-date closed-writer inventory correction is authoritative for this shared command. It adds no FU19
runtime/test path, permission, grant mutation, shutdown guarantee or live authority. The newly enumerated baseline
Auth handlers remain covered only by stopping the normal Auth writer host; reachable Platform administrator and
Tenant writers remain outside that stop boundary and are limited only by the existing source fingerprints and
durable manual-outcome behavior. In particular, Platform Tenant persistence uses whole-document replacement, so
tenant admin/settings and query-shaped writers cannot be classified as harmless metadata updates.

The operational entitlement reads themselves remain read-only and do not invoke the two query-shaped Tenant writers.
The 21-path shared allow-list, observed-quiescence residual, direct-client residual and stop-on-new-writer guard remain
unchanged. This factual inventory correction does not authorize implementation restart by itself, live plan/apply,
service/process/configuration/data mutation, stage, commit, push or merge.
