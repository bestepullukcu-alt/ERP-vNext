---
id: MOD-0018-FU20
name: Product Abbreviation Register (ABB) Permission Onboarding
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: EntityBase
status: review
owner: auth-owner / mdm-owner / platform-owner
branch: feature/mdm/mod-0290-product-item-sku-master
started: 2026-08-09
target: 2026-08-09
form_field_count: 0
parent_module: MOD-0018
consumer_module: MOD-0290-FU01
---

# MOD-0018-FU20 — Product Abbreviation Register (ABB) Permission Onboarding

> **Draft planning guard.** This pack authorizes no runtime code, permission catalog mutation, role/grant creation,
> entitlement mutation, token refresh, tenant operation, navigation change or Production enablement. Code may start only
> after the pack becomes `approved` or `ready-for-dev` and the user separately approves the exact Section 5 allow-list.
>
> **DCP-002 identity proof.** Master 8.1 `Blueprint_Data!A19:AG19` identifies canonical parent `MOD-0018` as
> `RBAC / ABAC Authorization`. No `MOD-0018-FU20` collision existed in the registry or module packs. On 2026-08-09,
> `verify_module_id.py . --check-id MOD-0018-FU20 --name "Product Abbreviation Register (ABB) Permission Onboarding" --parent MOD-0018`
> returned `OK MOD-0018-FU20: proven against Blueprint/registry`. This verifier is mechanical compatibility evidence;
> Master 8.1 remains the business/model authority.
>
> **Golden Reference decision.** This is backend-only authorization/catalog/onboarding work. It owns no Razor or
> DataTable surface, so `shell: none`, `golden_reference: none` and `form_field_count: 0` are intentional.

## 1. Module Summary

FU20 governs onboarding of the exact eight permissions already enforced by the MOD-0290-FU01 Product Abbreviation
Register. Permission definitions are global Auth catalog records. Tenant access is expressed only through the existing
`product-item-sku-master` entitlement and tenant-scoped role grants.

The generic entitlement sync currently gives Admin every permission declared by an entitled module and Viewer only
`read`. That default is safe for the existing Global Product/GSKU/LSKU/Finished Good create slices but is unsafe for
ABB: granting all eight ABB keys to Admin would collapse maker-checker responsibility separation. FU20 therefore plans
one narrow ABB grant profile while preserving the existing generic behavior for every non-ABB permission.

Exact permissions:

- `mdm.product-abbreviations.read`
- `mdm.product-abbreviations.request`
- `mdm.product-abbreviations.cancel`
- `mdm.product-abbreviations.approve`
- `mdm.product-abbreviations.reject`
- `mdm.product-abbreviations.correct`
- `mdm.product-abbreviations.retire`
- `mdm.product-abbreviations.audit`

Forbidden aliases/actions: `allocate`, `cancel-own`, `cancel-managed`, wildcard and `manage`.

## 2. Ownership and Boundaries

**Auth owner:**

- Global catalog uniqueness and tenant-assignable scope for the exact eight keys.
- Entitlement-aware, tenant-scoped, module-sourced grant reconciliation.
- Four dedicated ABB responsibility-role templates and their exact permission sets.
- Default Admin/Viewer behavior that does not bypass ABB separation of duties.
- Token refresh/new-login evidence after grant reconciliation.

**MDM owner:**

- Existing controller enforcement and domain maker-checker/own-cancel invariants.
- Additive ABB page/action declaration in the existing Product Item SKU Master manifest.
- `IsNavigationVisible: false`; this pack never enables menu/navigation.

**Platform owner:**

- Existing generic manifest, page/action descriptor, permission catalog sync and tenant-entitlement transport only.
- No new Platform runtime code is planned because the current generic chain already transports arbitrary declared keys
  and `ModuleCode = product-item-sku-master`.

**Out of scope:**

