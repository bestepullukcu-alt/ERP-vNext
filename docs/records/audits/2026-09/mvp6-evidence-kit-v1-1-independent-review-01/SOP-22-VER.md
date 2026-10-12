# SOP-22 VER — MVP6 evidence kit v1.1, independent review (Q63)

- **Work package:** MVP6-WP-EVIDENCE-KIT-V1-1-IVER-01 · Prompt Q63 v1.0 · Lane AL-MVP6-KIT-V11-IVER-01 (VER, independent: this lane did not write the kit).
- **Repo / base:** `/Users/natig/Projects/ERP-vNext-recovery`, `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched; no `.git/index.lock`).
- **Window (Istanbul):** start 2026-09-26 12:12 · end 2026-09-26 12:22
- **Method:** read-only. Every git command ran with `GIT_OPTIONAL_LOCKS=0`. Copies, diffs and tests ran only in the VM's `/tmp/q63` (Linux VM bridge). No .NET, Mongo, browser or kit run against services. The only install was the `shellcheck-py` linter into `/tmp/q63/sc`. Product source was read (grep) and never edited. Commands and exit codes: `COMMANDS.tsv`.
- **Inputs:** `docs/roadmap/plans/mvp6-evidence-kit-proposal-02/` (SHA256SUMS sha256 `53bda6f811431e03b47a1e454bd6768734b31a2497bc5d4742e90bc765e0823b`, ADOPTION-DECISION-v1.1.md `105974656b3e83a33c975ab410b992bfb8dad85efb53350c115015aada9d73b9`), proposal-01, A1 and v1.1-revision owner decisions, A12 VER-02 evidence, CT disposition a12-ver02, Loads `mvp6-loads-publication-guard-01/REPORT.md`.

## 1. Verdict table

| Step / item | Verdict | Evidence (command → result) |
|---|---|---|
| 1 Integrity | **PASS** | `sha256sum -c SHA256SUMS` in proposal-02 → 31/31 OK. All 26 §3 hashes of ADOPTION-DECISION-v1.1.md equal disk. The 7 `=v1.0` files equal the A1 hash, the proposal-01 disk file and `cmp` (k02, k03, k04b, k05, actors/overlays/overrides.tsv). Proposal-01 SHA256SUMS 26/26; its ADOPTION-DECISION.md = A1 `bound_to` `2f282dad…9d18`. PLACEMENT: the 22 carried-over install paths are unchanged; 4 new files go to `scripts/evidence-kit/` (k00_supervisor.py, k00_ctl.py, k10_snap.sh, templates/served-asset-hash.js); the decision-record row points to the v1.1 record name (justified: a new decision); the proposal row points to proposal-02. |
| D1 BackgroundJobs | **PASS** | service-map `platform.forced_keys`: `BackgroundJobs__Enabled=true; RegisterStandardJobs=false; DashboardEnabled=false`, and `extra_db_keys BackgroundJobs__StorageDatabaseName=DitenHangfire` → K04 writes `DitenHangfire_<suffix>` and `check` asserts it. Identical to A12 `raw/overrides.tsv` and SOP-22 §7.3. |
| D2 key pairing | **PARTIAL** | Mechanism PASS: the supervisor generates one value per group once (`SECRETS = {g: token_urlsafe(48)}`) and substitutes the same value into every env file; K04 `check` writes `#pairing` rows and FAILs when a member maps to another group, is missing, or its group is not rotated (unpaired group refused). **Content FAIL for `platform-mdm-active` → finding F1 (HIGH).** |
| D3 K01 wrapper | **PASS** | `wrapper_of()` strips one top folder only when all members are under it, the top is not a file, and it does not exist in the extracted tree; HEAD archive `allow_wrapper=False`; traversal checks run after stripping; headerless `path/sha256[/bytes]` manifests are accepted with a 64-hex check; `wrapper_stripped` column added; git uses `GIT_OPTIONAL_LOCKS=0`. Same rule as A12 `compose_source.py` / SOP-22 §7.1. Note F13 (INFO). |
| D4 no secret on disk | **PASS** (with F4, F6, F8) | grep of all kit files for LANE_SECRET/ACTOR_PW/SECRETS/environ: values exist only in supervisor memory and child environments. Env files hold `@@LANE:<g>@@` (K04 `check` fullmatch; literal → FAIL; overrides literal → render refused). K07 reads `ACTOR_PW_*` from the env and passes the bcrypt hash to mongosh through the env only. K08 reads exact values from the env only. COMMANDS rows are fixed descriptions; `processes.tsv` records argv only (`ps -o command=`). No `W/secrets`. `shutdown` clears both dicts and removes the socket (Python cannot zero string memory; children are stopped by K11 before the seal). No RLIMIT_CORE setting (F8). |
| G1 served-file hash | **PASS** | `k09 record-served` records the sha256 before serving; `fulfillHashed()` hashes the exact buffer passed to `route.fulfill` and aborts on an expectation mismatch; K09 requires both a pre and a served row per label, served ⊆ pre, and all rows PASS. INFO: only overrides that use the helper are bound (procedural). |
| G2 COMMANDS.tsv | **PASS** (F11 INFO) | `ek_run`: `"$@"; rc=$?` on the next line; fields go through `tr '\t\r\n'`; one row per call. `k06 test` reads `rc=$?` directly. Supervisor `cmdrow` is one row per launch/run. The seal and shutdown intentionally write no row (ASSUMPTION 6). |
| G3 unique names | **PASS** | `ek_unique`/`unique` `-aN` for logs, health, netcheck, identities, cleanup, seal and test dirs; `k10_snap.sh` noclobber; `k10_db_diff` refuses before==after and any file already used in `db-assertions.tsv`. |
| G4 final scan last | **PARTIAL** | Order PASS: `run-kit.sh seal` needs ARTIFACTS.sha256, runs K08 `--exact --seal` through the supervisor, records the ARTIFACTS hash, fails if any file changes during the scan (size+mtime), fails `--exact` without values, and writes no COMMANDS row after it. **Coverage gap → F3 (MEDIUM)**; no ARTIFACTS completeness check → F9 (LOW). |
| G5 setup deltas | **PASS** (F12 INFO) | Snapshot records `dbTotals` for every collection; the diff fails on unlisted counted changes (unless `--allow-unlisted <reason>`) and on any whole-DB delta not asserted via `--totals`/`--expect`. The writer's synthetic A12-F3 shape FAILs and passes with `--totals` (STATIC-CHECKS #11; logic re-read here). |
| G6 test results dir | **PASS** | `k06 test`: `dotnet test … --results-directory E/raw/test-results/<label>-aN.d --logger "trx;LogFileName=<label>.trx"`, one ek_run row. Matches the Loads REPORT deviation 1 lesson. |
| 3 Security of the 4 new files | **PARTIAL** | See §3. F2 (MEDIUM), F4 (MEDIUM), F7 and F10 (LOW); 127.0.0.1, 27017 refusal and cleanup ownership PASS. |
| 4 Static checks re-run | **PASS** | bash -n 7/7; `ast.parse`+`compile` 10/10 (`PYTHONDONTWRITEBYTECODE=1`); `node --check` 2/2; shellcheck 0.11.0 `-S warning -x` → 0 findings (info: 4×SC2015, 6×SC2094, as the writer reported). Scanner self-test **3/3 caught, no value printed**, `--exact` with no values exits 1 (§4). The extra probe found F3. |
| 5 ASSUMPTIONs | 7 acceptable · 1 wrong · 1 needs owner attention | §5 |
| 6 ADOPTION-DECISION-v1.1 text | **PASS** | §6 |
| **Overall** | **FAIL for adoption as-is (PARTIAL)** | One HIGH defect in a claimed fix (D2), plus four MEDIUM findings. **Recommendation: V2 — revise again (proposal-03).** |

