---
id: MOD-0018-FU18
name: GSKU Permission Onboarding
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: EntityBase
status: review
owner: auth-owner
branch: feature/pss/mod-0018-fu18-gsku-permission-onboarding
started: 2026-08-06
target: ""
form_field_count: 0
parent_module: MOD-0018
consumer_module: MOD-0290
---

# MOD-0018-FU18 — GSKU Permission Onboarding

> **Review/code-truth guard (2026-08-09).** The Section 5 implementation and separately authorized Local Development
> pilot reconciliation are complete. Admin read/create, Viewer read-only and tenant isolation are evidenced for the
> exact GSKU keys. This does not authorize Production enablement or further navigation mutation.
>
> **Identity preflight.** Master 8.1 `Blueprint_Data!A19:AG19` identifies canonical parent `MOD-0018` as
> `RBAC / ABAC Authorization`; `SoR_Map!A96:C96` assigns the Roles SoR to MOD-0018. Repo and registry inspection found
> no `MOD-0018-FU18` or `GSKU Permission Onboarding` collision. FU18 is a narrow Auth onboarding child of MOD-0018,
> not a new MDM capability and not an expansion of FU16/FU17. On 2026-08-07 the exact legacy verifier was run with the
> bundled workspace Python runtime and returned `OK MOD-0018-FU18: proven against Blueprint/registry`. This is
> mechanical compatibility evidence only. Master 8.1 remains the business/model authority.
>
> **Golden Reference decision.** This is backend-only authorization onboarding, not CRUD, Razor or DataTable work.
> Therefore `shell: none`, `golden_reference: none` and `form_field_count: 0` are intentional.

## 1. Module Summary

This follow-up governs Auth catalog/grant onboarding for exactly two permissions already approved for the MOD-0290
GSKU Register exposure:

- `mdm.gskus.read`
- `mdm.gskus.create`

Both permissions use `Permission.Module = product-item-sku-master` and the existing tenant module entitlement with
`ModuleCode = product-item-sku-master`. The same entitlement covers Global Product, GSKU and Finished Good. The
permission-key namespace remains `mdm`; it is not the entitlement or source-module code.

FU18 is required separately because MOD-0290 owns GSKU declaration/enforcement while MOD-0018 owns Auth catalog and
tenant-role onboarding. FU16 is limited to Global Product and FU17 is limited to Finished Good; their approval cannot
implicitly mint or grant GSKU permissions merely because all three surfaces share one entitlement.

## 2. Ownership and Boundaries

**In scope**

- Auth baseline exclusion so neither GSKU key is granted by the unconditional Viewer baseline.
- Auth catalog/grant contract for one global record per exact key, attributed to `product-item-sku-master`.
- Entitlement-aware default-role behavior: Tenant Admin receives read/create and Tenant Viewer receives read only.
- Tenant-scoped, module-sourced grants with exact `SourceModuleCode = product-item-sku-master`.
- Idempotent grant/revoke and fail-closed unavailable-result evidence through the existing generic entitlement chain.
- Regression evidence for existing Global Product, Finished Good and ABB permissions.

**Out of scope**

- GSKU manifest/page/action declaration; this belongs to the MOD-0290 GSKU exposure step.
- Any edit to MOD-0018-FU16 or MOD-0018-FU17.
- Runtime permission seed execution, catalog reconciliation execution, role/user grant, entitlement mutation, token
  issuance/refresh, live smoke, navigation change or production enablement.
- New Platform-specific catalog/entitlement code when the existing generic chain satisfies the contract.
- MDM controller, manifest, CQRS, domain, persistence, frontend or Gateway changes.
- Any permission beyond the two exact keys, including update/delete/bulk/lifecycle/manage aliases.
- ABB permission onboarding or changes to any `mdm.product-abbreviations.*` permission.

## 3. Owned Objects

| Object | Owner / invariant |
|---|---|
| `mdm.gskus.read` | Global Auth permission catalog record; no `TenantId`; globally unique; tenant-assignable |
| `mdm.gskus.create` | Global Auth permission catalog record; no `TenantId`; globally unique; tenant-assignable |
| Permission attribution | `Permission.Module = product-item-sku-master` for both exact keys |
| Tenant Admin grants | Both exact keys, tenant-scoped and module-sourced |
| Tenant Viewer grant | Read key only, tenant-scoped and module-sourced |
| Shared entitlement | Existing `product-item-sku-master` tenant entitlement; no GSKU-specific entitlement |

MDM owns the future additive `GSKUS` manifest page and its two permission descriptors. Platform owns generic manifest
reconciliation, permission-catalog forwarding and entitlement projection. Auth owns global permission persistence and
tenant role grants. FU18 owns only the GSKU-specific Auth onboarding contract and focused proof.

## 4. Entity Fields

No new entity or DTO is introduced. `entity_base: EntityBase` records the Auth tenant-owned grant boundary; it does
not authorize a new subtype.

| Existing field | Locked value / rule |
|---|---|
| `Permission.Key` | Exactly `mdm.gskus.read` or `mdm.gskus.create` |
| `Permission.Module` | Exactly `product-item-sku-master` |
| `Permission.Resource` | `gskus` |
| `Permission.Action` | `read` or `create` only |
| `Permission.TenantId` | Prohibited; Permission is global catalog data |
| `Permission.Scope` | Tenant-assignable under the existing scope convention |
| `RolePermission.TenantId` | Required, server-derived and exact |
| `RolePermission.GrantSource` | `Module` for automatic entitlement grants |
| `RolePermission.SourceModuleCode` | Exactly `product-item-sku-master` |
| `TenantModuleEntitlement.ModuleCode` | Exactly `product-item-sku-master` |

## 5. Repo Scope

This planning task may change only:

- `execution/domains/platform-shared-services/module-packs/MOD-0018-FU18-gsku-permission-onboarding.md`
- `execution/registries/module-id-registry.md` — one canonical FU18 identity row only

A later, separately authorized runtime implementation is limited to this exact allow-list:

- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs`
  — add only the two GSKU keys to `EntitlementOnlyViewerPermissions`.
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs`
  — prove exact baseline exclusion and adjacent Global Product, Finished Good and unrelated read non-regression.
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
  — prove the six-key shared-entitlement role matrix, idempotency, tenant/source isolation, catalog-missing behavior and
  ABB non-regression through the unchanged generic sync service.

No change to `EntitlementPermissionSyncService.cs` is planned: current code already grants Admin the declared set,
Viewer only `read`, scopes grants by tenant and removes only matching module-sourced rows. No Platform production/test
file is allow-listed because the current manifest/catalog/entitlement chain is generic and sufficient on inspected
code truth. A demonstrated implementation-blocking defect requires pack revision and fresh user approval.

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, Blueprint workbooks, DCPs and backlog files.
- MOD-0018-FU16, MOD-0018-FU17 and every other Module Pack.
- MOD-0290 Module Pack, Domain Contract and all MDM runtime/test/frontend files, including
  `ProductItemSkuMasterManifestProvider.cs`; GSKU declaration remains an MDM exposure responsibility.
