---
id: MOD-0018-FU25
name: Product Identity Cross-Module Descriptor Dependency Reconciliation
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: GlobalEntityBase
status: review
owner: auth-owner / product-data-owner
branch: feature/pss/mod-0018-fu25-product-identity-cross-module-descriptor
started: "2026-08-30"
target: ""
form_field_count: 0
parent_module: MOD-0018
consumer_modules: MOD-0290
---

# MOD-0018-FU25 — Product Identity Cross-Module Descriptor Dependency Reconciliation

> **Identity proof.** Master 8.1 `Blueprint_Data!A19:AG19` assigns authorization, roles and entitlement policy to
> canonical parent `MOD-0018 — RBAC / ABAC Authorization`. Registry inspection found no FU25 collision. The fail-closed
> DCP-002 preflight passed on 2026-08-30:
> `verify_module_id.py --check-id MOD-0018-FU25 --name "Product Identity Cross-Module Descriptor Dependency Reconciliation" --parent MOD-0018`
> → exit `0`, `OK MOD-0018-FU25: proven against Blueprint/registry`.
>
> **Execution state.** The user promoted this pack to `ready-for-dev` and authorized the exact Section 5 Auth
> runtime/test allow-list on 2026-08-30. Local commit and the already-scoped Local Development acceptance are allowed;
> push and Production/Staging remain prohibited.

## 0B. Global Product Lifecycle Descriptor Supersession — 2026-09-04

This amendment is the current authority for the complete `product-item-sku-master` descriptor after the MOD-0290
Global Product lifecycle completion. It supersedes the earlier exact-30 descriptor and `13 / 7 / 8` role-cardinality
statements, while preserving the existing four-key Workflow/WorkCenter dependency contract and dedicated
Brand/Product manifest ownership.

The complete module-owned descriptor grows from 30 to exactly 34 active, non-deleted, Tenant-scoped keys by adding:

- `mdm.global-products.update` with exact `product-item-sku-master/global-products/update/Tenant` tuple;
- `mdm.global-products.withdraw` with exact `product-item-sku-master/global-products/withdraw/Tenant` tuple;
- `mdm.global-products.request-correction` with exact
  `product-item-sku-master/global-products/request-correction/Tenant` tuple;
- `mdm.global-products.request-retirement` with exact
  `product-item-sku-master/global-products/request-retirement/Tenant` tuple.

The accepted cross-module dependency set remains exactly four: inbox view, Workflow instance start and Workflow task
approve/reject. No Brand/Product, additional Workflow or arbitrary globally known key is accepted. FU23 owns the
resulting exact lifecycle grant plans `16 / 7 / 10`; FU25 proves that the complete 34-key descriptor and the four
dependencies are neither confused nor used to reconstruct missing owned permissions.

The exact runtime allow-list is limited to
`ProductIdentityLifecycleEntitlementGrantProfile.cs`. Current code truth proves that
`EntitlementPermissionSyncService.cs` already compares the declared keys with the complete active module catalog,
removes stale source-owned grants before insertion and converges exact role plans; it is protected from modification.
The exact test allow-list is `ProductIdentityLifecycleEntitlementGrantProfileTests.cs`,
`EntitlementPermissionSyncServiceTests.cs` and `ProductIdentityLifecyclePermissionOnboardingMongoTests.cs`.

Acceptance requires exact descriptor cardinality 34, exact tuple validation for all four additions, rejection before
mutation for 33/35 keys or any duplicate/deleted/divergent/foreign key, exact four shared dependencies, exact
`16 / 7 / 10` role plans, source-safe removal of the Retirement Steward's stale module-sourced
`mdm.global-products.retire`, stable replay/revoke/restore and real-Mongo tenant isolation. Manual/system/other-module
retire grants are preserved. Runtime/test work, operational reconciliation, config/data, Production/Staging and push
remain separately gated.

## 0A. Workflow Start Shared-Dependency Amendment — 2026-08-31

