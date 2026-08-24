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

- Prepare a separate Production-enable runbook: Production catalog reconciliation, active entitlement verification,
  grant reconciliation, token refresh and allow/deny smoke for target tenants. Section 5 implementation and Local
  Development evidence are already recorded.
- Navigation enablement remains a separate H-step.
- Any LSKU update/delete/submit/approve/retire permission requires a separate approved authorization scope.
