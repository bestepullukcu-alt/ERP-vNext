---
id: MOD-0190
name: S&OP Workflow & Sign-offs
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: tenant
golden_reference: slim
entity_base: EntityBase
status: ready-for-dev
status_note: "Owner-promoted isolated pack 6a57769c… (2026-09-22) bound to CT-accepted bounded work package MVP6-MOD0190-401-DISPOSITION-CT-02 (§22). Common-checkout integration, gateway/shared registration, live DEMAND/Workflow/Event Bus, UI, E5/G5 and rollout remain open."
owner: supply-chain-execution / control-tower
branch: feature/mvp6-logistics
started: 2026-09-15
target: 2026-09-30
form_field_count: 5
---

# MOD-0190 — S&OP Workflow & Sign-offs

> **Identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0190 --name "S&OP Workflow & Sign-offs"`
> returned `OK` on 2026-09-15. Blueprint 8.1 and the module ID registry prove the canonical identity.
>
> **Execution gate:** the 2026-09-22 owner approved this exact bounded 38-path backend core after published-contract and Phase 1.5 verification. It grants isolated core DEV and later independent VER only. Gateway, permission-registry, shared registration, Program.cs, live producer, UI and rollout remain separately owned. The bounded MOD-0183..0187 sequence-evidence prerequisite is met; full E5/G5 is not.
>
> **ASSUMPTION:** UI journeys and form fields are not sufficiently defined, so the initial slice uses `shell: none` and
> `golden_reference: none`. Any UI requires an approved revision or follow-up pack.

## 1. Module Summary

MOD-0190 owns the governed S&OP plan, immutable review-input snapshot references and role-based sign-off decisions.
It lets planning, supply, finance, operations and executive actors approve or reject a known snapshot while preserving
an auditable correlation chain. It consumes frozen DEMAND v1 by reference and never becomes another demand source.

The planned initial slice is backend/contract only. It may be dispatched after the MVP-6 shipment lane is verified,
in parallel with MOD-0192, subject to this pack reaching `ready-for-dev`.

## 2. Ownership and Boundaries

**Owns:** `S&OPPlan`, immutable `S&OPSnapshot` provenance, `SignOff`, plan/sign-off lifecycle, idempotency records,
audit evidence and MOD-0190 lifecycle event payloads.

**Consumes in this bounded slice:** frozen DEMAND v1 via exact tenant/LE-scoped test fixtures and trusted JWT actor. No Workflow HTTP or Event Bus delivery; lifecycle events remain atomic Pending outbox records.

**Must not:** copy forecast series or demand quantities into a second SoR; edit MOD-0188 data; own capacity scenarios,
shipment, warehouse, inventory, supplier or gateway truth; accept tenant/legal-entity scope in request bodies.

## 3. Owned Objects

| Object | Purpose |
|---|---|
| `SandopPlan` | Planning horizon, frozen DEMAND reference and current review state |
| `SandopSnapshot` | Immutable input-reference provenance for a review cycle |
| `SignOff` | One role decision against one immutable snapshot |
| Commands | Create plan, capture snapshot, record sign-off |
| Queries | Get plan, list snapshots, list sign-offs |
| API | Six `/api/supply-chain/sandop-plans/**` operations from published SANDOP-CAPACITY 3.0.0, wire `v1` (identical to 2.0.0 for these six) |
| Events | Plan created, snapshot captured and sign-off recorded lifecycle events |
| Permissions | `supplychain.sandop-plans.read`, `.create`, `.snapshot.capture`, `.sign-off.record` |

## 4. Entity Fields

### SandopPlan

| Field | Rule |
|---|---|
| `Id` | Server-minted UUID |
| `TenantId` / `LegalEntityId` | Required, server-resolved, never request body |
| `Name` | Schema-valid nonempty string, exact value; no trim or added max-200 rule |
| `HorizonStart` / `HorizonEnd` | Date; end is not before start |
| `DemandPlanId` / `DemandPlanVersion` | Required opaque frozen-DEMAND reference |
| `Status` | `Draft`, `InReview`, `Approved`, `Rejected`, `Archived` |
| `CurrentSnapshotId` | Optional UUID; must belong to this plan |
| `Version` | Infrastructure optimistic-concurrency token |
| Soft-delete/audit | Repo-standard `IsDeleted`, `DeletedAt` and audit fields |

### SandopSnapshot and SignOff

| Field | Rule |
|---|---|
| Snapshot provenance | DEMAND plan ID/version, source contract/version, captured time and checksum |
| Supply input references | Opaque external references only; no foreign SoR payload copy |
| Sign-off role | DemandPlanning, SupplyPlanning, Finance, Operations or Executive |
| Decision | Approved or Rejected |
| DecidedBy / DecidedAt | Actor server-resolved; UTC timestamp |
| CorrelationId | Required UUID propagated from command to event/audit |

Tenant-first indexes: unique plan horizon+demand version among active records; unique snapshot sequence per plan;
unique active sign-off by `(TenantId, LegalEntityId, SnapshotId, Role)`.

## 5. Repo Scope

After a later `ready-for-dev` promotion, the bounded initial slice may write only:

- `services/Diten.SupplyChainService/src/**/Features/SandopPlans/**`.
- `services/Diten.SupplyChainService/tests/**/SandopPlans/**`.
- Module-local generated API reference and immutable verification evidence under canonical docs paths.
- This module pack's implementation notes/status when reality changes.

The 38 exact prospective runtime/test paths are pinned by `docs/roadmap/plans/mvp6-mod0190-0192-dispatch-preflight-01/MOD-0190-OWNED.tsv`. `Program.cs`, common DI, shared permission registry and gateway belong to integration owners.

`docs/analysis/contracts/sandop-capacity.openapi.yaml` is frozen implementation authority and is read-only during
runtime work. Contract changes require a separate versioned, single-writer specification task.

## 6. Protected Paths

- `.antigravity/**`.
- `docs/analysis/contracts/demand.openapi.yaml` and every other module-owned frozen contract.
- `docs/analysis/contracts/sandop-capacity.openapi.yaml`, `docs/analysis/contracts/sandop-capacity-semantics-v3.0.0.md` and `docs/analysis/contracts/sandop-capacity-semantics-v2.0.0.md` during runtime implementation.
- `gateway/Diten.ApiGateway/**/ocelot.json`; integration-agent only.
- Frontend Archive paths and `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- Domain-external services and every non-MOD-0190 feature path.
- MOD-0183..0187, MOD-0192 and MOD-0147/0148 runtime paths.

