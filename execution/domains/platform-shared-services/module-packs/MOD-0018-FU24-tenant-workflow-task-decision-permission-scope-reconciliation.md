---
id: MOD-0018-FU24
name: Tenant Workflow Task Decision Permission Scope Reconciliation
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: GlobalEntityBase
status: review
owner: auth-owner / workflow-owner
branch: feature/pss/mod-0018-fu24-tenant-workflow-task-decision-scope
started: 2026-08-30
target: 2026-08-30
form_field_count: 0
parent_module: MOD-0018
consumer_modules: MOD-0023, MOD-0290
---

# MOD-0018-FU24 — Tenant Workflow Task Decision Permission Scope Reconciliation

> **Identity proof.** Master 8.1 `Blueprint_Data!A19:AG19` assigns roles, entitlements and policies to
> `MOD-0018 — RBAC / ABAC Authorization`. Registry inspection found no FU24 collision. The fail-closed DCP-002
> preflight passed on 2026-08-30:
> `verify_module_id.py --check-id MOD-0018-FU24 --name "Tenant Workflow Task Decision Permission Scope Reconciliation" --parent MOD-0018`
> → exit `0`, `OK MOD-0018-FU24: proven against Blueprint/registry`.
>
> **Approved decision.** Exactly `platform.workflow.tasks.approve` and
> `platform.workflow.tasks.reject` become `PermissionScope.Tenant`. Every other Workflow definition, instance,
> task, transition and escalation key remains `PermissionScope.PlatformAdmin`. This is a scope correction, not a
> new permission, role, endpoint or bypass.

## 0A. Workflow Instance Start Scope Supersession — 2026-08-31

This amendment supersedes the earlier claim that only approve/reject are Tenant-scoped. The exact tenant workflow
execution set is now `{platform.workflow.instances.start, platform.workflow.tasks.approve,
platform.workflow.tasks.reject}`. All other ten Workflow keys remain PlatformAdmin.

Auth seed reconciliation owns these three exact system-permission scopes. Catalog replay may refresh their metadata
but cannot promote an already seeded Tenant row to PlatformAdmin. Catalog-created/user-defined rows and every other
permission retain the generic most-restrictive tie-break. Default Admin and Viewer receive none of these three keys;
SuperAdmin sees all three, while the Product Identity profile grants start only to ProductDataSteward and decisions
only to ProductIdentityApprover.

The approved amendment adds `DataSeeder.cs`, `DefaultRolePermissionTemplateTests.cs` and the existing FU24 seed/sync
tests to the exact runtime/test allow-list below. Local Development reconciliation remains separate until the focused
real-Mongo replay and build evidence is green.

Implementation evidence: combined scope/controller/default-role/lifecycle/sync and real-Mongo seed/replay suite
`133/133`, skipped `0`; exact scope partition `3 Tenant / 10 PlatformAdmin`; catalog replay preserves the three seeded
system Tenant scopes; catalog-created/user-defined rows keep generic most-restrictive behavior; default Admin/Viewer
receive zero execution keys; Auth API Release build `0` errors with one pre-existing `GuidRepresentation` warning.

## 0. Live Code-Truth Remediation Amendment — 2026-08-30

Local Development read-back proved that the seed-only implementation is not operationally stable. Auth startup
correctly reconciles both decision keys to `Tenant`, but Platform module self-registration subsequently declares
`platform.workflow.tasks.approve` from the Platform-admin route
`/Platform/Workflow/Definitions/{id}/Tasks`. `InternalPermissionsController.Sync` then applies its generic
"most-restrictive wins" tie-break and promotes the existing seeded permission back to `PlatformAdmin`. Reject has no
equivalent live manifest action and remains `Tenant`, producing an invalid split.

This is not fixed by changing the Workflow manifest, changing the route, weakening the generic tie-break, direct data
repair or relying on startup order. The exact two seeded system decision keys are Auth-owned explicit scope exceptions.
For those keys only, `PermissionScope.Tenant` remains authoritative when catalog sync replays; every other permission
keeps the existing generic most-restrictive behavior.

Measured Local Development evidence before any Product Identity lifecycle mutation:

