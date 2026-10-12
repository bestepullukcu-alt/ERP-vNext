# MVP6-MOD0187-AUTH-05 — SOP §22 DEV handoff

## Verdict

**PASS — bounded fresh Claims E2/E4 evidence.** AUTH-04 is historical and was not reused as runtime proof. This result does not claim full-module acceptance, E5/G5, deployment or independent VER.

## SOP §17 execution metadata

| Field | Value |
|---|---|
| Work package | MVP6-MOD0187-AUTH-05 |
| Lane / role | Claims evidence DEV / orchestrator |
| Profile / risk | B / HIGH |
| Source authority | immutable consumer integration baseline |
| Source archive SHA-256 | `eaf786e647ef019f862c7dcc4f0b2ab29b2b5a36ff1bee98d2cca8d2863cc766` |
| Combined source manifest | 97 entries, `4ae0f286479761512f50216e5bd8be721e48ae4839c062b57cfdd29696e2daa8` |
| Program.cs | `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c` |
| Base HEAD | `4a8d4d4b339528a88e6220fb8402e5a2c771136c` |
| Disposable source | `/private/tmp/mvp6-mod0187-auth05-01/source` |
| API / Mongo | `127.0.0.1:5064` / replica set `claims_auth05` on `127.0.0.1:27892` |
| Protected paths | production source, Program.cs, Returns/shared, canonical/guard/gateway |

The disposable source was rebuilt from the integration checkout without inherited `bin`, `obj`, evidence or Git metadata, then overlaid with the immutable archive. Initial and final verification matched 97/97 source hashes.

## Fresh build and binary chain

- Restore: exit 0.
- Rebuild: exit 0, 0 warnings, 0 errors.
- API binary SHA-256: `d9449651bbdda031dc403d1932beef0927f9e09641e63194900e912b8effcfcd`.
- Test binary SHA-256: `76a00ee6713de3ad2040d2fc8d33907934d35966d6e78f20e029665634e0facb`.
- Fresh Claims core regression: 120 passed, 0 failed, 0 skipped.
- First and restarted API process IDs/commands are recorded in the raw archive. Both executed the fresh API binary on port 5064.

## Fresh HTTP and persistence results

- Authorized list `200`; missing read permission `403`; tenant mismatch `403`.
- Real Shipment detail endpoint `200`; exact persisted lifecycle root matched. No stub counted as uptake.
- Create `201`; same-key/body/root replay `201`; changed payload `409`; Open→Investigating `200`; list `200`.
- Restart: fresh short-lived token, same binary/DB/fixture, producer detail `200`, list `200`, replay `201`, same claim identity and no additional writes.
- Same-key eight-way HTTP concurrency: all `201`, one claim/receipt/audit/outbox.
- Different-key eight-way HTTP concurrency: eight distinct claims and 8/8/8/8 documents.
- Missing transition action permission: `403`; before/after counts unchanged.
- Reference connection refusal: `503 CLAIM_REFERENCE_UNAVAILABLE`; claims/receipts/audit/outbox all zero for the fresh scope.
- Fresh core tests cover the transaction-stage failpoints, lost-response recovery, lifecycle matrix, exact amount comparisons, actor/scope replay and Pending outbox assertions. These remain E2 component/Mongo evidence and are not relabeled HTTP.

## Evidence and scope

- `raw-evidence.tar.gz` SHA-256: `828143e8a0b81799f1388f6502aa378b73ad9f76bc2696496dcb64311243efad`.
- `source-manifest.tsv` SHA-256: `4ae0f286479761512f50216e5bd8be721e48ae4839c062b57cfdd29696e2daa8`.
- R01–R30 mapping: `R01-R30.md`.
- Product/shared source changes: none.
- Output changes: only this audit directory.

API and Mongo processes were stopped; no listener remained on 5064 or 27892. The ephemeral JWT secret was deleted and no bearer or secret value is archived. No commit, push, stash, reset or clean operation occurred.

Writer complete: **yes**. Independent VER is the next gate.
