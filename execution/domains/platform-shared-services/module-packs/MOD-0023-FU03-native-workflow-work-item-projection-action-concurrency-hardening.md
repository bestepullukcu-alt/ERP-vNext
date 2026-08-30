---
id: MOD-0023-FU03
name: Native Workflow Work Item Projection and Action Concurrency Hardening
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: draft
owner: platform-workflow-workcenter-owner
branch: feature/pss/mod-0023-fu03-native-workflow-work-item-hardening
started: 2026-08-30
target: 2026-09-03
form_field_count: 0
---

# MOD-0023-FU03 — Native Workflow Work Item Projection and Action Concurrency Hardening

> **Planning-only gate:** This pack is `draft`. It authorizes no runtime/test edit, branch operation, commit,
> push, configuration, credential or data mutation. After owner review it must become `approved` or
> `ready-for-dev`, followed by a separate exact code-start.

> **DCP-002 identity:** This is a backend/shared-WorkCenter follow-up child of Master 8.1
> `MOD-0023 — Workflow Designer (Approvals/SLAs/Escalations)`. Preflight evidence is recorded in Section 18.
> `MOD-0023-FU01` is reserved for the separate Global Object Transition Gate and `MOD-0023-FU02` owns trusted
> consumer start/terminal evidence. This FU neither replaces nor widens either child.

## 1. Module Summary

This follow-up closes the remaining acceptance boundary between the existing native MOD-0023 Workflow provider and
the generic WorkCenter action bridge. It makes the complete current `WorkItemProjectionDto` contract executable for
Workflow approvals, proves the four Product Identity object types, maps requester/assignee facts truthfully, carries
the projected concurrency version into the Workflow transition, and preserves one stable action identity across
double-click, retry and lost-response replay.

It does not create an MDM provider. Global Product, GSKU, LSKU and Finished Good remain ordinary MOD-0023 Workflow
items rendered by provider code `workflow` and acted on by the native Workflow dispatcher.

## 2. Ownership and Boundaries

### This FU owns

- Native Workflow approval projection truth against the complete current `WorkItemProjectionDto` property set.
- Exact projection regressions for `global-product`, `gsku`, `lsku` and `finished-good`.
- Truthful `Assignee` and `Requester` value-or-explicit-absence rules using existing Workflow records.
- WorkCenter browser -> generic action endpoint -> native Workflow dispatcher propagation of `ExpectedVersion`.
- Native transition stale-version fencing without weakening existing assignment, permission, SOD or idempotency gates.
- One stable action idempotency identity for the same provider/item/action/projected-version attempt.

### Consumed ownership

| Concern | Owner / rule |
|---|---|
| Product lifecycle state, submit/poll reconciliation and audit intent | MOD-0290 MDM; read-only source identity only. |
| Workflow task/instance/assignment/transition authority | MOD-0023; remains authoritative. |
| Generic WorkCenter shell and single action address | WorkCenter shared surface; change only exact existing files in Section 5. |
| Permission definitions and grants | MOD-0018; consume existing keys, add none. |
| Trusted service start/evidence | MOD-0023-FU02; unchanged. |

### Explicitly out of scope

- Any `services/Diten.MdmService/**` edit, MDM remote provider endpoint, MDM action endpoint or MDM projection DTO.
- A provider configuration row, remote bridge, per-module Platform bridge class or manifest address.
- Workflow template/data provisioning, roles/grants, Gateway routes, navigation, secrets or Production/Staging.
- New entity, collection, index, schema profile, migration, audit aggregate or lifecycle action.
- ABB, retire, callback/webhook, BPMN and non-Workflow provider behavior.

## 3. Owned Objects

No new persisted object is owned.

