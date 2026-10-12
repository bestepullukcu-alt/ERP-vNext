# Q24b — evidence kit v1.2 validation on the Mac (WP-MVP6-ENV-097 Part 2, Q97 v2.1)

Lane AL-MVP6-ENV-097 · Claude Code in the owner's macOS terminal (Darwin arm64) · 2026-09-26 19:08:57Z – 20:1xZ.
Repo `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (read-only; unchanged at the end).
Evidence level (SOP §32.0): **runtime, executed locally (real .NET services, real Mongo replica set, real Auth, headless
Chromium).** Kit status stays **CANDIDATE — NOT ACTIVE**; this record is input for CT, not adoption.

## 1. Kit file hashes (27 files)

`KIT-HASHES.tsv`: **27/27 match** the install record (`mvp6-evidence-kit-install-01/INSTALL.tsv`, the ADOPTION-DECISION §3 values).
Modes on the Mac filesystem are already the requested 644/755 (27/27) — the 600/700 seen by Q24a through the device bridge
(Q101-F10) does not exist here. The kit files are untracked (not in HEAD); nothing in the kit was edited.

## 2. How the kit was exercised

The kit ran as the runtime lane of Part 3 (Q64b Claims UI), exactly as the guide describes (`run-kit.sh up` → lane work through
`k00_ctl.py run harness`, K10 pairs, K09 binding → `down` → `ARTIFACTS.sha256` → `seal`). Three `up` runs:

| Run | Evidence folder | Script | Outcome |
|---|---|---|---|
| A1 | `../mvp6-q64b-claims-build-01/kit/` (attempt 1) | `scripts/evidence-kit/run-kit.sh up`, unmodified | **K07 FAIL** → automatic abort path → K11 PASS |
| A2 | same folder (attempt 2) | `up-a2.sh` = `run-kit.sh` with ONE inserted supervisor harness step before K07 (diff in §4) | UP PASS; lane work; down; seal PASS |
| F1 | `../mvp6-q64b-claims-build-01/kit-fixes/` | same `up-a2.sh`, second lane (slot 6, own suffix/socket/workspace) | UP PASS; lane work; down; seal PASS |

Attempt-1 files overwritten by attempt 2 (K01–K04 write fixed names) are fingerprinted in `attempt1-kit-files.sha256`
(58 files) and `attempt1-COMMANDS.tsv` (the COMMANDS.tsv rows as they stood after attempt 1).

## 3. Validation steps (the six named checks first, then the others the runs exercised)

| Check | Result | Command (as recorded in COMMANDS.tsv) | Exit | Output excerpt / evidence |
|---|---|---|---|---|
| **K02** runtime | **PASS** (A1, A2, F1) | `k02_runtime.sh` → `dotnet --info (source dir)` | 0 / 0 / 0 | `K02 PASS: SDK 8.0.417, runtime 8.0.23, arm64`; `raw/runtime-check.tsv` result PASS; no global.json |
| **K05** isolated Mongo | **PASS** (A1, A2, F1) | `mongod --replSet rs<SFX> --port <lane> --bind_ip 127.0.0.1 … --fork`; `rs.initiate single member` | 0 / 0 | `K05 PASS: rsClaimsQ64b01 PRIMARY on 127.0.0.1:35994`; F1 `rsClaimsFixQ64b01 … 36994`; `raw/mongo-rs-status.json` |
| **K06** build | **PASS** (6/6 services × 3 runs) | `dotnet restore/build <svc csproj> -c Release --no-restore` | 0 (36 rows) | `K06 build PASS: <svc> <dll sha256>`; runtimeconfig net8.0 asserted; `raw/binary-sha256.txt` |
| **K06** launch | **PASS** (6/6 × 3) | supervisor `launch <svc>` (placeholders substituted in memory) | 0 | every service `listen ["127.0.0.1:<port>"]`, runtime `Microsoft.NETCore.App/8.0.23`, health 200 (MDM 400 — kit health path needs a tenant, known since A12 VER-02 §8) |
| **K06** netcheck | **PASS** (A1 a1; A2 a2+a3; F1 a1+a2) | `k06_build_launch.sh netcheck` | 0 | `0` connections to 27017 for every process (an operational mongod IS listening on 27017 on this Mac) |
| **K06** test | NOT USED | — | — | tests ran outside the kit (Part 3 §4); `k06 test` not exercised |
| **K07** identities | **FAIL (A1)** → **PASS (A2, F1)** | supervisor `run k07` | 1 / 0 / 0 | A1 `identities-a1.json`: the 5 tenant-97c5 actors → login **401**; the 2 tenant-0001 actors PASS. Cause: the lane Platform has no tenant record for seeded Auth tenant 97c5 (same as A12 VER-02 §7.5). A2/F1 `identities-a2.json`/`-a1.json`: 7/7 PASS (200, tenant claim match, previous hash = repo seed hash, `passwordPersisted:false`) |
| **K09** binding | **FAIL (A2, kit defect K-F4)** / **PASS (F1)** | `k09_binding.py --work … --served … --browser-url …` (run by hand; K09 writes no COMMANDS row) | 1 / 0 | A2: every attempt-2 row `match`/`listening`, but `raw/processes.tsv` still carries attempt-1 rows with dead PIDs → 6 stale rows fail. F1: `K09 PASS: 6 processes bound to source tree e6185f1a…; 5 browser binding row(s)` |
| **K11** cleanup | **PASS** (A1 abort path, A2, F1) | `k11_cleanup.sh` (abort path in A1) | 0 / 0 / 0 | `CLEANUP.tsv` / `CLEANUP-a1.tsv`: every lane port no listener, workspace removed, HEAD/branch/repo-status PASS. Observations K-F3, K-F5 |
| K01 source | PASS (3/3) | `k01_source.py (HEAD archive + overlays)` | 0 | A1/A2 `tree_manifest_sha256=2dfebddb…` — **identical** to the independent compose of Part 3 (`~/mvp6-env/claims`); F1 `e6185f1a…` (5 overlays) |
| K03 ports | PASS (3/3) | `k03_ports.py slot 5` / `slot 6` | 0 | slot 5: 5500/5501/5556/5557/5559/5561/5599, mongo 35994; slot 6: 56xx, 36994 |
| K04 / K04b config | PASS (3/3) | `k04_config.py shared/render/check`; `k04b_gateway_routes.py` | 0 | `effective-config-findings.txt: none`; every `#pairing` PASS; 0 `NOT FOUND` citations; 277 routes, 96 lane-mapped, 181 to the closed sink |
| K00 supervisor / abort (F4) | PASS | `k00_ctl.py abort` (A1 automatic) | 0 | A1 failure ran supervisor abort + K11 by itself; no orphan process, socket removed |
| K08 redact / seal | PASS (A2, F1) | `k08_redact_scan.py redact` (run logs); supervisor `run seal` | 0 | `SECRET-SCAN-FINAL-a1.txt`: `artifacts_verified PASS`, `files_changed_during_scan 0`, `result PASS` (147 and 116 files) |
| K10 DB pairs | PASS (all pairs whose expectation held) | `k10_snap.sh` + `k10_db_diff.py` (strict, whole-DB totals) | 0 / 1 | A2: 6 asserted pairs — 4 PASS, 2 FAIL **by product behaviour**, not kit (CU-10 503 zero-write; delivered UI flows could not reach the transitions, D-02) — plus the spec pair recorded without assertion (+1/+1/+1/+1 observed); F1: 6 asserted pairs, 6 PASS (`raw/db-assertions.tsv`) |

**Q24b verdict (agent):** the kit works end to end on the Mac **with one lane deviation** (tenant record before K07) and six
kit findings, one of which (K-F4) makes K09 report FAIL after any aborted attempt. Agent PASS ≠ CT ACCEPTED.

## 4. Lane deviation (the only one)

`up-a2.sh` (sha256 in SHA256SUMS) is `run-kit.sh` (`93220a42…`) with `K` pinned to the repo kit folder and one step inserted
before K07:

```
mkdir -p "$EK_EVIDENCE/raw/fixture"
step K07pre "tenant_fixture.py via supervisor harness (lane Platform DB tenant records; no secret)" -- ek_ctl run harness tenant_fixture.py -- "$(ek_port mongo)" "$EK_DB_SUFFIX" "$EK_EVIDENCE/raw/fixture/tenant-fixture.json"
```

`tenant_fixture.py` (in `../mvp6-q64b-claims-build-01/lane-scripts/`) clones the seeded Platform tenant document into the LANE
Platform DB for tenant 97c5 only if absent (A2/F1: T1 created, T2 already present). No kit file was changed.

## 5. Kit findings (for the kit owner / CT; none fixed here)

See `KIT-FINDINGS.tsv`.
