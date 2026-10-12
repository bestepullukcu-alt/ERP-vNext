# MVP6-MOD0190-SPEC-01 — SOP §22 handoff (proposal only)

**Verdict: DRAFT / HELD.** Existing MOD-0190 design is retained. This package proposes a narrow pack clarification, not a new design or runtime authority. Repository branch `feature/mvp6-logistics`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; initial dirty inventory 164 rows. Pack SHA256 `637690f32c1fa03e3039a4542bd53f7bb9b12f6b18d856a7d0e1f23d740c6877`. Frozen `SANDOP-CAPACITY` v1 (`info.version 1.0.0`, `contractVersion v1`) SHA256 `c255e92923ba91714cec8daf229262101b5d45648b7f714a03ab402811db683c`. Frozen DEMAND v1 SHA256 `3c77262e411976e311bcf9b65be131e2035fd18a33215d24075f87209f87bb9d`. DCP-002 fresh result: `OK MOD-0190` against Blueprint/registry. No current `Features/SandopPlans` source/test path exists.

## Frozen operation/acceptance crosswalk

| Operation (SANDOP-CAPACITY path) | Frozen success / declared failure | Required bounded observable |
|---|---|---|
| `createSandopPlan` POST `/sandop-plans` | 201; 409 `SANDOP_PLAN_ALREADY_EXISTS`; 422 `INVALID_DEMAND_REFERENCE` | Tenant/LE plan, ID/version provenance, unique active horizon+demand version, one receipt/audit/event; no DEMAND series copy. |
| `getSandopPlan` GET `/sandop-plans/{sandopPlanId}` | 200; 404 `UNKNOWN_SANDOP_PLAN` | Scoped detail and `SandopPlan` required/null fields. |
| `captureSandopSnapshot` POST `/sandop-plans/{sandopPlanId}/snapshots` | 201; 404, 409 `SANDOP_PLAN_STATE_CONFLICT`, 422 `INVALID_DEMAND_REFERENCE` | Immutable input-reference provenance, source checksum/time, append-only snapshot, one event. |
| `listSandopSnapshots` GET same `/snapshots` | 200; 404 | Scoped ordered immutable snapshot list; no foreign data leakage. |
| `recordSandopSignOff` POST `/sandop-plans/{sandopPlanId}/sign-offs` | 201; 404, 409 `SIGN_OFF_ALREADY_RECORDED`, 422 `INVALID_SNAPSHOT_REFERENCE` | Role/decision against same-plan snapshot; one decision per scope/snapshot/role; immutable actor/time and one event. |
| `listSandopSignOffs` GET same `/sign-offs` | 200; 404 | Scoped decision history; never overwrite prior evidence. |

Schema pointers: `CreateSandopPlanRequest`, `SandopPlan`, `CaptureSandopSnapshotRequest`, `InputSnapshotProvenance`, `SandopSnapshot`, `RecordSignOffRequest`, `SignOff`, `Error`; `SandopPlanCreatedEvent`, `SandopSnapshotCapturedEvent`, `SandopSignOffRecordedEvent`, `LifecycleEventEnvelope` (`sandop-capacity.openapi.yaml` lines 604–802, 1016–1086, 1155–1280). Header schemas: `Idempotency-Key` required on three mutations, minimum length 1 only; `X-Correlation-Id` UUID required on all six. Success/error examples echo correlation; bearerAuth applies. Frozen plan status enum is Draft/InReview/Approved/Rejected/Archived, sign-off decision Approved/Rejected. **No transition operation or transition matrix is published.** The three event types reflect create/snapshot/sign-off, not a separately authorized plan-state event.

## One owner decision set (all proposals; none approved by this report)

