# MOD-0117 Phase 2A Implementation Audit — 2026-07-29

## 1. Audit status

This document records the repository state produced under the scoped
[MOD-0117 module pack](../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md).
It is an implementation audit and API summary with scoped runtime evidence,
not production authorization or production-readiness acceptance.

- Phase 2A evidence promotes the module pack to `review`, not `done`.
- The implemented scope is Phase 2A Context & Referenceability Core only.
- The temporary browser-to-database chain, isolated live PSS-B1 → PPM-I1
  entitlement chain, and isolated PPM-to-MOD-0021 audit-delivery chain are
  evidenced; production deployment/configuration, branch integration, and
  production-environment replica-set rollback readiness are not claimed.
- Phase 2B, the typed ExternalContextReference provider, MOD-0354/DWS runtime integration, WorkCenter integration, and all four protected WorkCenter hazards remain excluded.

## 2. Implemented repository surfaces

### Backend service

The new [Diten.PpmService solution](../../services/Diten.PpmService/Diten.PpmService.sln)
contains the required API, Application, Domain, Persistence, and Infrastructure
projects plus a test project. The implementation includes:

- Portfolio, Initiative, Program, and Project domain entities;
- CQRS commands, queries, handlers, validators, and application services;
- MongoDB repositories, tenant-first indexes, optimistic concurrency, soft
  deletion, and a transaction health check;
- a producer-local `ppm_audit_intents` collection written in the same MongoDB
  transaction as each mutation;
- authenticated API controllers under `/api/v1/ppm/{resource}`;
- a fail-closed effective-permission adapter.

The service is configured for local port `5061`. That port is a backend
process boundary; it is not a browser-facing endpoint.

### Frontend

The implementation adds:

- tenant hub `/ppm`;
- `/ppm/portfolios`;
- `/ppm/initiatives`;
- `/ppm/programs`;
- `/ppm/projects`.

The controller is [PpmController](../../frontend/Diten.Web/Controllers/PPM/PpmController.cs).
It accepts only the four closed resource names and forwards API calls to
`GatewayUrl/api/v1/ppm/{resource}`. The frontend therefore targets Gateway
port `5000`; it contains no direct browser call to service port `5061`.

The pages use `_LayoutTenantShell`, DataTable v2 markup, Slim-style Index
create/edit offcanvas surfaces, details quick views, lifecycle badges, and
localized resources for the seven required languages. These files are
code-reality evidence only. They are not proof that the UI can complete a live
request while the gateway route and permission integration remain absent.

## 3. Entity and lifecycle summary

All four aggregates use a PPM-local entity base with `Guid` identity and
tenant identity, scalar UTC timestamps, soft-delete fields, actor audit fields,
and integer optimistic-concurrency versioning. `Code` and `Name` are normalized
with trim plus NFC before persistence. `VisibilityPolicyKey` is rejected while
its authoritative MOD-0018 validation path is unavailable.

| Entity | Parent rule | Initial state | Allowed forward transitions | New-reference states |
|---|---|---|---|---|
| Portfolio | None | `Draft` | `Draft → Active`; `Draft/Active → Archived` | `Draft`, `Active` |
| Initiative | Optional Portfolio | `Proposed` | `Proposed → Active/Cancelled`; `Active → OnHold/Completed/Cancelled`; `OnHold → Active/Completed/Cancelled` | `Proposed`, `Active`, `OnHold` |
| Program | Optional Portfolio | `Draft` | `Draft → Active/Cancelled`; `Active → OnHold/Completed/Cancelled`; `OnHold → Active/Completed/Cancelled` | `Draft`, `Active`, `OnHold` |
| Project | Exactly one Initiative XOR Program, represented by `ParentType + ParentId` | `Draft` | `Draft → Planned/Cancelled`; `Planned → Active/OnHold/Cancelled`; `Active → OnHold/Completed/Cancelled`; `OnHold → Active/Completed/Cancelled` | `Draft`, `Planned`, `Active`, `OnHold` |

Referenceability is derived from tenant visibility, soft-delete state, and the
closed lifecycle state. It is not persisted as a mutable authorization flag.
Terminal parent records cannot receive new references. Cross-tenant and
non-visible lookups are deliberately indistinguishable from absence.

## 4. API contract summary