- All Auth files outside the exact future runtime allow-list in Section 5, including seeders, permission repositories,
  internal permission sync controllers, role/user mutation endpoints, token issuance/refresh, consumers, clients,
  configuration and secrets.
- All `services/Diten.Platform/**`, `frontend/**` and `gateway/**` files.
- Permission catalog/seed execution, role/user grant execution, entitlement data/configuration and production data.
- Archive/frozen paths and unrelated user work.

## 7. Dependencies

- Master 8.1 MOD-0018 parent authorization authority.
- MOD-0290 `Product Definition Revision + First GSKU Register Exposure` permission and manifest contract.
- MOD-0018-FU16 Global Product and MOD-0018-FU17 Finished Good onboarding boundaries, referenced unchanged.
- Existing `DefaultRolePermissionTemplate.EntitlementOnlyViewerPermissions` baseline guard.
- Existing `EntitlementPermissionSyncService` Admin/full, Viewer/read-only, tenant/source-scoped generic behavior.
- Existing Platform `CatalogPermissionSyncService`, manifest reconciliation and tenant entitlement projection.
- Existing Auth global permission uniqueness and descriptor-key sync path.
- MOD-0018-FU13 token/cache bounded-staleness behavior, referenced unchanged.

## 8. Runtime Constraints

- The permission catalog contains exactly one global active definition per key and no per-tenant duplicate.
- Both keys retain the `mdm.gskus.*` namespace while module attribution is exactly `product-item-sku-master`.
- Global Product, GSKU and Finished Good share one entitlement; no surface-specific entitlement is created.
- Tenant scope exists only in role assignment, `RolePermission` and effective entitlement layers.
- Tenant Admin receives GSKU read/create and Tenant Viewer receives GSKU read only after authoritative active
  entitlement proof.
- Missing, disabled or expired entitlement grants neither permission to either default role.
- Confirmed authoritative absence/disable/expiry may revoke only `GrantSource.Module` rows for the same tenant and exact
  `SourceModuleCode`.
- Unavailable, timeout, 5xx, malformed, null-data, ambiguous or otherwise non-authoritative results grant nothing and
  revoke nothing.
- Manual, System, other-module and other-tenant grants are preserved.
- Grant/revoke is idempotent and tenant-isolated.
- Existing Global Product and Finished Good permissions remain unchanged; ABB permissions are neither included nor
  mutated by this descriptor set.
- Navigation visibility is a separate MOD-0290 decision and remains outside FU18.

## 9. Layout & Shell Contract

Not applicable. `shell: none`, `golden_reference: none` and `form_field_count: 0`; FU18 owns no Razor page, layout,
DataTable, frontend route or navigation item.

## 10. Backend File Convention

No feature folder, command, query, handler, controller, entity or repository is introduced. A later implementation may
only extend the existing exact set/test declarations in Section 5. Existing architecture and naming remain unchanged.

## 11. Frontend File Contract

Not applicable. No frontend file is authorized. GSKU remains direct-URL/navigation-hidden until the separate MOD-0290
navigation decision and production-enablement gates close.

## 12. Validation Rules

| Input/fact | Required rule | Fail-closed result |
|---|---|---|
| Permission set | Exact two-key allow-list | Reject aliases, wildcard or adjacent additions |
| ModuleCode | `product-item-sku-master` | No grant/revoke under `gsku`, `gskus`, `mdm` or MOD ID aliases |
| Catalog tenancy | Permission is global; no `TenantId` | Reject per-tenant catalog duplication |
| Role | Default tenant `Admin` or `Viewer` | No automatic grant to other/custom roles |
| Admin actions | `read`, `create` | No update/delete/lifecycle action inferred |
| Viewer actions | `read` only | Create remains denied |
| Entitlement result | Authoritative and active for grant | Otherwise no grant |
| Revocation evidence | Authoritative absence/disable/expiry | Unavailable/ambiguous state cannot revoke |
| Tenant | Server-derived and exact | Cross-tenant mutation prohibited |
| SourceModuleCode | Exact canonical module code | Preserve other-source grants |

## 13. Failure Path to Verify

- No entitlement → Admin and Viewer receive neither GSKU permission from onboarding.
- Disabled or expired entitlement → neither permission is effective through module onboarding.
- Active authoritative entitlement → Admin receives exact read/create; Viewer receives exact read; replay adds no row.
- Viewer with active entitlement attempts create → denied because Viewer receives no create grant.
- Confirmed entitlement removal/disable/expiry → only matching tenant/module-source rows are removed.
- Platform unavailable, timeout, 5xx, malformed response, null data or ambiguous result → no grant and no revoke.
- Catalog missing one or both GSKU keys → missing keys are not invented; available adjacent keys do not substitute.
- Duplicate/conflicting catalog identity → onboarding blocks; no second global Permission record is created.
- Tenant A reconciliation → no Tenant B row is added, removed or inspected as an authorization result.
- Same permission under Manual/System/another-module source → preserved during GSKU module revoke.
- Global Product and Finished Good shared-entitlement permissions → retain their prior role matrix.
- ABB permission rows → remain outside the descriptor set and are not granted, revoked, renamed or re-attributed.

## 14. Authorization Convention

| GSKU surface | Permission |
|---|---|
| list and detail | `mdm.gskus.read` |
| Global Product/UoM create selector and draft create | `mdm.gskus.create` |

Role matrix:

| Role / entitlement state | GSKU read | GSKU create |
|---|---|---|
| Tenant Admin + active entitlement | allow | allow |
| Tenant Viewer + active entitlement | allow | deny |
| Entitlement missing, disabled or expired | deny | deny |

Every automatic grant is tenant-scoped, has `GrantSource = Module`, and has
`SourceModuleCode = product-item-sku-master`. Other/custom roles receive no automatic grant from this pack. The matrix
is a design contract only and does not itself mutate a role or user.

## 15. Gateway / API Routing Decision

No route change. MOD-0290 owns the future `/api/gskus` Gateway/API exposure. FU18 adds no endpoint, proxy, header,
tenant injection, bypass or public entitlement surface; `ocelot.json` remains protected.

## 16. Acceptance Criteria

- [x] Master 8.1 proves parent MOD-0018 and Roles/authorization ownership.
- [x] Repo/registry collision check finds no FU18 identity or conflicting name.
- [x] FU18 is explicitly a child of MOD-0018 and not a new MDM capability.
- [x] Exact permission set is locked to `mdm.gskus.read` and `mdm.gskus.create`.
- [x] Canonical ModuleCode and shared entitlement are locked to `product-item-sku-master`.
- [x] MDM manifest declaration and Auth onboarding ownership are separated.
- [x] Existing generic Platform chain is sufficient on inspected code truth; no Platform code is allow-listed.
- [x] Exact runtime allow-list, protected paths and production-enablement separation are explicit.
- [ ] Both GSKU permissions exist once in the global Auth catalog, with no `TenantId`, exact module attribution and
      tenant-assignable scope.
- [x] GSKU keys are excluded from the unconditional Viewer baseline.
- [x] Active entitlement grants Admin read/create and Viewer read, idempotently and tenant-isolated.
- [x] Missing/disabled/expired entitlement grants neither role either GSKU permission.
- [ ] Confirmed absence revokes only matching tenant/module-source grants; unavailable results mutate nothing.
- [ ] Global Product and Finished Good role matrices remain unchanged.
- [ ] ABB permissions remain unaffected.
- [ ] No runtime seed/grant/entitlement/token/navigation or production mutation occurs under this draft.