## 2. D2 detail — finding F1 (HIGH): `platform-mdm-active` pairs the wrong MDM key

v1.1 (`REQUIRED_PAIRS`, service-map) asserts `Platform ModuleRegistrationCredentials:Mdm:ActiveSecret = MDM PlatformRegistration:InternalApiKey`, and leaves MDM `PlatformRegistration:ModuleRegistrationCredentialSecret` unrotated. The product source (read-only) shows the real consumers:

- MDM `ModuleRegistrationHostedService.cs:108-109` sends `X-Module-Registration-Credential` = **`ModuleRegistrationCredentialSecret`**. Platform `ModuleRegistrationCredentialAuthenticator.cs:44-52` accepts it only if it equals **`Mdm:ActiveSecret`**, or `PreviousSecret` inside `PreviousValidUntilUtc`.
- MDM `PlatformAuditForwarder.cs:86-90` sends **`InternalApiKey`** as `X-Internal-Api-Key` to Platform `/api/internal/audit/append`. Platform `InternalAuditController.cs:95` compares it with **`AuthService:InternalApiKey`**, which is the `auth-platform` group.
- Baseline fingerprints (sha256[:10], no values): Development config gives all four keys one value (`e96ab27d49`): Platform `AuthService:InternalApiKey` and `Mdm:ActiveSecret`, and MDM `InternalApiKey` and `ModuleRegistrationCredentialSecret`. Platform `PreviousSecret` is empty and `PreviousValidUntilUtc` is unset.