- MDM self-registration: `legal-entity`, `brand-product-master` and `product-item-sku-master` each succeeded on attempt 1.
- Platform catalog read-back: Brand/Product exact `8`, Product / Item / SKU exact `30`, Legal Entity exact `4` keys.
- Auth role read-back: ProductDataSteward `12`, ProductIdentityApprover `7`, ProductIdentityRetirementSteward `8`.
- Fresh unique `tenant.activated` reconciliation was accepted but failed closed on the divergent
  `platform.workflow.tasks.approve` descriptor (`Scope=PlatformAdmin`); no lifecycle transition was started.
- The failed pass consequently left one stale product-sourced `mdm.brands.read` Admin grant, so FU25 acceptance also
  remains open until this FU24 remediation lands and authoritative reconciliation is replayed.

### Exact remediation allow-list

**Auth runtime:**

- `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs`

**Auth tests:**

- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Permissions/InternalPermissionsControllerTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/WorkflowTaskDecisionPermissionScopeReconciliationTests.cs`

**Governance:** this pack and the canonical registry row only.

The user approved this exact remediation allow-list on 2026-08-30. The runtime guard and focused regression evidence
are implemented; Local Development reconciliation and Product / Item / SKU acceptance remain the next operational
step after the current binaries are restarted.

## 1. Module Summary

This backend-only Auth follow-up reconciles the two native Workflow task-decision permissions needed by tenant
responsibility roles. The existing Product Identity lifecycle profile already consumes these shared keys and grants
them only to `ProductIdentityApprover`; the seed currently classifies both as `PlatformAdmin`, so live catalog
reconciliation correctly refuses the divergent tenant dependency. FU24 makes the catalog scope agree with the
approved record-decision model while keeping Workflow control-plane administration platform-only.

## 2. Ownership and Boundaries

**In scope:**

- Change only the authoritative seed scope of `platform.workflow.tasks.approve` and `.reject` to `Tenant`.
- Preserve the exact keys, namespace, Module=`workflow`, Resource, Action, display metadata and global catalog identity.
- Reconcile existing persisted rows idempotently through the existing seed scope reconciliation.
- Prove default tenant Admin and Viewer receive neither decision permission automatically.
- Prove `ProductIdentityApprover` retains its exact seven-key matrix: four product reads, inbox view, approve, reject.
- Prove record assignment, expected-version, idempotency and maker-checker/SoD enforcement remain downstream gates.

**Out of scope:**

- New permission keys, aliases, roles, grants, role membership, entitlement logic or token policy.
- Reclassifying `definitions.*`, `instances.*`, `tasks.delegate`, `tasks.request-info`, `tasks.cancel`,
  `transitions.evaluate` or `escalations.*`.
- Generic permission-sync downgrade behavior; the most-restrictive conflict rule remains fail-closed.
- Workflow/WorkCenter runtime, Product / Item / SKU lifecycle runtime, Gateway, frontend, configuration or data
  operations.
- Production/Staging enablement and push.

## 3. Owned Objects

| Object | FU24 ownership |
|---|---|
| `platform.workflow.tasks.approve` catalog scope | Existing global definition; only scope becomes `Tenant` |
| `platform.workflow.tasks.reject` catalog scope | Existing global definition; only scope becomes `Tenant` |
| Remaining Workflow catalog definitions | Regression-only; remain `PlatformAdmin` |
| Existing persisted scope reconciliation | Reused unchanged; seed key is authoritative and `$ne` makes replay idempotent |
| Default Admin/Viewer matrices | Regression-only; zero automatic approve/reject grants |
| `ProductIdentityApprover` matrix | Regression-only; exact seven keys, no automatic membership |

No entity, collection, index, repository, endpoint, command, query or DTO is added.

## 4. Entity Fields

| Existing field | Exact rule |
|---|---|
| `Permission.Key` | Ordinal exact existing approve/reject key; no rename or duplicate |
| `Permission.Module` | Exactly `workflow` |
| `Permission.Resource` | Exactly `workflow.tasks` |
| `Permission.Action` | Exactly `approve` or `reject` |
| `Permission.Scope` | Exactly `Tenant` for these two keys only |
| `Permission.IsDeleted` | Existing lifecycle unchanged |
| Catalog `TenantId` | None; definition remains global |
| Tenant role grant `TenantId` | Existing server-bound tenant context |

## 5. Repo Scope

The runtime/test code-start is restricted to this exact allow-list.

**Runtime allow-list:**

- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs` — planned
  remediation only; requires separate amendment code-start.