## 17. Test Expectations

| Test area | Required evidence |
|---|---|
| Default-role baseline | Exact six-key entitlement-only set: Global Product, Finished Good and GSKU read/create; Viewer gets no GSKU read without entitlement; unrelated baseline reads stay unchanged |
| Active entitlement | One `product-item-sku-master` descriptor set grants Admin all six exact keys and Viewer the three read keys; Viewer receives no create key |
| Idempotency | Replay produces no duplicate `RolePermission` row |
| Tenant isolation | Tenant A grants/revokes produce no Tenant B mutation |
| Source isolation | Revoke removes only matching `GrantSource.Module` plus exact `SourceModuleCode`; Manual, System and other-module rows survive |
| Catalog absence | Missing GSKU keys are not invented or replaced by adjacent permissions |
| Unavailable result | Existing consumer/Platform tests prove timeout/failure/null/ambiguous results mutate neither direction; confirmed empty remains distinct |
| Global Product regression | Existing read/create Admin and read-only Viewer grants remain unchanged |
| Finished Good regression | Existing read/create Admin and read-only Viewer grants remain unchanged |
| ABB regression | `mdm.product-abbreviations.*` keys remain outside the GSKU descriptor set and are neither granted nor revoked by this module sync |
| Generic Platform chain | Existing manifest/catalog sync and entitlement projection tests remain green without a GSKU-specific Platform branch |
| MDM declaration | Read-only contract evidence confirms GSKU declaration is owned by the MOD-0290 exposure step; FU18 does not edit the manifest |
| Build/suite | Focused Auth tests, full Auth suite and Auth API build pass with real counts; relevant Platform/MDM tests are read-only regressions |

No live seed, grant/revoke, entitlement, token or production smoke is part of planning verification.

## 18. Ready-for-dev Checklist

- [x] Master 8.1 parent and SoR evidence recorded.
- [x] Registry/repo collision check completed.
- [x] Parent/FU child decision recorded.
- [x] Exact legacy verifier passed and is recorded as non-authoritative mechanical compatibility evidence.
- [x] `shell: none`, `golden_reference: none`, `form_field_count: 0` are justified.
- [x] Exact permission keys, ModuleCode, role matrix and shared-entitlement decision are locked.
- [x] MDM/Auth/Platform ownership boundaries are explicit.
- [x] Exact runtime allow-list and protected paths are explicit.
- [x] Failure paths and test matrix include tenant/source isolation and regressions.
- [x] Product/Auth design review is accepted and the pack is marked `ready-for-dev`.
- [x] User separately authorized code start for the exact Section 5 allow-list on 2026-08-07.
- [ ] Runtime operator supplies a separate production enablement plan and target tenant/environment evidence.

## 19. Implementation Notes

- The exact Section 5 runtime implementation completed on 2026-08-07. Focused tests passed `57/57`, the full
  AuthService suite passed `319/319`, and the AuthService Release build completed with zero errors. The only reported
  warning is the pre-existing obsolete `MongoClientSettings.GuidRepresentation` usage outside this FU18 change.
- Runtime changes remained limited to the three exact Section 5 files. No live catalog reconciliation, tenant grant,
  entitlement mutation, token refresh, navigation change or production enablement was performed.

- Add both GSKU keys to the entitlement-only baseline exclusion; do not add `product-item-sku-master` to the general
  Admin baseline because module entitlement sync owns these grants.
- The generic Auth sync already selects Admin full/Viewer read-only from declared keys and persists tenant/module-source
  attribution. Focused test expansion is preferred to a GSKU-specific production branch.
- The generic Platform manifest/catalog/entitlement chain already transports module code, scope and descriptor keys.
  Do not add Platform code unless a reproducible blocking defect is found and this pack is revised/approved.
- MDM's manifest declares `GSKUS`, `ADD_NEW` and `VIEW_DETAILS` with the two exact keys. The current page is visible;
  that later MOD-0290 navigation decision is code truth, not a FU18 runtime edit or further navigation authority.
- Catalog synchronization is best-effort; a logged sync failure or manifest declaration alone is not onboarding
  completion. Production readiness requires explicit reconciliation and observed catalog/grant state.
- Permission catalog rows are global; role/grant/entitlement rows carry tenant boundaries. Never seed Permission once
  per tenant.
- Passing tests does not prove a live tenant is entitled, a current JWT contains the keys, or navigation is enabled.

### Local Development reconciliation evidence — 2026-08-09

- The exact GSKU read/create descriptors participate in the active pilot `product-item-sku-master` entitlement; a
  refreshed Admin session proved read/create and Viewer proved read/create-options denial as specified.
- `GS-000000000003` create/read and same-idempotency replay returned the same identity with no duplicate GSKU,
  revision or reservation. Tenant isolation held. This is Development evidence only.

## 20. Follow-up Items

- MOD-0290 implemented and validated the manifest declaration; no further descriptor implementation is pending here.
- Prepare a separate Production-enablement runbook: target environment/tenants, Production catalog reconciliation,
  active entitlement verification, grant reconciliation, token refresh and allow/deny smoke. Local Development
  implementation and smoke are already recorded.
- Navigation enablement remains a separate Product/UX/production-readiness decision; FU18 keeps it unchanged.
- Any GSKU update, submit, approve, retire, delete or other permission requires a separate approved authorization scope.
- Exact legacy verifier command completed successfully on 2026-08-07 using the bundled workspace Python runtime;
  its mechanical result does not replace Master 8.1 authority.

## GSKU action onboarding amendment — approved 2026-09-08

Explicit user approval extends the historical read/create boundary only for these exact keys:
- ProductDataSteward: `mdm.gskus.update`, `mdm.gskus.withdraw`, `mdm.gskus.request-correction`.
- ProductIdentityRetirementSteward: `mdm.gskus.request-retirement`.
- ProductIdentityApprover, Admin and Viewer: no new mutation grants.

The MOD-0290 B/C/D/E responsibility matrix is authoritative for this delta. Existing grants for
other products and existing GSKU submit/retire remain unchanged. The lifecycle key set is 16;
base keys remain 8 and shared dependencies remain 4. Role totals are 19/7/11, not key counts.
No automatic user assignment, wildcard, config/provisioning or live acceptance is authorized.

