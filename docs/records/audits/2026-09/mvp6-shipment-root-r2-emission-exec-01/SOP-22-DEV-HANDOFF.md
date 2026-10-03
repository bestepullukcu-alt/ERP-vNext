# MVP6-SHIPMENT-ROOT-R2-EMISSION-EXEC-01 — SOP §22 DEV Handoff

## Verdict

**DEV PASS — writer complete.** The owner-approved Root R2 emission patch was applied only in the isolated source snapshot. The repository production sources were not changed. Independent runtime verification remains required before the browser handoff can become READY.

## Authority and exact inputs

- Branch / HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- Owner decision: the user approved the exact `OWNER-DECISION-TEXT.md` decision and patch for isolated three-file application plus a different-agent runtime VER on 2026-09-24.
- Candidate patch SHA-256: `2d44f0d3b61b29c5bf34437fa12564d29319fd64beaded366f443b08b1a0319e`.
- Baseline source overlay SHA-256: `8aba3d890f160cbed88eecc284cdcba78eda03b6b0955cf3061199a62171112e`.
- Root R2 contract candidate SHA-256: `dab5a2f9974283e2404a29a7fb8ed07a77ae8ed843929b1c7b4546c5615dd7cb` (candidate target, not canonical publication).

## Applied scope

Only these three paths changed in the isolated source:

1. `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Shipments/Shipment.cs`
2. `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Shipments/Handlers/CommandHandlers/CreateShipmentHandler.cs`
3. `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/ShipmentTests.cs`

The target hashes match the controlling rework package. `APPLIED-DELTA.tsv` and the 354-entry successor `FINAL-SOURCE-MANIFEST.tsv` (`7b6d2f6a679275b74ab88989f5ca943b139e7f1a365b5ab99d6f1285ceb1acae`) carry the exact bindings. The successor is the prior 351-entry integration overlay plus these three explicit Root R2 paths.

## Build and tests

- Native runtime: `/Users/natig/.dotnet/dotnet`, SDK 8.0.417 / runtime 8.0.23.
- Release restore/build: PASS, 0 warnings, 0 errors.
- API binary SHA-256: `fa829d3aa1467ba811a81be6944f94c73d735c7570634c57eb6abb1ded0a3090`.
- Golden emission test: 1/1 PASS.
- Legacy storage variants: 6/6 PASS.
- Replay/concurrency: 1/1 PASS.
- Rollback/retry: 1/1 PASS.
- Shipment regression: 12/12 PASS.

The targeted sets ran in separate processes because the known global BSON serializer registration race remains a test-process constraint. It was not hidden or treated as a product fix.

## Runtime evidence

An isolated Mongo replica set ran on `127.0.0.1:41020`; the operational `27017` endpoint was never used. The API ran on `127.0.0.1:5772` from the recorded Release binary.

- Fresh create returned 201 and persisted an explicit authoritative `LifecycleCorrelationId`.
- Detail returned the same root.
- Same-key replay returned 200 with `idempotentReplay=true` and no duplicate audit/receipt/outbox records.
- Persisted counts remained audit=1, receipt=1, outbox=1, Pending=1.
- Restart used the same DB, fixture, and binary; detail returned 200 with the same root.
- Runtime legacy matrix preserved missing→null, BSON null→null, malformed→500 `SHIPMENT_ROOT_INVALID`, valid UUID→same UUID, stored nil UUID→nil UUID.
- Soft-deleted record returned 404; missing read permission returned 403.
- Detail reads did not change scoped shipment counts.

The first restart attempt selected `sce_root_r2_exec_01` instead of the create DB and returned 404. It is classified as an evidence harness configuration error. The subsequent run used `diten_mod0183_root_exec` and supersedes it. The original create recorder also wrote a literal shell expression in its informational `binarySha256` field; the correct binary hash is independently recorded in `SOURCE-BINARY-PROCESS.tsv` and the restart evidence.

## Boundaries

- No canonical contract, guard, Gateway, UI, migration, backfill, operational Mongo, commit, push, or stash action occurred.
- Existing Carrier, Returns, Claims, and Loads sources were preserved.
- Root is persisted at create time from the request correlation context; detail does not derive or backfill it.
- This DEV PASS is not independent verification, CT acceptance, canonical uptake, rollout, E5, or G5.

## Cleanup

The API was stopped. All lane-owned isolated databases were dropped, and the lane-owned Mongo process on port 41020 was shut down. `CLEANUP.tsv` records the result.

## Independent VER handoff

The verifier must use `applied-three-file-source.tar.gz`, the baseline overlay identified above, and `FINAL-SOURCE-MANIFEST.tsv`; perform a fresh native .NET 8 build; use its own DB-010 replica set and ports; reproduce create/persist/detail/replay/restart and legacy variants; and report without changing sources.