- ABB domain, controller, API contract, Gateway or frontend changes.
- User assignment to a responsibility role; assignment remains an explicit supported Auth operation.
- Automatic approval rights for Tenant Admin or any platform-admin bypass.
- A new module code, ABB-specific entitlement, permission namespace or catalog tenancy model.
- Permission seed/grant/entitlement execution, live tenant mutation, navigation and Production enablement in planning.

## 3. Owned Objects

| Object | Owner | Exact invariant |
|---|---|---|
| Eight ABB `Permission` records | Auth global catalog | One global active definition per key; `Permission.Module = product-item-sku-master`; no `TenantId` |
| `PRODUCT_ABBREVIATIONS` manifest page | MDM | Existing module manifest; required permission `read`; all eight keys declared; navigation false |
| ABB grant profile | Auth | Applies only to the eight exact ABB keys; preserves generic behavior for every adjacent permission |
| `ProductAbbreviationRequester` role | Auth tenant system role | `read`, `request`, `cancel` |
| `ProductAbbreviationSteward` role | Auth tenant system role | `read`, `request`, `correct`, `cancel`, `retire` |
| `ProductAbbreviationApprover` role | Auth tenant system role | `read`, `approve`, `reject` |
| `ProductAbbreviationAuditor` role | Auth tenant system role | `read`, `audit`; no mutation |
| Tenant Admin ABB default | Auth grant profile | `read` only; never all eight automatically |
| Tenant Viewer ABB default | Auth grant profile | `read` only; `audit` is never implicit |
| Module-sourced grants | Auth | Tenant-scoped; `SourceModuleCode = product-item-sku-master`; exact-profile reconcile and revoke |
| Maker-checker/own-cancel | MDM domain | Canonical human subject checks remain independent of permission possession |

No new persisted entity or collection is introduced. Existing `Permission`, `Role`, `RolePermission`,
`RoleAssignmentVersion`, module manifest and tenant entitlement models are reused.

## 4. Entity Fields

| Existing object | Field/value | Rule |
|---|---|---|
| `Permission` | Base | Global catalog type; no `TenantId`; soft-deleted conflicts included in reconciliation |
| `Permission.Key` | Exact eight-key allow-list | Lowercase dotted; globally unique; no aliases or wildcard |
| `Permission.Module` | `product-item-sku-master` | Existing manifest/catalog attribution; key namespace remains `mdm` |
| `Permission.Resource` | `product-abbreviations` | Exact kebab-case resource |
| `Permission.Action` | `read/request/cancel/approve/reject/correct/retire/audit` | Exactly one action per key |
| `Permission.Scope` | `Tenant` | Tenant-role assignability; catalog record remains global |
| `Role.Name` | Four exact responsibility names | Tenant-scoped system role; collision with a non-system role fails before grant |
| `RolePermission.TenantId` | Server-bound tenant | Empty/mismatched/cross-tenant value fails closed |
| `RolePermission.GrantSource` | `Module` | System/manual grants are not rewritten by module reconciliation |
| `RolePermission.SourceModuleCode` | `product-item-sku-master` | No ABB-specific module alias |
| Entitlement | `TenantId + product-item-sku-master` | Only active/effective entitlement permits ABB module grants |

## 5. Repo Scope

Planning writes are limited to this pack and its registry row. A later separately approved runtime delivery is limited
to the following exact allow-list.

**MDM manifest owner:**

- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs`

**Auth runtime owner:**

- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/ProductAbbreviationEntitlementGrantProfile.cs` — new, pure exact role/key profile
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs`

**Auth tests:**

- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductAbbreviationEntitlementGrantProfileTests.cs` — new
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductAbbreviationPermissionOnboardingMongoTests.cs` — new, real `localhost:27017`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementSyncConsumerTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/RoleProvisioningServiceTests.cs` — regression only

**MDM evidence/regression tests, read-only unless a proven contract-test gap requires a minimal assertion:**

- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductAbbreviationApiContractTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductAbbreviationRegisterAuthorizationTests.cs`

No Platform path is allow-listed. The generic Platform manifest/catalog/entitlement chain is consumed unchanged.

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, Blueprint workbooks, DCPs and unrelated governance files.
- `execution/domains/master-data-management/module-packs/MOD-0290-FU01-product-abbreviation-register-abb-foundation.md`.
- All Auth files outside Section 5, especially `DataSeeder.cs`, token issuance/refresh implementation, role/user assignment
  handlers, repositories, entities, configuration and appsettings.
- All MDM files outside the two manifest paths; ABB controller, workflow, domain, persistence and frontend behavior are frozen.
- All `services/Diten.Platform/**`; the generic catalog/entitlement implementation is sufficient and remains unchanged.
- `gateway/**`, `frontend/**`, navigation/menu files and shared/frozen layouts.
- WorkCenter, Enterprise Strategy, Production/Staging configuration/data and every secret/credential surface.
- Branch, stage, commit and push operations.

## 7. Dependencies

- Master 8.1 parent `MOD-0018` and its existing RBAC/entitlement enforcement contracts.
- MOD-0018-FU13 token/cache invalidation and bounded-staleness behavior.
- MOD-0018-FU16–FU19 shared `product-item-sku-master` manifest/catalog/entitlement chain.
- MOD-0290-FU01 ABB exact permissions, direct-human actor requirement and domain lifecycle invariants.
- MDM `ProductItemSkuMasterManifestProvider`, current multi-provider registration transport and Platform generic
  catalog permission sync.
- Auth `DefaultRolePermissionTemplate`, `EntitlementPermissionSyncService`, `IRoleRepository.UpsertSystemRoleAsync`,
  module-sourced `RolePermission` and role-assignment version invalidation.
- Existing supported role/user assignment APIs for explicit responsibility-role membership.

## 8. Runtime Constraints

- Canonical ModuleCode is `product-item-sku-master`; no second ABB entitlement/module is created.
- Catalog definitions are global and unique. Tenant scope begins only at role, grant, entitlement and user-role assignment.
- Active entitlement alone creates no user assignment and does not give Admin all eight ABB permissions.
- Lowest-risk defaults: Admin `read`; Viewer `read`; Auditor `read+audit`; mutation requires an explicitly assigned
  Requester, Steward or Approver responsibility role.
- Responsibility-role provisioning is idempotent. Existing same-name non-system role is a conflict and receives no grant.
- Active-entitlement reconciliation is exact, not add-only: it adds missing profile grants and removes stale
  module-sourced ABB grants for the same role/module while preserving manual/system grants and non-ABB product grants.
- Missing, disabled or expired entitlement yields no ABB module-sourced grant. Revocation covers Admin, Viewer and all
  four ABB responsibility roles without affecting another tenant or a different grant source.
- Descriptor absence, catalog mismatch, unknown key, forbidden alias or partial eight-key set blocks ABB completion.
- Permission checks never replace domain checks. Requester/Steward cancel succeeds only for the canonical subject that
  owns the `REQUESTED` record. Approve/reject fails when maker and checker resolve to the same canonical human subject.
- No `platform_admin` shortcut is acceptance evidence. Runtime tests use direct `tenant_user` actors and exact claims.
- Token claims change only after supported refresh/new login; token contents are never logged.
- Navigation stays false and no startup auto-assignment of users occurs.

## 9. Layout & Shell Contract

`shell: none`; FU20 owns no view.

- No Razor file, frontend route, DataTable, localization resource or layout is created or changed.
- The existing ABB tenant page remains a consumer owned by MOD-0290-FU01 and explicitly uses
  `Layout = "_LayoutTenantShell"`.
- `golden_reference: none` is required because this pack is backend-only permission onboarding.

## 10. Backend File Convention

This is not a CRUD feature and does not create Golden Reference command/query folders.

- The new grant-profile type is one focused file under the existing Auth `Application/Common/Services` convention.
- It is a pure exact mapping from module permission keys to role templates; it performs no persistence or transport.
- `EntitlementPermissionSyncService` remains the single persistence orchestrator through existing repositories.
- MDM changes are additive manifest declarations only; controller/workflow behavior is not duplicated.
- Every cancellation token continues to the existing repository boundary.

## 11. Frontend File Contract

No frontend file is in scope.

- Existing ABB UI permission visibility remains consumer evidence, not authorization enforcement.
- Approver/checker operational acceptance may use supported API calls because the current UI does not expose every
  approve/reject/cancel action.
- Navigation is a separate decision and remains disabled.

## 12. Validation Rules

| Input/decision | Required | Exact rule | Failure |
|---|---:|---|---|
| Permission set | Yes | Exactly eight approved keys | Reject partial/extra set before grant |
| Forbidden keys | Yes | `allocate`, `cancel-own`, `cancel-managed`, wildcard absent | Reject and mutate nothing |
| ModuleCode | Yes | `product-item-sku-master` | Reject alias/new module code |
| Catalog tenancy | Yes | Global definitions; no `TenantId` | Reject per-tenant seed |
| Entitlement | Yes | Active/effective for current tenant | Missing/disabled/expired grants none |
| Role names | Yes | Four exact responsibility names | Non-system collision fails closed |
| Admin default | Yes | ABB `read` only | Any automatic ABB mutation grant fails |
| Viewer default | Yes | ABB `read` only | `audit` or mutation fails |
| Requester | Yes | `read/request/cancel` | Extra/missing key fails |
| Steward | Yes | `read/request/correct/cancel/retire` | Extra/missing key fails |
| Approver | Yes | `read/approve/reject` | Extra/missing key fails |
| Auditor | Yes | `read/audit` | Mutation key fails |
| TenantId | Yes | Server-bound non-empty current tenant | No grant or disclosure |
| Canonical subject | Yes for mutation | Direct tenant human; stable canonical GUID/subject | Domain 403 before write |

## 13. Failure Path to Verify

- Duplicate catalog sync replays idempotently; divergent definition or soft-deleted conflict produces no second row.
- Manifest omits one key or adds a forbidden key: profile reconciliation fails before any ABB grant.
- Active entitlement with the complete set: exact six-role matrix is reconciled without duplicate role/grant rows.
- Missing/disabled/expired entitlement: no ABB module grant exists after authoritative reconciliation.
- Entitlement transport failure or uncertain state: no grant/revoke and no false-success completion.
- Same role name occupied by a non-system role: conflict, no profile grant to that role and no silent adoption.
- Requester or Steward cancels another subject's request: `ABBREVIATION_CANCEL_NOT_REQUEST_OWNER`, no mutation.
- Maker with approve/reject permission checks their own allocation or retirement request:
  `ABBREVIATION_MAKER_CHECKER_VIOLATION`, no mutation.
- Approver checks another maker's request: allowed only with exact permission and expected version/state.
- Auditor calls any mutation: 403 before handler/repository mutation.
- Viewer calls audit or mutation: 403; read remains allowed only with active entitlement/refreshed token.
- Admin without a dedicated responsibility role calls ABB mutation: 403; read remains allowed.
- Tenant A reconciliation or assignment never creates, removes or discloses Tenant B grants/roles/data.

## 14. Authorization Convention

Policy remains `[Authorize]` plus the existing `[HasPermission]` exact key on each ABB endpoint.

| Role | Automatic grant after active entitlement | Operational boundary |
|---|---|---|
| Tenant Admin | `read` | No ABB mutation by default; explicit responsibility-role assignment required |
| Tenant Viewer | `read` | No audit or mutation |
| `ProductAbbreviationRequester` | `read`, `request`, `cancel` | Cancel only own `REQUESTED` record by domain check |
| `ProductAbbreviationSteward` | `read`, `request`, `correct`, `cancel`, `retire` | Cannot cancel another subject's request |
| `ProductAbbreviationApprover` | `read`, `approve`, `reject` | Cannot decide the same canonical subject's request |
| `ProductAbbreviationAuditor` | `read`, `audit` | No mutation |

Users receive no dedicated role automatically. An authorized operator assigns membership through the existing Auth role
assignment mechanism. Holding multiple roles never disables the MDM canonical-subject maker-checker check.

## 15. Gateway / API Routing Decision

Gateway change is unnecessary and prohibited.

- `/api/product-abbreviations` base/catch-all routes already exist and remain unchanged.
- FU20 adds no endpoint or HTTP method.
- Browser/service routing and current same-origin proxy behavior are MOD-0290-FU01 concerns.
- Navigation remains a separate, closed decision.

## 16. Acceptance Criteria

- [x] DCP-002 parent/collision proof remains green for the exact ID/name/parent tuple.
- [x] Manifest contains one nav-hidden `PRODUCT_ABBREVIATIONS` page under `product-item-sku-master` and declares exactly
      the eight controller-enforced keys with no forbidden alias.
- [x] Platform generic manifest/catalog sync produces exactly one global catalog record per ABB key and no Platform code change.
- [x] All eight keys use `Permission.Module = product-item-sku-master`, `Resource = product-abbreviations`, exact action
      and tenant-assignable scope without a catalog `TenantId`.
- [x] ABB keys are entitlement-only and do not enter the general Viewer/Admin baseline before active entitlement.
- [x] Active entitlement reconciles Admin and Viewer to `read` only and provisions the four exact responsibility roles
      with exact module-sourced grants but no user assignment.
- [x] Existing non-ABB Product Item SKU Master grants retain Admin full/Viewer read behavior.
- [x] Reconciliation is idempotent and exact; duplicate role/grant/catalog records and stale ABB module grants are absent.
- [x] Missing/disabled/expired entitlement yields zero ABB module-sourced grants in all six roles.
- [x] Entitlement uncertainty mutates nothing and cannot be reported complete.
- [x] Requester own-cancel/non-owner deny, maker-checker deny and valid distinct-subject approval remain domain-enforced.
- [x] Auditor and Viewer mutation deny, Admin mutation deny-by-default and tenant isolation pass.
- [ ] Token refresh/new login reflects reconciled grants; old token behavior remains bounded by FU13.
- [x] Global Product, GSKU, LSKU and Finished Good permission/grant behavior is unchanged.
- [x] No navigation, user-role auto-assignment, Platform runtime, Gateway/frontend, config/secret or Production mutation occurs.

## 17. Test Expectations

| Area | Required tests |
|---|---|
| Manifest/controller | Exact page, navigation false, eight-key set equality with controller attributes, forbidden-key absence |
| Catalog | Global uniqueness, idempotent replay, divergent/soft-deleted conflict, no per-tenant duplicate |
| Default roles | ABB absent without entitlement; Admin/Viewer both read-only after profile; audit never implicit |
| Responsibility roles | Exact four role names and exact matrices; no user assignment; non-system name collision fails |
| Entitlement | Active, missing, disabled, expired and uncertain outcomes; source-scoped revoke |
| Tenant safety | Tenant A/B isolation for role creation, grants, revoke and read-back |
| Domain authorization | Own-cancel success, non-owner cancel deny, same-subject approve/reject deny, distinct approver success |
| Role deny | Auditor mutation deny; Viewer audit/mutation deny; Admin mutation deny without dedicated role |
| Idempotency | Repeat catalog, role and grant reconciliation produces no duplicate row |
| Regression | Existing Global Product/GSKU/LSKU/Finished Good exact Admin/Viewer matrices; FU16–FU19 suites |
| Live Development | Supported catalog/entitlement reconciliation, explicit responsibility-role membership, token refresh and allow/deny matrix |

Runtime delivery must run focused Auth/MDM tests, full Auth suite, applicable MDM manifest/ABB tests and API builds.
Mongo-backed catalog/role/grant tests use real `localhost:27017`; fake/in-memory proof cannot close persistence claims.

## 18. Ready-for-dev Checklist

- [x] Master 8.1 parent and registry collision preflight completed; legacy verifier returned exit 0.
- [x] Exact eight permissions and three forbidden aliases match current MDM code/tests.
- [x] Existing manifest lacks ABB and the exact minimum MDM delta is identified.
- [x] Existing Platform generic catalog/entitlement chain is sufficient; no Platform runtime path is allow-listed.
- [x] Generic Auth Admin-all behavior is identified as unsafe for ABB and cannot be reused unchanged.
- [x] Canonical ModuleCode recommendation is `product-item-sku-master`.
- [x] Exact six-role matrix, no-auto-user-assignment rule and domain-independent maker-checker boundary are documented.
- [x] Exact runtime/test allow-list and protected paths are documented.
- [x] Auth owner accepts the ABB-specific grant profile, exact role names and Admin/Viewer read-only defaults.
- [x] MDM owner accepts the nav-hidden page/action manifest delta.
- [x] Product Data/Quality owners accept the responsibility-role matrix for code delivery.
- [x] User promotes this pack to `approved` and separately authorizes Section 5 code-start.
- [x] After implementation tests passed, a separate Local Development reconciliation/run approval was granted and
  exercised against the existing pilot tenant on 2026-08-09.
- [ ] Production enablement remains separately prohibited until an explicit Production gate closes.

## 19. Implementation Notes

Why FU20 is separate: FU16–FU19 intentionally onboard simple two-key read/create resources where generic Admin-full and
Viewer-read role selection is correct. ABB has eight lifecycle permissions and explicit maker-checker/own-request
semantics. Folding it into an earlier FU would widen completed scopes and conceal a materially different grant policy.

Code truth on 2026-08-09:

- MDM controller and authorization tests enforce the exact eight-key set and reject `allocate`, `cancel-own` and
  `cancel-managed`.
- Domain workflow already rejects non-owner cancel and same-canonical-subject approval/rejection before mutation.
- Before the FU20 implementation, Product Item SKU Master manifest had four pages and eight permissions for Global
  Product, Finished Good, GSKU and LSKU; that pre-implementation state is superseded by the evidence below.
- Gateway and ABB frontend already exist, but FU20 changes neither. Current frontend does not expose every checker action,
  so live role acceptance may combine UI read/request evidence with supported API allow/deny checks.
- Auth default provisioning has only Admin/Viewer. Custom role creation and permission assignment exist; the repository
  also supports idempotent system-role upsert. FU20 uses these existing seams and adds no new persisted model.
- Before the FU20 implementation, generic entitlement sync mapped Admin to all declared module keys and Viewer to read
  keys. That risk is now closed for the ABB subset by the profile evidenced below; other module behavior is preserved.

Implementation evidence on 2026-08-09:

- MDM now publishes one nav-hidden `PRODUCT_ABBREVIATIONS` page with the exact eight controller-enforced permission
  keys. No Gateway, frontend, navigation or Platform runtime file changed.
- Auth uses `ProductAbbreviationEntitlementGrantProfile` only for the ABB subset of
  `product-item-sku-master`. Admin and Viewer receive `read`; the four dedicated system roles receive their exact
  matrices. All role-name collision checks complete before role/grant mutation, and no user-role assignment path is
  called.
- Reconciliation removes only stale ABB `GrantSource.Module` rows for the matching source module, preserves
  manual/system/other-module grants, propagates cancellation and retains the generic non-ABB Admin/Viewer behavior.
- Release builds: Auth API `0 warning / 0 error`; MDM API `0 warning / 0 error`.
- Focused tests: Auth `80/80`, MDM manifest/controller/domain `57/57`; no skip.
- Full suites: Auth `337/337`, MDM `404/404`; no skip.
- Existing generic Platform manifest/catalog idempotency and authoritative reconcile regressions: `19/19`; no Platform
  source change.
- The real-Mongo focused test connected to `mongodb://localhost:27017`, used an isolated disposable database and
  proved six system roles, 18 exact module grants, replay-stable cardinality, revoke/restore recovery, zero automatic
  `userRoles`, Tenant B collision fail-before-grant and Tenant A isolation. It was not fake, in-memory or skipped.
- The separately approved Local Development operational run targeted tenant
  `74355e70-4c7d-410c-8cf6-db5fe3b9547f`. The existing active `product-item-sku-master` entitlement was reused; no
  tenant or entitlement was created.
- Live manifest/catalog read-back proved one nav-hidden `PRODUCT_ABBREVIATIONS` page and exactly one global definition
  for each of the eight ABB keys, all with module `product-item-sku-master` and resource
  `product-abbreviations`; `allocate`, `cancel-own` and `cancel-managed` were absent.
- Live reconciliation produced the exact six system roles and module-sourced matrices. Before explicit test setup,
  automatic ABB responsibility assignment count was zero. Four direct-human subjects were used; the two additional
  Development users were created through the supported invitation/set-password flow.
- Live ABB acceptance consumed exactly two codes. `QZX` was requested for `GP-000000000001`, non-owner cancel returned
  `403 ABBREVIATION_CANCEL_NOT_REQUEST_OWNER`, and owner cancel returned `200`. `VWK` was requested for
  `GP-000000000002`, same-subject approval returned `403 ABBREVIATION_MAKER_CHECKER_VIOLATION`, distinct Approver
  approval returned `200`, and Auditor register/evidence reads returned `200` with two immutable evidence events.
  Reuse of canceled `QZX` returned `409` and did not allocate a third record.
- Final real-Mongo read-back was register `2`, allocation ledger `2`, history `4`: `QZX` is `CANCELLED` version `1`
  and `VWK` is `ACTIVE` version `1`. Temporary responsibility memberships were removed, final ABB responsibility
  assignment count returned to zero, and both newly created test users were disabled. Catalog, system roles and
  module grants were retained.
- Fresh post-cleanup tokens proved Admin `read=200`, `request/approve/audit=403`; Viewer `read=200` and every ABB
  mutation plus audit `403`. Requester, Steward, Approver and Auditor live matrices were exercised through Gateway
  APIs; authorized non-mutating Steward correction/retirement probes returned `404`, while unauthorized paths returned
  `403`. A spoofed tenant header could not switch the JWT-bound tenant context or disclose another tenant's data.
- Operational verification repeated Release builds with `0 warning / 0 error`; focused Auth tests passed `75/75`,
  focused MDM ABB/manifest tests passed `57/57`, full Auth passed `337/337`, and full MDM passed `404/404`, all with
  zero skipped. The focused Mongo coverage used real `localhost:27017`; live cardinality was independently read back
  from the same server without writes.
- The tenant UI read back `VWK`, `ACTIVE`, version `1` and its Global Product binding. Browser console warning/error
  count was zero. Browser navigation remained on frontend `5001`; UI calls used the same-origin MVC proxy and Gateway
  `5000`, with no direct browser call to `5056`, `5057` or `5059`.
- The separately approved entitlement closure reused physical entitlement
  `59e9df08-2bb5-4abe-a8e8-30e96fa4b0d0` for tenant `74355e70-4c7d-410c-8cf6-db5fe3b9547f` and used the supported
  Platform disable/enable/expiry APIs plus the Auth entitlement reconciliation contract. Disabled read-back was
  `NoAccess` and expired read-back was `Expired`; at both checkpoints ABB module-sourced grants were exactly zero in
  Admin, Viewer and the four responsibility roles. Re-enable and final expiry restore both returned `Active`, no
  expiry, the original six active role IDs and the exact `1/1/3/5/3/2` ABB grant matrix. Admin/Viewer non-ABB grant
  counts returned to `28/11`, proving the source-scoped recovery of the shared Product Item SKU Master profile.
- Final real-Mongo read-back retained exactly one GSKU (`GS-000000000003`), one Revision, one TR LSKU
  (`LS-000000000004`), one Finished Good (`FG-000000000005`), `QZX` `CANCELLED` v1 and `VWK` `ACTIVE` v1. ABB
  responsibility assignment remained zero; no business record, permission, entitlement or user-role assignment was
  created by the entitlement cycle.
- An operational-helper GUID-serialization defect briefly created five duplicate responsibility-role rows. All five
  had zero user assignments; thirteen grants on the first four were removed and all five rows were soft-deleted through
  Auth repository contracts before the authoritative cycle was rerun. No direct Mongo write was used. Final active
  cardinality is again exactly six canonical roles with their original IDs and matrices. This was an operational-tool
  incident, not a runtime-source change.
- The remaining fresh-login sub-gate was closed through supported Auth flows. The existing pilot Admin credential was
  renewed by the idempotent tenant-admin provisioning/forced-change path, and the existing Viewer credential by the
  authorized reset/set-password path; no credential or token was logged. Fresh Gateway `5000` -> MDM login tokens were
  acquired in every state. Disabled and expired both returned `403` for all eight ABB actions (`read`, `request`,
  `cancel`, `approve`, `reject`, `correct`, `retire`, `audit`) for both users. Initial, re-enabled and final-restored
  states returned Admin/Viewer `read=200` and every ABB mutation plus audit `403`, exactly matching the FU20 defaults.
- Helper preflight pinned the tenant and all six canonical active role IDs and rejected empty/mismatched GUIDs before
  mutation. Initial/final/replay snapshots were identical: active roles `6`, soft-deleted recovery rows `5`, aggregate
  active grants on the six roles `54`, responsibility assignments `0`, and exact ABB grants `1/1/3/5/3/2`. A second
  authoritative reconciliation changed no role, grant, assignment or business cardinality; soft-deleted recovery rows
  were retained and not physically removed.
- Closure verification used isolated repo-local build artifacts: Auth API Release build completed with `0 errors`
  (`1` pre-existing obsolete-API warning), MDM API Release build with `0 errors` (`5` pre-existing warnings), focused
  Auth entitlement/profile/real-Mongo tests passed `34/34`, and focused MDM ABB controller/authorization/manifest tests
  passed `32/32`; both test runs had zero failures and zero skips.
- This evidence is Local Development only. Production/Staging reconciliation and enablement remain prohibited and
  separately gated.

## 20. Follow-up Items

**Completed Local Development live acceptance order (2026-08-09):**

1. Publish/reconcile the exact nav-hidden ABB manifest page and read back eight global catalog definitions.
2. Confirm the existing pilot tenant's active `product-item-sku-master` entitlement; do not create a new entitlement type.
3. Reconcile Admin/Viewer plus four responsibility roles and prove exact grants, tenant isolation and idempotent replay.
4. Explicitly assign test humans to Requester/Steward/Approver/Auditor through supported Auth role assignment.
5. Refresh/login again, then run read/request/own-cancel/non-owner-cancel/approve/reject/audit allow-deny checks.
6. Prove same-subject maker-checker denial and distinct-subject approval, then read back role/grant/token results.
7. Live disabled and expired reconciliation, fresh-login HTTP fail-closed behavior and exact final restore are proven.
   The entitlement remains enabled, effective `Active` and expiry-free; Local Development HTTP closure is complete.

Production catalog/entitlement/grant reconciliation, responsibility-role assignment, secret/configuration changes,
navigation and Production enablement require separate explicit authorization and operational evidence.