## 7. Dependencies

| Dependency | State | Rule |
|---|---|---|
| `SANDOP-CAPACITY` 3.0.0 / wire `v1` | PUBLISHED / FROZEN | YAML SHA-256 `5213b5353267ad4c25d150975d6f38422bced72f678c02271b0cccf6e768adab`; annex SHA-256 `f9d9d5553d70e1c54af3e03924600f4c8f426de50355ae861a4d3d5cef21ee64`; read-only during DEV. The six S&OP operations are identical to 2.0.0 (YAML `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`, annex `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`; `docs/roadmap/plans/mvp6-text-patch-q83-01/COMPARE-0190-PIN.md`); §21/§22 acceptance evidence stays bound to 2.0.0 |
| `DEMAND` v1 | FROZEN / TEST FIXTURE ONLY | Exact tenant/LE/ID/version/checksum fixture; no invented endpoint or real producer validation claim |
| Shipment lane MOD-0183..0187 | BOUNDED SEQUENCE EVIDENCED | CT sequence disposition met; not full E5/G5 or shared-checkout uptake |
| Workflow / actor identity | JWT ACTOR ONLY | Trusted JWT actor; no Workflow HTTP/template integration in this slice |
| Event Bus | PENDING OUTBOX ONLY | Three frozen lifecycle events atomically persisted Pending; no publisher/worker/delivery claim |
| MOD-0192 | Parallel peer | Disjoint 38/43 paths; no shared persistence or internal types |

## 8. Runtime Constraints

- Planned service is `Diten.SupplyChainService` on port 5061, using five layers and CQRS/MediatR.
- MongoDB data is tenant/legal-entity scoped; cross-scope access returns 404 without existence leakage.
- Mutations require a nonempty exact parsed `Idempotency-Key`; all six operations require one valid UUID `X-Correlation-Id`. No trim, case folding, Unicode normalization or added max-200 rule.
- Snapshots and sign-offs are append-only evidence. Prior decisions are not overwritten.
- Each mutation atomically writes state, durable receipt, audit and one Pending lifecycle event or exposes an explicit failure. Same-key/same-valid-payload replay returns original status/body without reread or new writes; current response correlation differs from original audit/event correlation when retried. Changed valid payload returns 409 `IDEMPOTENCY_KEY_REUSED`; unresolved unknown commit follows published 503 `COMMIT_RESULT_UNRESOLVED` and scoped receipt recovery.
- DEMAND ID/version/checksum is checked only against exact scoped test fixtures; no live DEMAND HTTP, Workflow HTTP, direct database/internal-type sharing or publisher is in this slice.
- This ready-for-dev pack authorizes only the exact isolated 38-path core DEV and subsequent independent VER granted by the 2026-09-22 owner decision; composed HTTP requires separate shared integration authority.

## 9. Layout & Shell Contract

`shell: none`. The initial slice has no Razor UI, menu, DataTable, localization surface or frontend route.

## 10. Backend File Convention

The eventual slice uses `Features/SandopPlans/Commands`, `Queries`, `Handlers/CommandHandlers`,
`Handlers/QueryHandlers`, `Validators` and `SandopPlanModels.cs`. Each public command/query/handler/validator has its
own file. Handler and validator names do not carry Command/Query suffixes. Runtime work must use the service's four
pipeline behaviors, base controller and explicit tenant/legal-entity repository predicates.

## 11. Frontend File Contract

Not applicable. A UI requires a revised/follow-up pack defining tenant shell, exact form-field count, localization,
authorization surface and Slim/Compact golden reference.

## 12. Validation Rules

| Field/Header | Required | Rule | Pre-check |
|---|---|---|---|
| `Idempotency-Key` | Mutations | Nonempty exact parsed value; no trim or max-200 rule | Scoped operation+target receipt and decoded-JSON fingerprint |
| `X-Correlation-Id` | All six operations | Single valid UUID; missing/malformed/duplicate → 400 `INVALID_CORRELATION_ID` | Current response trace; original audit/event trace on replay |
| `Name` | Create | Schema-valid nonempty exact string; no trim/max-200 rule | — |
| Horizon | Create | Valid dates; end >= start | — |
| Demand reference | Create/snapshot | Nonempty ID/version, checksum/time where published | Exact scoped test fixture; DEMAND v1 has no ID+version endpoint |
| Source checksum/time | Snapshot | Non-empty checksum, UTC time | DEMAND version must match plan |
| Snapshot ID | Sign-off | UUID | Exists in same tenant/legal entity and plan |
| Role/decision | Sign-off | Frozen enum values | No prior decision for same role/snapshot |

## 13. Failure Path to Verify

- Unknown/unpublished/cross-scope or checksum-mismatched exact DEMAND fixture returns 422 `INVALID_DEMAND_REFERENCE` with zero writes; fixture evidence is not live producer validation.
- Cross-tenant or cross-legal-entity plan returns 404 without existence leakage.
- Duplicate plan horizon+demand version returns 409 and no duplicate aggregate.
- Capture Draft/InReview appends an immutable snapshot and sets InReview/currentSnapshotId; Approved/Rejected/Archived returns 409 `SANDOP_PLAN_STATE_CONFLICT`.
- Sign-off outside InReview returns 409 `SANDOP_SIGN_OFF_STATE_CONFLICT`; wrong-plan snapshot returns 422 `INVALID_SNAPSHOT_REFERENCE`.
- Repeated role sign-off returns 409 `SIGN_OFF_ALREADY_RECORDED` without overwriting evidence.
- Same scoped key and equivalent valid payload replays original status/body with current response correlation and original event/audit correlation, no reread or second event; changed valid payload returns 409 `IDEMPOTENCY_KEY_REUSED`.
- Missing/malformed/duplicate correlation returns 400 `INVALID_CORRELATION_ID` with one rejection-only UUID in error body/header and no write. Known dependency failure is 503 `DEPENDENCY_UNAVAILABLE`; unresolved mutation commit is 503 `COMMIT_RESULT_UNRESOLVED` after scoped receipt resolution.

## 14. Authorization Convention

Tenant actor/service actor with default-deny JWT and permission checks:

- `supplychain.sandop-plans.read`
- `supplychain.sandop-plans.create`
- `supplychain.sandop-plans.snapshot.capture`
- `supplychain.sandop-plans.sign-off.record`

Shared permission definition/seed remains the platform owner's seam. A future runtime WP may consume approved keys
but cannot edit the shared permission registry without a separately authorized owner/integration task.

## 15. Gateway / API Routing Decision

