---
id: MOD-0023-FU02
name: Trusted Consumer Workflow Start and Terminal Decision Evidence Foundation
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: review
owner: platform-workflow-owner
branch: feature/pss/mod-0023-fu02-terminal-evidence-hardening
started: 2026-08-29
target: 2026-09-02
form_field_count: 0
---

# MOD-0023-FU02 — Trusted Consumer Workflow Start and Terminal Decision Evidence Foundation

> **DCP-002 identity:** This is a backend-only follow-up child of Master 8.1 `MOD-0023 — Workflow Designer
> (Approvals/SLAs/Escalations)`. The exact preflight command is recorded in Section 18. The name in this pack
> is the approved child name; it does not create a new top-level capability.

> **Code-start boundary:** `ready-for-dev` records the user's standing approval for every non-push gate in
> this delivery sequence. Runtime implementation is limited to the exhaustive Section 5 allow-list. It does
> not authorize operational credentials, configuration/data mutation, Production/Staging enablement, or push.
> The user's later standing authorization permits a scoped local commit after verification; push remains forbidden.

## 1. Module Summary

This follow-up closes the trusted cross-service workflow seam required by MDM and later business modules.
It provides two Platform-owned internal capabilities:

1. start a workflow from a trusted consumer with independently validated service identity and delegated
   human maker identity; and
2. read tenant-bound terminal approve/reject evidence after the workflow reaches a coherent terminal state.

The slice hardens the existing MOD-0023 runtime rather than replacing it. It makes workflow start replayable
and repairable across the already separate instance, task, assignment-snapshot, and transition-log writes;
binds the public approve/reject actor to the authenticated JWT instead of accepting body authority; and makes
terminal decision evidence a persisted read model derived from MOD-0023 records, not a callback claim.

There is no UI, navigation, DataTable, Gateway route, MDM consumer, Auth issuer change, or new workflow
provider in this pack.

## 2. Ownership and Boundaries

### MOD-0023-FU02 owns

- The trusted-consumer workflow-start transport and orchestration contract inside `Diten.Platform`.
- Independent validation of the Auth-issued service token and the delegated human JWT for start requests.
- Server-derived tenant and maker facts; neither is accepted as request authority.
- Mandatory start idempotency identity, canonical request fingerprint, durable monotonic checkpoints, exact
  replay, conflict detection, and crash recovery.
- Public approve/reject transport hardening so the acting user comes only from the validated user JWT.
- Repair of approve/reject partial writes across task, instance, assignment state, and transition log.
- Tenant-bound terminal decision evidence assembled from coherent persisted MOD-0023 facts.

### Consumed ownership

| Concern | Owner / consumption rule |
|---|---|
| Service client identity, RSA signing, five-minute token, audience/tenant grant, credential rotation | `MOD-0033-FU02`; consume its Auth-issued token and Platform validation seam. No Auth edit. |
| Human permissions and role grants | `MOD-0018`; consume regular user JWT permission claims. No grant/seed edit. |
| Product lifecycle state and durable local workflow binding | `MOD-0290` MDM; this pack never mutates a product. |
| Central audit delivery | `MOD-0021`; consume existing audit contracts only. No new audit aggregate. |
| WorkCenter projection and action dispatch | Existing native `workflow` provider/dispatcher; reuse unchanged. |

### Explicitly out of scope

- MDM submit/reconcile handlers, product status changes, callbacks, polling workers, and lifecycle UI.
- Auth token issuance entities, credentials, tenant grants, role grants, or permission onboarding.
- Gateway, frontend, WorkCenter provider/action-dispatch code, navigation, appsettings, secrets, or data.
- A new Mongo collection, new logical index, schema-budget change, or direct Mongo write.
- Delegate/request-info/cancel actor-transport redesign; only public approve/reject is in this slice.
- BPMN, visual workflow design, generic callback/webhook delivery, Production/Staging, commit, and push.

## 3. Owned Objects

| Object | Type | Contract |
|---|---|---|
| `WorkflowStartCheckpoint` | new domain enum | Monotonic durable progress over reservation, task, snapshot, start-log, and completion. |
| `WorkflowInstance` trusted-start fields | additive existing entity fields | Stores service client, maker, fingerprint, allocated child IDs, and checkpoint on the existing instance aggregate. |
| `IWorkflowInstanceStartCoordinator` | new application service | Owns exact replay, deterministic repair, checkpoint fencing, and completed read-back. |
| `StartTrustedWorkflowInstanceCommand` | new internal command | Carries only parsed business facts plus identities established by the request executor. |
| `StartTrustedWorkflowInstanceHandler` | new command handler | Delegates durable start to the coordinator; does not perform transport authentication. |
| `GetTrustedWorkflowTerminalDecisionEvidenceQuery` | new query | Reads one tenant-bound workflow instance and its terminal decision proof. |
| `GetTrustedWorkflowTerminalDecisionEvidenceHandler` | new query handler | Verifies task/instance/log coherence and returns sanitized evidence. |
| `TrustedWorkflowConsumerRequestModels` | new API transport models | Strict trusted start/evidence request and response shapes; no `TenantId`, maker, secret, or token field. |
| `TrustedWorkflowConsumerRequestParser` | new strict parser | Rejects unknown/duplicate/malformed JSON fields before MediatR dispatch. |
| `WorkflowTaskTransitionTransportModels` | new public transport models | Approve/reject bodies omit `ActorId`; controller injects the authenticated JWT actor into internal models. |
| `ITrustedWorkflowConsumerRequestExecutor` | new API security seam | Executes named authentication, tenant/maker binding, timeout, and deterministic context restoration. |
| `TrustedWorkflowConsumerServiceIdentity` | new API security fact | Exact Auth-issued MDM client, audience, token ID, and tenant grant; never returned or logged. |
| `TrustedWorkflowDelegatedUserIdentity` | new API security fact | Exact delegated human subject and tenant; never client-overridable. |
| `InternalTrustedWorkflowConsumerController` | new internal controller | Exposes trusted start and terminal-evidence routes only. |