| ID | Exact proposed choice and rationale | Alternative/contract effect | Acceptance oracle |
|---|---|---|---|
| D190-01 DEMAND | **Propose bounded isolated reference slice:** fixture map keyed by submitted opaque `demandPlanId + demandPlanVersion`, explicitly labeled test-only; accept Published fixture, reject unknown/unpublished fixture with declared 422. Store only ID/version and supplied provenance. No live 0188 version validation claim. | If live authoritative ID+version verification is required, DEMAND owner must publish a versioned lookup/read response including immutable version (and checksum/time if authoritative provenance is required), then consumer consent/uptake. DEMAND v1 only has GET `/plan?itemId&period`, response `planId/status`, no version or checksum. Do not invent `/plans/{id}/{version}`. | Fixture Published create 201; unknown/unpublished 422 with zero four-way write set. Same fixture can be switched off after capture and replay must not reread. A real DEMAND HTTP response without version may **not** be called validated ID/version. |
| D190-02 Workflow/actor | **Propose isolated trusted JWT actor + local sign-off rule only**, using the frozen role/decision enums and four pack permissions; no Workflow HTTP call or approval-template claim until a published consumer seam is bound. | Live Workflow (MOD-0023 pack is ready-for-dev, not a frozen 0190 consumed HTTP contract) needs producer-owned published operation/auth/scope/version and separate integration consent. | Missing `sign-off.record` grant 403; foreign scope 404; actor identity comes from trusted claim, never body; zero Workflow calls in isolated slice. |
| D190-03 Event Bus | **Propose atomic local Pending outbox evidence** for the three frozen lifecycle event payloads, with publisher/worker absent from this slice. | Pack currently says “publish”; narrowing to Pending-only requires explicit owner scope approval. Live bus requires platform-owned adapter/transport binding; MOD-0035 pack is `partial`, not a published 0190 delivery contract. | For each command, aggregate/snapshot/sign-off + receipt + audit + Pending outbox commit or all roll back; replay creates none. No worker/transport registration or delivery claim. |
| D190-04 lifecycle/sign-off | **Propose preserving only contract-visible operations and status enum** while holding automatic final-role status transition. A sign-off records an immutable decision; no invented status event. | If final-role decision must move InReview→Approved/Rejected, owner must define required role set, mixed approvals/rejections, snapshot replacement, who can sign each state, concurrency precedence, and whether an extra event is needed. Any new wire event/status/409 rule requires versioned SANDOP-CAPACITY amendment. | All five role values × two decisions accepted only where exact published/approved state policy permits; duplicate same snapshot/role 409 with original evidence unchanged; concurrent sign-offs produce one unique decision. No implicit Approved status in unresolved cases. |
| D190-05 replay/precision | **Propose same scope+operation+target/key+same payload original-result replay with no new write**; changed payload and different correlation behavior must be specified before runtime dispatch. Use request UUID as trace, never substitute for immutable provenance. | Frozen contract requires the key but does not define receipt tuple, fingerprint, TTL, changed-payload code, original/current correlation or replay headers. Publishing `IDEMPOTENCY_KEY_REUSED` 409 or narrower 1..200/trim for key/name needs versioned contract-owner amendment/consumer impact. Preserve current valid inputs until then. | 20 concurrent same-key attempts → one write set; postcommit lost response → original receipt recovery. Changed-payload and different-correlation cases remain HELD until owner+contract disposition; do not infer Claims/Returns semantics. |

**Additional frozen-policy mismatch:** pack §12 says key/name `Trimmed, 1..200`; schema only specifies key minLength 1 and name minLength 1, without maxLength or trimming. Proposed patch marks stricter pack rule pending, preventing silent rejection of currently valid contract input. Pack §19 automatic final-role transition is expressly an ASSUMPTION, not a frozen rule.

## Consumed-seam GAPs and Phase 1.5 proposal

