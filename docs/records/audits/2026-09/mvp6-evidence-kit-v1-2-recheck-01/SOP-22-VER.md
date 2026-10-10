# SOP-22 VER — MVP6 evidence kit v1.2, narrow independent re-check (Q67)

- **Work package:** MVP6-WP-EVIDENCE-KIT-V1-2-RECHECK-01 · Prompt Q67 v1.0 · Lane AL-MVP6-KIT-V12-RECHECK-01 (VER; this lane did not write v1.2).
- **Repo / base:** `/Users/natig/Projects/ERP-vNext-recovery`, `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched; no `.git/index.lock`).
- **Window (Istanbul):** start 2026-09-26 12:57 · end 2026-09-26T13:08+0300.
- **Method:** read-only. git ran only through `GIT_OPTIONAL_LOCKS=0` (read subcommands). Copies, tests and scratch ran only in the VM's `/tmp/q67`; the linter `shellcheck-py` was installed only there. Nothing ran against .NET, Mongo or a browser. The kit supervisor ran in /tmp against fake inputs and a fake `dotnet` listener, never against a service. The F4 driver test used a throwaway git repo in /tmp, never the owner repo. Every command and exit code is in `COMMANDS.tsv`.
- **Inputs:** proposal-03 (SHA256SUMS `e8bec4d7…caad` as dispatched; ADOPTION-DECISION-v1.2.md `3a5ea48f8254f6346e500447733f78c664e0abc08e11ccc344478402b19d59d2`; CHANGES.tsv `3356df76bbb066393dfad6be1bc76f630c18afe39297077e362160d15794e704`; key-pairing.tsv `751e6357c7dd08b4ddc60b5df1c5f7d838e914d57ad5eb01a9ecaa8046f275ae`); proposal-02; Q63 review SOP-22-VER.md `3b712385c0dbedf0f5e2c6174968f341b768c3b7999efdc82c73f9e33ccb7890`; CT verdict q63-kit-v2; product source = HEAD + the two sealed overlays in `templates/overlays.tsv` (archive hashes verified: OV1 `490d51be…` OK, OV2 `f50350b8…` OK).

## 1. Verdict table

| Step / item | Verdict | Evidence (command → result) |
|---|---|---|
| 0 Preflight | PASS | HEAD/branch match; no index.lock; start status snapshot = 411 lines (VM home, outside repo). |
| 1 Integrity | **PASS** | `sha256sum -c SHA256SUMS` → 33/33 OK. The 28 hashes in ADOPTION-DECISION-v1.2 §3 equal disk. The 12 files marked `=v1.1` are byte-identical to proposal-02 (`cmp`): k02, k03, k04b, k05, k06, k09, k10_db_snapshot.js, k10_snap.sh, actors/overlays/overrides.tsv, served-asset-hash.js. **The 16 changed/new files:** changed (14) PLACEMENT.tsv, guide, ek_lib.sh, k00_ctl.py, k00_supervisor.py, k01_source.py, k04_config.py, k07_identity.py, k08_redact_scan.py, k10_db_diff.py, k11_cleanup.sh, lane.env.example, run-kit.sh, service-map.tsv; new (2) key-pairing.tsv, tests/test_k04_pairing.py. No file was removed. |
| 2 F1 (HIGH) | **CLOSED** | §2 |
| 3 F2 (MEDIUM) | **CLOSED** | §3 T2–T6: allow-list only; the harness gets actor passwords only; peer credentials required (Linux path exercised); `reveal` in a real pty: the password goes to the terminal only, the socket reply has no password field, a wrong code is refused. |
| 3 F3 (MEDIUM) | **CLOSED** | §4: v1.2 catches 3/3 plain, 1/1 zip (base64 at an odd offset inside a trace-like zip) and 1/1 nested gz→tar→zip; bzip2 → `unscannable` HIT; no value printed. v1.1 on the same input misses 3/6. |
| 3 F4 (MEDIUM) | **CLOSED** | §3 T9–T11 and §5: a failed `up` → abort path → K11 → exit 1, workspace removed; `up` with a live supervisor refused and the live one kept; `run-kit.sh abort` removes the socket; supervisor abort stops a running service; idle expiry (`supervisor-expired`) removes the socket. |
| 3 F5 (MEDIUM) | **CLOSED** | Every git call is `ek_git` (allow-list rev-parse/status/archive/show/ls-files/ls-tree/cat-file/log/diff; `GIT_OPTIONAL_LOCKS=0` + `--no-optional-locks`) or K01 `git_argv` (rev-parse/status/archive); grep finds no raw git call; `ek_git … commit` refused. |
| 4 F6–F14 | **all reasonable** | §6 |
| 5 Regressions (D1–D4, G1–G6) | **none found** | §7 |
| 6 ADOPTION-DECISION-v1.2 | **PASS** | §8 |
| Static checks (re-run) | PASS | bash -n 7/7; Python `ast`+`compile` 11/11 (incl. tests/); `node --check` 2/2; shellcheck 0.11.0 `-S warning -x` → 0 (info: 4×SC2015, 6×SC2094); no `__pycache__` written. |
| **Overall** | **PASS** | F1–F5 closed with independent evidence; F6–F14 fixed or reasonably accepted; no regression; new findings N1–N5 are LOW/INFO only. **Recommendation: V1 — install v1.2** (then Q24). |

## 2. F1 — key pairing (was HIGH) → CLOSED

- **Citations:** `/tmp/q67/cites.py` composes HEAD < OV1 < OV2 read-only and opens every citation of `key-pairing.tsv`: **30 keys, 57 citations, 57 resolve at the cited line, 0 failures**. Every `layer` value names the layer that actually supplies the file.
- **Roles and groups**, read at each cited line:
  - **auth-platform (6 keys):** Platform `AuthService:InternalApiKey` is the receiver (InternalAuditController:95, InternalModuleRegistrationController:98, InternalTenantEntitlements:141, InternalTenantStatus:57) and the sender (AuthPermissionModulesClient:45). Auth `InternalEventAuth:ApiKey` is a receiver (InternalEventAuthService:23). The senders are Auth `PlatformService:InternalApiKey`, SupplyChain `PlatformRegistration:InternalApiKey` (ModuleRegistrationHostedService:101), Web `Platform:InternalApiKey` and **MDM `PlatformRegistration:InternalApiKey` (PlatformAuditForwarder:90 → `/api/internal/audit/append`)**.
  - **platform-mdm-active:** **Platform `ModuleRegistrationCredentials:Mdm:ActiveSecret`** (receiver; Authenticator:44) ↔ **MDM `PlatformRegistration:ModuleRegistrationCredentialSecret`** (sender; ModuleRegistrationHostedService:109). The controller confirms it: for the MDM modules (PRODUCT-ITEM-SKU-MASTER, LEGAL-ENTITY) only the credential pair is checked (InternalModuleRegistrationController:59–76); other modules use the `AuthService:InternalApiKey` gate (:77–98).
  - **Value pairs:** mdm-registration-id and svcid-kid/issuer/audience/callerid/enabled match the validator lines (Authenticator:39; PlatformServiceTokenValidator:49–63). svcid signer is TokenProvider:60 and validator is TokenValidator:56.
  - **jwt:** one signer, 5 validators. mfa and platform-mdm-previous are single keys.
- **Unit tests** (`tests/test_k04_pairing.py`, /tmp copy): 4/4 OK. The v1.2 table PASSes; the v1.1 wrong table FAILs for auth-platform and platform-mdm-active; the uncited v1.1 REQUIRED_PAIRS fail closed (all NOT FOUND); the citation states cited / moved / NOT FOUND are correct.
- **Independent end-to-end K04** (not the writer's test): a synthetic work dir of the composed source (cited files + appsettings + csproj; no user-secrets), running v1.2 `shared`/`render`/`check`:
  - v1.2 map → **check exit 0, 0 findings, 57/57 cited, every pairing PASS**;
  - v1.1 service-map through the v1.2 check → **exit 1: auth-platform and platform-mdm-active "DIFFERENT values"**;
  - v1.2 map with one planted uncited secret key → **exit 1 "rendered secret key … not in key-pairing.tsv (uncited)"**.
  No effective-config file contains a secret value.

## 3. F2 / F4 / F6 / F7 / F8 / F10 — supervisor run in /tmp (no service), `/tmp/q67/suptest.sh`, `pty_reveal.py`

| Test | Result |
|---|---|
| T1 socket folder `/tmp/…` mode 1777 | refused (F7) |
| T11 non-socket file at the socket path | refused (F7) |
| T2 start in a 0700 folder | socket `srw-------`; ping ok |
| T2 second supervisor on a live socket | refused "a live lane supervisor already owns …" (F4) |
| T3 `{"op":"run","argv":["id"],"cwd":"/"}` over a raw socket | refused "unknown task" (F2) |
| T4 harness `../outside/evil.py`, a symlink escaping the harness dir, arg `a;rm -rf /` | all refused (F2/F10) |
| T5 allowed harness | child environment = `ACTOR_PW_ACTOR_A` only (no LANE_SECRET_*), `RLIMIT_CORE=(0,0)` (F2, F8) |
| T6 reveal-arm / reveal from a process without a tty; reveal without arm | refused |
| T6b `reveal` inside a pseudo-terminal | correct code → the 32-char password is written to the pty; the socket reply is `{"ok": true, "written_to_terminal": true}` with no password key. Wrong code → refused, nothing shown (F2) |
| T7 70 000-byte request; client sending no newline | "request too large"; "timed out" after 10 s (F10) |
| T9 abort | supervisor exits; socket removed; fallback log folder removed (F4, F6) |
| T11b stale socket of a dead supervisor | replaced and served |
| T10 `--idle 6` | expired by itself (`supervisor-expired` row), socket removed (F4). The exit code could not be read because the process was not the shell's direct child. |
| F14 (`f14.sh`, fake `dotnet` listener) | bind `0.0.0.0` → `listen *:5856`, **NOT LOOPBACK-ONLY**, ok=false. Bind 127.0.0.1 → loopback passes (fails only on the runtime check, expected for a fake). Abort stops the launched service. The service received a resolved secret, not the placeholder. |
| COMMANDS rows | fixed descriptions only; 0 `ACTOR_PW_…=` / `LANE_SECRET_…=` lines in COMMANDS.tsv or the supervisor log |

## 4. F3 / F9 — scanner (`/tmp/q67/selftest.sh`, `f9_12_13.py`), FAKE values generated at run time

| Planted | v1.2 | v1.1 (same input) |
|---|---|---|
| 1 base64 value in a JSON field | caught | caught |
| 2 actor password in an HTTP query | caught | caught |
| 3 raw value in a .tar.gz member | caught | caught |
| 4 value base64-embedded at a 7-byte offset inside a trace-like **zip** | **caught** | missed |
| 5 value in **gz → tar → zip** (3 levels) | **caught** | missed |
| 6 value in a **bzip2** file | **HIT "unscannable: bzip2 …"** (fail closed) | missed |
| control file | clean | clean |

No value appeared in the report or on stdout (grep -F of all 6), and `--exact` with no values exits 1.
F9: a complete ARTIFACTS listing → `artifacts_verified PASS`; an unlisted file → FAIL; a hash mismatch → FAIL.

## 5. F4 / F5 — driver (`/tmp/q67/runkit_test.sh`, throwaway repo in /tmp)

- A) `run-kit.sh up` with a wrong expected HEAD: K01 fails, then "UP FAILED … abort path", then K11 `abort-cleanup`, **exit 1**; workspace removed. The CLEANUP repo-head/branch FAIL rows are expected, because the input was deliberately wrong.
- B) `up` while a live supervisor owns EK_SOCK → **refused**, exit 1; the live supervisor still answers (not orphaned).
- C) `run-kit.sh abort` → socket removed.
- D) `ek_git <repo> commit` → "refusing git commit".
- Not exercised end to end: a failure *after* the supervisor started (needs a real K02/K05 run). By code reading, the EXIT trap calls `ek_ctl abort` when the supervisor answers, and supervisor `abort` stopping services was verified in §3 (F14 test). Q24's abort drill covers the live path.

## 6. F6–F14 — fixed or accepted-with-reason

| Id | Disposition | Re-check |
|---|---|---|
| F6 | FIXED | Fallback log folder 0700 and removed at shutdown/abort/expiry (T9). **Reasonable.** |
| F7 | FIXED | Owner/mode check on the socket folder; type/owner/liveness check before replacing; default per-user $TMPDIR; lane.env.example no longer uses /private/tmp (T1, T11). **Reasonable.** |
| F8 | FIXED | RLIMIT_CORE 0, inherited (T5). **Reasonable.** |
| F9 | FIXED | §4. **Reasonable.** |
| F10 | FIXED (bounds, validation) / ACCEPTED (single-threaded) | T4, T7. Serialising lane operations is a sound reason. **Reasonable.** |
| F11 | ACCEPTED | Rows carry utc_start and utc_end; the guide documents reading them by start time. **Reasonable.** |
| F12 | FIXED | An unasserted async delta FAILs; `--totals-exempt` without `--exempt-reason` is refused; with a reason it PASSes and records `jobs+4 (hangfire async)`. **Reasonable.** |
| F13 | FIXED | wrapper_of: a wrapper with HEAD-relative manifest → strip; a real HEAD top → keep; a genuinely new top-level folder → keep; a mixed layout → refused. **Reasonable** (goes beyond the review's suggestion, correctly). |
| F14 | FIXED | §3 (all-interfaces bind rejected). **Reasonable.** |

## 7. Regressions — none found

- D1: the platform forced keys are unchanged; the only service-map change is MDM `secret_keys` (the F1 correction).
- D2: replaced by the stronger F1 mechanism.
- D3: wrapper logic kept and hardened.
- D4: the K04 literal-secret check is kept; the supervisor still holds values in memory only; K07 changed in comments only; the seal still reads values from its environment only.
- G1: k09 and served-asset-hash.js byte-identical.
- G2: `ek_run` unchanged; supervisor rows still one per task.
- G3: k10_snap.sh byte-identical; the k10_db_diff reuse and before==after refusals are kept.
- G4: the seal still records the ARTIFACTS hash, checks for changes during the scan, and refuses `--exact` without values; it now also verifies ARTIFACTS.
- G5: the strict rules are kept (§6 F12).
- G6: k06 byte-identical.

## 8. ADOPTION-DECISION-v1.2.md — PASS

- It states **NOT APPROVED** (front matter and heading).
- It keeps A1's terms (pilot, the §5 line and guide file name, the identity method, no product/.antigravity/gateway/contract/pack/guard/evidence change, lanes set their own switches, PNG separate), and it supersedes both the v1.0 and v1.1 bytes for installation.
- §5 makes Q24 prove live that **Platform accepted MDM's module registration and at least one MDM audit append returned 2xx**. It also requires: no NOT FOUND citation, loopback-only listeners, `artifacts_verified PASS`, no leftover supervisor or socket, and an abort drill.
- §4 names this narrow re-check as the precondition.

## 9. New findings (none blocks installation)

| Id | Severity | Finding |
|---|---|---|
| N1 | LOW | The guide says K04 refuses unless "every key … is still at its cited line", but the code treats a token found elsewhere in the cited file as `moved` (pass); only NOT FOUND fails. Generic tokens (`Enabled`, `Secret`, `HashSecret`) make `moved` permissive. Align the guide wording, or treat any `moved` row as a CT review item in Q24. |
| N2 | INFO | key-pairing.tsv covers every *configured* cross-service credential, but not every one in code: MDM `VerifiedGskuResolver/VerifiedMarketResolver:CredentialSecret` ↔ Platform `VerifiedGskuResolverCredential:Active/PreviousSecret` is absent. Neither side is set in any tracked appsettings (baseline unconfigured), so the kit changes nothing. A lane that exercises that resolver must add the pair first. |
| N3 | INFO | On a mismatch, K04 findings include sha256[:10] fingerprints of effective values (possibly of committed dev secrets). These are not values; acceptable. |
| N4 | INFO | The macOS peer-credential path (`LOCAL_PEERCRED`/`LOCAL_PEERPID`) and macOS tty resolution for `reveal` were verified by code reading only (the VM is Linux). Q24 exercises the peer path implicitly: `up` pings the supervisor and fails closed if it is broken. Q24 should also run one `reveal` on the Mac. |
| N5 | INFO | lane.env.example puts `EK_HARNESS_DIR` under `docs/records/…/lane-scripts`. Harness `.js`/`.py` files there are DocsPathGuard-scanned code; a `docs/<non-five>/…` string in them would turn the guard red (Loads lesson). A one-line guide note would suffice. |

## 10. Recommendation

**V1 — install v1.2**, then run Q24 exactly as in ADOPTION-DECISION-v1.2 §5. Adding one Mac `reveal` (N4) and a review of any `moved` citation (N1) to Q24 is advisable; neither needs a proposal-04.

## 11. Close-out

- `GIT_OPTIONAL_LOCKS=0 git status --porcelain` vs the 12:57 start snapshot: two new untracked entries.
  1. `docs/records/audits/2026-09/mvp6-evidence-kit-v1-2-recheck-01/` — this lane.
  2. `docs/roadmap/plans/mvp6-effort-update-08/` — **not written by this lane**: created 2026-09-26 13:01:23–13:01:57 +03:00 by a parallel lane (Q51, declared parallel-safe); no command in `COMMANDS.tsv` names that path.
  No other entry changed; HEAD `4a8d4d4b…1136c`; no `.git/index.lock`.
- Artifacts: `SOP-22-VER.md`, `COMMANDS.tsv`, `ARTIFACTS.sha256` (repo-root paths). **Uncommitted** (a chat lane cannot commit).
- Agent PASS ≠ CT ACCEPTED — returning to CT.