| Contract object | Existing source | Planned bounded change |
|---|---|---|
| `WorkItemProjectionDto` | `WorkAggregationModels.cs` | Read-only authority. Its complete then-current property set is inventoried by test; the measured count is 50 on 2026-08-30 but no numeric count is frozen. |
| Workflow projection | `WorkItemProjectionService` | Map source, assignment and requester facts; make every DTO property mapped or explicitly absent. |
| Workflow action dispatcher | `WorkflowApprovalWorkItemActionDispatcher` | Require/carry projected version and stable idempotency identity; never mint a per-retry GUID. |
| Workflow transition requests | `WorkflowModels.cs` | Add bounded optional transport of expected task version for native WorkCenter dispatch compatibility. |
| Native transition engine | `WorkflowTaskTransitionSupport` | After exact-idempotency replay lookup, reject a stale expected task version before mutation. |
| WorkCenter action attempt | existing browser state | Derive/preserve one bounded identity from provider, item, action and projected version through double-click/retry. |

### Exact action map

| Action | Existing permission | Required payload | Version/idempotency rule |
|---|---|---|---|
| `approve` | `platform.workflow.tasks.approve` | expected version; optional reason/comment/evidence per action contract | Stable key + stale fence. |
| `reject` | `platform.workflow.tasks.reject` | expected version + reason | Stable key + stale fence. |
| `requestInfo` | `platform.workflow.tasks.request-info` | expected version + reason; optional target/evidence | Stable key + stale fence. |
| `delegate` | `platform.workflow.tasks.delegate` | expected version + target principal | Stable key + stale fence. |

## 4. Entity Fields

No entity field or schema change is permitted. The existing facts are authoritative:

| Projection/action fact | Existing source | Rule |
|---|---|---|
| `Source.ObjectType/ObjectId` | `WorkflowInstance.ObjectType/ObjectId` | Exact ordinal source identity. |
| `Assignee` | `ApprovalTask.AssigneeRef` | Non-empty value becomes `WorkItemPersonDto`; `IsCurrentUser` is server-derived. Missing is explicit absence. Candidate lists do not replace the resolved assignee. |
| `Requester` | trusted `WorkflowInstance.DelegatedMakerUserId`, else `StartedBy` | Use persisted canonical human maker when available; never service-client identity. Missing is explicit absence. |
| `Concurrency.Token` | `ApprovalTask.Version` | Positive/current technical version string; copied to `ExpectedVersion`. |
| `IdempotencyKey` | WorkCenter action attempt | Stable, bounded, non-secret identity for provider/item/action/version; retry reuses it. |
| Transition expected version | action payload -> Workflow request | Exact match to current `ApprovalTask.Version` after idempotent replay lookup; mismatch is 409 with no mutation. |

## 5. Repo Scope

The following allow-list is exhaustive. Runtime code-start must stop before touching any other file.

### Existing Platform runtime files

1. `services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/Services/WorkItemProjectionService.cs`
2. `services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/Providers/WorkflowApprovalWorkItemActionDispatcher.cs`
3. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/WorkflowModels.cs`
4. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/ApproveWorkflowTaskHandler.cs`
5. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/RejectWorkflowTaskHandler.cs`
6. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/RequestInfoWorkflowTaskHandler.cs`
7. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/DelegateWorkflowTaskHandler.cs`
8. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/WorkflowTaskTransitionSupport.cs`

`WorkAggregationModels.cs`, provider repositories/entities and schema files are read-only. If implementation proves a
model/entity/schema edit necessary, code stops and the pack returns to planning.

### Existing WorkCenter frontend files

9. `frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/app.js`

The existing `work-items-api.js` and `WorkCenterNextController.cs` already preserve the JSON payload and are read-only.

### Platform test allow-list

10. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/WorkAggregation/WorkItemProjectionServiceTests.cs`
11. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/WorkAggregation/WorkItemProjectionSerializationTests.cs`
12. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/WorkAggregation/WorkItemActionDispatchTests.cs`
13. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/WorkflowTaskTransitionTests.cs`
14. new `services/Diten.Platform/tests/Diten.Platform.Application.Tests/WorkAggregation/WorkflowProductIdentityWorkItemContractTests.cs`

### Frontend test allow-list

15. `frontend/Diten.Web/tests/workcenter-next-action-dispatch.test.js`
16. `frontend/Diten.Web/tests/workcenter-next-work-items-api.test.js`

### Governance evidence allowed after successful implementation