Consequence in every v1.1 lane that runs Platform and MDM:
- MDM module registration is **rejected**: the dev secret doesn't match the lane `ActiveSecret`, and there is no previous-secret window.
- MDM audit forwarding gets **401**: the `platform-mdm-active` value doesn't match the `auth-platform` value.
- K04 still reports `#pairing platform-mdm-active PASS (2 keys, one value)`, and Q24's pass conditions (§4 of ADOPTION-DECISION-v1.1) would not detect it.

The A12 filtered logs neither confirm nor refute the effect: registration and audit lines were filtered out. The source is conclusive.

Required correction (for the writer, not applied here):
- MDM `PlatformRegistration__InternalApiKey` joins `auth-platform` (6 keys).
- `platform-mdm-active` becomes Platform `ModuleRegistrationCredentials__Mdm__ActiveSecret` ↔ MDM `PlatformRegistration__ModuleRegistrationCredentialSecret`.
- Update `REQUIRED_PAIRS`, service-map, the guide sentence ("one Platform↔MDM key") and the STATIC-CHECKS synthetic test.
- Consider a Q24 pass condition that proves both MDM calls succeed.

## 3. Security review — k00_supervisor.py, k00_ctl.py, k10_snap.sh, templates/served-asset-hash.js

| Check | Result |
|---|---|
| Socket permissions | PASS: `umask(0o177)` before `bind` → the socket is created 0600 atomically; the supervisor refuses a socket inside W. **F7 (LOW):** `lane.env.example` puts it in `/private/tmp` (shared, sticky); there is no check of the parent directory's owner and mode, and an existing path is unlinked without checking its type or owner (a pre-created socket by another local user → DoS or impersonation of the supervisor for clients). Prefer the per-user `$TMPDIR` (0700) and verify owner/mode. |
| Accepted operations | launch, run, reveal, pids, ping, shutdown. **F2 (MEDIUM):** `run` executes any `argv` in any `cwd` with **all** lane secrets and actor passwords in its environment. `reveal` returns a password to any socket client; the TTY refusal exists only in `k00_ctl.py` and is bypassed by a raw socket client. This is documented in the docstring ("starts any helper") but not restricted. Anything running as the same user can obtain every lane value. Recommend: allowlist of helper executables (kit dir + lane harness), per-helper secret scoping (e.g. only ACTOR_PW for harness, only LANE_SECRET for K08), peer check (`getpeereid`/`LOCAL_PEERCRED`), and an explicit statement of the same-user trust boundary in the guide. |
| Input validation | `launch` svc is validated through the service map (KeyError otherwise); `reveal` actor must exist. **F10 (LOW):** `run` label, argv and cwd are not validated (the label reaches a log file name; exploitation needs a pre-existing `run-…` directory); the request line is unbounded (`readline()`); the single-threaded loop blocks during long `run`s. |
| 127.0.0.1 only | PASS: every launch uses `--urls http://127.0.0.1:<port>`. Command line outranks appsettings; the only product `Urls` key (SupplyChain appsettings.json) is overridden, and no `Kestrel` endpoints exist in the six services' tracked appsettings. INFO (F14): the supervisor asserts listener PID == child PID but not the bound address. |
| Refusal of 27017 | PASS: k03 refuses 27017 and the operational bands; `ek_mongo_uri`, K04 `lane_mongo`, K07 and `k10_db_snapshot.js` refuse 27017 or any non-lane port; netcheck fails on any :27017 connection. |
| Cleanup ownership | PASS: the supervisor never kills. K11 stops only W/pids.tsv PIDs whose command line still contains the lane workspace, TERM then KILL after 20 s, never by port or name. **F4 (MEDIUM):** there is no failure path. If `run-kit.sh up` fails after the supervisor starts, the supervisor and any launched services keep running with the lane values in memory (no trap, no idle timeout). A rerun of `up` starts a new supervisor that **unlinks the old socket**, orphaning the old one (unreachable, still holding values) and leaving its services on the lane ports. Recommend: `up` refuses when `ping` answers or the socket exists; a trap on failure that runs K11 and `shutdown`; an idle/max-lifetime timeout in the supervisor. |
| k10_snap.sh | PASS: attempt-suffixed path + noclobber, fixed description (`<lane uri>`), refusal JSON detected. |
| served-asset-hash.js | PASS: hashes the same Buffer that is served; aborts on expectation mismatch; one-line TSV fields. `expect` defaults to `-` (no check), and K09 then relies on the pre-serve record. |