Desired public family is `/api/supply-chain/sandop-plans/**` routed to port 5061. This pack does not authorize an
`ocelot.json` change. An integration-agent WP must add/verify routes and propagation of authorization, tenant,
legal-entity, correlation and idempotency headers after endpoints exist.

## 16. Acceptance Criteria

- [ ] Create stores one scoped Draft plan with null currentSnapshotId referencing an exact scoped Published DEMAND ID/version fixture; this does not prove live producer validation.
- [ ] Draft/InReview capture appends immutable fixture ID/version/checksum/time provenance, updates plan to InReview and preserves older snapshots; no DEMAND series is stored.
- [ ] InReview sign-off binds to any same-plan immutable snapshot and role; duplicate role cannot overwrite the first and five approvals do not automatically advance plan status.
- [ ] Three mutations use scoped durable receipt, exact-key/decoded-JSON fingerprint, original-result replay and atomic audit plus at most one Pending event; no publisher or Workflow call.
- [ ] Current UUID is in response header/error; original UUID remains in audit/event on replay. Missing/malformed/duplicate UUID and application-generated 401 follow published fallback.
- [ ] Cross-scope access returns 404; missing permission returns 403.
- [ ] Six operations and three event payloads match published SANDOP-CAPACITY 3.0.0 YAML/annex, wire `contractVersion: v1`, including 400/401/409/422/503 precedence and required/null fields.
- [ ] MOD-0190 shares no persistence/internal DTOs with MOD-0192 and runs against frozen mocks.
- [ ] Bounded E4 evidence links decisions to immutable fixture provenance without overriding source SoRs; live producer uptake and E5/G5 are separate gates.

## 17. Test Expectations

- DCP-002 identity verifier and OpenAPI syntax/reference validation PASS.
- Domain tests cover plan lifecycle, snapshot immutability and sign-off uniqueness.
- Handler/API tests cover validation, RBAC, tenant/legal-entity isolation, idempotency and concurrency.
- Contract tests cover every declared success/error shape and lifecycle event example.
- Mongo integration tests use isolated DB-010 naming and tenant-first indexes.
- Prism-compatible mock smoke uses frozen `DEMAND` and `SANDOP-CAPACITY` contracts.
- Relevant service and repo architecture gates pass; G5 remains open until E5 integration evidence exists.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name passed DCP-002 on 2026-09-15.
- [x] DCP-009, domain boundary, MVP-6 brief and frozen contracts were read.
- [x] Backend/contract initial slice, shell and golden-reference decisions are explicit.
- [x] Owned objects, paths, validation, failures and acceptance are specified.
- [x] Published SANDOP-CAPACITY 2.0.0 YAML/annex and MOD-0190 consumer repin hashes verified; re-pinned to 3.0.0 after the six operations were proven identical (`docs/roadmap/plans/mvp6-text-patch-q83-01/COMPARE-0190-PIN.md`); DEMAND v1 remains fixture-only here.
- [x] Fixture-only DEMAND, exact key/name, replay and validation boundaries map to the published annex; no uncontracted endpoint or narrower key/name rule.
- [x] Bounded MOD-0183..0187 sequence evidence is recorded by Returns/Claims CT; not full E5/G5.
- [x] Owner approved trusted JWT actor/no Workflow and atomic Pending-only outbox/no publisher for this bounded slice.
- [x] The 38 prospective SandopPlans paths are absent and disjoint from MOD-0192's 43; shared composition is integration-owned. Recheck hashes at dispatch.
- [x] User/owner approved exact draft target SHA-256 `04f2e36f89cae0a21300217b63756b0cd3104c33af145d985b9fc101a83b38a2`, Phase 1.5 and isolated 38-path core DEV/VER on 2026-09-22.

Status is `ready-for-dev` for the exact isolated core scope. Shared composition and broader integration remain separately gated.

## 19. Implementation Notes

- **ASSUMPTION:** runtime feature root will be `SandopPlans`, derived from the frozen route and owned aggregate.
- **Published bounded policy:** sign-off stores an immutable decision while the plan stays InReview; no automatic approval/quorum/status event. Any future approval transition needs separate owner/contract authority.
- **ASSUMPTION:** `demandPlanVersion` is an opaque string because DEMAND v1 does not publish a stronger version type.
- MOD-0190 and MOD-0192 are parallel peers only after the shipment lane gate; neither may write the other's data.

## 20. Follow-up Items

- Retain bounded MOD-0183..0187 sequence CT evidence; do not call it full E5/G5 or shared-checkout integration.
- Keep Workflow and Event Bus delivery outside this bounded slice; later live integration needs separate producer/owner binding.
- Create an integration-agent WP for gateway and shared permission/registration changes.
- Define a separate tenant UI follow-up if product journeys require one.
- Complete G5 E5 integration across S&OP, capacity, logistics and source reconciliation.

## 21. Published-contract reconciliation and approved bounded core

The owner-approved pack binds the six operations to published YAML `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3` and annex `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`. D190-01…03 bounded fixture/JWT/Pending scope and D190-04/05 candidate design were later followed by exact consumer consent/publication. The separate 2026-09-22 owner decision approved draft target SHA-256 `04f2e36f89cae0a21300217b63756b0cd3104c33af145d985b9fc101a83b38a2`, its Phase 1.5 design and isolated core DEV/VER within the 38-path allowlist. The promotion status diff has its own hash; this promoted pack is not the draft target hash. Shared Program.cs composition, live integrations and broader acceptance remain excluded.

## 22. Accepted bounded scope binding

Approved: `docs/records/decisions/2026-09/mvp6-sop-pack-promotion-owner-decision-q27-01.md`

This section records, without changing any business rule above, the Control Tower acceptance that the owner-promoted isolated pack produced. The owner decision prepared in `docs/roadmap/plans/mvp6-pack-alignment-01/mod-0190-sop/PROMOTION-DECISION.md` is recorded in the `Approved:` record above.