This amendment is the current authority for Product Identity lifecycle-role grants. Earlier `12 / 7 / 8` grant
cardinality claims are historical and **superseded** by exact `13 / 7 / 8` grants for
`ProductDataSteward` / `ProductIdentityApprover` / `ProductIdentityRetirementSteward`. Role cardinality remains 12.

The fourth and final shared dependency is `platform.workflow.instances.start`. It is accepted only when the global
permission catalog contains one active, non-deleted exact tuple with Module=`workflow`,
Resource=`workflow.instances`, Action=`start`, Scope=`Tenant`. It is granted only to `ProductDataSteward`; Approver
and Retirement Steward remain exact `7 / 8`. Missing, duplicate, deleted, wrong-module/resource/action/scope or any
additional shared dependency fails before role/grant mutation. The other three shared dependencies remain the inbox
view and workflow task approve/reject permissions.

Implementation and evidence:

- focused profile/sync and real `localhost:27017` Mongo replay/revoke/restore: `79/79`, skipped `0`;
- the real-Mongo matrix preserves 12 roles and now persists exact total/product-sourced grant cardinalities `69/64`;
- entitlement disable removes matching module grants, restore recreates exact `13 / 7 / 8`, and replay is idempotent;
- full Auth: `700/702`; only the same two pre-existing `UserLookupValidationContractTests` failures remain
  (`MaskedName` / `MaskedEmail`), outside this amendment;
- Auth API Release build: `0` errors and one pre-existing `GuidRepresentation` warning.

**Superseded blocker evidence:** the initial isolated implementation found that Auth seed still declared
`platform.workflow.instances.start` as PlatformAdmin. The subsequently approved FU24/FU25 scope-supersession now
defines the exact seeded system execution set start/approve/reject as Tenant, preserves it across catalog replay and
keeps the other ten Workflow keys PlatformAdmin. Combined focused and real-Mongo scope/lifecycle evidence is
`133/133`, skipped `0`; Auth API Release build has `0` errors. Local Development reconciliation remains the next
separate operational proof and has not yet been claimed here.

The exact runtime/test/governance allow-list is unchanged from the remediation allow-list below. No generic
reconciliation behavior, automatic membership, config, secret, Production/Staging or push scope is added.

### Tenant-activated reliable reconciliation amendment — 2026-08-31

`tenant.activated` now reads a non-empty authoritative entitlement snapshot before inbox mutation, claims the event
under a bounded lease, completes it only after default-role and exact grant reconciliation succeed, and releases the
claim on exception or cancellation without masking the original failure. Unavailable/empty snapshots do not consume
the EventId; completed replay is a no-op; busy or identity/tenant-conflicting claims fail closed. The separate
`tenant-admin-invited` behavior is unchanged.

Evidence: dedicated controller reliability matrix `10/10`; focused controller/profile/sync `86/86`; existing real
`localhost:27017` Product Identity replay/revoke/restore `1/1`; Auth Release build `0` warnings / `0` errors. A fresh
Local Development event completed through this reliable path, but the persisted Steward read-back remained
`12` grants with no workflow-start grant despite a current 13-grant runtime template and exact catalog inputs.
Consequently lifecycle replay stayed fail-closed and the lower-level entitlement sync convergence defect remains an
open operational blocker; this amendment does not claim live acceptance.

## 0. Code-Truth Supersession and Remediation Amendment — 2026-08-30

This amendment is the current authority wherever the original Sections 1–20 describe `mdm.brands.read` as a required
Product / Item / SKU descriptor dependency. Those statements and their earlier runtime/test evidence are retained only
as historical evidence and are **superseded**.

The dedicated `brand-product-master` manifest now owns the Brands and Products pages and the exact Brand/Product
permission set. Consequently:

- the `product-item-sku-master` descriptor is complete at exactly **30** owned permissions and contains no
  `mdm.brands.read` dependency;
- Product Identity lifecycle role matrices remain exactly **12 / 7 / 8** for `ProductDataSteward`,
  `ProductIdentityApprover` and `ProductIdentityRetirementSteward`;