Exact runtime/test allow-list for this amendment:
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/ProductIdentityLifecycleEntitlementGrantProfile.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecycleEntitlementGrantProfileTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecyclePermissionOnboardingMongoTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs` — existing composite-matrix assertions only.

Tests must cover exact role responsibility, replay/revoke/restore, manual/other-source retention,
tenant isolation and no user assignment. The owned Mongo test may select an explicit test URI;
it must retain test-database and tenant-owned cleanup boundaries. No application data is touched.
Test execution evidence is recorded after validation, separately from historical live observations.

Validation 2026-09-08: Auth Release build succeeded; focused role/entitlement/human-login/scope
regressions 132 passed / 0 failed / 0 skipped. This includes one actual Mongo reconciliation test
on the isolated test-owned single-node replica set (1/0/0, not additive). It verifies exact matrices,
replay, revoke/restore, four-key manual/other-module grant preservation, tenant collision isolation
and zero user assignments. Existing stale Global Product catalog assertions were reconciled in the
approved template test; no Global Product runtime grants changed. No live provisioning was performed.

## Product-five entitlement contract fix amendment — approved 2026-09-29

This amendment records only `PRODUCT-FIVE-ENTITLEMENT-CONTRACT-FIX-01`. It does not alter this pack's
frontmatter, review status or historical evidence. For this narrow fix it supersedes any earlier statement that
would prevent the three runtime files and five tests below from reconciling the already-approved manifest,
catalog and entitlement-profile contract.

### Bounded contract

- The MDM descriptor, Auth catalog definition and lifecycle profile must agree on exact canonical keys and on
  canonical `Module`, `Resource`, `Action` and tenant `Scope` metadata. Case drift, duplicate descriptors,
  missing catalog definitions and adjacent-key substitution fail before role mutation.
- `mdm.product-identity.lifecycle-operations.recover` remains an exact, catalogued non-human descriptor. It is
  excluded from the generic Admin/Viewer bucket and from every Product Identity dedicated human-role plan.
  This amendment authorizes zero recovery grants to human roles and does not introduce a recovery operator,
  credential-management API or audit selector.
- `mdm.brands.read` remains owned by the existing `brand-product-master` catalog contract and is resolved only
  through the existing cross-module dependency path. Its owning module, Brand/Product behavior and existing
  authorization are not redefined or widened by `product-item-sku-master`.
- Existing approved Finished Good keys and role semantics are preserved. Descriptor reconciliation for those
  keys is not new Finished Good lifecycle implementation, acceptance evidence or production enablement.
- The exact grant-add delta jointly owned by FU18/FU19 is six tenant-scoped `Module` grants with
  `SourceModuleCode = product-item-sku-master`. FU18 owns the four GSKU rows below; no other GSKU grant is added:

| Human role | Exact GSKU grant-add delta |
|---|---|
| `ProductDataSteward` | `mdm.gskus.request-correction`, `mdm.gskus.update`, `mdm.gskus.withdraw` |
| `ProductIdentityRetirementSteward` | `mdm.gskus.request-retirement` |

- The existing GSKU direct-retire contract and grant are preserved; they are not part of the six-row add delta.
  FU19 separately owns the two LSKU additions and the stale LSKU direct-retire removal.
- Reconciliation remains idempotent and source-aware. Revocation may remove only a row with
  `GrantSource = Module`, the exact tenant and `SourceModuleCode = product-item-sku-master`; Manual, System,
  unrelated-permission, other-module and other-tenant rows survive.
- Consumer completion remains conditional on successful reconciliation. A transient or validation failure is
  retryable and must not mark the event complete or turn an unavailable result into a revoke.
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

- Manifest tests prove real descriptor/catalog alignment, exact canonical metadata, the non-human recovery
  descriptor, existing Brand dependency ownership and unchanged Finished Good key contract.
- Profile/sync tests prove the joint six-row target delta, zero recovery grant across Admin, Viewer and all
  dedicated human roles, exact Module/source attribution, and separate stale LSKU direct-retire removal.
- The test-owned Mongo regression proves replay idempotency, source-scoped revoke/restore, tenant isolation and
  preservation of Manual, System, unrelated and other-module grants without touching application data.
- Consumer regression proves failure/retry does not complete the event and a successful retry completes once.
- Focused tests and the affected Auth/MDM Release builds must pass. Results are recorded as test evidence only;
  they do not claim live reconciliation or Finished Good lifecycle acceptance.

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

## Product-five entitlement reconciliation operational-command contract — approved code/test start 2026-09-29

**Superseding approval record (2026-09-29):** the user explicitly answered `onaylıyorum` to the consolidated three-step
code/test approval. `PRODUCT-FIVE-ENTITLEMENT-RECONCILIATION-COMMAND-CONTRACT-01`, its corrected authority/concurrency
contract and exact **14 runtime + 7 test** paths are approved for bounded implementation, build and test-owned
verification. `auth.roles.assign-permission` is accepted as operator permission; the documented observed-quiescence
maintenance-window residual, including independent/versionless-writer risk, is explicitly accepted for this design.
This does not authorize live DB writes, service start, staging, checkpoint, push or merge. Frontmatter/general status
and earlier completed rules at `d5f811ad7d10426498c7d0460af65ae573df4382` remain unchanged; reuse the existing profile.

### Selected command and immutable invocation contract

The single selected design is a default-disabled Auth operational path entered before normal WebApplication/DI
startup:

```text
dotnet <verified-absolute-Diten.AuthService.Api.dll> \
  --run-product-identity-entitlement-reconciliation \
  --mode plan|apply \
  --tenant-id 74355e70-4c7d-410c-8cf6-db5fe3b9547f \
  --module-code product-item-sku-master \
  --operation-id <new-guid> \
  --provenance-manifest <verified-absolute-json> \
  --expected-provenance-sha256 <64-hex> \
  --expected-binary-sha256 <64-hex>
```

`plan` additionally requires a new, non-existing absolute output path:

```text
  --plan-output <verified-absolute-json>
```

It writes the canonical, non-secret plan JSON through create-new plus atomic rename and prints only the output path
and SHA-256. It does not write application data. `apply` never regenerates business decisions: it requires that exact
artifact as input, with the same operation ID and the exact values produced by the immediately preceding plan:

```text
  --plan-manifest <verified-absolute-json> \
  --expected-plan-sha256 <64-hex> \
  --expected-add-count 6 \
  --expected-remove-count 1 \
  --expected-remove-grant-id dc241b94-3a12-4825-bcb0-b66a7189124f