### API endpoints

| Method | Path | Authentication | Success |
|---|---|---|---|
| `POST` | `/api/internal/v1/workflow/trusted-consumer/start` | trusted service token + delegated user JWT | `201` first completion; `200` exact replay/recovery |
| `POST` | `/api/internal/v1/workflow/trusted-consumer/terminal-decision-evidence` | trusted service token with exact tenant grant | `200` coherent terminal evidence bound to expected object type/id |

The existing public endpoints remain in place:

- `POST /api/v1/workflow/tasks/{taskId}/approve`
- `POST /api/v1/workflow/tasks/{taskId}/reject`

Their request bodies no longer carry actor authority. Actor identity is resolved from the authenticated JWT.

### Terminal evidence and lost-start-response hardening (named acceptance step, 2026-08-29)

Code-truth measurement after the first implementation found three consumer-blocking gaps. The native transition
engine writes `WorkflowInstanceStatus.Completed` after the final approve, while the evidence reader accepted only
the legacy `Approved` value. A multi-step approve/reject history also contains more than one approve/reject log, but
the reader required exactly one. Finally, if trusted start completed remotely and the HTTP response was lost before
the consumer persisted the returned IDs, replay still required the delegated user's short-lived JWT; a background
worker may neither persist that JWT nor impersonate the maker.

This named step therefore owns:

- a distinct retryable `WORKFLOW_DECISION_NOT_TERMINAL` result for a coherent workflow that is still active;
- terminal evidence derived from the final monotonic approve/reject transition, accepting native final
  `Completed` (or legacy `Approved`) for approve and `Rejected` for reject, while preserving full task/instance/log
  coherence and fail-closed contradictory-history handling; and
- a service-only, tenant/object/maker/idempotency-bound sanitized start-result lookup. It returns a result only for
  the exact trusted service client and a `Completed` trusted-start checkpoint. It never accepts tenant authority,
  token, secret, template override or candidate override from the request and never returns the start fingerprint.

The additive endpoint is `POST /api/internal/v1/workflow/trusted-consumer/start-result`. `Idempotency-Key` remains
header-only. The strict body contains only expected object type, expected object ID and expected maker subject ID.
Tenant comes from the independently validated service token grant and client ID comes from that token's subject.
Missing/mismatched tenant, client, object or maker is the same non-leaking `404`; incomplete start is retryable
`409 WORKFLOW_START_NOT_COMPLETED`; contradictory persisted facts remain fail-closed `409`. No new collection or
index is required because the existing tenant-bound unique idempotency lookup is reused.

## 4. Entity Fields

No new aggregate or collection is introduced. `WorkflowInstance` continues to inherit the live Platform
tenant-owned base contract (`TenantScopedEntity`/`BaseEntity` equivalence: `Id`, `TenantId`, soft delete,
timestamps, technical `Version`). The following fields are additive to the existing entity:

| Field | Type | Required | Rule / purpose | Index decision |
|---|---|---|---|---|
| `TrustedConsumerClientId` | `Guid?` | trusted start only | Exact service-token `sub`; server-derived and immutable after reservation. | Existing tenant/idempotency lookup is sufficient. |
| `DelegatedMakerUserId` | `Guid?` | trusted start only | Exact delegated JWT subject; same tenant as service grant. | None. |
| `StartRequestFingerprint` | `string?` | trusted start only | Lowercase SHA-256 hex over the canonical trusted-start facts; ordinal exact replay comparison. | None. |
| `StartCheckpoint` | `WorkflowStartCheckpoint` | trusted start only | Monotonic; never moves backward and cannot skip persisted-proof checks. | None. |
| `InitialApprovalTaskId` | `Guid?` | trusted start only | Allocated before reservation and reused by recovery. | Existing task ID lookup. |
| `InitialAssignmentSnapshotId` | `Guid?` | trusted start only | Allocated before reservation and reused by recovery. | Existing snapshot ID lookup. |
| `StartTransitionLogId` | `Guid?` | trusted start only | Allocated before reservation and reused by recovery. | Existing log ID lookup. |