- Admin and Viewer product-entitlement plans no longer acquire `mdm.brands.read` through
  `product-item-sku-master`;
- arbitrary, additional or undeclared cross-module descriptor keys continue to fail closed;
- any stale `SourceModuleCode=product-item-sku-master` grant for `mdm.brands.read` must be removed during
  authoritative reconciliation, while manual grants and grants sourced from `brand-product-master` remain untouched.

The earlier implementation is not accepted as the final runtime state. The remediation below is implemented and this
pack is now `review`; Local Development acceptance and Production readiness are not claimed by this amendment.

### Remediation implementation evidence — 2026-08-30

- `ProductIdentityLifecycleEntitlementGrantProfile` no longer declares or validates a Brand descriptor dependency.
- `EntitlementPermissionSyncService` compares the Product / Item / SKU descriptor only with the complete active
  `product-item-sku-master` catalog set; the measured descriptor cardinality is exactly `30`.
- An additional `mdm.brands.read`, `mdm.products.read` or any other cross-module key fails before role/grant mutation.
- Authoritative reconciliation removes a stale `SourceModuleCode=product-item-sku-master` Brand grant while preserving
  manual, system and `SourceModuleCode=brand-product-master` grants.
- Admin and Viewer receive no product-sourced Brand grant. Dedicated lifecycle roles remain exact `12 / 7 / 8`.
- Focused profile/sync/real-Mongo: `72/72`, skipped `0`.
- FU20–FU25 role/profile/reconciliation regression: `108/108`, skipped `0`.
- Full Auth: `688/690`; only the two pre-existing `UserLookupValidationContractTests` failures remain
  (`MaskedName` / `MaskedEmail`), outside this pack's allow-list.
- Auth API Release build: `0` warnings, `0` errors.
- `git diff --check`, conflict-marker, trailing-whitespace, BOM and final-newline gates passed.
- No config, secret, data, Production/Staging or push operation occurred.

### Exact remediation allow-list

**Auth runtime:**

- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/ProductIdentityLifecycleEntitlementGrantProfile.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs`

**Auth tests:**

- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecycleEntitlementGrantProfileTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecyclePermissionOnboardingMongoTests.cs`

**Governance:** this pack and the canonical `MOD-0018-FU25` registry row only.

Acceptance requires focused tests, real `localhost:27017` Mongo reconciliation/replay evidence, the full Auth suite and
Release build. Tests must prove exact descriptor cardinality 30, current lifecycle cardinalities 13/7/8, stale
product-sourced Brand dependency revocation, preservation of manual/Brand-module grants, entitlement disable/restore
replay and fail-closed rejection of every other cross-module key. Runtime work requires a separate explicit approval.

## 1. Module Summary

FU25 reconciles Product Identity entitlement plans against one complete, exact 30-key
`product-item-sku-master` descriptor plus four explicitly trusted shared Workflow/WorkCenter dependencies. Brand and
Product permissions belong only to `brand-product-master`; no Brand key is accepted from or granted on behalf of the
Product / Item / SKU descriptor.

The four shared keys are `platform.work-aggregation.inbox.view`, `platform.workflow.instances.start`,
`platform.workflow.tasks.approve` and `platform.workflow.tasks.reject`. Each must resolve to one active, non-deleted,
exact Tenant-scoped catalog definition. Any additional, missing, duplicate or tuple-divergent dependency fails closed.

## 2. Ownership and Boundaries

**In scope:**

- Accept exactly the four frozen Workflow/WorkCenter dependencies beside an otherwise complete 30-key
  `product-item-sku-master` descriptor.
- Resolve each dependency from the global Auth catalog and validate its exact key/module/resource/action/Tenant scope.
- Preserve exact completeness for every permission actually owned by `product-item-sku-master`.
- Grant inbox/start only to ProductDataSteward and approve/reject only to ProductIdentityApprover.
- Preserve the dedicated Product Identity lifecycle role matrices exactly `13/7/8`.
- Remove stale product-sourced Brand grants while preserving manual/system/`brand-product-master` grants.
- Fail before role/grant mutation for a missing, deleted, duplicate, divergent or additional cross-module key.