| Binding | Exact value |
|---|---|
| Owner-promoted isolated pack (this pack before §22) | SHA-256 `6a57769ced4396d2bc4228749a7e24b0daf36ce279930bb77c5dfdfe19fd0983`, from approved draft target `04f2e36f89cae0a21300217b63756b0cd3104c33af145d985b9fc101a83b38a2`; recorded in `docs/records/audits/2026-09/mvp6-mod0190-mod0192-isolated-dispatch-01/README.md` |
| Controlling CT acceptance | `docs/records/audits/2026-09/mvp6-mod0190-401-disposition-ct-02/SOP-22.md` SHA-256 `91de3760119f95be388332799576c5f474629e7533ee764e5aadf3271fa391cb` — ACCEPTED, approved isolated work package only |
| Row-level acceptance | `SUCCESSOR-ACCEPTANCE-MATRIX.tsv` SHA-256 `e05c0cf9f9d8c81e8c4ebbf7bf63da279ef5625ea7806cd925345266c464bb0b`; evidence index `EVIDENCE-HASHES.tsv` SHA-256 `415c3c101497d849f93945582f3901f1f4b59a143049a00e467fa302b06f8ce9` |
| Accepted source | 379-entry manifest `ae7ef59e0158113c22389ccd3eec84207418f3a859c40480e70b42ee888a9bef`; archive `8fa00d40814a81472ec4221ad909321d058dea18a11f64dd2ac16ea800ecb745` |
| Accepted composition (`Program.cs`, integration-owner surface) | `a2a216be15d07fa656f5fd7da43711aa989c4a8d52cd51c58e8ded48d5d735e5` |
| Contract at acceptance | SANDOP-CAPACITY 2.0.0 YAML `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`; annex `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda` |

**Composition uptake gate (CT-QUEUE Q292, owner-approved 2026-10-03; ruling Q282):** the accepted composition above stands as the target shape, and its hash is kept. **Uptake is blocked** until MOD-0188 exists and a DEMAND v1.1 returns plan ID, version and checksum. Until then this module is not composed, and its `IDemandFixtureReader` is not registered in any composed host, because the only implementation is a declared test-only fixture. Evidence: `docs/records/audits/2026-10/mvp6-q281-demand-seam-01/` (`OPTIONS.md`, `UNRESOLVED.tsv`) and CT-QUEUE Q281, Q282, Q292.
- DEMAND v1 has only `GET /plan` and `GET /forecast`, keyed by item and period, and returns no version and no checksum; this module matches plan ID + version + checksum (`OPTIONS.md` §2 item 2; `docs/analysis/contracts/demand.openapi.yaml:14-55`).
- The producer MOD-0188 is `reserved / planned` and has no pack (`OPTIONS.md` §2 item 1; `execution/registries/module-id-registry.md:284`).
- The only implementation, `Infrastructure/Features/SandopPlans/DemandFixtureReader.cs`, takes `IEnumerable<DemandFixture>`; in a composed host the set is empty and every write is refused while the module looks alive (`OPTIONS.md` §4; CT-QUEUE Q281). The accepted composition feeds it from `Sandop:DemandFixtures` only in the `Testing` environment and an empty set otherwise (`docs/records/audits/2026-09/mvp6-mod0192-http-process-dev-01/PROGRAM-COMPOSITION.patch:21-23`, whose baseline is this `a2a216be…` `Program.cs` per `SOURCE-DELTA.tsv` there).
- This `IDemandFixtureReader` is MOD-0190's own (`Domain/Features/SandopPlans/ISandopRepository.cs:3`, `MatchesAsync`). MOD-0192 has a separate interface with a different signature; they are not one shared seam (F-Q281-1, `OPTIONS.md` §4).

Accepted rows A01–A09, F01–F04A, T01–T02 and R01 are satisfied only within the boundaries stated in that matrix. F04B (application-generated 401) is NOT_APPLICABLE to the accepted pipeline and is not a waiver. T03 (broad same-host 271/276) and T04 (Loads unknown-commit 0/1) remain NON_PASS and are not waived.

**Owned paths:** the 38 paths of `docs/roadmap/plans/mvp6-mod0190-pack-phase15-close-01/OWNED-PATHS.tsv` (SHA-256 `4bfc17f4811073553f1da773748cbc3733cd554671078188d122214ee3947982`). All 38 occur in the accepted 379-entry manifest; none exists in the common checkout yet. `Program.cs` is not a MOD-0190 owned path.

**Contract drift after acceptance (open, not decided here):** canonical SANDOP-CAPACITY is now 3.0.0 (YAML `5213b5353267ad4c25d150975d6f38422bced72f678c02271b0cccf6e768adab`, annex `f9d9d5553d70e1c54af3e03924600f4c8f426de50355ae861a4d3d5cef21ee64`). The publication patch changed metadata, the annex pointer and the Capacity `createCapacityScenario` 409 only; the six S&OP operations were not edited. The MOD-0190 evidence remains bound to 2.0.0 until a CT/owner disposition re-pins it.

**Still open:** common-checkout integration, gateway and shared permission registration, live DEMAND/Workflow/Event Bus delivery, UI, rollout, E5/G5 and full-module acceptance. This section is not `done` status and grants no new DEV scope.

## 23. Tenant UI scope (UI-REVISION-01) — GoldenReferenceSlim

Approved: `docs/records/decisions/2026-09/mvp6-sop-capacity-ui-pack-signoff-owner-decision-01.md`

This section adds the tenant UI scope approved as option A in `docs/records/decisions/2026-09/mvp6-sop-capacity-ui-scope-owner-decision-01.md`
(SHA-256 `1814672b501cda4aa3a65e40b41930c013fcea77545a43e63ffb10b237be1553`), bound to the analysis package
`docs/roadmap/plans/mvp6-ui-scope-190-192-01/` (SHA256SUMS `83c6b7e96331e246cacc9f75890b8282c32fb6bda066b2d504d66e22e0f1e985`;
`UI-SCOPE-0190.md` `2447669e1050d3bcd04817fc8cc956afbcfdba42b252a51f27307b54d7facb82`; Phase 1.5 table `PH15-UI-190.md`
`fb6ad9d8f63381822e0287a0251fb69ddfe7354ff753442c0198a4a8a9945cf8`). It stacks on §22 and changes **no** business, contract,
backend, owned-backend-path or acceptance rule in §§1–22. The frontmatter changes `shell: tenant`, `golden_reference: slim` and
`form_field_count: 5` apply to the UI only. For the UI, §9 ("`shell: none`"), §11 ("Not applicable") and the header ASSUMPTION
on `shell: none` are superseded by this section; for the backend they remain as written. `status` stays `ready-for-dev`
(backend scope, per §§21–22). This section does not by itself authorize UI code (§23.14).

### 23.1 Identity

UI revision inside MOD-0190; no FU, child or new ID. The pack author runs
`python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0190 --name "S&OP Workflow & Sign-offs"` before applying;
non-zero exit stops. The UI adds no owned object, operation, field or permission key.

### 23.2 Layout and shell contract