**Test allow-list:**

- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecycleEntitlementGrantProfileTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/WorkflowTaskDecisionPermissionScopeReconciliationTests.cs` (new)
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecyclePermissionOnboardingMongoTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Permissions/InternalPermissionsControllerTests.cs` — planned
  remediation only; requires separate amendment code-start.

**Governance allow-list:**

- this pack
- `execution/registries/module-id-registry.md`

## 6. Protected Paths

- Every Auth runtime/test file outside Section 5. The permission sync controller remains protected except for the exact
  two-key remediation explicitly listed above after separate code-start.
- `services/Diten.Platform/**`, `services/Diten.MdmService/**`, `services/Diten.Platform.Common/**`.
- `gateway/**`, `frontend/**`, appsettings, environment files, secrets and operational helpers.
- MOD-0018-FU16–FU23 and MOD-0023-FU03 runtime files.
- `.antigravity/**`, DCPs, Blueprint and backlog.
- Production/Staging data and all push operations.

## 7. Dependencies

| Dependency | Required contract |
|---|---|
| MOD-0018 | Existing global permission catalog and seed scope reconciliation |
| MOD-0018-FU23 | Approver consumes exact shared inbox/approve/reject dependencies and receives exactly seven grants |
| MOD-0023-FU03 | Native actions enforce caller permission plus assignment, expected version and idempotency |
| DCP-004 | WorkCenter displays/dispatches; source module remains lifecycle authority |
| Product / Item / SKU Master | Record-level distinct-maker/approver and lifecycle transition enforcement |

## 8. Runtime Constraints

- The scope delta is exactly two keys; a third Workflow key changing scope fails acceptance.
- Scope is not inferred from Module name. The seed literals are authoritative for these explicit exceptions.
- Existing persisted `PlatformAdmin` rows converge to `Tenant` through the current idempotent key-based reconcile.
- Generic internal catalog synchronization remains unable to weaken a scope from `PlatformAdmin` to `Tenant`.
- `Tenant` means eligible for a tenant responsibility-role grant, not automatically granted to Admin/Viewer.
- Permission possession never bypasses task candidacy/assignment, same-subject SoD, expected-version or idempotency.
- No automatic user-role assignment is introduced.

## 9. Layout & Shell Contract

`shell: none`. No Razor view, layout, DataTable, route, navigation or browser asset is added. WorkCenter remains the
existing consumer surface and is not modified by this pack.

## 10. Backend File Convention

No CRUD/CQRS feature is introduced. The only production edit is two existing `Permission` constructor scope values
and their explanatory seed comment. No new public type, handler, validator, repository or service is permitted.

## 11. Frontend File Contract

No frontend file is in scope. Existing WorkCenter and Product / Item / SKU pages are acceptance consumers only.

## 12. Validation Rules

| Input / state | Required | Exact validation | Failure |
|---|---:|---|---|
| Approve key | Yes | Existing key, Module=`workflow`, Scope=`Tenant` | Build/test failure; no operational run |
| Reject key | Yes | Existing key, Module=`workflow`, Scope=`Tenant` | Build/test failure; no operational run |
| Other Workflow keys | Yes | All remain `PlatformAdmin` | Any drift fails |
| Admin baseline | Yes | Zero approve/reject automatic grant | Any grant fails |
| Viewer baseline | Yes | Zero approve/reject automatic grant | Any grant fails |
| ProductIdentityApprover | Yes | Exact 7; includes inbox/approve/reject | Missing/extra key fails |
| Role membership | Yes | Zero automatic assignment | Any automatic link fails |
| Generic sync conflict | Yes | Existing PlatformAdmin row is not generically downgraded | Any weakening fails |

## 13. Failure Path to Verify

