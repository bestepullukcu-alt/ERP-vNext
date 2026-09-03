---
id: MOD-0023-FU04
name: Tenant-Targeted Workflow Definition Administration Bridge
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: review
owner: platform-workflow-owner
branch: feature/pss/mod-0023-fu04-tenant-targeted-workflow-definition-administration-bridge
started: 2026-08-30
target: 2026-09-03
form_field_count: 0
---

# MOD-0023-FU04 — Tenant-Targeted Workflow Definition Administration Bridge

> **Implementation gate:** The user approved the Section 5 exact runtime/test allow-list on 2026-08-30.
> Runtime and test implementation is complete with focused, Workflow, real-Mongo and Release-build evidence below.
> Operational tenant/template mutation, configuration, credentials, Production/Staging, commit and push remain gated.

> **DCP-002 identity:** This is a backend-only follow-up child of Master 8.1
> `MOD-0023 — Workflow Designer (Approvals/SLAs/Escalations)`. Preflight passed on 2026-08-30:
> `verify_module_id.py . --check-id MOD-0023-FU04 --name "Tenant-Targeted Workflow Definition Administration Bridge" --parent MOD-0023`
> -> `OK MOD-0023-FU04: proven against Blueprint/registry.`

## 1. Module Summary

MOD-0023 workflow templates are tenant-owned, but the current administration surface is contradictory:
the manifest publishes Workflow Definitions under `/Platform/Workflow`, so catalog permissions are
`PlatformAdmin`, while the API lives under `/api/v1/workflow`, where tenant resolution accepts only a
`tenant_user`. A Platform Admin therefore cannot administer a selected tenant's templates, and a tenant
role cannot safely receive the PlatformAdmin-scoped definition permissions.

This follow-up adds a narrow server-authoritative bridge for a Platform Admin to create, read and publish
workflow definitions **inside one explicitly selected active tenant**. It reuses the existing MOD-0023 CQRS,
tenant repositories, immutable version publication and response envelope. It does not make workflow
definitions global and does not weaken the existing tenant endpoint.

Creating a definition in the Platform System Tenant or another admin/reference tenant is not a workaround:
trusted workflow start resolves templates through the caller tenant's repository filter, so such a definition
is wrong-tenant data and cannot be consumed by the pilot tenant.

## 2. Ownership and Boundaries

### This follow-up owns

- The PlatformAdmin-only tenant-targeted HTTP bridge for the existing definition surface.
- Exact active-target validation before entering tenant scope.
- Defense-in-depth validation of a single authenticated `platform_admin` actor.
- Deterministic `TenantScope` entry/restoration around existing CQRS dispatch.
- Server-derived create/publish actor attribution on existing workflow definition records.
- Executable actor, tenant-isolation, scope-restoration and real-Mongo evidence.

### This follow-up does not own

- Workflow entities, repositories, indexes, schema, migrations or permission catalog entries.
- A global/shared workflow-template catalog or cross-tenant inheritance.
- Definition update/delete APIs; current code truth exposes create/read/version/publish only.
- Tenant-user permission remediation or a bypass for `/api/v1/workflow`.
- Partner-admin tenant access; no partner-to-tenant assignment contract exists here.
- Product lifecycle orchestration, MDM state, WorkCenter projection/action dispatch or trusted consumer start.
- Gateway, frontend, navigation, localization, configuration, credentials or operational data.

## 3. Owned Objects

No new persisted object is owned. The bridge operates on existing tenant-owned objects:

| Existing object | Use in this follow-up | Ownership rule |
|---|---|---|
| `WorkflowTemplate` | Create/list/detail in the selected tenant | Existing MOD-0023 tenant ownership remains authoritative. |
| `WorkflowTemplateVersion` | Version list/detail and immutable publish | Existing MOD-0023 versioning remains authoritative. |
| `Tenant` | Validate the route-selected target | Read-only registry fact; must be active and non-deleted. |
| Existing CQRS commands/queries | Perform all business operations | No duplicate handler or repository path is introduced. |

The new API surface is exactly:

| Method | Route | Existing CQRS |
|---|---|---|
| `POST` | `/api/platform/tenants/{tenantId}/workflow/definitions` | `CreateWorkflowDefinitionCommand` |
| `GET` | `/api/platform/tenants/{tenantId}/workflow/definitions` | `GetWorkflowDefinitionListQuery` |
| `GET` | `/api/platform/tenants/{tenantId}/workflow/definitions/{id}` | `GetWorkflowDefinitionByIdQuery` |
| `GET` | `/api/platform/tenants/{tenantId}/workflow/definitions/{id}/versions` | `GetWorkflowDefinitionVersionsQuery` |
| `GET` | `/api/platform/tenants/{tenantId}/workflow/definitions/{id}/versions/{versionId}` | `GetWorkflowDefinitionVersionByIdQuery` |
| `POST` | `/api/platform/tenants/{tenantId}/workflow/definitions/{id}/publish` | `PublishWorkflowDefinitionCommand` |

No PUT, PATCH, DELETE, bulk, clone, import or tenant-copy endpoint is introduced.

## 4. Entity Fields

No entity or BSON field is added. The existing inherited and business facts remain:

| Field/fact | Existing owner | Rule in this follow-up |
|---|---|---|
| `TenantId` | `TenantScopedEntity` / repository context | Never accepted in request JSON; set from validated route target via ambient scope. |
| `IsDeleted` | existing base/repository filter | Deleted templates, versions and tenants remain invisible. |
| `TemplateCode` | `WorkflowTemplate` | Existing tenant-unique exact behavior remains; same code in different tenants is allowed. |
| `Version` | existing technical concurrency | Existing publish expected-version behavior remains. |
| `CreatedBy` | existing `BaseEntity` | Set from server-resolved current Platform Admin actor; never request-supplied. |
| `PublishedBy` | existing `WorkflowTemplateVersion` | Existing server-derived current actor remains authoritative. |
| `UpdatedBy` | existing `BaseEntity` | Publish stamps the server-resolved current actor on the updated template. |

No new repository/index/schema budget is justified because every data operation goes through existing
tenant-scoped repositories and indexes.

## 5. Repo Scope

The following allow-list is exhaustive. Runtime work must stop before touching any other file.

### Platform runtime