## 4. Static checks and scanner self-test (re-run here, /tmp only)

- `bash -n` 7/7 OK; Python `ast.parse`+`compile` 10/10 OK; `node --check` 2/2 OK; shellcheck 0.11.0 (`pip --target /tmp/q63/sc shellcheck-py`) `-S warning -x` → 0; info level 4×SC2015, 6×SC2094.
- Self-test (`/tmp/q63/selftest.sh`, three **fake** values generated at run time, with forms and containers different from the writer's):
  1. a base64 (std, padded) value in a JSON field → caught (`b64`, `b64url`, `b64-nopad`, `b64url-nopad`);
  2. an actor password in an HTTP query string → caught (`raw`, `urlenc`);
  3. a raw value inside a `.tar.gz` member → caught.
  The control file was not flagged. **3/3 caught, exit 1; no value in the report or stdout** (grep -F of all three values: none). `--exact` without values → exit 1.
- Extra probe (`/tmp/q63/zipprobe.sh`): a fake value inside a deflated `.zip` member (Playwright trace format) and inside a plain `.gz` file → **scan PASS (missed)** → F3.

## 5. Writer ASSUMPTIONs (proposal-02 README)

| # | Judgement | Reason |
|---|---|---|
| 1 Guide keeps name v1.0 | Acceptable | Keeps the A1 §5 line valid; the decision text states it explicitly. |
| 2 `secrets` = alias of `shared` | Acceptable | Verified: `main()` maps both to `cmd_shared`; no secret is generated. |
| 3 Default rotated groups; `platform-mdm-previous` inherited | Acceptable | Platform `PreviousSecret` is empty in the tracked appsettings, so inheriting it is harmless. The group *membership* problem is ASSUMPTION 5. |
| 4 KeyId `lane-<suffix>` | Acceptable | Non-secret; same shape as A12 `lane-a12-rtver02`. |
| 5 D2 key names from A12 §7.4 + overrides + v1.0 member | **Wrong** (partly) | `auth-platform` membership matches the source; the `platform-mdm-active` pair is wrong (F1). A12 §7.4 does not state that pair; it was carried over from v1.0. |
| 6 Seal/shutdown add no COMMANDS row | Acceptable | A documented, deliberate exception so that ARTIFACTS covers COMMANDS.tsv; the seal report records its own command. |
| 7 Unchanged files; other A12 observations out of scope | Acceptable | 7 files byte-identical (step 1); the listed observations are outside D1–D4/G1–G6. |
| 8 `ast`+`compile` instead of py_compile | Acceptable | Reproduced here the same way. |
| 9 Commit not done | **Needs owner attention** | Proposal-01, proposal-02, the A1 record and the v1.1 revision record are all untracked (`??`). The installation decision binds hashes of uncommitted files; CT should arrange the local commit. |

## 6. ADOPTION-DECISION-v1.1.md text

PASS on every point asked:
- It states **NOT APPROVED** (front matter and heading).
- It keeps A1's other terms: pilot; the §5 line as quoted in A1; the identity method (isolated Auth DB, never 27017, bcrypt(12), destroyed at cleanup, password only in supervisor/child memory); lanes set their own security switches; PNG separate; no reinterpretation of approvals or hash boundaries.
- It grants **no** change to product code, `.antigravity`, gateway configuration, contracts, packs, guards or existing evidence.
- It names the **Q24 validation run** (K00, K02, K05, K06, K07, K09, K11 and the final seal on the v1.1 bytes, then CT review) as the precondition for becoming required. That is stricter than A1: K00 and the seal are added.
- Limitation relevant to F1: the §4 Q24 pass conditions (`no #pairing FAIL`) would not reveal F1.

## 7. Findings (most severe first)

| Id | Severity | Area | Finding |
|---|---|---|---|
| F1 | **HIGH** | D2 | `platform-mdm-active` pairs Platform `Mdm:ActiveSecret` with MDM `InternalApiKey`; the product uses MDM `ModuleRegistrationCredentialSecret` for registration and Platform `AuthService:InternalApiKey` for MDM's audit calls. Registration is rejected and audit forwarding gets 401 in every lane, while K04 reports PASS (§2). |
| F2 | MEDIUM | Security/D4 | Supervisor `run` executes arbitrary argv with all lane values; `reveal` serves passwords to any socket client (TTY check client-side only). Trust boundary = same Unix user, undocumented as such (§3). |
| F3 | MEDIUM | G4 | The exact-value/pattern seal scan does not look inside `.zip` or plain `.gz` files: planted fake values → PASS. Playwright traces are zip files. Either inspect them (zipfile/gzip) or refuse such files in evidence. |
| F4 | MEDIUM | Security/D4 | No failure path: a failed `up` leaves the supervisor (holding values) and services running; a rerun unlinks the old socket and orphans the old supervisor. No trap, no idle timeout (§3). |
| F5 | MEDIUM | Repo boundary | K11 runs `git rev-parse` and `git status --porcelain` without `GIT_OPTIONAL_LOCKS=0` (k11_cleanup.sh lines 65, 69), so `git status` may write `.git/index`/`index.lock` in the owner repo during a lane (K01 was fixed; K11 was not). |
| F6 | LOW | D4 | Helper output goes to `W/logs` (removed by K11). After W is gone, the `run seal` log goes to `$TMPDIR/<sock>-logs/`, which nothing removes. Values are not printed by kit helpers, but any lane helper that echoes its environment would leave them there. |
| F7 | LOW | Security | The socket in `/private/tmp` in lane.env.example; no parent-dir owner/mode check; the existing path is unlinked without a type or owner check. |
| F8 | LOW | D4 | No core-dump suppression (`RLIMIT_CORE=0`) for the supervisor or its children; this relies on the macOS default. |
| F9 | LOW | G4 | The seal records the ARTIFACTS.sha256 hash but does not verify its entries or that every evidence file is listed. |
| F10 | LOW | Security | `run` label/cwd/argv are unvalidated; the request size is unbounded; the single-threaded loop blocks during long runs. |
| F11 | INFO | G2 | Nested logging: a `run-kit` step row plus inner `ek_run`/SUP rows for the same action; the outer row is written after the inner ones. |
| F12 | INFO | G5 | Strict whole-DB totals have no documented escape for asynchronous writes (Platform background jobs are now enabled by D1); lanes must assert them or snapshot another DB. |
| F13 | INFO | D3 | The wrapper test uses the composed tree (HEAD + earlier overlays), not a pristine HEAD listing. A real new top-level folder would be stripped, but manifest verification then fails closed. |
| F14 | INFO | Security | Listener bind address not asserted (127.0.0.1 is effective via `--urls`). |

No defect was fixed; no file outside this folder was written.

## 8. Recommendation

**V2 — revise again (proposal-03).** F1 must be corrected, because it is a claimed D2 fix that is wrong in content and undetectable by Q24 as written. F2–F5 should be addressed or explicitly accepted by the owner in the same revision. F6–F14 are optional hardening or documentation. Once the corrections are made, only the changed files need a narrow re-check. V1 is not recommended as-is; V3 (v1.0 bytes) has the same F1 pairing plus the four known defects.

## 9. Close-out

- `GIT_OPTIONAL_LOCKS=0 git status --porcelain` vs the 12:12 start snapshot (407 lines): two new untracked entries.
  1. `docs/records/audits/2026-09/mvp6-evidence-kit-v1-1-independent-review-01/` — this lane.
  2. `docs/roadmap/plans/mvp6-decision-prep-01/` — **not written by this lane**. Created 2026-09-26 12:14:51–12:14:54 +03:00 during this run by a parallel decision-prep lane (Q60/Q61 are declared parallel-safe); no command in `COMMANDS.tsv` names that path.
  No other entry changed. HEAD still `4a8d4d4b…1136c`; no `.git/index.lock`.
- Artifacts: `SOP-22-VER.md`, `COMMANDS.tsv`, `ARTIFACTS.sha256` (repo-root paths). Uncommitted (chat lane cannot commit).
- Agent PASS ≠ CT ACCEPTED — returning to CT.
