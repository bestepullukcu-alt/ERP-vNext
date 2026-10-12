# MVP6-MOD0187-NORMAL-RUNTIME-VER-01 — independent verification, SOP §22

## Verdict and boundary

**Agent Verdict: PASS for the bounded normal-baseline R14/R21/malformed-producer runtime request. Verification Verdict: PASS for the reproduced rows below. Control Tower Acceptance: not asserted; E5/G5 and module completion remain outside this report.** This verifier did not author the integration or rework. It made no product-source, contract, guard, pack, Git, or evidence-injection change. Only this audit directory and an isolated disposable verification copy were written.

The input authority is `../mvp6-mod0187-normal-baseline-integration-01/normal-source.tar.gz` SHA-256 `edb759a07475184e11ae7ef94698f6300572b72aaeb2a39c7e2be13b74795a21`. Its manifest SHA-256 is `92879d2098e5c50fb4c2862ee52060cbe8ba1aab2e038e77f513f49680e80f80`. I extracted it into `/var/folders/f_/xfqgm56x3msgx1mh0s8j7zq40000gn/T/mvp6-mod0187-normal-ver-ftmpk8e2/source`; all **341/341** file hashes matched before build and again after all verification work. The integration `WRITER-COMPLETE.md` exists and says the source writer completed. The isolated copy contains the normal NoOp persistence probe hashes (`ClaimPersistenceRegistration.cs` `0361088e…`, `NoOpClaimCommitProbe.cs` `4082bb18…`), which match the R22/R25 evidence patch **baselines**, not its injected targets (`c4ca2690…`, `05cb8787…`). No R22/R25 injection was applied.

## Source → binary → process

An isolated .NET 8.0.417 restore and `-t:Rebuild --no-restore` succeeded. Build result: **0 errors, 1 NU1900 warning** because the NuGet vulnerability index was unavailable. API DLL SHA-256: `4673a6b970a4ee31c3413325b4024349ad91773262fb6e56e84eebbd1106fb8b`; file mtime `2026-09-22T07:00:32.061939Z`. Runtime logs show the exact DLL launched later at local `10:01:48` on API port `5187` (PID `30183`), with a forward-only Shipment capture proxy `5287` and DB-010 replica `claims_normal_ver` on `27987`. The second security/restart run used API `5188`, proxy `5288`, replica `claims_normal_ver_security` on `27988`; its API PID changed from `30663` to `31048` while the DLL hash stayed the same. Fresh regressions used separate replicas on `27989` and `27990`. The raw process, binary, build, Mongo and cleanup records are in `raw-evidence.tar.gz`; all eight lane listeners were closed afterward. Operational Mongo port `27017` was untouched.

The initial default restore stalled on package-source access, so it was stopped; a cache-based restore completed. The first sandboxed Mongo launch failed with loopback `Operation not permitted`; the same isolated probe then ran with runtime permission. These setup attempts are retained as evidence and were not counted as product failures or passing runtime checks.

## R-row and failure-path results

| Row | Fresh authenticated HTTP / persistence observation | Verdict |
|---|---|---|
| R14 | Raw TCP header tests: ASCII 1/128 accepted, 129 rejected; 128 UTF-8 `é` and emoji scalars accepted, 129 rejected; invalid UTF-8 rejected by HTTP parser; duplicate/missing key rejected; valid unrelated ASCII/UTF-8 header accepted. All 12 expectations matched. The parser-owned invalid-UTF-8 rejection had HTTP 400 without an application error code. | PASS |
| R21 non-nil | Real Shipment detail returned the stored non-nil `lifecycleCorrelationId`; matching Claims create returned 201. Captured outbound dependency `X-Correlation-Id` was a separate non-nil UUID, while inbound response correlation remained the requested business root. One claim, receipt, audit and outbox record persisted. | PASS |
| R21 nil | Real Shipment detail returned the stored nil UUID; matching create returned 201. Aggregate, receipt, audit and outbox all retained the nil root. A separate non-nil outbound dependency trace was captured. Immediate replay returned 201 with `idempotentReplay=true`, no extra records and no dependency reread. | PASS |
| R21 missing / null | Each producer detail was 200 with its respective absent/null root. Claims returned 503 `CLAIM_REFERENCE_INCOMPLETE`; all four scoped collection counts stayed zero. | PASS |
| R21 malformed | Real producer detail returned **500 `SHIPMENT_ROOT_INVALID`**; forward-only capture delivered it to Claims, which returned **502 `CLAIM_REFERENCE_INVALID`**. Inbound Claims correlation was preserved; all four scoped collection counts stayed zero. | PASS |
| R21 mismatch | Distinct valid stored/request roots returned 409 `CLAIM_CORRELATION_MISMATCH`; zero scoped writes. | PASS |
| Dependency distinctions | A different producer 500, connection refusal and five-second timeout each returned 503 `CLAIM_REFERENCE_UNAVAILABLE`; malformed successful 200 JSON returned 502 `CLAIM_REFERENCE_INVALID`. Each had one captured read and zero scoped writes. | PASS |
| Security / scope | Anonymous create returned 401 `UNAUTHENTICATED`; signed read-only grant returned 403 `FORBIDDEN`; foreign tenant and foreign legal entity each returned 404 `CLAIM_NOT_FOUND`. The owner's four scoped collections stayed empty. | PASS |
| Restart | The API process restarted against the same replica. The existing nil-root key replayed as 201 with `idempotentReplay=true`; collection counts remained one each and dependency capture count remained 8 before/after. | PASS |