```

- `ASPNETCORE_ENVIRONMENT=Development` and `DOTNET_ENVIRONMENT=Development` are both mandatory and must agree.
  Production/Staging, a missing selector, an unknown argument, duplicate argument (including case variants), a
  conflicting operational selector, wrong tenant/module, malformed GUID/hash or unexpected count is rejected before
  DI, Mongo or network access.
- The operator token is supplied only through the process environment key
  `DITEN_ENTITLEMENT_OPERATOR_ACCESS_TOKEN`. It is never accepted in argv, the provenance manifest, evidence, output
  or audit payload. Existing JWT signing/config providers and the existing Platform internal-client configuration are
  consumed as present values; their values are not copied or reported.
- `plan` is authenticated and application-data read-only; its only write is the named evidence artifact. `apply`
  verifies the artifact hash, schema, operation ID, target, provenance and all rows, then rereads current authority and
  local preconditions before applying the artifact's exact rows. It does not silently recompute or widen the plan and
  cannot accept a plan without all expected fences. No HTTP endpoint, UI, broker event, entitlement toggle, manifest replay, tenant-activated event,
  `refresh-projection`, direct Mongo patch, generic migration or normal Auth host is part of this contract.
- The command does not call normal `AddPersistence`/seed/index initialization, MassTransit registration or hosted
  services. It builds only the explicitly allow-listed operational services and verifies existing required index
  specifications through read-only `listIndexes`; it never creates or alters an index.

### Authority and authoritative-source gates

- A Windows identity and a caller-supplied ActorId are provenance only, never application authority. The command
  requires a current, short-lived Platform Admin bearer token and applies the normal issuer, audience, signature,
  lifetime, algorithm and clock-skew validation. Exactly one `sub` and one canonical tenant claim are required;
  `actor_type=platform_admin`, unchanged-password state and `auth.roles.assign-permission` are mandatory.
- The token is followed by a current Auth read-back: the actor must be active, not deleted, confirmed, not awaiting a
  password change, resident in the Platform Admin tenant, and currently entitled to
  `auth.roles.assign-permission` through the UserRole/RolePermission chain. The actor's normalized email obtained from
  that `sub` is passed to the existing Platform administrator-status contract; the queried normalized email must
  equal the current Auth value and Platform must return active. Email supplied independently by the operator is not
  accepted, and the boolean status result is not represented as an actor-ID lookup. The target tenant remains the distinct active tenant
  `74355e70-4c7d-410c-8cf6-db5fe3b9547f`; the actor is not hard-coded to ERPVNE8869 or any Windows account.
- At plan and apply preflight, token `exp` must extend beyond the configured maximum operation deadline. The same
  operator User/UserRole/RolePermission, role/permission state and Platform administrator status are read again
  immediately before the transaction and after local commit. A post-commit loss of authority cannot undo the local
  commit, but it blocks terminal success and produces the durable manual outcome defined below.
- Target tenant activity and the current `product-item-sku-master` entitlement descriptor/key set are read only from
  the existing authoritative Platform internal contracts. Unavailable, stale, contradictory or changed authority is
  a hard failure. Repository manifests or hard-coded keys may verify the response but may never replace it.
- Both bounded owner decisions were accepted on 2026-09-29: existing `auth.roles.assign-permission` is the operator
  permission for this Development-only command, and the maintenance-window residual below is accepted. This does not
  turn observed quiescence into a global writer fence or authorize a live apply.

### Plan, exact delta and write set

The canonical plan is stable JSON with sorted rows and a SHA-256 over the full payload. Each row includes action,
tenant, module/source module, role ID/name, permission ID/key/Module/Scope/owner, grant ID, GrantSource, actor,
precondition identity and provenance. It also binds the authoritative-source digest, local precondition digest,
current role-assignment version, affected role-holder IDs, affected active refresh-token IDs, source/HEAD/build/DLL
hashes and operation ID. It reports, rather than merely totals, these exact business rows:

- add four deterministic `GrantSource=Module`, `SourceModuleCode=product-item-sku-master` rows to
  ProductDataSteward: `mdm.gskus.request-correction`, `mdm.gskus.update`, `mdm.gskus.withdraw`,
  `mdm.lskus.withdraw`;
- add two equivalent rows to ProductIdentityRetirementSteward: `mdm.gskus.request-retirement` and
  `mdm.lskus.request-retirement`;
- remove only grant `dc241b94-3a12-4825-bcb0-b66a7189124f`, after re-proving tenant, role,
  `mdm.lskus.retire`, `GrantSource=Module` and `SourceModuleCode=product-item-sku-master` on that row.

`mdm.gskus.retire`, Manual/System/other-module grants, historical recovery grants and every User, UserRole and
membership row are negative-set assertions. `mdm.brands.read` must still resolve to owner
`brand-product-master`, and no new human recovery grant may be present in the computed plan.

The apply transaction has the following complete authorized write set and no other writes:

1. insert the six deterministic Module RolePermission rows when absent;
2. delete the one exact stale LSKU row with the full identity/provenance predicate;
3. revoke active refresh tokens for current holders of the role losing `mdm.lskus.retire`, matching the existing
   revocation security effect and exposing affected token IDs in the non-secret plan;
4. increment the target tenant's role-assignment version exactly once, so RBAC cache keys invalidate through the
   existing version contract; and
5. append seven row-level RBAC audit records plus one deterministic local-commit receipt to the existing
   `authAuditLogs` collection. The six additions use canonical event name `role_permission_granted`; the removal uses
   `role_permission_revoked`; the fixed local receipt event is
   `product_identity_entitlement_reconciliation_local_commit_recorded` with state
   `LOCAL_COMMITTED_AUTHORITY_REVALIDATION_PENDING`. Every audit `_id` is deterministic from the operation ID and row
   ordinal/type. Row metadata contains tenant, role ID, permission ID/key, GrantSource, SourceModuleCode, before/after
   state, operation/correlation ID and plan hash; local-receipt metadata additionally contains the source/local
   fingerprints, counts, role-assignment version before/after, provenance-manifest hash and binary hash. All records
   use the validated Platform Administrator actor and contain no token or secret.

After the transaction commits, a bounded finalization step rereads complete local post-state, current operator
authority and authoritative Platform tenant/module state. It appends a deterministic
`product_identity_entitlement_reconciliation_authority_revalidated` receipt to `authAuditLogs`: `SUCCESS` only when
all three match, otherwise `COMMITTED_MANUAL_RECONCILIATION_REQUIRED` with one exact reason:
`EXTERNAL_AUTHORITY_DRIFT_AFTER_COMMIT`, `POST_COMMIT_AUTHORITY_UNAVAILABLE`,
`OPERATOR_AUTHORITY_DRIFT_AFTER_COMMIT`, `LOCAL_CONCURRENCY_DRIFT_AFTER_COMMIT` or, on a later exact replay that
finds only the local receipt, `POST_COMMIT_FINALIZATION_MISSING`. That replay writes the same deterministic
`product_identity_entitlement_reconciliation_authority_revalidated` receipt with manual-required state; it does not
create success. If a previously successful exact replay later detects drift, it appends the
single deterministic `product_identity_entitlement_reconciliation_manual_hold_recorded` receipt; that manual hold
dominates the earlier success. A failed finalization append leaves the durable pending local receipt and returns
manual-required, never success. No new collection, index or credential is required.

Existing access tokens may remain usable until their configured expiry; that bounded residual is reported, and
refreshed sign-in is required before acceptance.

### Concurrency guarantee, invalidating writers and bounded window

Snapshot reads are not write locks, and a no-op update is not a fence. The plan still fingerprints the exact
role/permission documents, all active provenance rows for both target roles, role-assignment version, holders and
active refresh-token IDs; apply rereads them in a snapshot/majority transaction and performs an expected-version
conditional update. That guarantees the command's own atomic write set and detects a writer that actually conflicts
on the same written document/version in time. It does not exclude existing sessionless writers that touch a distinct
or read-only document, delay their version increment or never increment the version.

| Protected invariant | Exact invalidating writers | Existing coordination and guaranteed boundary | Counterexample and mandatory acceptance test |
|---|---|---|---|
| Exact RolePermission/catalog state and `+6/-1` | `AssignPermissionCommandHandler`, `RevokePermissionCommandHandler`, `EntitlementPermissionSyncService`, `FullCatalogPermissionGrantService`, `RoleProvisioningService`, `InternalPermissionsController`, `DataSeeder` | Unique RolePermission index prevents only duplicates; some handlers increment role-assignment version after their sessionless write and some never do. Command transaction/CAS protects only its snapshot and colliding writes until commit. | Pause command after snapshot; another client inserts/reactivates/deletes a relevant or preserved row and delays/omits version bump. Command must conflict or finish local commit as `LOCAL_CONCURRENCY_DRIFT_AFTER_COMMIT`; never terminal success. |
| Holder/UserRole set used for token revocation | `AssignRoleCommandHandler`, `RevokeRoleCommandHandler`, `RegisterCommandHandler`, `InternalEventsController`, `PlatformAuthController`, `DataSeeder` | UserRole unique index is duplicate-only; version bump is separate or absent. Fingerprint is point-in-time, not a holder-set lock. | Add a retirement-role holder after snapshot. Post-read must detect the new holder and durable outcome is manual-required; the missed holder is not silently treated as revoked. |
| Active refresh-token set | `LoginCommandHandler`, `RefreshTokenCommandHandler`, `PlatformLoginCommandHandler`, `VerifyMfaCommandHandler`, `ForcedChangeTenantPasswordCommandHandler`, `RegisterCommandHandler`, `PlatformAuthController`; `RevokePermissionCommandHandler` and `RevokeRoleCommandHandler` consume the holder set | Token uniqueness and `RefreshTokenRepository.RevokeAllByUserAsync` cover only matching documents at call time; no shared generation/version fence exists. | Create a new eligible holder token after the fingerprint/revoke read. A surviving token makes terminal success impossible and produces local-concurrency manual state. |
| Operator/user/role/permission authority | `UpdateUserCommandHandler`, `SetUserActiveStatusCommandHandler`, `DeleteUserCommandHandler`, `AdminResetPasswordCommandHandler`, `SetTenantPasswordCommandHandler`, `ChangePasswordCommandHandler`, `ForcedChangeTenantPasswordCommandHandler`; `AssignRoleCommandHandler`, `RevokeRoleCommandHandler`, `AssignPermissionCommandHandler`, `RevokePermissionCommandHandler`, `UpdateRoleCommandHandler`, `DeleteRoleCommandHandler`, `CreatePermissionCommandHandler`, `DeletePermissionCommandHandler`, `InternalPermissionsController`; Platform `SuspendPlatformAdministratorHandler`, `ReactivatePlatformAdministratorHandler`, `UpdatePlatformAdministratorHandler`, `DeletePlatformAdministratorHandler`, `BulkDeletePlatformAdministratorsHandler` | Cryptographic JWT validation plus Auth/Platform rereads prove only each read instant. Transaction-read documents are not locked; Platform status is outside the Auth transaction. | Deactivate/delete/reset the actor, require a password change, revoke operator permission, change role/permission state or Platform-admin status after precheck. Pre-commit observation rejects with no write; post-commit observation records operator-authority manual state. |
| Active target tenant and authoritative module descriptor | Platform `SuspendTenantCommandHandler`, `ReactivateTenantCommandHandler`, `DeleteTenantCommandHandler`, `UpdateTenantCommandHandler`; `AddTenantModuleEntitlementCommandHandler`, `EnableTenantModuleEntitlementCommandHandler`, `DisableTenantModuleEntitlementCommandHandler`, `UpdateTenantModuleEntitlementExpiryCommandHandler`, `RemoveTenantManualModuleOverrideCommandHandler`; `AssignPlanToTenantCommandHandler`, `CreateTenantSubscriptionCommandHandler`, `ActivateTenantSubscriptionCommandHandler`, `SuspendTenantSubscriptionCommandHandler`, `CancelTenantSubscriptionCommandHandler`, `ExpireTenantSubscriptionCommandHandler`, `RenewTenantSubscriptionCommandHandler`, `ReactivateTenantSubscriptionCommandHandler`; plan writers `CreateSubscriptionPlanCommandHandler`, `UpdateSubscriptionPlanCommandHandler`, `ActivateSubscriptionPlanCommandHandler`, `DeactivateSubscriptionPlanCommandHandler`, `SeedDefaultSubscriptionPlansCommandHandler`; catalog writers `CreateModuleCatalogItemCommandHandler`, `UpdateModuleCatalogItemCommandHandler`, `ActivateModuleCatalogItemCommandHandler`, `DeactivateModuleCatalogItemCommandHandler`, `DeleteModuleCatalogItemCommandHandler`, `BulkDeleteModuleCatalogItemsCommandHandler`; descriptor writers `RegisterModuleManifestCommandHandler`, `CreateModulePageDescriptorCommandHandler`, `UpdateModulePageDescriptorCommandHandler`, `ActivateModulePageDescriptorCommandHandler`, `DeactivateModulePageDescriptorCommandHandler`, `DeleteModulePageDescriptorCommandHandler`, `CreateModulePageActionDescriptorCommandHandler`, `UpdateModulePageActionDescriptorCommandHandler`, `DeleteModulePageActionDescriptorCommandHandler` | Plan and immediate pre-apply digest comparison reject prior drift; no cross-service transaction or CAS joins Platform to Auth. | Change or make the authoritative source unavailable after local commit. Local commit remains explicit, terminal success is forbidden, and the exact external-authority manual receipt is required. |

This writer inventory is closed against HEAD `d5f811ad7d10426498c7d0460af65ae573df4382` and the Auth
Application/Api/Persistence plus Platform tenant, tenant-subscription, subscription-plan, module-catalog,
module-registration, module-page and platform-administrator handler source roots. Implementation tests must enumerate
calls that mutate User, UserRole, Role, RolePermission, Permission, RefreshToken, tenant entitlement/subscription,
SubscriptionPlan, ModuleCatalogItem, module page/action descriptor or Platform administrator state. Any additional
writer discovered by that guard invalidates the maintenance-window deny-list and stops implementation for a pack
correction; it is not silently treated as covered.

#### Closed-writer inventory factual correction — 2026-09-30

The implementation guard found baseline writers that existed at protected HEAD
`d5f811ad7d10426498c7d0460af65ae573df4382` but were omitted from the table above. In accordance with the mandatory
stop rule, implementation remains stopped until this pack correction is consumed. This is a source-fact correction,
not a new permission, mutation, endpoint, shutdown topology, runtime/test path or live-operation authorization. The
21-path allow-list and the accepted observed-quiescence residual remain unchanged.

Additional **Auth normal-host entry points** that must be included in the inventory/guard are:

- active refresh-token state: `services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/LogoutCommandHandler.cs:58`
  calls `IRefreshTokenRepository.UpdateAsync` after revoking the token;
- role and role-assignment-version state: `services/Diten.AuthService/src/Diten.AuthService.Application/Features/Roles/Handlers/CommandHandlers/CreateRoleCommandHandler.cs:39,42`
  creates a Role and increments the tenant authorization version;
- user/operator state: `services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/CreateUserCommandHandler.cs:66,88`
  creates a User through both supported branches; and
- user/password-reset authority: `services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/ResendUserInvitationCommandHandler.cs:57`
  updates the tenant User's password-reset token fields.

These handlers are covered operationally only because maintenance rule 2 stops every normal Auth API writer host.
They are still named writers: logout changes the active-token fingerprint; role creation changes Role/version state;
and user create/invitation changes actor-authority input. Repository mechanisms include
`RepositoryBase.cs:40,50,57`, `GlobalRepositoryBase.cs:34,44`, `UserRepository.cs:103,118`,
`RoleRepository.cs:73,105`, `UserRoleRepository.cs:46`, `RolePermissionRepository.cs:54,74,82`,
`PermissionRepository.cs:71,87`, `RefreshTokenRepository.cs:49,61` and
`RoleAssignmentVersionRepository.cs:40`. These are mechanisms used by entry points, not additional independently
invokable operational commands. The exact refresh-token method name is `RevokeAllByUserAsync`; the earlier
`RevokeAllForUserAsync` wording is corrected by this amendment.

Additional **reachable Platform entry points** are not excluded by stopping Auth and therefore remain external
authority writers subject only to the existing before/immediate-before/after fingerprints and manual-outcome rule.
Unless an absolute path is shown, every path below has the common prefix
`services/Diten.Platform/src/Diten.Platform.Application/Features/`:

- platform-administrator authority:
  `PlatformAdministrators/Handlers/CommandHandlers/AssignPlatformAdministratorRolesHandler.cs:61`,
  `InvitePlatformAdministratorHandler.cs:46,77` and
  `ResendPlatformAdministratorInviteHandler.cs:38,57`;
- tenant deletion/creation/subscription snapshot:
  `Tenants/Handlers/BulkDeleteTenantsCommandHandler.cs:42`,
  `Tenants/Handlers/RegisterTenantCommandHandler.cs:247,270,327,419`,
  `Tenants/Commercial/Subscriptions/TenantSubscriptionCommandSupport.cs:77,145` and
  `Tenants/Commercial/Subscriptions/TenantSubscriptionTransactionWriter.cs:45,47,63,69`;
- tenant-admin aggregate writers:
  `Tenants/Handlers/ActivateTenantAdminUserCommandHandler.cs:56`,
  `CreateTenantAdminUserCommandHandler.cs:56`, `DeleteTenantAdminUserCommandHandler.cs:52`,
  `InviteTenantAdminUserCommandHandler.cs:142` and `UpdateTenantAdminUserCommandHandler.cs:55`;
- tenant settings/branding/login writers:
  `Tenants/Handlers/UpdateTenantSettingsCommandHandler.cs:46`,
  `UpdateTenantBrandingCommandHandler.cs:49` and `UpdateTenantLoginSettingsCommandHandler.cs:67`; and
- query-shaped tenant writers:
  `Tenants/Handlers/GetTenantAdminUsersQueryHandler.cs:28` and
  `GetTenantUsersSummaryQueryHandler.cs:29`, which may materialize the initial admin/count while serving a read.

`TenantSubscriptionCommandSupport` and `TenantSubscriptionTransactionWriter` are persistence mechanisms delegated to
by command entry points, not separately invokable operational endpoints; their inclusion prevents the underlying
Tenant/subscription writes from being hidden by indirection.

`Diten.Platform.Infrastructure/Persistence/Repositories/TenantRegistryRepository.cs:76,84` persists Tenant through
whole-document `ReplaceOne`. Consequently, an admin/settings/query writer cannot be dismissed as metadata-only: a
stale aggregate replacement can overwrite status or other authoritative fields and invalidate the command's source
snapshot. These reachable Platform writers are **not stopped or fenced** by the maintenance contract. Drift before
local commit blocks; drift observed after local commit yields the existing durable manual-required outcome. No hard
cross-service exclusion is claimed.

The reconciliation client's actual reads remain read-only:
`GET /api/internal/tenants/{id}/entitled-modules` and
`GET /api/internal/tenants/{id}/entitled-modules-with-permissions` dispatch
`GetTenantModuleEntitlementsQuery`, `GetTenantModuleEffectiveAccessQuery` and
`GetTenantEntitledModulePermissionsQuery`; none invokes the two query-shaped tenant writers above. This distinction
does not remove the concurrent-writer residual while Platform remains reachable.

The existing 21-path implementation allow-list therefore supports only a **conditional, observed-quiescence
maintenance window**, not unconditional all-writer exclusion. The operational preconditions are:

1. mint the existing short-lived Platform Admin token through the supported path before stopping Auth writers; prove
   its remaining lifetime exceeds the bounded plan/apply deadline without recording its value;
2. stop every known normal Auth API instance, Auth MassTransit consumer, seed/provisioning/reconciliation command and
   login/refresh issuer; prove absence through PID/owner/command-line plus listener inspection, not merely a stop
   request;
3. immediately before plan and again before apply, inspect Mongo client/current-operation state for the Auth database
   and reject any unknown or active writer; keep the observed writer set unchanged until finalization;
4. keep the authoritative Platform status and tenant/module endpoints reachable, with the existing internal-client
   credential present by name/provenance only, while the normal Auth writer host remains stopped; and
5. take matching local/source/operator fingerprints at window start, immediately before the transaction and after
   commit, and close the window only after a durable success or manual terminal receipt exists.

These observations materially reduce concurrency risk but cannot prevent an undetected/new direct Mongo client from
connecting after inspection. The operational-command mutex excludes only another copy of this command. It is not a
global Auth writer fence. The user accepted this residual for bounded code/test on 2026-09-29; live apply still requires
separate approval and no hard global writer fence is claimed. A hard all-writer fence would require changes across existing repositories, handlers, seed/provisioning and
token issuers outside the 21 paths and is deliberately not designed here.

### Replay and ambiguous post-commit outcomes

- Same operation ID with another plan hash is a conflict. With no local-commit receipt and exact unchanged pre-state,
  the operation is not applied and only a new explicit plan may proceed. Mixed pre/post-state without a receipt is
  ambiguous/manual; no automatic repair occurs.
- A pending local-commit receipt proves that the local transaction committed but does not prove authoritative success.
  Exact replay must freshly read operator authority, Platform authority and complete local post-state. If no terminal
  receipt exists, replay persists/manual-reports `POST_COMMIT_FINALIZATION_MISSING`; it cannot manufacture success.
- A manual terminal receipt or manual-hold receipt is immutable and always dominates. Replay still performs fresh
  authority/local reads for diagnostics, but later equality cannot automatically upgrade the operation to success;
  separate human reconciliation and authorization are required.
- A prior `SUCCESS` terminal receipt returns no-write success only after fresh current operator/source/local checks
  still match and no manual receipt exists. Any later drift creates the deterministic manual hold and returns manual.
- Unknown commit result is classified by majority read-back. Local receipt plus exact post-state means
  local-committed/pending, not success; no receipt plus unchanged pre-state means not applied; every other combination
  is ambiguous/manual. Driver-level blind transaction retry, automatic rollback, restore and retry loops are forbidden.

### Exact implementation allow-list

Existing runtime files that may change:

1. `services/Diten.AuthService/src/Diten.AuthService.Api/Program.cs`
2. `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IEntitlementPermissionSyncService.cs`
3. `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs`
4. `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/ITenantEntitlementClient.cs`
5. `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/PlatformTenantEntitlementClient.cs`
6. `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/ITokenService.cs`
7. `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/TokenService.cs`
8. `services/Diten.AuthService/src/Diten.AuthService.Persistence/DependencyInjection.cs`

New runtime files:

9. `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Entitlements/EntitlementReconciliationPlan.cs`
10. `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IEntitlementReconciliationOperationStore.cs`
11. `services/Diten.AuthService/src/Diten.AuthService.Api/Operational/EntitlementReconciliationOperationalMode.cs`
12. `services/Diten.AuthService/src/Diten.AuthService.Api/Operational/EntitlementReconciliationCommandOptions.cs`
13. `services/Diten.AuthService/src/Diten.AuthService.Api/Operational/EntitlementReconciliationOperationalRunner.cs`
14. `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/EntitlementReconciliationOperationStore.cs`

Existing test files that may change:

15. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
16. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecyclePermissionOnboardingMongoTests.cs`

