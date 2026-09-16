# MVP-6 development plan

Status: MOD-0183 readiness reconciliation recorded; runtime acceptance NOT READY. Inspected 2026-09-15.
Branch: `feature/mvp6-logistics`. Baseline: main synchronization at `bc109afa`.

## 1. Authority and current position

Apply `AGENTS.md`, `.antigravity/agents/orchestrator.md`, `.antigravity/workflows/add-module.md`,
the Supply Chain domain config, DCP-009, the MVP-6 brief and Control Tower SOP §17.
This plan schedules work; it does not approve draft packs or authorize runtime implementation.

- MVP-6 is the first contract-first build lane under the owner's decision and DCP-009 OD-4.
  The brief's older “after G3 / last” sentence describes the historical integration order; mock development
  need not wait for live upstream services. Final integrated acceptance still requires real upstream evidence.
- MOD-0183 is locally `ready-for-dev`; eight other module packs are `draft`.
- Three owned OpenAPI files and nine packs are present as untracked local work. The previous stash remains preserved.
- Central `WAREHOUSE-OUTBOUND` and `SUPPLIER` contracts are present and FROZEN v1.
- `Diten.SupplyChainService` is not scaffolded. Its approved domain port is 5061.
- The previous Phase-A report is historical evidence. Its missing-contract statements no longer describe the current checkout.
- DCP-009 OD-4 explicitly authorizes the approved MOD-0183 scaffold trigger; its `runtime_code_allowed: false` /
  “no member ready” summary is stale. Record that discrepancy for central CT without overriding the more specific
  approved pack or editing central governance. User approval of the reconciled scope and Phase 1.5 is recorded.
- Independent acceptance check: MOD-0183 service/runtime/tests are absent; no E4 implementation evidence exists.
  MOD-0184 remains draft and its conditional preparation gate has not passed.
- [MOD-0183 mapping, DoR, Phase 1.5 and SOP §17 prompt](mod-0183-readiness-and-development-prompt.md) is the current owned planning record.

## 2. Stage 0 — reconciliation status and remaining lane work

Owner: local CT / module-pack-author for owned specs; central CT for shared governance and consumed contracts.

1. MOD-0183 pack and Shipment/Warehouse/Supplier contracts have been reconciled in the linked record. Its stale
   missing-Warehouse note is replaced by present-with-compatibility-gates. Frozen annotations remain unchanged.
   The other eight draft packs remain future work; no claim that all nine packs are reconciled.
2. MOD-0183 field mapping is recorded (linked record §3); before implementing the inbound adapter verify:
   outboundId and warehouseId provenance, orderRef, structured shipTo, line identity, skuLevel, lots/serials,
   package references, and planned shipment time. The current owned create command requires shipToReference,
   lineNumber and plannedShipAt; the central contract does not supply those in the same shape.
   Keep upstream facts as references/snapshots; never create another warehouse or inventory system of record.
3. Correlation compatibility is explicitly PARTIAL (GAP-0183-01/04): central OutboundShipmentReadyEvent has optional string correlationId;
   the owned Shipment API requires UUID. Inspect the platform event envelope and document how existing correlation
   is preserved. Never silently replace a supplied correlation ID or assume all central strings are UUIDs.
4. Event/poll deduplication and source reconciliation are specified in linked §§4–5 using stable outbound provenance plus tenant/legal entity. Repeated notifications
   and polling must not create duplicate shipments. Inspect the Event Bus envelope for transport event IDs/context.
5. Validate SUPPLIER lookup/validate consumption and authenticated supplier-to-actor mapping. The frozen identity
   contract provides supplier records; it does not establish a portal actor binding. Use an existing authoritative
   auth seam if available; otherwise report that precise GAP to central CT.
6. Recheck frozen examples. Central files contain OpenAPI 3.0-style `nullable` under a 3.1 root, and the Supplier
   unknown-ID example uses `status: null` against a non-null enum. Report strict-validator incompatibilities to the
   owner; do not locally rewrite central contracts or mistake Prism startup for complete schema validation.