17. this pack
18. `execution/registries/module-implementation-status.md`
19. `docs/product-backlog.md` and `docs/product-backlog-closed.md` only to close `BL-317` in the same verified turn.

## 6. Protected Paths

- `.antigravity/**`.
- `services/Diten.MdmService/**`, `services/Diten.AuthService/**`, `services/Diten.Platform.Common/**`.
- `gateway/**`, configuration/appsettings, credentials/secrets and operational data.
- Workflow entities, repositories, Mongo schema manifest/budgets/indexes/migrations.
- Remote WorkCenter provider/configuration classes and DCP-004.
- Product Identity controllers, handlers, manifests and frontend registers.
- WorkCenter views, RESX, navigation and every non-listed frontend file.

## 7. Dependencies

- Existing MOD-0023 Workflow task/instance/assignment/transition runtime.
- Existing MOD-0023-FU02 trusted maker facts (`DelegatedMakerUserId`, `StartedBy`) where the workflow was trusted-started.
- Existing generic WorkCenter provider/dispatcher and same-origin proxy.
- Existing MOD-0018 Workflow permission grants.
- MOD-0290 only as a producer of exact Workflow `ObjectType/ObjectId` bindings; no runtime dependency edit.

## 8. Runtime Constraints

- Tenant and actor remain server-derived. Browser supplies neither.
- Idempotency lookup precedes version rejection so an exact lost-response replay returns the prior successful result.
- A first stale attempt returns stable 409 and performs no task/instance/snapshot/log mutation.
- Missing/non-positive/malformed WorkCenter expected version is payload-invalid before MediatR dispatch.
- Missing WorkCenter idempotency identity is payload-invalid; dispatcher does not generate randomness.
- Direct legacy Workflow API compatibility may keep absent expected version only where the existing controller owns that
  contract; the generic WorkCenter path must always supply and enforce it.
- Requester/assignee IDs are identity facts, not display names. No directory lookup or invented label is added.
- Full projection conformance is property-set based. Adding a future DTO property breaks the inventory test until the
  Workflow provider declares a mapped value or an explicit absence rule.

## 9. Layout & Shell Contract

`shell: none`. This is hardening of an existing backend/shared WorkCenter surface, not a new page. No Razor view,
layout, navigation, DataTable, RESX or route is added. Existing WorkCenter continues to own its current shell.

## 10. Backend File Convention

No new runtime class is planned. Existing Platform feature locations remain authoritative:

- projection/dispatcher: `Application/Features/WorkAggregation/**`;
- transition requests/support: `Application/Features/Workflow/**`;
- tests: matching `WorkAggregation/**` and `Workflow/**` folders.

The one new test class is sealed and stays in the matching WorkAggregation test namespace. No alternate handler,
provider, DTO file or repository abstraction is created.

## 11. Frontend File Contract

Only the existing `WorkCenterNext/app.js` action-attempt construction may change. It continues to call the single
same-origin `WorkCenterNextApi.dispatchAction` seam. No provider-specific URL, direct service port, new partial,
modal, localization key, fixture-only production path or MDM address is allowed.

## 12. Validation Rules

| Field | Required | Format/rule | Failure |
|---|---|---|---|
| `providerCode` | yes | Existing projection value; exact `workflow` for this provider. | Existing provider refusal. |
| `itemId` | yes | Existing non-empty Workflow task GUID. | Existing non-leaking not-found. |
| `actionCode` | yes | Exact one of the four declared codes. | `WORK_ITEM_ACTION_UNKNOWN`. |
| `expectedVersion` | yes on WorkCenter path | Integer `> 0`, parsed from projection concurrency token. | `400 WORK_ITEM_ACTION_PAYLOAD_INVALID`. |
| `idempotencyKey` | yes on WorkCenter path | Non-empty, max 128, stable for provider/item/action/version; ordinal exact. | `400 WORK_ITEM_ACTION_PAYLOAD_INVALID`. |
| `assignee` | projection | Exact non-empty resolved task assignee or absent. | Contract test failure; never guessed. |
| `requester` | projection | Exact trusted maker/started-by human identity or absent. | Contract test failure; service identity forbidden. |