- A third Workflow permission becomes tenant-scoped: contract test fails.
- Approve or reject remains/migrates back to `PlatformAdmin`: seed/read-back test fails.
- Admin or Viewer receives either decision key: default-role regression fails.
- ProductIdentityApprover lacks either key or gains submit/retire/create: exact-matrix test fails.
- Actor has the role but is not the task candidate/assignee: native action remains denied.
- Maker attempts to approve the same business request: source-module SoD remains denied.
- Stale expected version: 409 with no business/workflow mutation.
- Exact idempotency replay: prior result, no second transition.
- Generic catalog sync proposes Tenant against an existing PlatformAdmin permission: existing most-restrictive rule
  remains fail-closed; FU24 does not broaden this route.

## 14. Authorization Convention

| Role | Automatic decision grants | Exact effective intent |
|---|---:|---|
| SuperAdmin | Existing full-catalog behavior | Platform operations unchanged |
| Tenant Admin | 0 | No automatic workflow decision |
| Tenant Viewer | 0 | No automatic workflow decision |
| ProductIdentityApprover | 2 decision keys within exact 7-key profile | May attempt assigned Product Identity decisions |

The shared keys remain `platform.workflow.tasks.approve` and `.reject`; no product-specific decision keys are added.
Native Workflow and MDM record-level authorization are both required after RBAC succeeds.

## 15. Gateway / API Routing Decision

Gateway change is unnecessary. No endpoint or route changes. Existing Gateway 5000 → Platform/MDM paths are used only
for the separately authorized Local Development acceptance after runtime tests pass.

## 16. Acceptance Criteria

- [x] Exactly approve/reject seed definitions have `PermissionScope.Tenant`.
- [x] Every other Workflow seed definition remains `PermissionScope.PlatformAdmin`.
- [x] Existing persisted approve/reject rows reconcile to Tenant idempotently with no duplicate permission.
- [x] Admin/Viewer automatic grants for approve/reject are zero.
- [x] ProductIdentityApprover exact matrix remains 7 and includes inbox/approve/reject.
- [x] Zero automatic responsibility-role memberships.
- [x] Generic scope-conflict behavior remains most-restrictive and fail-closed.
- [x] Catalog replay contract cannot re-promote either exact seeded-system decision key to `PlatformAdmin`.
- [ ] Product / Item / SKU maker-checker, assignment, stale-version and replay gates remain green.
- [x] Focused tests, full Auth suite and Auth API Release build complete with scoped results recorded.
- [x] `git diff --check`, conflict-marker, changed-line trailing-whitespace and final-newline gates pass.
- [x] No push or Production/Staging operation occurs.

## 17. Test Expectations

- Source/contract test enumerates all Workflow seed keys and proves the exact two-versus-rest scope partition.
- Real Mongo seed/reconciliation test starts with persisted divergent scopes, runs the supported seed path twice and
  proves one global definition per key plus stable Tenant read-back.
- Default-role tests prove Admin/Viewer zero automatic approve/reject grants.
- FU23 profile and real-Mongo tests prove exact `12/7/8` responsibility matrices and zero user assignment.
- Existing internal permission-sync regression proving no generic scope downgrade remains green.
- Focused Auth tests, full Auth suite and Auth API Release build run before any operational reconciliation.
- Local Development acceptance then proves distinct maker submit → assigned approver WorkCenter decision → source
  lifecycle read-back, with tenant isolation and no duplicate business record.

## 18. Ready-for-dev Checklist

- [x] AGENTS.md, PSS domain config, DCP-002, FU23, MOD-0023-FU03 and DCP-004 read.
- [x] DCP-002 preflight passed; no FU24 collision.
- [x] Backend-only decision recorded: shell/golden reference none.
- [x] Exact two-key scope delta and protected remaining Workflow keys frozen.
- [x] Exact runtime/test allow-list frozen.
- [x] Admin/Viewer zero grant and Approver exact-seven matrix frozen.
- [x] Assignment, maker-checker/SoD, version and replay boundaries preserved.
- [x] User explicitly approved pack preparation, ready-for-dev and runtime/test code-start.

## 19. Implementation Notes

