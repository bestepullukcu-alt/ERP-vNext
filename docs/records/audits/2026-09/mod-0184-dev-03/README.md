# MOD-0184 DEV-03 — evidence-only rework handoff

Date2026-09-17. WP `MVP6-MOD0184-DEV03`; lane `AL-MVP6-MOD0184-DEV03`; prompt `mod-0184-dev-03-evidence-rework-v1.0.md`. Sole writer, testing-agent scope.

**Agent verdict: PASS for VER-184-01 evidence rework; independent VER required.** Production scope and acceptance are unchanged. Architecture remains historical **14 PASS / 4 FAIL, BLOCKED**; this rework grants no waiver or CT acceptance.

## Baseline and authority

Branch `feature/mvp6-logistics`; HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Fresh expected dirty state preserved. Authority is the DEV03 dispatch and independent VER02 report at `/private/tmp/mod0184-runtime-ver02/verification-report.md`.

`baseline-hashes.sha256` records14,392 non-generated content files. **14,391 remain byte-identical**, including all95 existing SupplyChain production source files and all30 DEV02 evidence files. The sole existing changed file is the named `tests/carriers/restart_probe.py`. No contract, pack, shared rule, other service, original evidence or production source edit. No git mutation, commit/push/stash.

## Exact correction and changed files

`recorder.diff` shows the only existing-source change: absent `r.data` is recorded as zero bytes instead of one space; present bytes remain exact. No request execution, persistence or business behavior changed.

New `tests/carriers/test_outage_capture.py` extracts and executes the actual recorder function for regression tests without booting its fixture. It also observes actual requests immediately at the urllib transport boundary for live comparison. No JWT or signing secret is retained. All new report/evidence files live in this DEV03 directory. `changed-files.json` lists exact hashes, with null only for its own self-reference.

## RED → GREEN evidence

- `regression-red.log`: old recorder produced `b' '` for observed `b''`; **1 failed / 2 passed**. Present JSON and later-caller-mutation positive controls already passed.
- `regression-green.log`: repaired recorder **3/3 PASS**, covering zero-byte GET, exact present JSON bytes, and immutable recorded input after caller mutation.
- `live/transport-capture.json`: nine independently observed actual HTTP requests; authentication material excluded.
- `live/transport-comparison.json`: **9 compared / 0 mismatches / 2 bodyless GETs**. Both GET200 and GET503 now retain empty base64 strings, seven POST requests retain actual JSON bytes. Comparison also checks method/path/status/parsed request.

These are fresh regenerated artifacts. Old DEV02 failed recording remains unchanged and is not relabeled as correct.

## Affected functional and persistence checks

`live/outage.json`, `live/commit-uncertainty.json`, `live-run.log` reproduce:

- Actual persistence outage: Carrier GET/create/status503.
- Unavailable startup: nonzero process exit, no HTTP listener.
- Restored Mongo: same key fresh201 then replay201.
- Actual Mongo `commitTransaction` failCommand with `UnknownTransactionCommitResult`: two failures recover201, three failures exhaust to503; same-key recovery retains exactly one entity/receipt/audit.
- Failpoint final mode0 is recorded in `failpoint-final.json`. Owned HTTP5061/proxy27686 processes stopped. Existing agent-owned isolated Mongo27684 remained available; operational Mongo27017 untouched.

No build, full99-case service suite, Shipment suite or architecture rerun: production bytes and unaffected tests unchanged, as requested. Prior VER02 observations remain historical evidence; this report does not claim they were rerun. No runtime feature expansion, schema or create422 test claim.

## Reproduce

```sh
python3 services/Diten.SupplyChainService/tests/carriers/test_outage_capture.py
MOD0184_TEST_MONGO='mongodb://127.0.0.1:27684/?replicaSet=mod0184&serverSelectionTimeoutMS=3000' \
  python3 services/Diten.SupplyChainService/tests/carriers/test_outage_capture.py --live /private/tmp/carrier-dev03-verify
```

The second command needs an isolated replica set with test commands enabled and a previously built unchanged service binary. It refuses occupied5061/27686 listeners and disables failpoints in finally. Run no other fixture traffic against that isolated Mongo while failpoints are enabled.

## SOP §22 closure

Golden/contract flow: unchanged. Sub-flows: only evidence serialization and observation. Failure path: absent-body substitution reproduced before repair. Tests/persistence/audit: artifacts above. Security/tenant behavior: unchanged; observation excludes auth values. Observability: exact observed and recorded request bytes compared. Migration/rollback: no data/schema migration; no cleanup/drop. Decisions: deterministic empty bytes for absent body, preserve JSON payload. Blockers: independent verification pending; repository gate remains14/4 with previously documented provenance. Known gaps: inherited gateway/E5/G5 exclusions and old fourth architecture failure remain. Out-of-scope changes: none.