| Gate | Current evidence | Required closure before DEV |
|---|---|---|
| Shipment sequence | 0183/0184 bounded CT accepted, 0185 bounded WP CT accepted; 0186 R01 CT closure is not whole-WP acceptance; 0187 normal VER is not CT acceptance (see `mvp6-next-wave-readiness-01/READINESS.md`). | CT records exact bounded 0186/0187 sequence disposition. Full E5/G5 is separate. |
| DEMAND | `demand.openapi.yaml` GET `/plan` takes `itemId`, optional `period`; no ID+version lookup. | Approve D190-01 bounded fixture or request producer-owned versioned amendment. No live-lookup claim under fixture choice. |
| Workflow/Event Bus | MOD-0023 planning pack and MOD-0035 partial pack exist; no frozen 0190-consumed HTTP/transport contract identified in `docs/analysis/contracts/`. | Approve D190-02/03 bounded absence or bind actual published seam, auth/scope/error and delivery semantics. |
| Contract parity | Six operations/three event schemas frozen, but replay tuple/fingerprint, exact status transitions, key/name max rules are not. | D190-04/05 and any necessary versioned SANDOP amendment before wire-visible tests/DEV. |
| Repository architecture | Five-layer SupplyChain service, four pipeline behaviors and feature-local patterns exist; `SandopPlans` absent. | Phase 1.5: map each schema to entity/wire/base fields; indexes for tenant/LE/horizon/version and snapshot/role; atomic Mongo aggregate/receipt/audit/outbox; JWT/permissions/tenant/LE/soft-delete; required/null/error/header/replay matrix; DB-010 and source→binary→process evidence. UI/lookup N/A (`shell:none`). Check current dirty inputs and exact allowed paths. |
| Shared composition | `Program.cs`, shared permission definition/seed, gateway and any common DI/project file are outside feature ownership. | Single integration owner authorizes exact files/diff later; isolated feature core may be separate only after pack/runtime authority. |

## Prospective owned-path allowlist (not runtime authorization)

- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/SandopPlans/**` (controller/contract adapter only).
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/**` (six commands/queries, handlers, validators, models, interfaces).
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/SandopPlans/**` (plan/snapshot/sign-off policy and immutable evidence types).
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/SandopPlans/**` (scoped repository, unique indexes, receipts/audit/Pending outbox).
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/SandopPlans/**` (bounded DEMAND fixture adapter and trusted actor integration only if approved).
- `services/Diten.SupplyChainService/tests/**/SandopPlans/**` (contract, domain, concurrency, Mongo, HTTP/JWT tests).

These prefixes are disjoint from `Features/CapacityPlans/**` and all logistics feature roots. Shared `Program.cs`, `.csproj`, permission registry, gateway, canonical YAML, Workflow/Event Bus service and MOD-0192 paths are protected. Exact individual new file names and count require approved decisions and Phase 1.5, not an invented file-count target.

## Acceptance → test map, conditional on owner decisions

| AC | Test/evidence |
|---|---|
| Six operation parity and wire v1 | Schema/ref/example tests for all six statuses and required/null fields; no wrapper substitution; authenticated HTTP once composition authorized. |
| Scope and grants | JWT 401/403; tenant/LE/soft-delete 404; create/snapshot/sign-off permissions independently denied; no write on denial. |
| DEMAND provenance | D190-01 fixture Published/unknown/unpublished; no forecast series stored; absence of live version assertion. Amendment branch requires live producer test after publication. |
| Snapshot/sign-off | Immutable capture, same-plan snapshot 422, duplicate role 409, concurrent decisions unique, audit actor/time retained. Status transition test waits D190-04. |
| Replay/atomicity | Same-key race, restart/response-loss recovery, four collection rollback and Pending-only event tests if D190-03/05 approved. Changed payload/correlation oracle waits exact contract-owner decision. |
| Event boundary | Three frozen event types/payloads, UUID correlation, one event per committed command, no worker in bounded option; live publish tested only in separate integration scope. |

`proposed-pack.patch` is a unified diff against the pinned **draft** pack; it only clarifies unapproved assumptions/GAPs and keeps status draft. It has not been applied. DEV/VER drafts remain HELD in this directory. No source, pack, contract, registry, shared composition or Git state changed; no tests were run. Parallel dirty-worktree changes prevent a whole-tree no-change claim; pinned pack and contract hashes were checked for this handoff.