The producer path was real Shipment HTTP through a forward-only capture proxy for normal R21 rows. Only the explicit “Dependency distinctions” rows used proxy-controlled failure responses; no product source or persisted Shipment response was injected. The R14 raw wire probe used authorized JWT and tenant/LE headers. Each failure row recorded a before/after scoped Mongo snapshot and application response correlation.

## Fresh regressions and historical failed filter

| Fresh run | TRX count | Exit | Replica |
|---|---:|---:|---|
| Claims reference | 36/36 pass | 0 | `27989` |
| Claims + Returns + Loads + Carriers same-host | 272/272 pass | 0 | `27989` |
| Exact `Diten.SupplyChainService.Tests.ShipmentTests` class | 12/12 pass | 0 | `27990` |

All 36 Claims reference test IDs are contained in the 272-test same-host TRX, so these numbers are **not added**. The integration handoff's historical broad `~Shipment` filter selected 51 tests and failed 12 with repeated Mongo `Guid` serializer registration in that mixed process (`test-evidence.tar.gz`, `evidence/tests/test-evidence-net8/shipment.trx`). That failure remains visible; the fresh exact-class 12/12 result is its bounded comparison, not a claim that the broad filter passed. The old host .NET 10 TestHost failure was likewise not counted as a product PASS.

## SOP §22 handoff fields

- **Branch / HEAD:** `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; staged paths empty at final check.
- **Changed files:** verifier-owned `docs/records/audits/2026-09/mvp6-mod0187-normal-runtime-ver-01/` only. Other agents were concurrently changing the intentionally dirty common checkout; whole-tree status equality is therefore not claimed. This verifier did not stage, commit, push, stash, switch branch, or edit shared source/contracts/guards/packs.
- **Golden/contract flow:** authenticated Shipment detail → Claims create; source root, outbound trace, response correlation and persistence bound to the same fresh binary/process.
- **Sub-flows:** R14 wire boundary; R21 nil/non-nil, missing/null/malformed/mismatch; replay/restart; same-host regressions.
- **Failure paths:** exact producer 500 mapping, other 5xx, refusal, timeout, malformed 200, JWT/RBAC and foreign scope, all with zero-write checks where applicable.
- **Tests:** fresh 36/36, 272/272 and exact 12/12, with distinct overlap noted above.
- **Persistence evidence:** scoped Mongo `claims`, `claims_receipts`, `claims_audit`, `claims_outbox` snapshots and nil-root values in raw runtime JSON.
- **Security/RBAC/Tenant evidence:** `raw/runtime-security/security.json`, auth and scope HTTP results.
- **Audit/Evidence:** this immutable report, copied 341-entry source manifest, 53-entry raw evidence manifest and compressed archive.
- **Observability:** API, Mongo, proxy capture, process PID/port and listener-cleanup logs in raw archive.
- **Migration/Rollback:** none performed; isolated DBs stopped, no production migration or rollout.
- **Decisions:** no new authority; independent bounded verification only.
- **Blockers:** none remaining for this verification request. The initial sandbox bind and restore setup issues were resolved, with failed attempts retained.
- **Known gaps:** no R22/R25 evidence-injection run in this baseline, no CT acceptance, E5/G5, canonical/guard change or module-completion claim.
- **Out-of-scope changes:** none by this verifier.

## Evidence integrity and no-change

`raw-evidence.tar.gz` SHA-256 `9f9a16efd0286c212e9ba80117265a58f8abc9055bf2bc4e7729a1e3c0448f4d` contains the raw HTTP JSON, scoped Mongo snapshots, process/build logs, three TRX files, exact probe scripts and per-file `raw/EVIDENCE-MANIFEST.sha256`; `SHA256SUMS` binds the archive and copied source manifest. The final extracted source recheck remained 341/341 exact. `git diff --check` produced no output. The common checkout remained on the same branch and HEAD with an empty staged set; because concurrent writers changed its preexisting dirty inventory, a byte-for-byte whole-tree no-change assertion would be unsupported. The verifier wrote no source, contract, guard, pack or Git metadata.