New test files:

17. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Operational/ProductIdentityEntitlementReconciliationCommandContractTests.cs`
18. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Operational/ProductIdentityEntitlementReconciliationAuthorizationTests.cs`
19. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Operational/ProductIdentityEntitlementReconciliationRunnerTests.cs`
20. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Operational/ProductIdentityEntitlementReconciliationMongoTests.cs`
21. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Persistence/DisposableAuthMongoReplicaSet.cs`

The following existing files are mandatory run-only regressions and are not writable unless a separately reviewed
compile-time contract change proves an exact need:

- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecycleEntitlementGrantProfileTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementSyncConsumerTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/RevokePermissionCommandHandlerTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/AssignPermissionCommandHandlerTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Audit/RbacAuditRecorderTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Permissions/FullCatalogPermissionGrantServiceTests.cs`

Pack-only amendment paths are this pack and
`execution/domains/platform-shared-services/module-packs/MOD-0018-FU19-lsku-permission-onboarding.md`. Any further
runtime/test/pack path, project reference, package, collection, index, credential or configuration change requires a
revised, explicit approval. The existing profile remains the rule owner; if implementation proves that changing
`ProductIdentityLifecycleEntitlementGrantProfile.cs` is necessary, work stops rather than silently widening scope.

The 21 paths are sufficient for the conditional maintenance-window command, expected-version CAS, complete
post-commit rereads and deterministic pending/success/manual receipts in existing `authAuditLogs`. They are explicitly
insufficient for a hard guarantee against all current Auth writers. No path expansion is approved by this amendment.