1. new `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/PlatformTenantWorkflowDefinitionsController.cs`
2. new `services/Diten.Platform/src/Diten.Platform.API/Security/IPlatformTenantWorkflowDefinitionRequestExecutor.cs`
3. new `services/Diten.Platform/src/Diten.Platform.API/Security/PlatformTenantWorkflowDefinitionRequestExecutor.cs`
4. `services/Diten.Platform/src/Diten.Platform.API/Program.cs` — only the scoped executor DI registration
5. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/WorkflowModels.cs` — only bounded reason-code constants if required
6. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/CreateWorkflowDefinitionHandler.cs`
7. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/PublishWorkflowDefinitionHandler.cs`

### Platform tests

8. new `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/PlatformTenantWorkflowDefinitionsControllerTests.cs`
9. new `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/PlatformTenantWorkflowDefinitionRequestExecutorTests.cs`
10. new `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/PlatformTenantWorkflowDefinitionAdministrationMongoTests.cs`
11. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/WorkflowDefinitionFoundationTests.cs`
12. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/WorkflowTemplateVersionPublishTests.cs`

### Governance evidence after successful implementation

13. this pack
14. `execution/registries/module-id-registry.md` — status/evidence only
15. `execution/registries/module-implementation-status.md` — factual status/evidence only

Runtime code-start is still separately required. Planning approval does not activate this allow-list.

## 6. Protected Paths

- `.antigravity/**`.
- `services/Diten.Platform.Common/**`, including `TenantResolutionMiddleware` and `TenantScope`.
- Workflow entities, repository interfaces/implementations, Mongo schema manifests, budgets, indexes and migrations.
- Existing `WorkflowDefinitionsController.cs` and the `/api/v1/workflow` contract.
- `services/Diten.AuthService/**`, `services/Diten.MdmService/**` and every other service.
- `gateway/**`, `frontend/**`, appsettings, environment/configuration, credentials and secrets.
- Module manifest, permission catalog/grants, WorkCenter projection/action code and trusted-consumer code.
- Operational tenant/template data, Production/Staging and all Git delivery operations.

## 7. Dependencies

- Existing `PlatformAdminOnly` authorization policy; unlike `PlatformActor`, it excludes `partner_admin`.
- Existing admin-path tenant middleware behavior for `/api/platform/**`.
- Existing global `ITenantRegistryRepository` for non-deleted target lookup.
- Existing `TenantScope.Begin`, which snapshots and restores unresolved/platform/tenant context.
- Existing MOD-0023 commands, queries, handlers, validators, repositories and response envelope.
- Existing `/api/platform/tenants/{everything}` Gateway catch-all with GET/POST/OPTIONS support.
- MOD-0023-FU02 trusted start, which continues resolving templates in its service-token tenant.
- MOD-0023-FU03 WorkCenter action hardening; no shared runtime file is changed by this follow-up.

## 8. Runtime Constraints

- Controller class uses `[Authorize(Policy = "PlatformAdminOnly")]`; `PlatformActor` is insufficient because it
  also accepts `partner_admin` without a partner-to-tenant scope contract.
- The executor independently requires one authenticated `platform_admin` actor claim and one non-empty canonical
  subject claim. Duplicate/ambiguous claims fail closed.
- `tenantId` is required in the route, must be non-empty, must not equal `SystemTenantRules.PlatformSystemTenantId`,
  and must resolve to a non-deleted `TenantStatus.Active` registry row.
- `X-Tenant-Id` is forbidden on this admin path. Body/query tenant authority is forbidden.
- The target is validated before `TenantScope.Begin(targetTenantId)`; no tenant repository is invoked before scope.
- Existing CQRS is dispatched inside one `using` scope. Success, `Response<T>` failure, exception and cancellation
  all restore the prior platform context deterministically.
- The executor does not replace `HttpContext.User`, mint an actor, change permission claims or suppress cancellation.
- Platform/System/Admin tenant records are never copied or inherited into a customer tenant.
- Existing duplicate, concurrency, immutability and non-leaking 404 behavior is unchanged.
- No startup worker, seed, direct Mongo write or automatic tenant-wide publication is added.

## 9. Layout & Shell Contract

`shell: none`. This is a backend administration bridge only. No Razor page, layout, navigation entry, DataTable,
offcanvas, RESX or browser script is added. `golden_reference: none` and `form_field_count: 0` are intentional;
Slim/Compact frontend contracts are not applicable.

## 10. Backend File Convention

The controller remains thin and delegates guard/scope execution to one focused API security executor, then dispatches
the existing sealed CQRS records through MediatR. It inherits `CustomBaseController` and returns the existing
`Response<T>` envelope through `CreateActionResultInstance`.

The executor interface and implementation live beside existing trusted request executors under `API/Security`.
No new Application command/query/handler, repository abstraction, base controller or tenant context is created.
Existing handler names and folders remain unchanged. Server actor attribution uses existing `ICurrentUserContext`.

## 11. Frontend File Contract

None. Frontend is protected. The current Platform Workflow UI is not rewired by this pack. Operational/API smoke may
call the Gateway route with an authenticated Platform Admin token, but no browser code or direct service-port call is
introduced.

## 12. Validation Rules

| Input/fact | Required | Exact rule | Failure |
|---|---|---|---|
| Authenticated actor | yes | One `actor_type=platform_admin`; valid non-empty subject; `PlatformAdminOnly` policy | 401/403, no tenant scope |
| `tenantId` route | yes | Canonical non-empty GUID | route 404 or 400, no scope |
| System tenant | forbidden | Exact `PlatformSystemTenantId` rejected | `400 WORKFLOW_TARGET_TENANT_INVALID` |
| Tenant registry row | yes | Non-deleted row must exist | `404 WORKFLOW_TARGET_TENANT_NOT_FOUND` |
| Tenant status | yes | Exact `TenantStatus.Active` | `409 WORKFLOW_TARGET_TENANT_NOT_ACTIVE` |
| `X-Tenant-Id` | forbidden | No value, including duplicates | 400 before dispatch |
| Create body | yes | Existing `CreateWorkflowDefinitionValidator` only | Existing 400 |
| Publish body | yes | Existing `PublishWorkflowDefinitionValidator` only | Existing 400 |
| Definition/version IDs | yes where routed | Non-empty GUID by route constraint | 404/400; no cross-tenant leak |

No validator duplicates existing template-code, JSON, schema/expression or expected-version rules.

## 13. Failure Path to Verify

| Scenario | Expected result |
|---|---|
| Anonymous request | 401; zero target lookup or workflow dispatch. |
| `tenant_user`, service token or `partner_admin` | 403; zero target lookup or workflow dispatch. |
| Platform Admin supplies `X-Tenant-Id` | 400; header cannot override route target. |
| Empty/System Tenant target | Stable 400; no workflow mutation. |
| Missing/deleted tenant | Non-leaking 404; no scope/mutation. |
| Provisioning/suspended/deactivated tenant | Stable 409; no scope/mutation. |
| Tenant A template read through Tenant B route | 404; no identifier/metadata leak. |
| Same `TemplateCode` in A and B | Both succeed as separate tenant-owned records. |
| Definition created in Platform/System/Admin tenant | Pilot trusted start cannot resolve it; never treated as shared. |
| Handler returns failure | Prior platform context restored. |
| Handler throws or cancellation fires | Prior platform context restored; cancellation propagates. |
| Duplicate create or stale publish | Existing 409 behavior; no bypass or cross-tenant lookup. |

## 14. Authorization Convention

- Class-level policy: exact existing `PlatformAdminOnly`.
- Existing semantic permission attributes remain:
  - create: `platform.workflow.definitions.manage`
  - list/detail/version: `platform.workflow.definitions.view`
  - publish: `platform.workflow.definitions.publish`
- No new permission is defined or seeded. Current `HasPermission` semantics intentionally treat a validated Platform
  Admin as privileged; the actor-type policy is therefore the primary boundary.
- `partner_admin` is denied even though the broader `PlatformActor` policy would accept it.
- Tenant users continue using neither this bridge nor PlatformAdmin-scoped definition permissions.
- Actor/tenant fields are never accepted in JSON. `CreatedBy`, `UpdatedBy` and `PublishedBy` are server-derived.

Actor matrix:

| Actor | Active customer tenant | Other/inactive/system target |
|---|---|---|
| `platform_admin` | Allow according to existing definition permission semantics | 404/409/400 fail closed |
| `partner_admin` | 403 | 403 |
| `tenant_user` | 403 | 403 |
| service actor | 403 | 403 |
| anonymous | 401 | 401 |

## 15. Gateway / API Routing Decision

No Gateway edit is required or allowed. The existing base/catch-all route
`/api/platform/tenants/{everything}` already forwards GET, POST and OPTIONS to Platform 5057. The new controller
therefore uses `/api/platform/tenants/{tenantId}/workflow/definitions...` exactly.

No browser calls Platform 5057 directly. No `/api/v1/workflow` route is relaxed, duplicated or redirected.

## 16. Acceptance Criteria

- [x] A valid Platform Admin can create a definition in an explicitly selected active, non-system tenant.
- [x] List/detail/version/publish all execute inside the same validated target tenant and reuse existing CQRS.
- [x] The created template and published immutable version carry the exact target `TenantId` and server-derived actor.
- [x] Tenant A data is absent through Tenant B routes; same template code may exist independently in both.
- [x] A template in Platform/System/Admin tenant is not visible to pilot trusted start and is never copied implicitly.
- [x] Anonymous, tenant, service and partner actors fail before target-scope dispatch.
- [x] Missing/deleted/inactive/system targets fail with the exact stable matrix and zero mutation.
- [x] `X-Tenant-Id`, request-body tenant and request-body actor cannot influence authority.
- [x] Prior tenant/platform/unresolved context is restored after success, controlled failure, exception and cancellation.
- [x] Existing create duplicate, publish immutability/concurrency and trusted tenant start regressions remain green.
- [x] No repository, entity, schema, index, permission, Gateway, frontend, configuration or data delta exists.
- [x] Focused tests, Workflow regressions and Release build add no failure/skip. Full-suite and architecture baseline failures are recorded below and are outside this allow-list.

## 17. Test Expectations

### Controller/security contract

- Reflection proves exact `/api/platform/tenants/{tenantId}/workflow` route and `PlatformAdminOnly` policy.
- Exact six endpoints dispatch the expected existing command/query and preserve correlation/cancellation.
- Actor theories cover anonymous, tenant, service, partner, platform admin, duplicate actor claims and invalid subject.
- Header theory proves every `X-Tenant-Id` presence is rejected before mediator dispatch.

### Executor/scope

- Active/non-deleted target enters exact tenant scope; target lookup occurs before scope entry.
- System, missing, deleted and all inactive statuses return exact failures with zero dispatch.
- Starting from unresolved, tenant and platform contexts, disposal restores the exact prior state.
- Controlled response failure, thrown exception and cancellation all restore state; caller cancellation is not converted.

### Real Mongo

- Use existing `MongoIntegrationHarness.CreateIsolatedAsync("fu04_workflow_admin", SchemaProfile.Core,
  SchemaProfile.WorkflowWorkCenter)` because the bridge test persists target rows through the real tenant registry
  and proves the fixed Platform System Tenant boundary. The harness-owned fixed scope is emptied and reused, never
  minted per run; no bespoke client, schema bootstrap or unowned sweeper is allowed.
- Create/publish/read-back in Tenant A, same code in Tenant B, A-via-B 404 and immutable version tenant binding.
- Trusted start in A resolves A's version; trusted start in B cannot resolve A/System/Admin tenant data.
- No fake/in-memory substitute and no skipped localhost:27017 evidence.

### Regression/quality

- Existing `WorkflowDefinitionFoundationTests`, `WorkflowTemplateVersionPublishTests`, trusted-start and security tests.
- Full Workflow test slice, full Platform suite, Platform API Release build and architecture tests.
- `git diff --check`, conflict-marker, trailing-whitespace, UTF-8 BOM and final-newline checks.
- Read-only proof for all protected paths and no runtime/config/data/Git operation during planning.

## 18. Ready-for-dev Checklist

- [x] AGENTS, PSS domain config, Master Development Plan, registry, delivery board and DCP-002 read.
- [x] Module-pack standard and `/prepare-module-pack` workflow read.
- [x] Parent MOD-0023 and exact FU03 code-truth pack read; FU03 does not authorize this surface.
- [x] Backend-only decision recorded: `shell: none`, `golden_reference: none`, `form_field_count: 0`; Golden UI reference N/A.
- [x] DCP-002 preflight passed for FU04 with parent MOD-0023; collision scan found no FU04.
- [x] Current controller, middleware, manifest, permission scope, tenant repository and trusted-start template lookup measured.
- [x] Exact runtime/test allow-list, protected paths, validation, failure, authorization and acceptance matrices written.
- [x] User approved Phase 1.5 preparation and promotion to `ready-for-dev` on 2026-08-30.
- [x] Separate Section 5 exact runtime code-start granted on 2026-08-30.
- [ ] Operational Local Development template mutation separately authorized after implementation evidence.

## 19. Implementation Notes

### Code-truth evidence (2026-08-30)

- `WorkflowDefinitionsController` is `[Route("api/v1/workflow")]` plus `[Authorize]`; definition endpoints use
  existing manage/view/publish permission keys.
- `TenantResolutionMiddleware` treats ordinary `/api/v1/**` as tenant scope and rejects any actor other than
  `tenant_user`; only `/api/admin/**` and `/api/platform/**` enter admin handling.
- `WorkflowManifestProvider` publishes definition pages under `/Platform/Workflow`; route-derived catalog scope is
  therefore PlatformAdmin and cannot be assigned to ordinary tenant roles.
- `WorkflowTemplateRepository.GetByTemplateCodeAsync` and all version reads include the ambient tenant execution filter.
- `WorkflowInstanceStartCoordinator.ResolveTemplateAsync` uses those same tenant-scoped repository methods.
- The existing `PlatformAdminOnly` policy already excludes `partner_admin` and needs no policy/config change.
- `TenantScope.Begin` already snapshots and restores unresolved, tenant and platform contexts; it is reused unchanged.
- Gateway already carries `/api/platform/tenants/{everything}` GET/POST/OPTIONS traffic; no integration-agent delta.

### Bounded implementation order after code-start

1. Write controller/policy and executor guard/scope tests first.
2. Implement the executor and thin Platform tenant-targeted controller; add only the DI registration.
3. Add server-derived actor attribution to existing create/publish handlers and their focused tests.
4. Add real-Mongo A/B/System tenant isolation and trusted-start consumption tests.
5. Run focused/full regressions, build, architecture and protected-path gates.
6. Record factual evidence and move the pack to `review`; do not perform operational mutation without a new approval.

### Implementation evidence (2026-08-30)

- Added the exact six-endpoint `PlatformAdminOnly` route surface and one scoped request executor.
- The executor independently validates exactly one ordinal-canonical `actor_type=platform_admin` claim and one
  canonical GUID `sub`, rejects
  any `X-Tenant-Id`, empty/System Tenant targets, missing/deleted rows and every non-Active status before dispatch.
- Existing create/read/version/publish CQRS executes inside `TenantScope.Begin(targetTenantId)` and restores unresolved,
  tenant or platform context after success, response failure, exception and cancellation.
- Create and publish now stamp `CreatedBy`, `UpdatedBy` and `PublishedBy` from the existing server current-user context.
- Focused FU04 security/controller/real-Mongo tests: `21/21`, zero skipped; these include exact-case claim rejection,
  anonymous/duplicate-sub/deleted-target guards, controlled-failure/cancellation restoration and trusted-start
  A/B/System isolation.
- FU04 plus existing definition/publish regressions: `39/39`, zero skipped.
- Full Workflow slice: `259/259`, zero skipped.
- Platform API Release build: zero warnings, zero errors.
- Full Platform suite: `3872/3895`; the 23 failures are existing, out-of-scope Document Management/BRD baselines and
  include no FU04 or Workflow failure. Architecture suite: `10/11`; its sole failure lists two pre-existing audit
  Mongo per-run database violations outside this pack's allow-list.
- `git diff --check` passed. No repository/entity/schema/index/permission/Gateway/frontend/config/data change and no
  operational mutation, commit or push was performed.

### Integration-port revalidation (2026-09-03)

- Ported the same exact allow-list delta onto integration base `786a54c6` without carrying Brand, ABB, FU24-FU26,
  MOD-0033, configuration or data changes.
- Focused FU04 security/controller/real-Mongo tests: `21/21`; FU04 plus existing definition/publish regressions:
  `39/39`; both had zero failures and zero skips.
- Platform API Release build: zero warnings, zero errors. `git diff --check` passed.
- The broad `FullyQualifiedName~Workflow` slice produced `258/259`, zero skipped. The sole failure is the pre-existing
  `WorkflowProductIdentityWorkItemContractTests.Every_current_projection_property_is_mapped_or_explicitly_absent`
  projection inventory mismatch for `Closure`; neither that test nor its projection source is changed by this FU04
  allow-list. FU04 focused, real-Mongo, definition and publish tests remain green.

## 20. Follow-up Items

1. A Platform Workflow admin UI tenant selector/proxy is separate frontend work and is not required for API foundation.
2. Definition draft update/delete, clone/import/export and tenant-to-tenant copy require separate owner design and pack scope.
3. Partner-admin access requires an authoritative partner-to-tenant scope contract; it must not reuse this bridge by default.
4. Local Development Product Identity template create/publish and WorkCenter acceptance are separate operational gates.
5. Production/Staging enablement, audit observability/runbook and credential policy remain separately gated.
