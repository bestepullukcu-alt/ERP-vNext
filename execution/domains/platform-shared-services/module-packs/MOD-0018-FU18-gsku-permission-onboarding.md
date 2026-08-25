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