The existing `IdempotencyKey` is mandatory for trusted start, maximum 128 characters, exact ordinal after a
single trim at the transport boundary. The canonical fingerprint includes client ID, server-derived tenant,
delegated maker, resolved template/version, object type/id/ref, ordered unique candidates, reason code,
comment/evidence requirements, and normalized UTC due date. It never includes secrets, raw tokens, or
correlation ID.

Terminal evidence is a DTO, not a persisted entity. It contains only workflow/instance/task identifiers,
source object facts, terminal action (`Approve` or `Reject`), actor user ID, reason code, decision timestamp,
transition sequence, final task/instance status, and correlation ID. It excludes token, credential, tenant
grant, public key, request fingerprint, checkpoint, and internal recovery metadata.

## 5. Repo Scope

This allow-list is exhaustive. Runtime/test code-start must stop before touching any path not listed here.

### Existing runtime files allowed to change

1. `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Workflow/WorkflowInstance.cs`
2. `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Workflow/ApprovalTask.cs`
3. `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IWorkflowRepositories.cs`
4. `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs`
5. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/WorkflowModels.cs`
6. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/StartWorkflowInstanceHandler.cs`
7. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/WorkflowTaskTransitionSupport.cs`
8. `services/Diten.Platform/src/Diten.Platform.API/Controllers/WorkflowDefinitionsController.cs`
9. `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedServiceTokenValidationExtensions.cs`
10. `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs`
11. `services/Diten.Platform/src/Diten.Platform.API/Program.cs`

`TrustedServiceTokenValidationExtensions.cs` is supplied by the approved `MOD-0033-FU02` dependency. If
that dependency is not present in the implementation worktree, integration stops; this pack does not
recreate or fork the Auth-issued service-token contract.

### New runtime files allowed

12. `services/Diten.Platform/src/Diten.Platform.Domain/Enums/Workflow/WorkflowStartCheckpoint.cs`
13. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Services/IWorkflowInstanceStartCoordinator.cs`
14. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Services/WorkflowInstanceStartCoordinator.cs`
15. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Commands/StartTrustedWorkflowInstanceCommand.cs`
16. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/StartTrustedWorkflowInstanceHandler.cs`
17. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Queries/GetTrustedWorkflowTerminalDecisionEvidenceQuery.cs`
18. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/QueryHandlers/GetTrustedWorkflowTerminalDecisionEvidenceHandler.cs`
19. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Validators/GetTrustedWorkflowTerminalDecisionEvidenceValidator.cs`
20. `services/Diten.Platform/src/Diten.Platform.API/Models/Workflow/TrustedWorkflowConsumerRequestModels.cs`
21. `services/Diten.Platform/src/Diten.Platform.API/Models/Workflow/TrustedWorkflowConsumerRequestParser.cs`
22. `services/Diten.Platform/src/Diten.Platform.API/Models/Workflow/WorkflowTaskTransitionTransportModels.cs`
23. `services/Diten.Platform/src/Diten.Platform.API/Security/ITrustedWorkflowConsumerRequestExecutor.cs`
24. `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedWorkflowConsumerRequestExecutor.cs`
25. `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedWorkflowConsumerServiceIdentity.cs`
26. `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedWorkflowDelegatedUserIdentity.cs`
27. `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalTrustedWorkflowConsumerController.cs`

### Additive hardening runtime files

The named hardening step may additionally create only:

28. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Queries/GetTrustedWorkflowStartResultQuery.cs`
29. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/QueryHandlers/GetTrustedWorkflowStartResultHandler.cs`
30. `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Validators/GetTrustedWorkflowStartResultValidator.cs`

It may modify only the already-listed `WorkflowModels.cs`, terminal-evidence handler, strict request models/parser,
trusted request executor/interface and internal controller. Repository interfaces/implementations, entities,
schema manifest and transition mutation code are read-only unless a failing real-Mongo test proves the existing
tenant-bound idempotency lookup or native transition output differs from the measured contract.

### Test allow-list

1. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/WorkflowInstanceStartTests.cs`
2. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/WorkflowTaskTransitionTests.cs`
3. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/TrustedWorkflowStartRecoveryMongoTests.cs` (new)
4. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/TrustedWorkflowTerminalDecisionEvidenceMongoTests.cs` (new)
5. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Security/TrustedWorkflowConsumerSecurityTests.cs` (new)
6. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/WorkflowPublicActorBindingTests.cs` (new)
7. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Security/TrustedServiceTokenValidationTests.cs`
8. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/DependencyInjectionSmokeTests.cs`
9. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/WorkAggregation/WorkItemActionDispatchTests.cs`

The named hardening step may edit only tests 3-5 above and
`Workflow/WorkflowTaskTransitionTests.cs` to add actual transition-to-evidence, multi-step terminal-log,
nonterminal/inconsistent separation, strict start-result parsing/security, completed lookup, incomplete lookup,
client/maker/object mismatch and cross-tenant non-leakage regressions. Hand-authored terminal states alone are not
acceptable evidence; at least one real-Mongo test must invoke the native transition support and then the evidence
handler.

Governance maintenance while implementing is limited to this pack and its single canonical registry row.

## 6. Protected Paths

- `.antigravity/**`.
- `services/Diten.AuthService/**`, including service identity, token issuance, credentials, tenant/audience
  grants, role grants, and permission seeds.
- `services/Diten.MdmService/**`, all product entities/lifecycle commands, audit outbox, HTTP clients, workers,
  manifest, and tests.
- `gateway/Diten.ApiGateway/**`, `frontend/Diten.Web/**`, and all navigation/configuration files.
- Existing WorkCenter native provider and action dispatcher runtime, including
  `WorkflowApprovalWorkItemProvider` and `WorkflowApprovalWorkItemActionDispatcher`.
- MOD-0021 audit runtime and persistence, MOD-0018 authorization runtime, and MOD-0033 issuance runtime.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/**`; no new collection,
  index, schema profile, schema budget, or migration is permitted without measured evidence and a pack revision.
- `appsettings*.json`, environment files, secrets, credentials, data, seed, operational scripts, and `.work/**`.
- Production/Staging operations, branch deletion, reset/stash/restore, commit, and push.

## 7. Dependencies

| Dependency | Required evidence before runtime starts |
|---|---|
| Parent MOD-0023 runtime | Existing instance/task/snapshot/log repositories, unique tenant/idempotency index, transition-log uniqueness, public task actions, and Workflow WorkCenter provider are present. |
| `MOD-0033-FU02 — Service Identity Token Issuance Foundation` | Auth-issued dedicated `Diten.MDM` client, five-minute RSA token, tenant/audience grant, current/previous-key rotation, and Platform public-key validation seam are integrated into the implementation worktree. |
| `MOD-0018` | Human maker and approver JWTs use existing authentication and permissions; no permission is minted here. |
| MOD-0021 | Existing audit behavior/outbox remains authoritative; no second audit store. |
| MongoDB | Existing WorkflowWorkCenter schema profile and indexes only; real Mongo at `mongodb://127.0.0.1:27017` for integration evidence. |

The trusted workflow service audience is additive and exact: `TRUSTED_WORKFLOW_CONSUMER`. The allowed
service name is exact `Diten.MDM`. The same service token must carry one non-empty tenant grant; it cannot
grant access to any other tenant. A credential alone is not tenant authority.

## 8. Runtime Constraints

### Authentication and tenant authority

- Service authentication scheme: `TrustedWorkflowConsumerService`.
- Delegated human scheme: `TrustedWorkflowDelegatedUser`; it reads only
  `X-Delegated-Authorization: Bearer <JWT>` and validates independently from the service token.
- Service token travels in standard `Authorization: Bearer <token>` and is validated for issuer, RSA
  signature, exact audience, five-minute lifetime, exact client/service claims, token ID, and tenant grant.
- Trusted start requires both identities. Service-token tenant, delegated-user `tenant_id`, and the tenant
  installed into `ITenantContext` must be the same non-empty GUID.
- Request/body/query `TenantId`, maker/actor ID, service name, audience, or credential fields are rejected.
- `X-Tenant-Id` is not an authority for these internal routes; supplying it is rejected to prevent ambiguity.
- Maker is derived from the independently authenticated delegated user and that user must satisfy the existing
  `platform.workflow.instances.start` permission through the canonical `PermissionClaimEvaluator`. A valid human
  token without that permission returns `403` before parsing or mutation. Terminal evidence is a machine
  reconciliation read and requires the exact trusted service identity/tenant grant but no delegated maker.
- Tokens, claims dumps, secrets, public-key material, and delegated headers are never logged or returned.
- The executor restores ambient tenant/security context in `finally`; cancellation propagates unchanged.
- Each internal request has a linked two-second budget. Budget expiry maps to `504`; caller cancellation is
  not translated into timeout success/failure.

### Durable start and replay

- Trusted start requires an `Idempotency-Key` header. The body cannot provide a second key.
- The coordinator reserves one existing `WorkflowInstance` with preallocated child IDs and exact canonical
  fingerprint before creating child records.
- Checkpoint order is monotonic:
  `Reserved -> TaskPersisted -> AssignmentSnapshotPersisted -> StartLogPersisted -> Completed`.
- After every child write, the exact persisted record is re-read and compared before checkpoint advance.
- A crash before checkpoint advancement is repaired by exact-ID/exact-facts read-back; it never creates a
  second task, snapshot, log, or instance.
- Exact replay returns the same IDs and resolved facts. Same tenant/client/key with a different fingerprint
  returns `409 WORKFLOW_START_IDEMPOTENCY_CONFLICT` before any further mutation.
- Concurrent matching requests converge through the existing unique `{TenantId, IdempotencyKey}` index and
  optimistic `Version` fencing. Concurrency is never solved by last-write-wins.
- `Completed` is returned only after instance, task, snapshot, start log, checkpoint, and pinned template
  version are coherently re-read.

### Approve/reject and evidence

- Public approve/reject bodies contain reason/comment/evidence/idempotency facts only. Any unknown
  `ActorId` field is rejected by strict transport binding; internal `ActorId` is filled from the JWT.
- The transition support treats task update, instance update, any next-step snapshot/task update, and log
  append as a repairable idempotent operation. A retry re-reads exact facts and completes only missing writes.
- Transition log sequence remains monotonic and uses the existing unique tenant/instance/sequence and
  task/action/idempotency indexes. No new index is authorized.
- Terminal evidence is available only for coherent `Approve`/`Reject` terminal state with exactly matching
  task, instance, and append-only transition log facts. Incomplete/contradictory state fails closed.
- Workflow decision does not mutate MDM product state and no callback/webhook is emitted.

## 9. Layout & Shell Contract

- `shell: none`; backend-only.
- Razor layout: none.
- View folder, frontend route, DataTable, localization, navigation, and browser surface: none.
- `_LayoutPlatformAdmin`, `_LayoutTenantShell`, `_Layout.cshtml`, `_ViewStart.cshtml`, and all Razor files are
  protected and must remain unchanged.

## 10. Backend File Convention

The Section 5 paths are the exact convention for this follow-up:

- commands under `Application/Features/Workflow/Commands/` as sealed records;
- queries under `Application/Features/Workflow/Queries/` as sealed records;
- command handlers and query handlers in their separate live folders, without `Command`/`Query` suffix;
- validators under `Validators/`, without `Command` suffix;
- focused orchestration in `Services/`, not an oversized controller/handler;
- API-only parsing/security under `Diten.Platform.API/Models/Workflow` and `API/Security`;
- thin controllers dispatch through MediatR/services and do not access MongoDB;
- repository interfaces remain in Domain; Mongo driver code remains in Infrastructure.

One public type per file and cancellation propagation are mandatory. Existing live filenames in the exact
allow-list are preserved even where older code predates the current Golden naming convention.

## 11. Frontend File Contract

No frontend files exist or may be created by this pack. `golden_reference: none`, `form_field_count: 0`,
and `shell: none` are intentional. WorkCenter continues to render the existing native workflow provider;
there is no MDM-specific WorkCenter card, bridge, action dispatcher, JavaScript, locale, or route.

## 12. Validation Rules

| Field / fact | Required | Format / rule | Authority / pre-check |
|---|---|---|---|
| `Idempotency-Key` | yes, trusted start | Trim once; 1..128 printable characters; no control character. | Header only; canonical fingerprint lookup. |
| `TemplateId` / `TemplateCode` | exactly one | Non-empty GUID or exact existing template code; no alias/fuzzy fallback. | Tenant-bound active published template/version. |
| `ObjectType` | yes | Trimmed, 1..128, exact ordinal business type. | Consumer fact; cannot convey tenant. |
| `ObjectId` | yes | Trimmed, 1..256, opaque identifier. | Consumer fact; validated by owning module before call. |
| `ObjectRef` | optional | If absent, deterministic `ObjectType|ObjectId`; if supplied, 1..512 and fingerprinted. | Exact replay fact. |
| `CandidatePrincipalIds` | yes | 1..100 distinct non-empty exact principal values; deterministic ordinal order. | Existing workflow candidate resolution. |
| `ReasonCode` | optional | Trimmed, max 128, no control character. | Exact replay fact. |
| `CommentRequired` / `EvidenceRequired` | yes | Boolean. | Effective values combine with pinned template step. |
| `DueAt` | optional | UTC `DateTimeOffset`; must be future at initial reservation. | Canonical UTC fingerprint. |
| Service token | yes, both routes | Exact named scheme, RSA SHA-256, zero clock skew, five-minute lifetime, exact audience/service/client/tenant grant. | Auth-issued; independently validated. |
| Delegated JWT | yes, start | Standard signed user JWT, exact non-empty subject and `tenant_id`; same tenant as service grant. | Independently validated from delegated header. |
| `WorkflowInstanceId` | yes, evidence | Non-empty GUID. | Tenant-bound repository read; cross-tenant is non-leaking 404. |
| `ExpectedObjectType` | yes, evidence | Trimmed, 1..128, exact ordinal business type. | Must equal the persisted workflow object type or return the same non-leaking 404. |
| `ExpectedObjectId` | yes, evidence | Trimmed, 1..256, opaque identifier. | Must equal the persisted workflow object ID or return the same non-leaking 404. |
| Approve/reject idempotency | yes | 1..128; exact ordinal. | Existing transition-log unique identity. |
| Public actor | server-only | Non-empty authenticated JWT user ID; body actor forbidden/unknown. | Controller authentication principal. |

Strict JSON parsing rejects unknown, duplicate, case-drifted, and technically sensitive fields. No trim,
case-fold, alias, or fuzzy matching changes a business identity after parsing.

## 13. Failure Path to Verify

| Scenario | Expected fail-closed behavior |
|---|---|
| Missing/malformed service token | `401`; no tenant context, instance, task, snapshot, or log mutation. |
| Wrong audience/service/client, missing tenant grant, or revoked/expired token | `403` for authenticated-but-forbidden facts or `401` for invalid authentication; no mutation. |
| Missing/invalid delegated JWT on start | `401`; service credential never substitutes for the human maker. |
| Service/delegated tenant mismatch or supplied `X-Tenant-Id` | `403`; no cross-tenant metadata. |
| Missing/unpublished template | non-leaking `404` or stable `409` no-active-version; no child graph. |
| Missing idempotency key | `400 WORKFLOW_START_IDEMPOTENCY_REQUIRED`; no mutation. |
| Same key and exact fingerprint | `200` with the same instance/task/snapshot IDs after repair/read-back. |
| Same key with drifted payload, maker, client, tenant, template/version, candidates, or due date | `409 WORKFLOW_START_IDEMPOTENCY_CONFLICT`; existing graph unchanged. |
| Crash after any start write/checkpoint | Replay repairs only missing exact records and returns one coherent graph; false success is forbidden. |
| Concurrent identical starts | One persisted graph; both completed responses identify the same graph. |
| Public approve/reject body contains `ActorId` | `400`; body spoof cannot reach transition logic. |
| JWT actor is not current assignee / violates SOD | Existing `403`; actor cannot be replaced by body data. |
| Crash during approve/reject partial writes | Same operation replay repairs missing writes; no duplicate decision/log/sequence. |
| Cross-tenant or expected-object-mismatched evidence query | Same `404`; existence, actor, status, and timestamps are not leaked. |
| Non-terminal or internally inconsistent evidence | `409` or `503` with stable reason; no fabricated decision evidence. |
| Two-second internal budget exceeded | `504`; caller cancellation propagates and is not misreported. |

All error envelopes use stable reason codes and correlation IDs and expose no stack trace, token, service
claim, internal fingerprint, checkpoint, or credential detail.

## 14. Authorization Convention

- Internal routes use the named trusted-service scheme and exact service/audience/tenant-grant policy; they
  are never `[AllowAnonymous]` and never use a shared static API key.
- Trusted start additionally requires an independently authenticated delegated user JWT. Its maker identity
  and tenant are server-derived, and its claims must satisfy the existing
  `platform.workflow.instances.start` permission through the canonical permission evaluator.
- Public approve/reject retain their existing permissions:
  - `platform.workflow.tasks.approve`
  - `platform.workflow.tasks.reject`
- The authenticated public JWT user is the sole actor authority. Assignment snapshot and maker-checker/SOD
  checks remain in Workflow transition logic.
- No new permission key, role, default grant, entitlement, service identity, or tenant grant is created here.
- Service credentials grant client authenticity only; the exact token tenant grant limits the tenant. They
  do not grant human approval permission.

## 15. Gateway / API Routing Decision

Gateway change is **not required and is forbidden in this pack**:

- Both new endpoints are internal service-to-service Platform endpoints, not browser endpoints.
- The MDM consumer will call the configured Platform internal base address in its own later, separately
  approved pack. That consumer is not implemented here.
- Existing public workflow/WorkCenter traffic keeps its current Gateway routes.
- Browser code never calls Platform `5057` directly, but this pack adds no browser consumer.
- If a future external/browser surface is requested, route work belongs to `integration-agent` under a
  separate approved allow-list including `OPTIONS`; it must not reuse these internal routes.

Lookup/reference-data decision: no lookup, select list, or hardcoded reference data is introduced. This
pack consumes a pinned workflow template/version through existing MOD-0023 repositories.

## 16. Acceptance Criteria

- [ ] Trusted start is inaccessible without an independently valid Auth-issued service token for exact
  audience `TRUSTED_WORKFLOW_CONSUMER`, exact service `Diten.MDM`, and exact tenant grant.
- [ ] Trusted start independently validates a delegated human JWT and derives tenant/maker server-side;
  body/query/header tenant or actor spoofing cannot influence persisted facts.
- [ ] Initial completed start returns `201` only after exact persisted read-back of one instance, one initial
  task, one assignment snapshot, one start log, pinned template version, and `Completed` checkpoint.
- [ ] Exact replay/recovery returns `200` with the same IDs/facts and creates no duplicate.
- [ ] Same idempotency identity with any canonical fingerprint drift returns stable `409` before mutation.
- [ ] Real-Mongo injected crash at every checkpoint resumes the same graph to completion; concurrent exact
  starts converge through existing unique index and optimistic fencing.
- [ ] Public approve/reject transport does not accept actor authority; transition actor and log actor equal
  the authenticated JWT subject even when a malicious body attempts `ActorId`.
- [ ] Approve/reject retry repairs every injected partial-write boundary and produces one terminal mutation
  and one monotonic decision log for the idempotency identity.
- [ ] Terminal evidence is returned only for a coherent tenant-bound persisted approve/reject decision and
  contains no technical security/recovery metadata.
- [ ] Cross-tenant, expected-object-mismatched, and inconsistent-state evidence paths fail without leakage.
- [ ] Existing public workflow start, transition gate, escalation, native WorkCenter workflow provider/action
  dispatch, MOD-0021 audit, and MOD-0033 token-validation regressions remain green.
- [ ] No new Mongo collection/index/schema budget, MDM/Auth/Gateway/frontend/config/data change exists.
- [ ] Platform API Release build, focused tests, all Workflow tests, full Platform suite, and architecture
  guards pass with zero skipped tests; any current-main failure is proven byte-identical and not overclaimed.

## 17. Test Expectations

### Unit and contract

- Strict parser: exact fields, duplicate/unknown/case-drift/technical-field rejection, bounded values.
- Authentication: invalid signature/key/audience/service/client/tenant/lifetime, current/previous-key rotation,
  delegated header parsing, dual-auth independence, and context restoration.
- Canonical fingerprint: order normalization only where explicitly allowed; payload/identity drift conflict.
- Public actor binding: `ActorId` unknown-field `400`, JWT subject used in command/log, missing subject `401`.
- Decision evidence: approve/reject only, exact task/instance/log coherence, sanitized DTO.

### Real Mongo

- Use `mongodb://127.0.0.1:27017`, the shared fixed Platform test database, a fresh `TenantId` per test, and
  only the existing `SchemaProfile.WorkflowWorkCenter` schema application.
- DB-010 is mandatory: no per-run/GUID database, no `EnsureIndexesAsync`, no full schema, no new profile copy.
- Inject a crash after reservation and every child/checkpoint/transition boundary; replay must finish the
  same IDs with cardinality `1/1/1/1` for instance/task/snapshot/start-log.
- Run matching concurrency and same-key/different-fingerprint races; assert one graph and deterministic
  success/conflict outcomes.
- Assert decision-log sequence monotonicity, exact replay, cross-tenant non-disclosure, and no residual test
  facts outside each fresh tenant.

### Regression and quality gates

- Focused files in Section 5: zero failed, zero skipped.
- All `Workflow` tests: zero failed, zero skipped.
- Full `services/Diten.Platform` suite and Platform API Release build.
- `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests` without adding a known-violation.
- `git diff --check`, conflict-marker, trailing-whitespace, UTF-8 BOM, and final-newline checks.
- Read-only proof that protected paths, schema manifest/budget, collection names, indexes, operational data,
  secrets, and WorkCenter provider/dispatcher did not change.

## 18. Ready-for-dev Checklist

- [x] `AGENTS.md`, PSS domain config, master development plan, canonical registry, delivery board,
  module-pack standard, relevant rules, and parent MOD-0023 pack read.
- [x] Backend-only decision recorded: `shell: none`, `golden_reference: none`, `form_field_count: 0`;
  Golden DataTable code is not applicable.
- [x] DCP-002 preflight passed on 2026-08-29:
  `verify_module_id.py . --check-id MOD-0023-FU02 --name "Trusted Consumer Workflow Start and Terminal Decision Evidence Foundation" --parent MOD-0023`
  -> `OK MOD-0023-FU02: proven against Blueprint/registry.`
- [x] Registry collision scan found no prior `MOD-0023-FU02` identity; `MOD-0023-FU01` remains a separate
  Global Object Transition Gate reservation outside this slice.
- [x] Parent/FU decision fixed: trusted workflow start/evidence hardens MOD-0023; it is not a new module.
- [x] Ownership boundaries with MOD-0033, MOD-0018, MOD-0021, MOD-0290, and WorkCenter are explicit.
- [x] Exact runtime/test allow-list and protected paths are frozen.
- [x] Entity fields, validation, failure mappings, authorization, routing, acceptance criteria, and test
  matrix are testable.
- [x] Existing Mongo indexes are sufficient for the planned identities; no speculative index is allowed.
- [x] User standing approval authorizes `ready-for-dev` planning status and later exact code-start, excluding
  push and operational/Production mutations.

## 19. Implementation Notes

### Code-truth findings that this pack closes

- Existing public workflow start writes instance, task, snapshot, and start log separately. An incomplete
  replay currently returns conflict instead of repairing the graph.
- Existing approve/reject public transport accepts `ActorId` in the body. This pack replaces that transport
  authority with the authenticated JWT subject while preserving internal command compatibility.
- Existing task transitions have idempotency/log seams but must reconcile partial task/instance/snapshot/log
  writes before reporting success.
- Existing transition-log uniqueness and instance idempotency indexes are adequate; adding storage is not
  justified.
- The Auth-issued service-token validator arrives from `MOD-0033-FU02` with audit-specific validation. This
  pack may add a named workflow scheme/audience to that existing seam; it may not weaken or replace the
  audit scheme.
- WorkCenter already owns the native `workflow` provider and generic action dispatch. No consumer-specific
  Platform bridge is needed.

### Planned implementation order

1. Integrate the approved `MOD-0033-FU02` service-token dependency into a current-main worktree and rerun its
   focused tests.
2. Add entity/checkpoint/repository coordination and real-Mongo start recovery first.
3. Add the trusted internal transport/security chain and prove dual-auth/tenant binding.
4. Bind public approve/reject actors to JWT and add partial-write recovery.
5. Add terminal evidence query/controller surface.
6. Run focused, Workflow, WorkCenter regression, full Platform, build, architecture, and protected-path gates.
7. Record real evidence and move to `review`; do not claim operational or MDM lifecycle completion.

### Implemented evidence — 2026-08-29

- The approved `MOD-0021-FU01`, `MOD-0021-FU02`, and `MOD-0033-FU02` dependency commit was integrated locally
  before this runtime slice. No push or operational mutation was performed.
- Trusted start now reserves one durable workflow identity and repairs the task, assignment snapshot, start log,
  and checkpoint chain under optimistic fencing. Exact replay returns the same facts; payload drift returns 409.
- The internal transport independently validates the MDM service token and delegated human JWT, rejects tenant
  headers, binds the maker to the JWT subject, requires `platform.workflow.instances.start`, applies a two-second
  budget, and restores both principal and tenant context deterministically.
- Terminal evidence requires the expected object type and object ID and returns the same non-disclosing 404 for
  missing, cross-tenant, casing-mismatched, or different-object requests.
- Public approve/reject transport no longer accepts body-supplied actor identity; the authenticated JWT subject is
  authoritative. Existing native WorkCenter workflow provider/dispatcher code was not changed.
- Focused security and DI tests: **49/49 passed**. Expanded workflow, dispatch, security, and real-Mongo tests:
  **140/140 passed**, with no skipped tests. Durable/evidence/DI subset: **25/25 passed**.
- Full Platform comparison proves no new failure: pre-change baseline **3674/3698 passed, 24 failed**; this slice
  **3712/3736 passed, 24 failed**. The same 24 pre-existing failures remain outside this allow-list; all 38 added
  tests passed.
- Platform API Release build completed with zero errors. `git diff --check` passed.
- The JWT clock-skew architecture guard exposed one literal in the approved service-token dependency; the
  validator now uses the shared `JwtValidationDefaults.ClockSkew`, and the focused guard passes **4/4**.
  The full architecture project improved from **8/11** to **9/11**; its two remaining failures are the
  pre-existing Mongo per-run database inventory/debt outside this pack's allow-list.

### Terminal-evidence and lost-start-response hardening evidence — 2026-08-29

- Native workflow transition support was exercised against real Mongo for both final `Approve` and `Reject`.
  Final approve is accepted in the native `Completed` state and in the legacy `Approved` state; reject remains
  `Rejected`. Earlier coherent approvals in a multi-step workflow are retained as history and the final monotonic
  approve/reject transition is authoritative. Prior reject, duplicate/non-monotonic sequence, terminal-action
  mismatch, or task/log/instance actor/status/reason mismatch remains fail-closed.
- Evidence is also bound to the authenticated service client and a coherent completed trusted-start proof.
  Wrong same-tenant client, generic/non-trusted workflow, or incomplete trusted start returns the same non-leaking
  404. Transition sequence numbers must be exactly contiguous from 1 through the terminal record; a `1,3` gap is
  inconsistent rather than merely increasing.
- A coherent active or pending workflow now returns retryable `409 WORKFLOW_DECISION_NOT_TERMINAL` instead of
  being misclassified as corrupt evidence. Focused terminal-evidence real-Mongo tests passed **17/17** and the
  expanded Workflow plus trusted-security group passed **196/196**, with no skipped tests.
- `POST /api/internal/v1/workflow/trusted-consumer/start-result` provides service-only recovery after a lost start
  response. The idempotency key is header-only; tenant and service client come from the validated service token;
  the exact object type, object ID, and maker subject are request assertions. A delegated JWT is neither required
  nor accepted. Tenant/client/object/maker mismatch returns the same non-leaking 404, an incomplete checkpoint
  returns retryable 409, and inconsistent persisted task/snapshot/start-log facts return fail-closed 409.
- The recovery response reuses the sanitized `TrustedWorkflowStartResult`; no fingerprint, credential, token, or
  tenant authority is exposed. Persisted proof additionally requires exact maker `StartedBy`, start-log
  `ActorRef == ActorId`, and native `Active` target state/status. Strict JSON, auth, context-restoration, timeout,
  cancellation, corruption, and real-Mongo recovery tests passed **32/32**, with no skipped tests.
- Platform API Release build passed with zero errors. The complete Platform suite recorded **3734/3758 passed**
  and the same **24** pre-existing failures already recorded above; all newly added hardening tests passed.
  `git diff --check` remained clean. No collection, index, schema profile, repository, entity, configuration,
  credential, or operational data mutation was introduced.

## 20. Follow-up Items

1. **MOD-0290 MDM lifecycle consumer:** separate approved pack/step for product submit validation,
   Draft-to-Pending local mutation, durable workflow binding, trusted-start/token clients, terminal evidence
   polling/reconciliation, final product state, and local audit delivery.
2. **MOD-0018-FU23 completion:** MDM must publish the exact eight product lifecycle permission definitions
   and live catalog/grant reconciliation before that permission pack can move beyond review.
3. **Operational Local Development:** dedicated MDM service client, tenant/audience grant, current/previous
   credential rotation, configured Platform public keys, and end-to-end start/approve/reject/evidence smoke
   require separate operational mutation approval.
4. **Production/Staging:** credentials, rotation runbook, metrics, alerts, retention, capacity, and security
   review remain explicitly gated.
5. **Callbacks/webhooks:** not selected. MDM reconciliation reads terminal evidence; any future event push is
   a separate contract and cannot become workflow state authority.
6. **Other trusted consumers:** no wildcard audience/service grant. Each consumer requires an explicit
   identity, tenant grant, and owner-approved contract.