- `shell: tenant` → the S&OP page views (`Index.cshtml`, `Details.cshtml`) state `Layout = "_LayoutTenantShell";` explicitly; partial views (`_*.cshtml`) set no `Layout`, because an explicit Layout on a partial renders a second shell (Q64b D-02; UI-PM-01); `_ViewStart.cshtml` unchanged.
- View folder `frontend/Diten.Web/Views/SupplyChain/SandopPlans/`; routes under `/SupplyChain/SandopPlans`; no `/Platform` prefix; no `Areas/`.
- Skeleton, empty and error are three distinct states (VIEW-001 §3.1); no spinner or "Loading" text.

### 23.3 Bound operations (nothing else)

Published SANDOP-CAPACITY operations, bound by `operationId`: `createSandopPlan`, `getSandopPlan`, `captureSandopSnapshot`,
`listSandopSnapshots`, `recordSandopSignOff`, `listSandopSignOffs`. These six are identical in 2.0.0 (YAML
`9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`, the §22 acceptance pin) and canonical 3.0.0 (YAML
`5213b5353267ad4c25d150975d6f38422bced72f678c02271b0cccf6e768adab`); the module is pinned to 3.0.0 (§7) and the
§22 acceptance evidence stays bound to 2.0.0 for these identical operations (`docs/roadmap/plans/mvp6-text-patch-q83-01/COMPARE-0190-PIN.md`). No plan list, search, paging, edit, delete, status transition, bulk,
import/export, DEMAND lookup, Workflow, notification or Capacity call.

### 23.4 Screens, routes, permissions

Browser → same-origin MVC (`proxy-profile`, NET-001) → Gateway 5000 → SupplyChain 5061. The browser never calls 5000/5061 or holds a bearer token.

| Surface | MVC route | Gateway downstream | UI display gate | Backend |
|---|---|---|---|---|
| Entry page | GET `/SupplyChain/SandopPlans` | — | `supplychain.sandop-plans.read` | — ("Create plan", "Open plan by ID"; **no plan list**, F190-LIST) |
| Plan workspace page | GET `/SupplyChain/SandopPlans/Details/{sandopPlanId:guid}` | — | read | — |
| Get adapter | GET `/SupplyChain/SandopPlans/api/{sandopPlanId:guid}` | GET `/api/supply-chain/sandop-plans/{sandopPlanId}` | read | `getSandopPlan` |
| Create adapter | POST `/SupplyChain/SandopPlans/api` | POST `/api/supply-chain/sandop-plans` | `.create` | `createSandopPlan` |
| Snapshots list adapter | GET `/SupplyChain/SandopPlans/api/{sandopPlanId:guid}/snapshots` | GET `/api/supply-chain/sandop-plans/{sandopPlanId}/snapshots` | read | `listSandopSnapshots` |
| Capture adapter | POST `/SupplyChain/SandopPlans/api/{sandopPlanId:guid}/snapshots` | POST `/api/supply-chain/sandop-plans/{sandopPlanId}/snapshots` | `.snapshot.capture` | `captureSandopSnapshot` |
| Sign-offs list adapter | GET `/SupplyChain/SandopPlans/api/{sandopPlanId:guid}/sign-offs` | GET `/api/supply-chain/sandop-plans/{sandopPlanId}/sign-offs` | read | `listSandopSignOffs` |
| Sign-off adapter | POST `/SupplyChain/SandopPlans/api/{sandopPlanId:guid}/sign-offs` | POST `/api/supply-chain/sandop-plans/{sandopPlanId}/sign-offs` | `.sign-off.record` | `recordSandopSignOff` |
| Unsupported list/edit/delete/transition/bulk/import/export | absent | absent | — | no route, control, proxy or request |

Permission keys stay exactly as published (§14): `supplychain.sandop-plans.read`, `.create`, `.snapshot.capture`, `.sign-off.record`.
The UI check is a display decision (UAS-001 §4); `[HasPermission]` on the backend stays the authority.

Adapter headers: `Authorization` server-side only; `X-Correlation-Id` = fresh UUID per request; `Idempotency-Key` = browser
per-intent key forwarded exactly (§12). Tenant and legal entity never come from the browser (§2). Antiforgery on every POST.
"Open plan by ID" only navigates to the workspace route; a malformed UUID is rejected client-side without a request.

### 23.5 Workspace tables — bounded DataTables v2 profile (no plan list)

The entry page has no DataTable (F190-LIST). The workspace shows a plan summary (name, horizon, demand reference, localized status,
current snapshot, created) and two client-side tables: **Snapshots** over `listSandopSnapshots` (captured at, provenance
ID/version/checksum/time, supply input reference count; the current snapshot marked) and **Sign-offs** over `listSandopSignOffs`
(snapshot, localized role, localized decision, comment, decided by as UUID, decided at). Both use `data-dt-standard="v2"`,
`DtDefaults.create()`, `stateSave: false`, `serverSide: false`; client search/sort/paging over the returned set only; no filter,
column visibility, saved view, export or QuickView. UUIDs and checksums are LTR and copyable. Absent field → "not provided";
malformed envelope → error, never empty.

### 23.6 Forms — field count and validation

`form_field_count: 5` (create form) → **GoldenReferenceSlim**; offcanvas forms only (no edit mode), `.diten-field` + icon per field.
Client checks mirror the published schemas only (required, date order, enum, length); no tightening (§8, §12).

| Form (schema) | Field | Required | UI rule |
|---|---|---|---|
| Create plan (`CreateSandopPlanRequest`) | `name` | yes | Text sent as typed; no trim or maxlength |
| | `horizonStart`, `horizonEnd` | yes | Dates; end ≥ start mirrored, server authoritative |
| | `demandPlanId`, `demandPlanVersion` | yes | Free text (F190-DEMAND); no lookup |
| Capture snapshot (`CaptureSandopSnapshotRequest`) | `demandPlanId`, `demandPlanVersion`, `sourceChecksum` | yes | Free text |
| | `sourceCapturedAt` | yes | UTC date-time |
| | `supplyInputRefs[]` (`source`, `resourceId`, `resourceVersion`) | array optional (default `[]`); each row all 3 | Repeater; order kept |
| Record sign-off (`RecordSignOffRequest`) | `snapshotId` | yes | Select from this plan's `listSandopSnapshots` only |
| | `role` | yes | DemandPlanning / SupplyPlanning / Finance / Operations / Executive |
| | `decision` | yes | Approved / Rejected; final confirmation through `window.showConfirm` (MOD-0013) |
| | `comment` | no | ≤ 2000 |

On 201 from create the browser navigates to the returned `sandopPlanId`. First-open progress: create form 0/5 required filled.