**Out of scope:**

- Permission re-attribution, seed/catalog creation, MDM manifest changes or Platform catalog changes.
- Brand create/update/archive or `mdm.products.*` dependencies.
- Generic cross-module dependency support, wildcard/prefix matching or operator-configurable dependency lists.
- Dedicated-role changes, automatic membership, WorkCenter behavior, Gateway/frontend/navigation or business data.
- Configuration, credentials, operational reconciliation, Production/Staging and push.

## 3. Owned Objects

| Object / contract | FU25 boundary |
|---|---|
| Descriptor dependency allow-list | Exact four-key Workflow/WorkCenter set |
| Dependency definition validator | Exact key/module/resource/action/Tenant scope and active/non-deleted proof |
| Module-owned descriptor set | Complete active `product-item-sku-master` catalog set; no missing or extra owned key |
| Admin/Viewer desired plan | Existing product plan only; no product-sourced Brand or shared execution grant |
| Dedicated lifecycle roles | Exact `13/7/8`; shared keys distributed by responsibility |
| Module grant attribution | Existing `SourceModuleCode=product-item-sku-master`; dependency ownership is not rewritten |

No entity, collection, index, endpoint, role or permission is introduced.

## 4. Entity Fields

| Existing `Permission` field | Required shared-dependency rule |
|---|---|
| `Key` | Exact four-key set named in Section 1, ordinal exact |
| `Module` | `platform` for inbox; `workflow` for start/approve/reject |
| `Resource` | `work-aggregation.inbox`, `workflow.instances` or `workflow.tasks` as frozen by key |
| `Action` | `view`, `start`, `approve` or `reject` as frozen by key |
| `Scope` | `Tenant` |
| `IsDeleted` | `false` |
| Catalog `TenantId` | None; existing global definition |

The definition is consumed, never rewritten. Tenant grants remain server-bound and use the existing module-source
attribution so entitlement removal can revoke the effective dependency grant safely.

## 5. Repo Scope

Any later runtime code-start is restricted to this exact allow-list.

**Runtime allow-list:**

- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/ProductIdentityLifecycleEntitlementGrantProfile.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs`

**Test allow-list:**

- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecycleEntitlementGrantProfileTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecyclePermissionOnboardingMongoTests.cs`

**Governance allow-list:** this pack and the canonical registry row.

## 6. Protected Paths

- Every Auth runtime/test file outside Section 5, especially `DataSeeder.cs`, permission repositories and controllers.
- `services/Diten.MdmService/**`, `services/Diten.Platform/**`, `services/Diten.Platform.Common/**`.
- `gateway/**`, `frontend/**`, configuration, secrets, operational helpers and data migrations.
- MOD-0018-FU16–FU24 runtime/governance files, MOD-0290 packs and implementation tracker.
- `.antigravity/**`, DCPs, Blueprint, backlog, Production/Staging and all push operations.

The MDM manifest and Brand/Product implementation are read-only code-truth inputs.

## 7. Dependencies

| Dependency | Required contract |
|---|---|
| MOD-0018-FU23/FU24 | Existing special profile, exact shared Workflow dependencies and `13/7/8` roles |
| MOD-0290 Product / Item / SKU | Complete descriptor contains exactly 30 product-owned permissions |
| MOD-0290-FU02 Brand/Product | Exclusively owns Brand/Product catalog and navigation definitions |
| Platform module catalog | Publishes descriptor keys without changing permission ownership |
| Tenant entitlement | Existing `product-item-sku-master` entitlement remains the grant/revoke source |

FU25 is blocked for runtime until this draft is explicitly approved or promoted to `ready-for-dev` and its exact
Section 5 code-start is separately authorized.

## 8. Runtime Constraints