### Acceptance matrix and provenance gate

- Parser tests cover Development-only entry, exact positive plan/apply invocations, case-variant duplicates,
  unknown/conflicting selector, malformed IDs/hashes, wrong tenant/module, create-new/atomic plan output, missing or
  mismatched plan artifact/apply fences and rejection before DI/network/DB.
- Authorization tests cover canonical Platform Admin success and wrong tenant, duplicate/conflicting claims,
  expired/bad issuer/bad audience/bad signature token, missing actor type/permission, stale UserRole, inactive/deleted/
  unconfirmed/password-change actor and absent/contradictory Platform status. Secret values never enter output.
- Planner tests prove authoritative-source-only behavior, six adds/one removal with every identity/provenance field,
  canonical artifact/hash stability, apply consuming rather than silently regenerating its rows, brand ownership,
  zero new human recovery grant and preservation of all negative-set rows.
- Command/runner tests with operational-DI spies prove seed, role upsert, catalog sync, index DDL,
  broker/consumer/hosted services and any other module reconciliation are absent; normal no-argument Auth host
  registration remains unchanged.
- Real test-owned Mongo replica-set tests prove transaction commit/abort, preflight drift, competing writer,
  deterministic replay/conflict, partial and ambiguous outcomes, +6/-1 only, refresh-token revocation, one version
  increment, audit rows/summary, tenant isolation and zero User/UserRole/membership/unrelated-grant mutation. Local
  Development databases are forbidden. The fixture owns a dynamic loopback port and separate dbPath/database,
  establishes writable PRIMARY, proves transaction support and refuses port 27017 or `DitenERP_Dev`; the command
  itself performs no index DDL.