### 23.7 State-gated actions

The UI never changes plan status; it shows the server status as returned. Five approvals do not advance the plan (§16, §19).

| Action | Shown when | Key |
|---|---|---|
| Create plan | always on the entry page | `.create` |
| Capture snapshot | status Draft or InReview (§13) | `.snapshot.capture` |
| Record sign-off | status InReview and at least one snapshot listed (§13) | `.sign-off.record` |
| any other | never | — |

No native dialog, manual `Swal.fire` or inline handler.

### 23.8 Errors, replay and concurrency (published codes; localized, never raw text)

| Code (HTTP) | UI behaviour |
|---|---|
| `INVALID_REQUEST`, `INVALID_CORRELATION_ID` (400) | Localized form summary; safe field mapping only |
| `UNAUTHENTICATED` (401) | Standard session surface; JSON adapters return 401 JSON without redirect |
| `FORBIDDEN` (403) | Page: `_AccessDenied` in shell. Action: close/disable + localized denial |
| `UNKNOWN_SANDOP_PLAN` (404) | One identical safe-not-found text + support reference for unknown, foreign-scope and soft-deleted plans; workspace content removed; late responses never re-expose it |
| `SANDOP_PLAN_ALREADY_EXISTS` (409) | Specific localized message; inputs kept |
| `SANDOP_PLAN_STATE_CONFLICT`, `SANDOP_SIGN_OFF_STATE_CONFLICT` (409) | Stale-state message + support reference; plan and lists reload |
| `SIGN_OFF_ALREADY_RECORDED` (409) | "Already recorded for this role and snapshot"; sign-offs reload; the first decision stays shown |
| `IDEMPOTENCY_KEY_REUSED` (409) | Stop retry; new user intent required |
| `INVALID_DEMAND_REFERENCE`, `INVALID_SNAPSHOT_REFERENCE` (422) | Specific localized message; inputs kept |
| `DEPENDENCY_UNAVAILABLE`, `COMMIT_RESULT_UNRESOLVED` (503) | Temporarily unavailable; same-key retry; unknown commit never shown as rolled back |

Per intent: one pending request; the same key and **identical body text** on network/503 retry.
**An intent is one opened create form or command panel (snapshot capture, sign-off), not one payload** (amended 2026-10-04, Q403). The UI mints the `Idempotency-Key` when that form or panel
opens and keeps it unchanged across edits, failures and network/503 retries until it closes; only a newly opened form or panel is a new
intent with a new key. A user who edits while the outcome is unknown resends under the same key, so a committed first attempt answers
409 `IDEMPOTENCY_KEY_REUSED` instead of creating a second record. On that 409 the UI stops, keeps the inputs, tells the user the
request was already received with different values and that a different request needs a new form, and never mints a key to get past it.
Measured on Returns (MOD-0186), whose create and command surfaces have this shape, by R-2 (`docs/records/audits/2026-10/mvp6-r2-returns-ui-01/evidence/traps-browser.md` §T2): a key re-minted per payload created **two Returns from one intent**; one key per opened form gave 409 and **one** Return.
Acceptance row SU-14 below still reads "edited payload new intent"; its replacement is proposed to the owner in `docs/records/audits/2026-10/mvp6-q403-intent-definition-01/REPORT.md`.
A replayed 201 is shown as completed, then the workspace reloads; a replay body is never shown as current state. The response
`X-Correlation-Id` is a copyable support reference.

### 23.9 Localization, UAS-001, accessibility

Seven tenant languages (en, tr, fr, es, zh, ar, ru) in `Resources/Views/SupplyChain/SandopPlans/{SandopPlansIndex,SandopPlanDetails}.{lang}.resx`,
marker classes `SandopPlansIndex` and `SandopPlanDetails`, `_IndexL10n.cshtml`/`_DetailsL10n.cshtml` JSON bridges + `index.l10n.js`/`details.l10n.js`.
Keys: the list in `UI-SCOPE-0190.md` §7 (titles, field labels, the five status, five role and two decision values) plus one key per
error code in §23.8; shared toolbar words from `SharedResource`; no English placeholder in other languages, no hardcoded fallback.
Arabic RTL; UUIDs, versions, checksums and timestamps LTR-isolated; 390/768/1024/1440 without horizontal overflow; keyboard focus/Escape in
offcanvas and dialogs. UAS-001: without `.read` only `_AccessDenied` inside the shell (no title, form, table, skeleton, button, toast
or redirect); no control without its key.

### 23.10 Owned UI paths (32, new files only), protected paths, shared handoff

Owned: `frontend/Diten.Web/Controllers/SupplyChainSandopPlansController.cs`; `frontend/Diten.Web/Models/SupplyChain/SandopPlans/SandopPlanViewModels.cs`;
`frontend/Diten.Web/Views/SupplyChain/SandopPlans/{SandopPlansIndex.cs, SandopPlanDetails.cs, Index.cshtml, Details.cshtml, _CreateOffcanvas.cshtml, _CaptureSnapshotOffcanvas.cshtml, _RecordSignOffOffcanvas.cshtml, _IndexL10n.cshtml, _DetailsL10n.cshtml}`;
`frontend/Diten.Web/wwwroot/assets/js/SupplyChain/SandopPlans/{index.js, index.l10n.js, details.js, details.l10n.js}`;
`frontend/Diten.Web/Resources/Views/SupplyChain/SandopPlans/SandopPlansIndex.{en,tr,fr,es,zh,ar,ru}.resx`;
`frontend/Diten.Web/Resources/Views/SupplyChain/SandopPlans/SandopPlanDetails.{en,tr,fr,es,zh,ar,ru}.resx`;
`frontend/Diten.Web.Tests/{Controllers/SupplyChainSandopPlansControllerTests.cs, Forms/SandopPlanFormContractTests.cs, JavaScript/SandopPlanDetailsBehaviorTests.cs}`.
These are separate from, and add nothing to, the 38 backend owned paths of §22.

Protected for the UI writer: all backend source (including `Features/SandopPlans/**` and service `Program.cs`), contracts/annexes,
`gateway/**`, shared layouts/partials/JS/CSS, `SharedResource.*.resx`, frontend `Program.cs`/DI, navigation/module/permission catalogues,
`frontend/Diten.Web/tests/diten-field-icons.test.js`, other modules' UI files (including MOD-0192), Golden Slim (read only), packs,
registries, `.antigravity/**`, guards, existing records and Git state.