## 13. Failure Path to Verify

| Scenario | Expected behavior |
|---|---|
| Four Product Identity object types | Each remains provider/lifecycle owner `workflow`, exact source ID, current complete DTO-conformant. |
| Missing source instance | Item remains non-projectable; no partial row. |
| Missing requester/assignee source fact | Field is omitted under an asserted explicit absence rule; no fabricated person. |
| Stale expected version, new key | 409 Workflow concurrency reason; zero mutation/log. |
| Same stable key after successful action/lost response | Existing idempotent result; no second transition/log. |
| Double-click before first response | Same key/version reaches both calls; one business effect and coherent replay/conflict result. |
| Missing/non-positive version or missing key | 400 before native command dispatch. |
| Wrong assignment, permission or maker-checker | Existing 403/409 preserved; version/idempotency never bypasses it. |
| New DTO property without Workflow decision | Property-set contract test fails. |

## 14. Authorization Convention

- Controller remains `[Authorize]` and WorkCenter read remains `platform.work-aggregation.inbox.view`.
- Action permissions remain exactly the four keys in Section 3; no new permission is defined or seeded.
- Actor comes from the server-resolved JWT `WorkItemActor`; request body actor/tenant remains impossible.
- Native Workflow assignment snapshot and maker-checker/SOD remain mandatory after shared permission dispatch.
- A stable idempotency key is replay identity, never authorization.

## 15. Gateway / API Routing Decision

Gateway change is unnecessary and forbidden. Browser continues through:

`/WorkCenterNext/api/work-items/{itemId}/actions/{actionCode}` -> existing Gateway
`/api/v1/work-items/{itemId}/actions/{actionCode}` -> native Workflow dispatcher.

No MDM route, remote provider route, direct `5057/5059` browser call or `ocelot.json` edit is introduced.
No lookup/reference-data dependency exists.

## 16. Acceptance Criteria

- [ ] The then-current complete `WorkItemProjectionDto` property set has an executable mapped-or-explicit-absence
  inventory; the historical number 31 is not used as authority and the measured 50 is not frozen as a magic count.
- [ ] `global-product`, `gsku`, `lsku` and `finished-good` native Workflow projections pass the C# contract and the
  existing browser executable fixture contract with exact source binding and native action map.
- [ ] Assignee equals `ApprovalTask.AssigneeRef`; requester equals trusted delegated maker then human `StartedBy`
  fallback; current-user flags are correct; unavailable facts are omitted, not invented.
- [ ] WorkCenter copies the projected version into all four Workflow actions and supplies one stable bounded
  idempotency identity reused by double-click and retry.
- [ ] Dispatcher rejects absent/invalid version or key, carries both unchanged to Workflow, and never calls
  `Guid.NewGuid()` for action idempotency.
- [ ] Native transition checks exact idempotency replay first, then fences the caller's expected task version before
  mutation. Stale new operations return 409; exact replay returns the original result.
- [ ] Existing permission, assignment, SOD, evidence, terminal-state and tenant-isolation behavior remains green.
- [ ] No MDM/runtime remote provider, provider row, bridge, route, schema, config, data or permission delta exists.
- [ ] Focused Platform and frontend tests, full Workflow/WorkAggregation regressions, Platform Release build, frontend
  Release build and applicable architecture guards pass with zero new failure/skip.

## 17. Test Expectations

### Projection/contract

- Reflection-derived equality between the current DTO property names and an explicit Workflow mapped/absence ledger.
- Serialization omits absent requester/assignee and emits exact person shape when facts exist.
- Exact four-object theory for source, lifecycle owner, actions, concurrency and full executable-contract acceptance.
- A future DTO field makes the property-inventory test red until classified.

### Dispatch/concurrency/replay

- All four actions carry the projected version and stable idempotency identity into the matching command.
- Missing/zero version and missing/overlong key are rejected before mediator mutation.
- Stale expected version produces one 409 reason and no write/log.
- Same key after first success returns the same transition result even though persisted version advanced.
- Concurrent same-key/double-click has one terminal effect/log; different key with stale version loses.
- Existing assignment/SOD/permission/evidence tests remain green.