- The accepted shared-dependency set has cardinality exactly four and is compared ordinally.
- The module-owned exact set remains complete; the dependency cannot compensate for a missing owned key.
- Dependency lookup uses the complete active catalog and must return exactly one valid definition.
- An arbitrary key with Action=`read`, another Brand permission or another module's permission fails closed.
- Validation completes before system-role creation, stale revoke or grant mutation.
- Admin and Viewer receive no product-sourced Brand or shared execution grant.
- ProductDataSteward, ProductIdentityApprover and ProductIdentityRetirementSteward remain exact `13/7/8`.
- Exact replay is idempotent. Entitlement removal revokes the module-sourced dependency grant with the existing source
  module while manual/system/other-module grants remain untouched.

## 9. Layout & Shell Contract

`shell: none`. This backend-only Auth reconciliation adds no view, DataTable, menu item or browser asset. The existing
Brands page remains the consumer surface and its visibility contract is not changed here.

## 10. Backend File Convention

No CQRS/API feature or new public service is added. The allow-list may add a named constant/set and exact validator to
the existing lifecycle profile, plus the smallest composition change in entitlement sync. No generic dependency
registry or new abstraction is permitted.

## 11. Frontend File Contract

No frontend file is in scope. UI evidence may be used only after a separately authorized Local Development
reconciliation; it cannot expand this pack.

## 12. Validation Rules

| State | Required result |
|---|---|
| Complete owned descriptor + exact Brand read dependency | Accept |
| Complete owned descriptor without dependency | Reject missing declared page dependency |
| Missing/extra owned permission | Reject |
| Duplicate/blank descriptor key | Reject before mutation |
| Missing/deleted/duplicate shared dependency row | Reject before mutation |
| Wrong module/resource/action/scope for a shared dependency | Reject before mutation |
| Brand/Product key inside the product descriptor | Reject before mutation |
| Unknown/arbitrary cross-module key | Reject |
| Exact replay | Stable grants and role cardinality |

## 13. Failure Path to Verify

- Cross-module key is silently ignored: test fails because descriptor and effective plan must agree.
- Dependency is treated as module-owned: test fails on exact ownership tuple.
- Dependency is cloned under `product-item-sku-master`: test fails on re-attribution.
- A permissive prefix such as `mdm.brands.*` is accepted: negative tests fail.
- Profile preflight partially creates roles/grants: fail-before-mutation tests detect cardinality drift.
- Shared dependency leaks into the wrong responsibility role: exact `13/7/8` tests fail.
- Revocation deletes a manual or `brand-product-master`-sourced grant: source-isolation tests fail.

## 14. Authorization Convention

| Role | Shared dependency grant |
|---|---:|
| SuperAdmin | Existing platform policy unchanged |
| Tenant Admin | 0 from the shared execution set |
| Tenant Viewer | 0 from the shared execution set |
| ProductDataSteward | Inbox view + workflow start; exact 13 total |
| ProductIdentityApprover | Workflow approve + reject; exact 7 total |
| ProductIdentityRetirementSteward | 0; remains exact 8 |

This grant permits the descriptor's read surface only. Brand mutation still requires the separately owned Brand/Product
entitlement/grant contract.

## 15. Gateway / API Routing Decision

No route or endpoint change is required. Auth reconciles catalog facts; existing Gateway and MDM routes remain
authoritative and protected.

## 16. Acceptance Criteria

- [x] Complete exact 30-key Product / Item / SKU descriptor plus the exact four shared dependencies is accepted.
- [x] Module-owned exact descriptor set remains complete and unchanged.
- [x] Each shared dependency definition is unique, active, non-deleted and exact Tenant-scoped.
- [x] Missing, duplicate, divergent, unknown or additional cross-module keys fail before mutation.
- [x] Admin and Viewer receive no product-sourced Brand or shared execution grant.
- [x] Dedicated lifecycle roles remain exact `13/7/8`; shared grants do not leak across responsibilities.
- [x] Replay/revoke/restore and grant-source isolation remain idempotent.
- [ ] Focused, real-Mongo, full Auth and Release build evidence passes. Focused/real-Mongo and build are green; full Auth retains the two pre-existing UserLookup contract failures recorded below.
- [x] No MDM/Platform/Gateway/frontend/config/data/Production change or push occurs.

