# MVP6-SHIPMENT-ROOT-R2-EMISSION-VER-01 — SOP §22 Independent Verification

## Verdict

**PASS — exact bounded Shipment Root R2 emission implementation independently verified.** The owner-approved three-file delta persists the first accepted create correlation as the distinct authoritative `LifecycleCorrelationId`, returns it from detail, preserves it across replay and real process restart, and retains the approved legacy missing/null/malformed/valid/stored-nil behavior. This is an isolated implementation verdict, not CT/full-module/G5/rollout or canonical-publication acceptance.

The existing Shipment browser `NEXT-HANDOFF.md` may be marked **READY for this exact 354-entry source manifest only**. This verifier did not edit that handoff or start browser work.

## Authority and provenance

- Owner authority: the user's 2026-09-24 message explicitly approved the exact decision in `mvp6-shipment-root-r2-emission-rework-01/OWNER-DECISION-TEXT.md`, patch `2d44f0d3b61b29c5bf34437fa12564d29319fd64beaded366f443b08b1a0319e`, isolated three-file application, and a different-agent runtime VER.
- Decision-text file SHA-256: `33dabd9e3f36289d6b00f6634a38bee4b8f7e2efec1e079ac327894be1d5b204`.
- Writer handoff SHA-256: `e8d47451c4a60071e3da26c3b7825b284a6d6a72e4bb32c07af336b344a52eae`.
- Branch/HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- Exact Root R2 candidate remains `dab5a2f9974283e2404a29a7fb8ed07a77ae8ed843929b1c7b4546c5615dd7cb`; this runtime result does not publish it canonically.

## Independent source reconstruction

The verifier materialized a unique disposable source from:

1. `git archive` of the recorded HEAD;
2. immutable 351-source integration overlay `8aba3d890f160cbed88eecc284cdcba78eda03b6b0955cf3061199a62171112e`;
3. writer's applied-three-file archive `c3fba911952951eef3be80cf591bad99d543acd53bbde0badc7e317dacfb11f4`.

All 354 manifest entries matched path, SHA-256 and byte length. A recursive baseline/final comparison found exactly the three approved paths and no others:

- `Shipment.cs`: `a6c4f67...` → `afbeb8e...`; nullable persisted property at final source line 25.
- `CreateShipmentHandler.cs`: `96f9d319...` → `25cbd3bd...`; validated create correlation assigned at final source lines 42–43.
- `ShipmentTests.cs`: `4399366c...` → `a64b3b24...`; persisted/detail/restart assertions at final source lines 127–156.

The final source manifest SHA-256 is `7b6d2f6a679275b74ab88989f5ca943b139e7f1a365b5ab99d6f1285ceb1acae`.

## Fresh build and process binding

- Native SDK/runtime: `/Users/natig/.dotnet/dotnet`, SDK `8.0.417`, runtime `8.0.23`; no major roll-forward.
- Fresh restore: PASS.
- Fresh Release build: PASS, 0 warnings, 0 errors.
- Independently built API binary SHA-256: `754329d79a2aa6b94430acc4b81f9bf60204229e45bfb3b4a0e4e436e6104333`.
- Restart process: PID `46776`, `/Users/natig/.dotnet/dotnet Diten.SupplyChainService.Api.dll --urls http://127.0.0.1:5783`.
- DB-010 environment: verifier-owned replica set `rsShipmentRootR2Ver01` on `127.0.0.1:41132`, writable PRIMARY. The operational listener at `27017` was not used.

## Reproduced acceptance

Fresh authenticated HTTP create returned 201. Mongo stored both `CorrelationId` and `LifecycleCorrelationId` as the accepted root `c04a2ebc-81c7-4323-975d-b795e6334a2c`; detail returned that same root. Four concurrent same-key requests returned durable 200 replays, while changed payload returned 409. Scoped state was exactly one shipment, history, audit, receipt and outbox record; the outbox record remained `Pending` with the same root.

Cross-tenant and cross-LE reads returned 404. Missing read permission returned 403. The legacy runtime matrix produced missing→null, BSON null→null, malformed→500 `SHIPMENT_ROOT_INVALID`, valid UUID→same UUID and stored nil→nil. Every measured detail read retained identical before/after scoped counts. Soft-deleted detail returned 404.

The API process was stopped and the same binary was started again against the same database and fixture. Authenticated detail returned 200 with the same shipment and root. Post-restart persistence remained shipment/history/audit/receipt/outbox/Pending = `1/1/1/1/1/1`.

Independent tests were run in separate processes where the known process-global BSON serializer race could otherwise interfere:

- golden create/lifecycle/POD/replay/restart: 1/1 PASS;
- legacy storage states: 6/6 PASS;
- replay/concurrency: 1/1 PASS;
- rollback/retry: 1/1 PASS;
- scope/RBAC/correlation/soft-delete: 1/1 PASS;
- complete `ShipmentTests`: 12/12 PASS.

The four focused `ShipmentTests` results overlap the 12-test regression and are not summed. The initial golden attempt without `MOD0183_TEST_MONGO` failed before product execution and is preserved in the raw archive; its isolated-replica-set rerun is the controlling result. The initial URL launch selected configured 5061, was stopped before requests, and was superseded by the explicit lane port 5783 launch.

## Evidence and boundaries

- Acceptance matrix: `ACCEPTANCE.tsv`.
- Source/build/process chain: `SOURCE-BINARY-PROCESS.tsv`.
- Raw evidence archive SHA-256: `e4f1da354b385334fa284ae0ffad3a56f83717fc515c16674a920a1433885ce6`.
- Cleanup: `CLEANUP.tsv`.

The repository production/test sources were not modified by this verifier: the three approved repository files still have their preimage hashes. Only this new immutable verification record was added. No contract, guard, Gateway, UI, migration, backfill, commit, push, stash or operational rollout occurred.

## Remaining gates

- Control Tower acceptance remains separate.
- Canonical Root R2 publication/uptake remains separate.
- Browser execution remains a separate lane; only its exact-source handoff readiness is cleared here.
- Warehouse ingress, E5/G5, full-module and rollout gates remain outside this verdict.
