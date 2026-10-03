# MVP6-SHIPMENT-ROOT-R2-EMISSION-REWORK-01 — SOP §22

## Verdict

**CANDIDATE READY / APPLICATION HELD.** The exact root-emission defect is reproduced and the narrow three-file candidate is GREEN in a disposable source snapshot. The real checkout was not changed because the prior Root R2 owner authority was restricted to 11 detail-read/probe paths and the CT acceptance explicitly excluded mutation.

## Root cause and source reconciliation

The accepted Root R2 producer implementation added presence-aware detail reading and projection. Its controlling `changed-files.json` contains 11 paths and does not include the Shipment aggregate or create handler. The CT acceptance states “No mutation”. The later 351-source integration overlay correctly retained those accepted read paths, but its create path still had only `Shipment.CorrelationId` and `CreateShipmentHandler` only assigned that field.

This is therefore an implementation scope gap, not an overlay transfer loss or wrong binary selection. The real-Auth recovery binary `554e115715d282a04cfb3203246eafaa53e71a1c98da54cefa3cfe6d2cb201d2` was built from the exact 351-source snapshot and reproduced the missing persisted field.

## Controlling root rule

The published Shipment root semantics require explicit emission of the authoritative persisted root and prohibit derivation/backfill. The Shipment lifecycle event rule defines the immutable business root as the first command's accepted `X-Correlation-Id`. The candidate writes that already validated create correlation into the distinct persisted `LifecycleCorrelationId` property. It does not derive the value during reads and does not change the current-request trace header, audit, history, receipt or outbox correlation behavior.

## Exact candidate

- Full candidate patch: `2d44f0d3b61b29c5bf34437fa12564d29319fd64beaded366f443b08b1a0319e`
- Production patch: `c78fefe4cfe0377ec014b58918656ddf05121504fd54fb40bcf0521677852011`
- Test patch: `c154f3352f3b266e2a52c3eee8d97008dc2823fc2a9fa6aaa5dc0d066a5363fe`
- Candidate source archive: `1feb88e5605c650774255bf581a257b05213a265124a96673e98df5bbf5653b7`
- Evidence archive: `dfdfc854c53f06446e842bf8b5e4411549abbbe25311ac8cb4d25fbe5c94bc60`
- Exact preimage and target hashes: `target-manifest.tsv`

The patch modifies only:

1. `Shipment.cs` — adds nullable persisted `LifecycleCorrelationId` so legacy missing/null remains representable.
2. `CreateShipmentHandler.cs` — sets it once from the validated create correlation.
3. `ShipmentTests.cs` — asserts persisted BSON, detail response and restart equality.

`git apply --check` passed against the reconstructed 351-source baseline, and applying the patch produced all three expected target hashes.

## RED → GREEN and runtime evidence

RED on the exact final source: fresh create returned 201, then the new assertion failed because the persisted BSON document did not contain `LifecycleCorrelationId`.

GREEN candidate:

- native .NET 8 Release build and focused create/persist/detail/restart test: 1/1;
- legacy missing/null/malformed/valid/nil materialization: 6/6;
- replay/concurrency: 1/1;
- atomic rollback/retry: 1/1;
- external API create/detail/replay against isolated DB-010 replica set: PASS;
- same binary and database after process restart: PASS;
- one audit, one receipt and one Pending outbox remained bound to the same immutable root.

The attempted combined parallel filter is retained as an environment/test-host failure: multiple `WebApplicationFactory` instances raced to register the process-global Mongo Guid serializer. It is not presented as a product failure or PASS; the relevant tests were rerun in separate processes and passed.

## Environment and provenance

- Base branch/HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
- Baseline: HEAD plus immutable 351-source overlay `8aba3d890f160cbed88eecc284cdcba78eda03b6b0955cf3061199a62171112e`
- Candidate API binary: `94d1a5fd6c4cc82b48ac0b233f1f7e1940abc7237e468d3f8872783b3b89b7b3`
- Evidence API port: `5762`
- Isolated Mongo: `40996`, replica set `rsShipmentRootR2Emission01`
- Operational Mongo `27017` was not used.

## Remaining gate

Only the exact application decision in `OWNER-DECISION-TEXT.md` is missing. After that decision, a single writer must apply the hash-bound three-file patch in an isolated checkout and a different verifier must reproduce the external runtime/restart evidence. Candidate GREEN is not runtime acceptance, CT acceptance, rollout, full-module completion or G5.

No product, contract, guard, Gateway, UI or Git state was changed in the real checkout.