### Browser

- Stable attempt key is identical for two dispatches of the same provider/item/action/version and changes when the
  projected version changes.
- One submit guard prevents accidental duplicate UI state, but backend replay remains the authority.
- Same-origin single endpoint and no provider-specific/MDM/service-port address guards remain green.

### Quality gates

- Focused Section 5 tests; all Platform Workflow and WorkAggregation tests; full Platform suite.
- Focused WorkCenterNext Vitest and full frontend suite with pre-existing failures separately baselined.
- Platform and frontend Release builds; architecture tests; `git diff --check`, conflict-marker, trailing-whitespace,
  UTF-8 BOM and final-newline checks.
- Read-only protected-path proof and `git status --short`; no operational mutation.

## 18. Ready-for-dev Checklist

- [x] AGENTS, PSS domain config, master plan, registry, delivery board, module-pack standard, author workflow, parent
  MOD-0023 and FU02 packs read.
- [x] Backend/shared-surface decision recorded: `shell: none`, `golden_reference: none`, `form_field_count: 0`.
- [x] DCP-002 preflight passed on 2026-08-30 using bundled Python:
  `verify_module_id.py . --check-id MOD-0023-FU03 --name "Native Workflow Work Item Projection and Action Concurrency Hardening" --parent MOD-0023`
  -> `OK MOD-0023-FU03: proven against Blueprint/registry.`
- [x] Collision scan found no existing FU03. FU01 reservation and implemented/review FU02 remain distinct.
- [x] Native Workflow provider decision, current complete DTO authority and exact four ObjectTypes are frozen.
- [x] Exact runtime/test allow-list, protected paths, validation, failures, authorization and test matrix are written.
- [ ] MOD-0023 and WorkCenter owners accept requester/assignee source precedence and stable key derivation.
- [ ] User reviews this draft and promotes it to `approved` or `ready-for-dev`.
- [ ] Separate exact runtime code-start is granted.

## 19. Implementation Notes

### Code-truth evidence (2026-08-30)

- `WorkItemProjectionDto` currently exposes 50 top-level properties. The rejected Product Identity remote-provider
  candidate's 31-field count is stale; this pack uses the full property set, not either number as a permanent rule.
- `WorkItemProjectionService` already has `ApprovalTask` and `WorkflowInstance`, so assignee/requester mapping needs no
  repository, remote lookup or MDM call.
- Current projection emits exact source/concurrency/actions but omits `Assignee` and `Requester`.
- Browser already posts projected `expectedVersion`; `WorkItemActionPayloadDto` already carries both version and
  idempotency, and the same-origin proxy preserves JSON unchanged.
- Native Workflow dispatcher currently ignores expected version and creates `Guid.NewGuid()` when key is absent.
- Workflow request DTOs currently omit expected version. Transition support uses the repository-read task version,
  so a stale caller can succeed unless this follow-up fences the projected version explicitly.
- Existing repository conditional updates are sufficient. No schema/index/repository change is justified.

### Bounded implementation order

1. Add property-set/four-object projection tests first; map requester/assignee from existing facts.
2. Add dispatcher tests that fail for missing version/key and prove unchanged propagation for four actions.
3. Add optional expected version to native request/handler/support seam; prove idempotent replay before stale fence.
4. Add the stable WorkCenter browser attempt identity and double-click/retry tests.
5. Run focused, full Platform/frontend, build, architecture and protected-path gates.
6. Record evidence, close BL-317 only if every acceptance item passes, then move pack to `review`.

## 20. Follow-up Items

1. MOD-0290 H Local Development submit -> native WorkCenter decision -> secure poll -> MDM state/audit smoke remains a
   separate operational acceptance after this FU is implemented and reviewed.
2. Any directory-backed display name is a separate identity-display contract; IDs remain truthful without it.
3. Any generic deep link or additional Workflow action requires a separate WorkCenter owner decision.
4. Production/Staging operational readiness, metrics/runbook and credential/configuration remain separately gated.