The live Product / Item / SKU reconciliation exposed the contradiction rather than a unit-test-only issue: MDM
published the complete lifecycle catalog, but FU23 rejected the shared dependencies because the Auth seed still marked
the two tenant record-decision actions as platform administration. The correction deliberately does not make Workflow
definition management tenant-scoped and does not change the generic most-restrictive sync rule.

After code/test completion, update this section with exact focused/full/build counts and operational read-back. Move
status to `review`, not `done`, until Local Development maker/approver acceptance completes.

### 2026-08-30 runtime/test evidence

- Only the two authoritative seed literals `platform.workflow.tasks.approve` and `.reject` now use
  `PermissionScope.Tenant`; the other eleven Workflow definitions remain `PlatformAdmin`.
- The existing seed reconcile was reused unchanged. A real `localhost:27017` test starts with the inverse legacy
  scopes, invokes the supported seed permission path twice and proves one stable permission identity per key plus the
  exact `2 Tenant / 11 PlatformAdmin` partition on both passes.
- Default Admin and Viewer receive zero approve/reject grants. Both keys remain tenant-assignable for an explicit
  responsibility role, while SuperAdmin's existing full-catalog behavior is unchanged.
- The Product Identity dependency validator rejects a legacy `PlatformAdmin` decision definition, and its exact
  seven-key Approver matrix plus zero automatic user-role assignment remain green.
- Focused FU24/profile/default-role/real-Mongo tests passed `36/36`; generic internal-sync and entitlement regressions
  passed `81/81`, including the existing most-restrictive no-downgrade tie-break.
- Full Auth completed `686/688`; the only failures are the two pre-existing User Lookup contract assertions that still
  expect no `MaskedName/MaskedEmail` fields. No FU24 test failed or skipped.
- Auth API Release build passed with zero errors and one pre-existing `GuidRepresentation` warning.
- `git diff --check`, conflict-marker, changed-line trailing-whitespace, UTF-8 BOM and final-newline checks passed.
  Eight unrelated pre-existing trailing-whitespace lines remain in `DataSeeder.cs` outside the FU24 hunks.
- No operational mutation, configuration, credential, secret, Production/Staging, commit or push occurred in this
  runtime/test step. Local Development maker/approver acceptance remains the next separately authorized action.

### 2026-08-30 catalog-replay remediation evidence

- `InternalPermissionsController` now recognizes exactly the two Auth-owned seeded-system keys after startup has
  reconciled them to `Tenant`. Replayed Platform-admin route metadata may refresh display/module data but cannot
  promote either key back to `PlatformAdmin`.
- The exception requires `IsSystem=true`, exact ordinal key membership and current `Tenant` scope. Therefore it does
  not downgrade a legacy `PlatformAdmin` row, does not affect a catalog-created/user-defined row and cannot be used
  by an arbitrary third key.
- The generic most-restrictive tie-break remains unchanged for all non-exception keys. Dedicated tests prove a
  non-exception Workflow key still promotes to `PlatformAdmin`, while catalog-created approve/reject rows keep their
  existing generic scope behavior.
- Focused controller plus real-Mongo seed/replay tests passed `30/30`; the broader FU24/FU23/default-role/entitlement
  regression group passed `124/124`, with zero skipped tests.
- Full Auth completed `693/695`; only the same two pre-existing User Lookup assertions concerning
  `MaskedName`/`MaskedEmail` failed. No FU24 test failed or skipped.
- Auth API Release build passed with zero errors and one pre-existing `GuidRepresentation` warning.
- `git diff --check` and scoped conflict-marker/trailing-whitespace/final-newline checks passed. No configuration,
  secret, data, Production/Staging, commit or push operation occurred in this remediation step.

## 20. Follow-up Items

1. Run the separately authorized Local Development catalog/grant reconciliation and Product / Item / SKU WorkCenter
   acceptance after code/tests pass.
2. Production/Staging enablement remains a separate explicit decision.
3. Any future tenant use of delegate/request-info/cancel requires its own measured permission/record-policy pack; FU24
   is not precedent for reclassifying those actions.
4. Push remains user-controlled and is forbidden in this work.
5. Restart the current Auth binary, rerun fresh tenant reconciliation and require approve/reject both `Tenant`, stale
   product-sourced Brand grant `0`, and exact `12/7/8` before lifecycle acceptance.