Each of `portfolios`, `initiatives`, `programs`, and `projects` exposes the
same Phase 2A route shape:

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/v1/ppm/{resource}` | Tenant-scoped active list |
| `GET` | `/api/v1/ppm/{resource}/{id}` | Tenant-scoped detail |
| `POST` | `/api/v1/ppm/{resource}` | Create |
| `PUT` | `/api/v1/ppm/{resource}/{id}` | Metadata/parent update with expected version |
| `POST` | `/api/v1/ppm/{resource}/{id}/lifecycle` | Closed lifecycle transition with expected version |
| `DELETE` | `/api/v1/ppm/{resource}/{id}?expectedVersion={version}` | Soft delete |

The API uses the repository `Response<T>` envelope and string enum values;
integer enum values are rejected. The implemented error boundary is:

- `400` — malformed input or validation failure;
- `401` — absent or invalid authentication;
- `403` — authenticated command lacks the required MOD-0018 permission result;
- `404` — missing, soft-deleted, cross-tenant, or otherwise invisible entity/reference;
- `409` — duplicate tenant-scoped code, invalid lifecycle transition, or stale concurrency version;
- `503` — required Mongo transaction capability is unavailable.

No ExternalContextReference validation endpoint, provider interface, route,
transport, or cache has been implemented.

## 5. Persistence and audit behavior

The implementation defines `ppm_portfolios`, `ppm_initiatives`,
`ppm_programs`, `ppm_projects`, and `ppm_audit_intents`. Active codes use
tenant-first partial unique indexes. Reads and mutations are constrained by
tenant and soft-delete state; mutation replacement additionally checks the
expected version.

Every mutation and its producer-local technical audit intent are executed
inside one Mongo session transaction using snapshot read concern, majority
write concern, and primary read preference. Failure to create or commit the
transaction aborts the operation and is mapped to `503` where the failure means
replica-set transaction support is unavailable.

This local record is not the MOD-0021 business audit system of record. The PPM
producer, shared durable outbox seam, and Platform consumer now provide an
isolated, real at-least-once delivery path to MOD-0021; §9 records its live
evidence. Production activation/configuration, operational replay, external
pager/alarm integration, and the independent legacy-zero broker observation
remain open. No exactly-once claim is made.

## 6. Authorization posture

The application declares closed `ppm.{resource}.{action}` permission names and
routes all reads and mutations through `IEffectivePermissionEvaluator`. A
shared signed-JWT evaluator and the PPM adapter now implement the reusable
in-process enforcement path; their focused suites pass **21/21** and **17/17**
respectively.

The authorization and entitlement code reality has advanced beyond the
original browser smoke. PSS-A implements exact, explicit PPM grants without
implicit entitlement-to-grant creation (`23d56efb`). PSS-B1 supplies the
authoritative PPM entitlement provider (`90eec003`), PSS-B2A supplies the
secure PPM audit consumer foundation (`121fa2bf`), the shared permission
evaluator is present (`5d183069`), and the PPM consumer enforces entitlement
before exact permission evaluation (`11aa3766`).

These checkpoints establish executable repository behavior, not completed
production deployment. The original real AuthService tenant-administrator
token used during browser verification contained **zero** PPM permissions,
and that smoke used an ephemeral, environment-only signed JWT. No AuthService
data, roles, grants, or tenant configuration were changed by that smoke or by
the live evidence recorded below. Production configuration, deployment,
authorized tenant-scoped grant administration, and branch integration must
not be inferred from these commits.

The service does not calculate roles, grants, or effective permissions and
does not repurpose `IEntitlementChecker` as a permission engine. Missing tenant
or actor context also fails closed.

## 7. Test and validation evidence

Repository test sources cover:

- lifecycle and terminal-state rules;
- Project parent identity requirements;
- referenceability;
- enum string serialization and numeric-enum rejection;
- fail-closed authorization;
- mutation/audit transaction coupling;
- frontend route allowlisting, Gateway-only targeting, tenant shell, and
  DataTable v2 markup.

The current checkpoint-level automated results are:

- PPM unit/contract: **76/76 PASS**;
- PPM integration: **29/29 PASS** in each of **5** fresh processes;
- PPM outbox composition: **2/2 PASS**;
- BuildingBlocks Eventing: **33/33 PASS**;
- Platform PPM routing/security targeted tests: **PASS**;
- Platform Eventing: **69 PASS / 3 opt-in live RabbitMQ SKIP**;
- PPM API build: **0 warnings / 0 errors**;
- Platform API build: **0 warnings / 0 errors**;
- MOD-0117 canonical preflight: **PASS**;
- `git diff --check`: **clean**.

The three opt-in Platform Eventing skips are not counted as passes. Earlier
focused evidence remains valid: the shared signed-JWT evaluator passed 21/21,
the initial PPM service/adapter suite passed 17/17, the targeted Gateway PPM
route passed 1/1, `Diten.Web.Tests` passed 22/22, and the delegated jsdom check
passed.

The full Gateway suite still contains a pre-existing HCM failure unrelated to
MOD-0117. The focused PPM route result is therefore reported separately and is
not presented as a clean full-suite result.

A real authenticated browser smoke exercised this temporary chain:

`UI :5191 → Gateway :5200 → Diten.PpmService :5061 → Mongo replica set :27018`

It created Portfolio, Initiative, and Program records; created Projects with
both Initiative and Program parents; opened quick view; edited a record;
transitioned a Project from `Draft` to `Planned`; and soft-deleted a record.
The browser console reported **0 errors and 0 warnings**. The temporary gateway
and Mongo ports are verification-only; the governed production-facing browser
boundary remains Gateway `5000`, and service port `5061` remains non-browser
facing.

The successful browser smoke used the ephemeral signed JWT bootstrap described
in §6. It is real UI-to-database CRUD evidence, but it is not evidence that
the PSS/Auth/PPM checkpoints have been merged, configured, and deployed in a
production environment.

DataTable verification now reports **60 PASS / 4 known policy/profile false
positives per resource surface**. The four remaining findings are recorded as
verifier-policy/profile incompatibilities rather than silently treated as
passes.

## 8. Live PSS-B1 → PPM-I1 Entitlement Evidence — 2026-07-30

This runtime evidence was produced from commit `11aa3766` in an isolated local
topology. It exercised the real
`Diten.Platform.API` PSS-B1
`InternalPpmEntitlementDecisionController`, its real `IEntitlementChecker`,
the real `Diten.PpmService` PPM-I1 consumer, and real Mongo repositories.
No mock provider, stub controller, or fake PASS was used.

The isolated ports were:

| Runtime | Port |
|---|---:|
| Disposable Mongo replica set | `27028` |
| Real Platform provider — enabled | `5257` |
| Real Platform provider — disabled | `5258` |
| Real PPM consumer — correct credential | `5261` |
| Real PPM consumer — wrong credential | `5262` |
| Real PPM consumer — disabled provider | `5263` |
| Real PPM consumer — unreachable provider | `5264` |

The observed HTTP matrix was:

| Scenario | Platform result | PPM result |
|---|---:|---:|
| Active PPM entitlement + exact permission | `200`, `isAllowed=true` | `201` |
| PPM entitlement deny + permission present | `200`, `isAllowed=false` | `403` |
| Active entitlement + permission absent | `200`, `isAllowed=true` | `403` |
| Expired entitlement + stale permission | `200`, `isAllowed=false` | `403` |
| Wrong dedicated credential | `401` | `503` |
| Provider disabled | `503` | `503` |
| Provider unreachable | Not reachable | `503` |
| Missing object after both gates | Not applicable | `404` |
| Cross-tenant object after both gates | Not applicable | `404` |
| Soft-deleted object after both gates | Not applicable | `404` |

The allow path created a real Portfolio and then soft-deleted it; the persisted
record remained with `IsDeleted=true` and version `2`. Its create and
soft-delete mutations produced two transaction-local audit intents. The
entitlement-deny, permission-deny, and expired-entitlement attempts produced
no repository records. Provider authentication/dependency failures also
failed closed before repository mutation.

The caller correlation ID was forwarded by PPM and observed in the real
Platform controller/request logs under distinct runtime traces. Exact-value
scanning of the service logs found **0** matches for the ephemeral dedicated
credential, wrong credential, or JWT signing secret. No secret, JWT, or
temporary credential value is retained in this audit.

After the matrix completed, all six disposable API processes were stopped,
the replica-set process was stopped, all three disposable databases were
dropped, and the temporary script, logs, and response artifacts were removed.
The existing `5000`, `5001`, `5056`, `5057`, and `27017` processes were
observed before and after the run and were not restarted, killed, or modified.

This evidence does not claim production deployment/configuration or merged
branch state. The governed Gateway normal route, normal-port final integration,
Phase 2B, and ExternalContextReference remain open. The subsequently evidenced
PPM audit delivery chain is recorded separately in §9. MOD-0117 is not `done`.

## 9. Live PPM audit delivery evidence — 2026-07-31

### 9.1 Verified checkpoint ancestry

The evidence was produced from a PPM branch containing these verified
checkpoints:

- `b27cebf7e51d2ee55c4277b932e9fc6c302affa3` —
  `feat(ppm): publish transactional audit intents`;
- `b8a5829e7f676dad1a2187fd29c49a1846b37460` —
  `fix(ppm): register shared event outbox store`;
- `c06205543f5c01f1aeebd8674104e1bd46f00340` —
  `fix(ppm): serialize BSON configuration initialization`.

The PSS-owned routing regression is preserved independently at
`095b1a1016fb6b70d7ede70948721718445130d7` —
`test(eventing): lock PPM audit routing semantics`. The shared ancestry also
contains PSS-B2A secure consumer `121fa2bf`, PSS-C1 signed canonical outbox
`c8768efa`, PSS-C2 shared delivery seam `70eee58e`, and PSS-C3 kebab-case
event-name grammar `64a92561`. These PPM-branch ancestry identifiers are kept
distinct from their PSS-branch counterparts. All are repository evidence, not
production deployment authority.

Ownership remains partitioned: PPM owns the business mutation, transactional
`ppm_audit_intents`, dispatcher, and `ppm_event_outbox`; BuildingBlocks/PSS owns
the signed canonical transport envelope and shared outbox seam; Platform and
MOD-0021 own the PPM consumer, `ppm_audit_inbox`, and MOD-0021 `audit_outbox`.
The evidence does not transfer ownership between these boundaries.

### 9.2 Live entitlement and mutation precondition

The isolated run used real Platform and PPM processes; no mock or stub was
used. The Platform decision returned HTTP `200`, `moduleCode=PPM`, and
`isAllowed=true`. The authenticated JWT carried a canonical non-empty `Guid`
`tenant_id`, a canonical non-empty `Guid` `sub`, and the exact lowercase
`ppm.portfolios.create` permission. The real PPM mutation returned HTTP `201`.
The entitlement-deny, permission-deny, and expired-entitlement plus stale-JWT
paths remained consistent with §8: each failed closed as `403` before a
repository side effect.

### 9.3 Live end-to-end audit chain

The observed real chain was:

`PPM API mutation → business entity → transactional ppm_audit_intents → PPM dispatcher → ppm_event_outbox → shared canonical RabbitMQ transport in the isolated disposable topology → Platform PPM consumer → ppm_audit_inbox → MOD-0021 audit_outbox`

The persisted result counts were exactly:

| Evidence | Result |
|---|---:|
| Business entity | `1` |
| PPM audit intent | `1` |
| PPM event outbox | `1` |
| PPM outbox status | `Published` |
| Platform PPM inbox | `1` |
| MOD-0021 audit outbox | `1` |

The shared transport type was
`Diten.BuildingBlocks.Eventing.EventTransportMessage`. The accepted envelope
used `EventName=ppm.audit-intent.submitted.v1`, `EventVersion=1`, and
`Producer=Diten.PpmService`. `EventId`, `TenantId`, `ActorId`, `EntityId`, and
`CorrelationId` were preserved across the chain. Re-delivery of the same event
did not create another record. The same `EventId` with a different re-signed
payload produced a conflict, and the accepted record remained unchanged.

### 9.4 Security and routing evidence

- an unrelated event and a future-v2 event identity were safe v1-consumer
  no-ops;
- exact v1 name with a wrong version field was rejected by the shared transport
  contract;
- exact v1 identity with the wrong producer caused terminal contract failure;
- malformed or noncanonical payload caused contract failure;
- a wrong HMAC signature or uppercase hexadecimal signature, unknown `KeyId`,
  and a partial signature tuple each caused terminal security failure;
- after every negative case, `ppm_audit_inbox=1` and `audit_outbox=1` remained
  unchanged;
- exact-value secret scanning of the captured service-log and evidence-artifact
  set returned `0` matches.

The producer validates signature metadata structure and preserves a valid
persisted signature for idempotent retry/key rotation. Cryptographic HMAC
verification and unknown-key rejection remain the Platform consumer's
fail-closed responsibility. No credential, JWT, signing key, or temporary
secret value is recorded here.

### 9.5 BSON initialization closure

The former completion flag was published before process-wide BSON serializer
and convention initialization finished. The observed pre-fix gap was **28/29**.
The fix serializes initialization with a process-wide lock and publishes
completion only after successful Guid, UTC DateTime, and convention setup.

The fixed full integration project passed **5/5 fresh processes × 29/29**. A
temporary early-publication mutation was detected in **1 of 5** runs, where
**9 tests failed**; the nondeterminism is part of the race evidence. The
mutation was removed byte-for-byte, after which the restored implementation
again passed **5/5 × 29/29**. `GuidRepresentation.Standard` and scalar UTC
`DateTime` remain unchanged, and an incompatible serializer still fails
closed.

### 9.6 Evidence boundaries still open

The isolated live delivery is proven, but none of the following is promoted to
PASS or CLOSED:

- the independent broker metric proving legacy-URN publishes equal `0`;
- repository-default production-facing activation, which remains
  `Enabled=false` and `WorkerEnabled=false`; the disposable live run used only
  environment-scoped activation;
- governed normal-port `5000/5001` deployment evidence;
- production secret/configuration provisioning;
- operational replay and external pager/alarm integration;
- the generic entitled-modules surface's omission of `EnabledByOverride`,
  which is a separate PSS semantic observation and grants no PPM runtime
  authority.

## 10. Remaining blockers

| Blocker | Current result |
|---|---|
| AuthService PPM grant provisioning | **IMPLEMENTED CHECKPOINTS; PRODUCTION DEPLOYMENT/CONFIG OPEN.** PSS-A exact explicit-grant behavior (`23d56efb`), PSS-B1 provider (`90eec003`), shared evaluator (`5d183069`), and PPM consumer (`11aa3766`) exist. The original tenant-admin token still evidenced zero PPM permissions; production merge, configuration, and authorized tenant grant administration are not claimed. |
| Gateway `/api/v1/ppm/**` route | **ISOLATED EVIDENCE ONLY; NORMAL ROUTE OPEN.** The earlier targeted temporary route test passed, but governed Gateway `5000` normal-route and final normal-port integration evidence remain open. |
| Mongo replica-set runtime proof | **EVIDENCED FOR PHASE 2A.** Isolated integration tests pass 9/9 and the real CRUD chain reached Mongo replica set `27018`. |
| MOD-0021 semantic audit contract and delivery | **ISOLATED LIVE DELIVERY EVIDENCED; PRODUCTION ACTIVATION/OPERATIONS OPEN.** The producer, shared canonical transport in the disposable live topology, idempotent consumer, inbox, and MOD-0021 outbox chain are proven in §9. Production activation/configuration, legacy-URN-zero observation, operational replay, and external pager/alarm remain open. |
| DataTable verifier | **PARTIAL.** Each resource surface reports 60 PASS and 4 known policy/profile false positives. |
| End-to-end UI/API acceptance | **EVIDENCED WITH AUTH CAVEAT.** Real browser CRUD passed through the full temporary chain with zero console errors/warnings, using an ephemeral signed JWT bootstrap rather than a real AuthService PPM grant. |
| ExternalContextReference provider and DWS integration | **EXCLUDED.** Requires a later explicit implementation authorization. |
| Phase 2B and MOD-0136/MOD-0138/MOD-0072 runtime links | **EXCLUDED.** No implementation authority in this slice. |
| WorkCenter projection or protected task/approval behavior | **EXCLUDED.** Any protected hazard requires Gate 2 before production changes. |

## 11. Closure statement

Phase 2A now has automated, isolated replica-set, real browser-to-database, and
isolated audit-delivery evidence consistent with its existing `review` status,
but not `done`: production PSS/Auth/PPM deployment and
configuration, production audit activation, governed Gateway normal-route
evidence, final normal-port integration, and the operational/legacy observation
gates in §9.6 remain open. Isolated real PPM-to-MOD-0021 delivery is evidenced;
that evidence does not authorize production activation.
The DataTable verifier retains four known policy/profile false positives per
surface. Phase 2B, the ExternalContextReference provider, DWS runtime
integration, and WorkCenter scope remain outside this review.