- Barrier-controlled counterexamples cover sessionless RolePermission insert with delayed/missing version bump, new
  UserRole after snapshot, refresh-token creation after revocation read, read-only role/permission/user change and
  operator revocation. Each must conflict before commit or yield durable local-committed/manual, never success.
  Required named cases include
  `Apply_WhenVersionlessRolePermissionWriterCommitsAfterSnapshot_MustNeverReturnSuccess`,
  `Apply_WhenVersionedWriterMutatesThenIncrementsOutsideTransaction_MustAbortOrReturnCommittedManual`,
  `Apply_WhenUserRoleIsAddedAfterHolderSnapshot_MustReturnCommittedLocalDriftManual`,
  `Apply_WhenRefreshTokenIsIssuedAfterTokenSnapshot_MustReturnCommittedLocalDriftManual`,
  `Apply_WhenRoleOrPermissionChangesAfterSnapshotWithoutVersionBump_MustReturnCommittedLocalDriftManual`,
  `Apply_WhenConcurrentWriterTouchesTargetVersion_MustAbortTransactionWithoutPartialRows`,
  `Apply_WhenMaintenanceEvidenceContainsOnlyStoppedProcess_MustRejectAsInsufficient` and
  `Apply_WhenCurrentOpShowsUnknownAuthWriter_MustRejectBeforeTransaction`.
- Authority/finalization tests cover post-commit Platform drift, timeout/unavailability, operator-authority drift,
  local drift, crash after local receipt, terminal-receipt insert failure, and replay of success, pending and manual
  states. Receipt plus local post-state alone never succeeds; later authority equality never clears manual state.
  Required named cases include `Apply_WhenPostCommitAuthorityDrifts_PersistsLocalCommittedManualOutcome`,
  `Apply_WhenPostCommitAuthorityReadFails_PersistsLocalCommittedManualOutcome`,
  `Apply_WhenOperatorAuthorityChangesPostCommit_MustReturnCommittedManual`,
  `Finalization_WhenTerminalReceiptAppendFails_LeavesPendingReceiptAndReturnsManual`,
  `Replay_WhenManualOutcomeExistsAndPostStateMatches_RechecksAuthorityButRemainsManual`,
  `Replay_WhenOnlyLocalCommitPendingReceiptExists_RechecksAuthorityButNeverPromotesToSuccess` and
  `Replay_WhenPriorAuthorityVerifiedSuccess_RequiresFreshAuthorityBeforeNoWriteSuccess`. Consumer tests retain
  existing retry/completion behavior.
- Release verification records clean HEAD, every allow-listed source SHA-256, build command/output, DLL/deps.json/
  runtimeconfig hashes and focused test results using the same output with `--no-build`; final source hashes must
  match. The provenance manifest is hashed, and plan/apply both verify the manifest plus their executing DLL hash.

Bounded command code/test start is **APPROVED** by the superseding record above; implementation and independent
verification remain to be completed. Passing these gates does not authorize Local Development execution. A later live
operation still requires separate approval over a reviewed plan hash, exact mutation scope and observed pre-state;
the accepted maintenance-window residual must be disclosed, never represented as a hard global writer fence.