## 17. Test Expectations

- Profile unit tests cover exact acceptance and every definition-drift/unknown-key negative case.
- Sync tests prove authoritative comparison is `complete active 30-key module-owned set + exact four dependencies`.
- Sync tests prove Admin/Viewer non-grant and responsibility-specific shared grants.
- Real `localhost:27017` onboarding regression proves exact replay, revoke/restore, source isolation and stable `13/7/8`.
- Existing FU20/FU22/FU23/FU24 special-profile tests remain green.
- Full Auth suite and Auth API Release build run before any operational reconciliation.
- `git diff --check`, conflict-marker, changed-line trailing-whitespace, BOM and final-newline gates pass.

## 18. Ready-for-dev Checklist

- [x] AGENTS.md and PSS domain config read.
- [x] DCP-002 preflight passed and registry collision check found no FU25.
- [x] FU23, FU24, Auth profile/sync code, MDM manifest and Brand/Product pack measured.
- [x] Backend-only shell/golden-reference decision recorded.
- [x] Exact four-key dependency set and definition tuples frozen.
- [x] Exact runtime/test allow-list and protected paths frozen.
- [x] Least-privilege role matrices and failure behavior frozen.
- [x] User explicitly approved/promoted this pack and separately authorized Section 5 runtime code-start on 2026-08-30.

## 19. Implementation Notes

The initial defect was discovered during Local Development Product / Item / SKU WorkCenter acceptance. Subsequent
code-truth established dedicated Brand/Product manifest ownership, an exact 30-key Product / Item / SKU descriptor and
four explicit Workflow/WorkCenter dependencies. The original Brand-dependency implementation below is historical and
must not be used as the current contract.

No runtime code, test, catalog row, tenant grant or business data was changed while authoring this pack.

### 2026-08-30 superseded implementation evidence

The evidence below belongs to the original cross-module dependency implementation and is retained only as historical
trace. The amendment and remediation evidence at the top of this pack are authoritative.

- The special descriptor comparator remains the complete active `product-item-sku-master` catalog set plus the exact
  singleton dependency. It does not accept a generic globally-known cross-module key.
- The dependency definition must be the unique active ordinal `mdm.brands.read` row with exact
  `brand-product-master/brands/read/Tenant` ownership. Missing, duplicate, deleted and tuple-drift cases fail before
  role/grant mutation.
- Default Admin and Viewer plans include the read dependency. ProductDataSteward, ProductIdentityApprover and
  ProductIdentityRetirementSteward remain exact `12/7/8` and receive no Brand permission.
- Focused profile/sync/real-Mongo: `78/78`, skipped `0`.
- FU20–FU25 special-profile regression: `92/92`, skipped `0`.
- Full Auth: `694/696`; the only failures are the pre-existing
  `UserLookupValidationContractTests.ResponseDtoContainsOnlyUserIdAndReferenceable` and
  `ResponseJsonDoesNotLeakTenantOrProfileAuthorizationOrStatusDetails` drift (`MaskedName`/`MaskedEmail`), outside
  the Section 5 allow-list.
- Auth API Release build: `0` warnings, `0` errors.
- Local Development catalog/grant reconciliation and WorkCenter acceptance remain the next already-authorized
  operational step. No config, secret, data, Production/Staging or push operation occurred in this implementation step.

## 20. Follow-up Items

1. Preserve the reviewed exact descriptor/dependency contract in future catalog changes.
2. Rerun separately authorized Local Development Product / Item / SKU catalog/grant reconciliation
   and WorkCenter acceptance.
3. Any second cross-module descriptor dependency requires a new measured owner decision; FU25 is not a generic
   precedent.
4. Production/Staging enablement and push remain separate user-controlled gates.