7. Future work: reconcile MOD-0147/0148 domain/service ownership before promoting their draft packs. WP ownership of the delivery
   work does not by itself settle all domain-config and service ownership details.
8. Revalidate owned OpenAPI refs, examples, errors, lifecycle semantics and pack parity after any approved spec update.
   Any required wire extension follows the frozen-contract version/change policy and consumer review.

Exit: explicit mappings and defaults are recorded; required central decisions are resolved or narrowly scoped out;
MOD-0183 runtime authority is consistent; its approved slice has measurable acceptance criteria.
Independent work may proceed while a particular adapter is blocked. A blocked adapter must not be reported complete.

## 3. Development sequence

| Stage | Module / work | Dependency | Deliverable and exit check |
|---|---|---|---|
| 1 | MOD-0183 Shipment Tracking & POD + service scaffold | Stage 0; approved pack | Five-layer service, persistent shipment/POD lifecycle, mocks, replay protection, isolation, audit/outbox; independent backend verification |
| 2 | MOD-0184 Carrier Management | Verified Shipment contract/core; promote its pack | Carrier create/query/status lifecycle; no duplicate supplier master; contract tests |
| 3A | MOD-0185 Routing & Load Planning | MOD-0183 + MOD-0184 | Routes/stops, load lifecycle and shipment/carrier references; invalid transitions and replay tests |
| 3B | MOD-0186 Reverse Logistics | MOD-0183 | Return/RMA lifecycle, source shipment/line reconciliation; inventory movement references, no local stock balance |
| 3C | MOD-0187 Claims Management | MOD-0183 | Claim lifecycle, evidence and shipment references; no unauthorized settlement/payment ownership |
| 4A | MOD-0190 S&OP Workflow & Sign-offs | Shipment wave verified; frozen DEMAND | Immutable versioned snapshots, sign-offs, provenance and reproducibility |
| 4B | MOD-0192 Capacity Planning | Shipment wave verified; frozen DEMAND | Capacity plans/scenarios/evaluations with reproducible inputs and versioned results |
| 5A | MOD-0147 Supplier Performance & Risk | Logistics evidence; SUPPLIER mock; ownership resolved | Evaluations/scorecards/risk and explicit feedback provenance |
| 5B | MOD-0148 Supplier Portal | SUPPLIER mock; actor mapping/ownership resolved | Authenticated supplier-scoped submissions and status; cross-supplier access denied |
| 6 | Integration and G5 | All accepted module slices; integrator routes and live dependencies | End-to-end flow, source reconciliation, correlation, reproducible snapshots and regression evidence |

Stages 3A/3B/3C, 4A/4B and 5A/5B may run in parallel only after their packs are approved and paths are disjoint.
Contract ownership does not allow two workers to edit the shared bundle concurrently. Frozen contract changes remain
single-writer work. Service composition, common persistence infrastructure, gateway routes and permissions also have
one designated writer. Parallel workers return feature registration hooks rather than editing the shared root.

## 4. MOD-0183 first implementation slice

Use `.antigravity/workflows/add-module.md` phases 0–6 with backend-only applicability recorded explicitly.

1. **Phase 0 / 1:** validate identity, pack status, updated consumed mapping, backlog overlap and owned/protected paths.
2. **Phase 1.5:** produce the required architecture table: exact fields, naming, tenant/LE predicates and soft delete,
   entity base, separate CQRS files, required/nullable fields, lookup decisions, and UI items marked N/A for this slice.
   Record the applicable owner authorization before Phase 2; a planning request alone is not implementation approval.
3. **Phase 2:** create Api/Application/Domain/Persistence/Infrastructure projects, tenant-scoped Mongo persistence,
   indexes, append-only lifecycle history, idempotency and atomic state/outbox handling. Verify transaction deployment
   prerequisites or the documented partial-failure strategy; do not promise atomicity unsupported by the local runtime.