One CT-appointed **integration owner** alone delivers exact diffs for: gateway routes (**listed, not edited here**): GET+POST
`/api/supply-chain/sandop-plans`, GET `/api/supply-chain/sandop-plans/{sandopPlanId}`, GET+POST `/api/supply-chain/sandop-plans/{sandopPlanId}/snapshots`,
GET+POST `/api/supply-chain/sandop-plans/{sandopPlanId}/sign-offs`, explicit routes with OPTIONS (NET-001), `/sandop-plansXYZ` not matched (§15);
page/permission registration; tenant navigation and Ctrl+K with registry-reconciled codes; shared L10n nav keys in 7 languages;
the `ICON_MAP` entries; the DCP-009 §21.1 exclusion change; and uptake of the accepted S&OP backend (38 paths + approved composition)
into the integrated target. That uptake is held by the §22 composition uptake gate (CT-QUEUE Q292): it does not start until MOD-0188
exists and DEMAND v1.1 returns plan ID, version and checksum.

### 23.11 Single acceptance matrix (UI) — early vertical slice first

No row has run. HTTP/browser/DB expectations follow §§13 and 16 and the tables above.

| ID | Criterion | Readiness |
|---|---|---|
| SU-VS1 | Early vertical slice: real-Auth actor with all four keys creates a plan on the exact DEMAND fixture (201 Draft, no current snapshot), captures a snapshot (201; plan InReview; snapshot listed and marked current), records one Approved sign-off after confirmation (201); reload shows the same rows; plan stays InReview; DB +1 plan, +1 snapshot, +1 sign-off, 3 receipts, 3 audits, 3 Pending events | BLOCKED (target, gateway, permission seed, fixture seed) |
| SU-01 | Same-origin chain: browser requests only `/SupplyChain/SandopPlans/**`; none to 5000/5061; no bearer token in the browser | BLOCKED (gateway) |
| SU-02 | Entry page issues no list request; malformed UUID blocked client-side; unknown ID → safe-not-found | READY |
| SU-03 | Skeleton / empty / error distinct in workspace and both tables | READY |
| SU-04 | UAS-001 without `.read` | READY |
| SU-05 | Action gating by key and status (§23.7); direct POST without key → 403, zero writes | READY |
| SU-06…SU-08 | Create (5 fields), capture (4 + repeater, `[]` when empty) and sign-off (snapshot from list, enums, comment ≤ 2000) body parity with the schemas; no trim or tightening | READY |
| SU-09 | Sign-off confirmation through `window.showConfirm`; no native dialog | READY |
| SU-10 | Duplicate role/snapshot → 409 `SIGN_OFF_ALREADY_RECORDED`; first decision unchanged in DB and UI | READY |
| SU-11 | Capture outside Draft/InReview → 409 `SANDOP_PLAN_STATE_CONFLICT`; sign-off outside InReview → 409 `SANDOP_SIGN_OFF_STATE_CONFLICT`; reload | READY |
| SU-12 | Five approvals do not change the displayed status | READY |
| SU-13 | Unknown, foreign-scope and soft-deleted plan → identical safe-not-found | READY |
| SU-14 | Idempotency: same key and body on retry; replay shown as completed; edited payload new intent; `IDEMPOTENCY_KEY_REUSED` stops | BLOCKED (DN-01) |
| SU-15, SU-16 | 422 demand/snapshot reference messages with inputs kept; 503 same-key retry, unknown commit not shown as rolled back | READY |
| SU-17 | 7 languages + RTL; LTR isolation; 390/768/1024/1440; keyboard | READY |
| SU-18 | Family routing `/sandop-plansXYZ`; regression incl. S&OP backend suites on target | BLOCKED |
| SU-19 | Source→binary→process→browser binding | READY |
| SU-20 | PNG via supported export only | BLOCKED (PRES-183-04) |

OUT at the start (scope rows, approved as option A): SU-SCR-01 plan list / search / paging / filters — **no plan list: the contract has
no list operation** (O-01, F190-LIST); SU-SCR-02 edit, delete, archive, bulk (O-02); SU-SCR-03 plan status change (O-03);
SU-SCR-04 overwrite/delete of snapshots or sign-offs (O-04); SU-SCR-05 DEMAND lookup, forecast series, quantities (O-05);
SU-SCR-06 Workflow tasks, notifications, Event Bus, live producers (O-06); SU-SCR-07 import/export, column visibility, saved views,
foreign QuickView (O-07); SU-SCR-08 Capacity data (O-08); SU-SCR-09 generic `verify_datatable_page.py --reference slim` as acceptance
(run and kept as a record only).

### 23.12 Test expectations (UI)

Module tests in the three owned test files; `python3 .antigravity/scripts/verify_datatable_page.py . --area SupplyChain --module SandopPlans --reference slim`
run and recorded (SU-SCR-09); RESX parity 7/7 for both markers with no placeholder; build of frontend, gateway and SupplyChain service
on the integrated target; real-Auth browser smoke with read-only, create, capture and sign-off identities in separate profiles;
`grep` scans for native dialogs, inline handlers and "Permission denied"/"Forbidden" text; independent VER on a frozen source.

### 23.13 Remaining gaps (recorded, not decided here)

- **F190-LIST:** no plan browsing; users reach a plan only by the ID returned on create or pasted in. A list needs a versioned contract change (§5).
- **F190-DEMAND:** demand references are free text and usable with the exact fixtures only until live DEMAND.
- **F190-PIN:** resolved — pinned to 3.0.0 (§7); the six operations are identical to 2.0.0 (`docs/roadmap/plans/mvp6-text-patch-q83-01/COMPARE-0190-PIN.md`).
- **F190-ENT:** backend entity base differs from `entity_base: EntityBase` (PH15-UI-190 row 4); recorded only.
- **Decided-by display:** `decidedBy` is a UUID; a person name needs a user lookup that is out of scope.
- **Verifier tension:** the generic DataTable verifier expects a list page; record-only until a scope-aware profile (DN-02 / PRES-183-03) exists.
- G-TARGET integrated target; S&OP backend absent from the common checkout; DN-01 retry policy; PNG; nav codes; DCP-009 §21.1 exclusion; G-ICONMAP.

### 23.14 Effort and authorization boundary

O/M/P (person-hours, option A; replaces `0190-1-REMAINING` 4.8/8/12.8 and `0190-4-REMAINING` 33.6/56/112 and is the UI sub-item of
`0190-5-REMAINING` 19.2/32/51.2 and `0190-6-REMAINING` 14.4/24/38.4; not additive): pack/Phase 1.5 4/8/16; frontend 28/44/72;
shared integration 8/14/24; independent UI VER 12/20/36; **total 52/86/148** (`ESTIMATE.tsv` `03669f33abb67a6be2ebdd1ae4e17fa26a7566d9190dc5d551f4969076a109e3`).

