# Evidence checklist — process v1.0 §5 (verifier, before verdict)

| §5 item | Status | Pointer |
|---|---|---|
| Exact source manifest and archive SHA-256; base HEAD; overlay list | **MET**: HEAD `4a8d4d4b…` archive (`head.tar` `96ae026a…`, 14,251 files); overlay 1 A12 `7b6a0d1a…` / manifest `8ffa6c96…` 360/360; overlay 2 Auth `f50350b8…` / manifest `b9713185…` 22/22; overlap 0; source-tree manifest `e4528e14…`. Separate `BUILD-INPUT-MANIFEST.tsv` (6,968 files, 0 missing) | `raw/01-input-hashes.txt`, `raw/02-compose-source.txt`, `raw/overlays.tsv`, `raw/SOURCE-TREE-MANIFEST.tsv`, `BUILD-INPUT-MANIFEST.tsv` |
| Native .NET 8 SDK/runtime versions | **MET**: SDK 8.0.417; runtimes 8.0.23; every process loaded `Microsoft.NETCore.App/8.0.23`; Release; no .NET 10 | `raw/runtime/toolchain.txt`, `raw/processes.tsv`, `SOURCE-BINARY-PROCESS.tsv` |
| Effective configuration captured before start | **MET**: K04 resolution per service (appsettings < Development < user-secrets < environment < command line) with secrets redacted; 0 findings before launch (re-rendered and re-checked after each override change) | `raw/effective-config-*.json`, `raw/effective-config-summary.tsv`, `raw/effective-config-findings.txt`, `raw/overrides.tsv`, `raw/lane.env` |
| No connection to 27017 | **MET**: lane `mongod` on 34994 only; `netcheck` after start and before cleanup: 0 connections to 27017 for every lane PID; DB snapshots refuse any port but 34994 | `raw/netcheck.tsv`, `raw/netcheck-final.tsv`, `raw/lane-mongo-connections.txt`, `raw/mongo-rs-status.json` |
| Source → binary → process → browser binding (binary hashes, ports, PIDs) | **MET**: 6/6 DLL now = at build, PID = listener, runtime 8.0.23; served `details.js` = A12 target `69626e61…`; 21 browser URLs all on `127.0.0.1:5401` | `SOURCE-BINARY-PROCESS.tsv`, `raw/browser-binding.tsv`, `raw/binary-sha256.txt`, `raw/processes.tsv` |
| DB before/after for every mutation and every negative case | **MET**: 23 assertion rows (late-async attempt 1 and 2 both kept). Every negative case is a zero-write pair over the whole SupplyChain lane DB. Every mutation has an exact delta and state (A08/A09 commits; soft-delete fixture steps) | `raw/db/db-assertions.tsv`, `raw/db/*.json`, `raw/backend-404-probe.json` (`dbZeroWrite`) |
| Redacted raw evidence (no bearer tokens, cookies or reusable credentials) | **MET**: K08 pattern scan PASS (171 units); exact-value scan of all 8 in-memory lane values, raw/base64/URL-encoded forms, PASS (170 files, 0 hits); only cookie *names* are recorded. Post-cleanup pattern rescan: see `SECRET-RESCAN-AFTER-CLEANUP.txt` | `SECRET-SCAN.txt`, `SECRET-SCAN-EXACT.txt`, `SECRET-RESCAN-AFTER-CLEANUP.txt` |
| Cleanup record: processes, ports, secrets, Mongo data removed | **MET with one explained FAIL row**: all 7 lane processes stopped, 8 lane ports free, workspace (source, env, secrets placeholders, Mongo dbpath, gateway-runtime, logs, browser profiles) removed, supervisor exited, HEAD/branch unchanged. The `repo-status` FAIL row is two CT records created by CT during the run (not this lane) | `CLEANUP.md`, `CLEANUP.tsv`, `raw/listeners-before-cleanup.txt`, `raw/repo-status-before.txt`, `raw/repo-status-after.txt` |
| Incomplete archive → HEAD-archive + overlay method before "not runnable" | **MET**: applied; runnable | `raw/02-compose-source.txt` |

Also recorded: early vertical slice first (`raw/browser/vs.json`); negative/sabotage controls (`raw/browser/negctl.json`); PNG method and hashes (`png/PNG-INDEX.tsv`); every command and phase (`COMMANDS.tsv`, `raw/harness-runs/`).

Evidence-kit candidate scripts used (proposal `mvp6-evidence-kit-proposal-01`, not adopted; used as candidates, hashes in `raw/kit-files-used.sha256`):

- K03 ports, K04b gateway routes, K04 render/check, K05 Mongo, K06 netcheck, K08 redact/scan, K09 binding and K11 cleanup: used unmodified.
- K01: adapted as `scripts/compose_source.py`.
- K06 launch: adapted inside `scripts/lane_supervisor.py`, so secrets stay in memory.
- K07: not used, because it writes passwords to a file; replaced by `scripts/fixture_prepare.py`, `scripts/auth_fixture.js` and the supervisor.
- K10 snapshot: adapted as `lane-scripts/db_snapshot.js` (whole-DB fingerprint).
- K08 exact scan: replaced by `lane-scripts/exact_secret_scan.py`, which runs in the supervisor.
- Template a08 `scenario_prepare.py`: adapted as `lane-scripts/shipments_fixture.py`.