4. **Phase 3:** implement exactly the approved Shipment create/query/detail/transition/POD wire surface. Install the
   four pipeline behaviors, JWT/RBAC and base controller. Preserve the pack's explicit frozen-wire envelope exception.
   Add Warehouse and Inventory clients against contract mocks only after the mapping gate passes.
5. **Phase 3.5:** hand route and shared permission requirements to integration-agent. Verify gateway header propagation
   and exact `/api/shipment-bundle` paths. Report backend-only acceptance separately if gateway work is still pending.
6. **Phase 4 / 4.5:** current pack is `shell: none`; no UI is implied. Run API/persistence/audit smoke for the backend
   slice. This does not substitute for required UI-inclusive E4 evidence when a user-facing module is later delivered.
7. **Phase 5 / 6:** independent security/quality verification, service README, API narrative and dated audit evidence.
   Record the accepted slice and remaining integration/UI work; do not mark the entire MVP complete.

Future UI requires an approved pack revision with actual form fields and actor journeys: tenant shell, seven languages,
Slim for at most eight editable fields or Compact above eight, DataTable v2, required-field parity, manifest/self-registration,
navigation permissions and browser smoke. No arbitrary UI pattern is selected by this plan.

## 5. Prompt and ownership protocol

Before each module, CT issues one SOP §17 prompt containing WP/prompt IDs and version, exact branch/base HEAD,
dirty-worktree baseline, module/pack authority, dependencies, allowed/protected paths, risk class, lane and integration order.
Use the mandatory NE / NEDEN / NASIL / YAPMA / DOĞRULA structure, plus persistence, consistency, output and failure contracts.

- **NE:** one bounded, approved module slice with observable runtime results.
- **NEDEN:** authority, dependency readiness and business flow served.
- **NASIL:** frozen schemas, mock dependencies, CQRS/persistence rules and exact file ownership.
- **YAPMA:** no foreign SoR, central governance edit, frozen breaking change, implicit shared registration or unrelated cleanup.
- **DOĞRULA:** happy path plus denial/replay/concurrency/dependency-failure tests, runtime evidence and independent review.

Use documented defaults and record `ASSUMPTION: ...`; ask no routine implementation questions. Stop the affected work
for contract-breaking choices, security/data-loss risk or unauthorized ownership changes and report the concrete gap.
No commit, push, branch change or stash operation is part of this planning task.

## 6. Verification and final acceptance

Per module: contract schema/example parity; meaningful state-transition and idempotency tests; tenant and legal-entity
isolation; permission denial; concurrency/retry behavior; persistence and audit/outbox evidence; declared failure responses.
Run service build/test and `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests` when runtime code exists.
Use isolated test databases according to DB-010. Do not test against operational data.

G5 closes only after independent verification demonstrates:

- Warehouse-ready source → one shipment → carrier/load → dispatch → POD → return/claim, including exceptions.
- Correlation and source identity survive the full chain and retries do not duplicate writes/events.
- Source warehouse, inventory and supplier records reconcile without competing local masters/balances.
- S&OP/capacity snapshots are versioned and reproducible from recorded inputs.
- Supplier performance feedback and portal access respect authoritative source and actor boundaries.
- Gateway/auth integration and the regression suite pass; required runtime evidence is linked in the acceptance report.

Mock-backed module acceptance is an intermediate milestone. Live cross-module integration and data-quality evidence
remain required for G5; “developer PASS” alone never closes the gate.

## 7. Next action

MOD-0183 Stage 0 artifacts are recorded in [the readiness and development prompt](mod-0183-readiness-and-development-prompt.md).
The latest independent acceptance gate is NOT READY: no service or E4 implementation evidence exists. Do not advance
to MOD-0184 on the historical Phase-A mock report. Complete the approved MOD-0183 core implementation and independent
E4 verification, resolve the affected adapter gaps with central owners, then prepare MOD-0184 for review without
promoting its draft status. No subsequent module prompt is dispatched by this reconciliation.