Not authorized by this section: UI code other than under the owner's separate decisions of 2026-09-26 ("modules first", draft
overlays; CT verdict Q64a F2) and a versioned UI dispatch; gateway, permission, navigation, L10n, icon-map or DCP edits except by the
single integration owner; contract or backend changes; `done` status; commit or push.

## 24. Self-registration

Approved: `docs/records/decisions/2026-09/mvp6-sop-capacity-ui-pack-signoff-owner-decision-01.md`

**Authority:** `docs/records/decisions/2026-09/mvp6-self-registration-design-owner-decision-01.md` (D1–D5 = A). **Design:** [`mvp6-self-registration-prep-01`](../../../../docs/roadmap/plans/mvp6-self-registration-prep-01/MANIFESTS.md) (MANIFESTS.md, NAV-L10N-KEYS.tsv, TEST-PLAN.md); MANIFESTS.md recorded MOD-0190 as "no UI scope → no manifest now", which §23 lifts.
**Foundation:** DCP-009 §21 (single integration owner).
This section specifies; it authorizes no code, `Program.cs`, `.csproj`, appsettings, `SharedResource`, Platform or gateway change, no new permission key or ID, and no commit, push or stash.

### Identity (D3 = A)

| Field | Value |
|---|---|
| ModuleCode / ModuleName / DisplayName | `sop-workflow-signoffs` / `SopWorkflowSignoffs` / `S&OP Workflow & Sign-offs` |
| Domain / Service | `SupplyChainExecution` / `DitenSupplyChainService` |
| ModuleVersion / IsTenantAssignable / IsBaseline | `1.0.0` / true / false |
| SortOrder / Icon (SOFT, proposed) | 440 / `bx-check-double` |
| Provider / tests (proposed paths) | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/ModuleRegistration/SopWorkflowSignoffsManifestProvider.cs` / `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/ModuleRegistration/SopWorkflowSignoffsManifestProviderTests.cs` |
| Scope | every RoutePath starts with `/SupplyChain/`, none with `/Platform/` → Tenant |

### Pages

| PageCode | DisplayName | RoutePath | RequiredPermission | Parent | Nav | PageType | Sort |
|---|---|---|---|---|---|---|---|
| `SANDOP_PLANS` | S&OP Plans | `/SupplyChain/SandopPlans` (§23.4; not a route yet) | `supplychain.sandop-plans.read` | — | **true** | List (entry page without table, F190-LIST) | 10 |
| `SANDOP_PLAN_DETAILS` | S&OP Plan | `/SupplyChain/SandopPlans/Details/{sandopPlanId:guid}` | `supplychain.sandop-plans.read` | `SANDOP_PLANS` | false | Detail | 11 |

### Actions

| Page | ActionCode | DisplayName | PermissionKey | Placement | Dangerous |
|---|---|---|---|---|---|
| `SANDOP_PLANS` | `CREATE` | Create Plan | `supplychain.sandop-plans.create` | Toolbar | no |
| `SANDOP_PLAN_DETAILS` | `CAPTURE_SNAPSHOT` | Capture Snapshot | `supplychain.sandop-plans.snapshot.capture` | Toolbar | no |
| `SANDOP_PLAN_DETAILS` | `RECORD_SIGN_OFF` | Record Sign-off | `supplychain.sandop-plans.sign-off.record` | Toolbar | no (final decision; confirmation dialog) |

Existing keys only: `SandopPermissions` constants `Read`, `Create`, `Capture`, `SignOff` (accepted isolated source, archive `8fa00d40…b745`). Pages and buttons mirror the §23 tenant UI scope.

### Navigation keys (0/7 present today)

| Key | Required languages | Ships with |
|---|---|---|
| `Nav.Module.SOPWORKFLOWSIGNOFFS` | en, tr, fr, es, zh, ar, ru | this provider |
| `Nav.Page.SANDOP_PLANS` | en, tr, fr, es, zh, ar, ru | this provider |

Values are added to `frontend/Diten.Web/Resources/SharedResource.{lang}.resx` by the integration owner and reviewed by the l10n agent (no empty value, no English placeholder, no key echoing its own name). This pack does not supply or approve values.

### Tests (TEST-PLAN.md §2; all required before the module counts as closed)

| ID | Pass condition |
|---|---|
| M-01 | Identity exactly as above; `IsTenantAssignable` true |
| M-02 | Every manifest `RequiredPermission` / `PermissionKey` is in the reflected `public const string` fields of `SandopPermissions` |
| M-03 | Every reflected key is in the manifest or on the API-only allow-list with a reason: none expected (all four constants are modeled) |
| M-04 | Manifest RoutePaths = the frontend view-route set of `SupplyChainSandopPlansController` (§23.4; to be built), counts equal (cross-checked by W-01) |
| M-05 | The action table above equals the manifest actions per page (code, key, placement, dangerous flag) |
| M-06 | PageCodes, RoutePaths and ActionCodes (per page) unique, case-insensitive |
| M-07 | Exactly one `IsNavigationVisible` page, with a null parent; every other page has a parent |
| M-08 | No RoutePath starts with `/Platform/` |

Shared guards W-01…W-04 and reconcile-state R-01…R-04 (DCP-009 §21.3) must also be green for this module.

### Ship rule (D4 = A)

The provider, its `AddSingleton<IModuleManifestProvider, …>` line and its navigation keys ship **together with this module's UI** in the integrated target, never ahead of it (SR-D4 overlay built with the UI, applied only at final integration).

### Open gaps (carried, not solved)

1. `SandopPermissions.cs` exists only in the accepted isolated source, not in the common checkout.
2. Closed: DCP-009 §21.1 no longer excludes MOD-0190 (applied DCP-009 text SHA-256 `ab728d67662037cd6bb73a8f5ad89f82c8fac64f2faf364d72d874cd21328fdf`; approved in `docs/records/decisions/2026-09/mvp6-text-patch-q83-signoff-owner-decision-01.md` (SHA-256 `d27458e19cda13c7453bd3c5378c22286078b3551f081ae5d91f0db1aff381da`)).
3. The S&OP UI is scoped (§23) but not built; routes and actions are re-checked against the built UI (M-04/M-05, W-01/W-02).
4. Runtime tests R-02…R-04 need a native executor and the integrated target.
