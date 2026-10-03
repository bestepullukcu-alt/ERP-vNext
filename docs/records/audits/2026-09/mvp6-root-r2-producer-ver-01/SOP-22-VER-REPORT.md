# MVP6-ROOT-R2-PRODUCER-VER-01 — SOP §22

## Verdict

**REWORK / runtime evidence incomplete.** The DEV source and bounded tests are independently reproducible in a hash-identical disposable copy, but the required root-specific HTTP/JWT/BSON/process/restart evidence was not produced. This is an evidence gap, not a newly proven product defect.

## Integrity and provenance

- DEV handoff writer-complete record and `changed-files.json` were read.
- All 11 DEV source hashes matched `source-manifest.sha256`.
- Root R2 candidate hash matched `dab5a2f9974283e2404a29a7fb8ed07a77ae8ed843929b1c7b4546c5615dd7cb`.
- Disposable source copy was made from HEAD plus the exact current SupplyChainService source/test tree; repository source was not modified by VER.
- Fresh disposable API build: PASS. Binary SHA-256: `23b6fc7144b9684b6c691fc684c88e79ace3939ac118a9f24cad0bce22d0d948`.
- Binary path: `/private/tmp/mvp6-root-r2-producer-ver-01/repo/services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/bin/Debug/net8.0/Diten.SupplyChainService.Api.dll`.

## Reproduced tests

| Suite | Result | Interpretation |
|---|---:|---|
| `ShipmentRootStorageTests` | 6/6 PASS | Presence-aware missing/null/malformed/valid/nil model and no mutation. |
| Root bounded filter (`FullyQualifiedName~ShipmentRoot`) | 7/7 PASS | Includes the 6 storage tests plus bounded HTTP guard test; overlapping count, not additive coverage. |
| Existing `ShipmentTests` | 12/12 PASS | Existing HTTP/JWT/tenant/LE/soft-delete/replay/restart regression against disposable replica set; does not inject Root BSON variants. |

The 6/6 and 7/7 counts are overlapping; they are not summed as 13 unique Root tests.

## Acceptance matrix

- R01/R02: **PASS at model/storage level**; independent unit execution.
- R03/R04: **OPEN** for real HTTP detail body and persisted-root/trace binding.
- R05/R06: **PASS for existing Shipment regression only**; Root-specific detail authorization/fixture coverage remains open.
- R07/R08: **OPEN** for single raw-read/no-write measurement against live API.
- R09: **OPEN** for Root variants across actual process restart.
- R10: **PASS** for existing Shipment regression; no mutation path changed by observed tests.
- R11: **OPEN** for provenance/history distinction in runtime fixtures.
- R12: **PARTIAL**; source→binary and test execution bound, process/HTTP evidence absent.

## Environment and limits

A disposable Mongo replica set `mvp6r2ver` ran on `127.0.0.1:27118`; operational port 27017 was not used. It reached PRIMARY before tests and was shut down afterward. The available runtime probes are fail-closed collectors and do not generate HTTP/JWT/BSON capture by themselves; no false PASS was recorded.

No source, contract, Program.cs, guard, pack, migration, runtime rollout, commit, push or stash was performed by VER. Candidate runtime result is not canonical publication or CT acceptance.

## Recommendation

Return to DEV with a narrow evidence-only rework: run the built binary as a disposable API process, seed missing/null/malformed/valid/nil BSON fixtures in the same isolated DB, capture authenticated HTTP detail responses and response traces, measure all scoped collections before/after, and repeat across process restart. Do not change the 11-path implementation unless a concrete behavior defect is reproduced.
