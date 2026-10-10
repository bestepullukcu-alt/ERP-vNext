# Q168 — independent VER of the Q167 apply (SOP v2.5 r2 + dispatch-wp.md) — SOP §37

| Field | Value |
|---|---|
| WP | Q168 (CT-QUEUE `mvp6-process-pilot-01/CT-QUEUE.tsv:225`, READY at preflight) · read-only-auditor (`/read-only-audit`) |
| Where | LANE 4, Cowork Linux VM (`uname -s` = `Linux`). Not the writer's chat: Q167 was LANE 2 (`mvp6-q167-sop-v25-apply-01/README.md` §1). This chat wrote no Q162/Q167 file. See D-1 on "new chat" |
| Start gate | `docs/records/audits/2026-09/mvp6-q167-sop-v25-apply-01/SHA256SUMS` exists (writer hand-off, 08:28:08Z) |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; `GIT_OPTIONAL_LOCKS=0`; `git status --porcelain` only (no `git diff`); no `.git/index.lock` at start or end |
| Baseline | 29 ` M` + 2 untracked UC-01 + record folders = 535 porcelain lines, taken by this VM at 2026-09-28T08:26:39Z (Q165 pre-write snapshot, before the Q167 apply at 08:26:59Z); Q167 README §2 reports the same 29 ` M` + 506 `??` = 535 |
| Time | 2026-09-28 11:33 → 11:35 +03:00 (Istanbul; VM clock UTC 08:33 → 08:35) |
| Scratch | `/tmp/q168` in the VM only: three copies of the live SOP (reverse dry-run, real reverse, forward re-apply) |
| Authority | OD-SIGN-v2.5 (`mvp6-ct-verdicts-q161-q164-sop-v25-signoff-2026-09-28.md` `aa6af3ba5478825e9ddb7547b589251c56165db2c346730776f9d504179b9722`) |

## 0. Verification report

```text
VERIFICATION REPORT

WP ID:               Q168 (independent VER of Q167)
Verifier:            LANE 4 (Cowork, Linux VM) — read-only-auditor; independent of the Q167 writer (LANE 2)
Verification date:   2026-09-28
Branch/HEAD:         feature/mvp6-logistics @ 4a8d4d4b… (unchanged start → end)

Agent Verdict:       Q167 README "APPLIED at exact hashes"
Verification Verdict: PASS (6/6)
CT Status:           pending (Agent PASS ≠ CT ACCEPTED)

Evidence level achieved: E1 (file/hash, reverse and forward patch on /tmp copies, byte compare, porcelain delta)
Required evidence level: E1

Checks:
- 1 Q167 folder integrity:             PASS
- 2 live SOP = c1afe981…:              PASS
- 3 reverse dry-run + reverse = e85854bd…: PASS
- 4 dispatch-wp.md = af0a080e…:        PASS
- 5 no .orig/.rej:                     PASS
- 6 porcelain delta = allowed set:     PASS

Failed criteria:     none
Rework required:     no
Next gate:           CT disposition → ledger record (LANE 1)
```

## 1. Checks

| # | Check | Verdict | Evidence |
|---|---|---|---|
| 1 | Q167 folder: `sha256sum -c` OK | **PASS** | `mvp6-q167-sop-v25-apply-01/SHA256SUMS` = `8d68661b8d5c2c0537e17bc7d0214acb41208bf4d944dcf05e02b32628f131eb`, **1/1 OK** (`README.md` `e29c17ce896c504007adfa74f907d325ffddad3ccea1610a7b0a5a0040579450`). The folder holds README.md + SHA256SUMS only |
| 2 | `docs/guides/operations/control-tower-sop.md` = `c1afe981…` | **PASS** | `c1afe981c8f432087c5220035e49a0fc4ff9369df29f7c60a4c77badbcccf032`, 2,327 lines (= OD-SIGN "after"; = Q167 README §3) |
| 3 | /tmp copy: `patch -R --dry-run -p1 < …/SOP-v2.5-r2.patch` exit 0; real reverse = `e85854bd…` | **PASS** | Q162 folder `SHA256SUMS` `00c03274acf6f8a7c2533b89d7e6d7be6a5218692c68c025e12df59f1d07bfa0` 4/4 OK; patch = `3e8a0dba32dfe620500229b6494106443a19629032f4a558d579120239aea5f6`. Reverse dry-run: "checking file docs/guides/operations/control-tower-sop.md", exit 0 (copy unchanged, still `c1afe981…`). Real reverse on a 2nd copy: exit 0 → `e85854bd1ecb4895406d8e5a1276adbc82a2af250dc304abad796a2ae7a4de36`, 2,073 lines (= v2.4 preimage). Extra: forward re-apply on that reversed copy → `c1afe981…` again (round trip closes) |
| 4 | `.antigravity/workflows/dispatch-wp.md` = `af0a080e…` | **PASS** | `af0a080e85664dff29cb52206beccd8acd9971988f8b95b85f972b9a3cea3e50`; `cmp` byte-identical to `mvp6-q162-sop-v25-r2-01/dispatch-wp.md` (same hash, listed in the Q162 SHA256SUMS) |
| 5 | No `.orig`/`.rej` under `docs/guides` or `.antigravity` | **PASS** | `find docs/guides .antigravity \( -name '*.orig' -o -name '*.rej' \)` → 0. Also 0 in `/tmp/q168` |
| 6 | Porcelain vs baseline: only the SOP, `dispatch-wp.md` and new record folders (Q164–Q168) | **PASS** | vs the 08:26:39Z baseline (535 lines): exactly 4 added lines — ` M docs/guides/operations/control-tower-sop.md`, `?? .antigravity/workflows/dispatch-wp.md`, `?? docs/records/audits/2026-09/mvp6-q165-ver-q164-01/`, `?? docs/records/audits/2026-09/mvp6-q167-sop-v25-apply-01/` (539 lines at preflight); plus this folder at the end. No line removed. The 29 known ` M` paths and both UC-01 files (`PlatformMongoTestConnection.cs`, `…Tests.cs`) unchanged. The Q164 folder and the Q166 verdict record were already present in the baseline |

**Overall: PASS (6/6).** The live SOP is exactly the signed v2.5 r2 postimage, it reverses cleanly to the signed v2.4 preimage, `dispatch-wp.md` is the signed r2 file, and nothing else in the tree changed.

## 2. Deviations

| # | Level | Deviation | Evidence |
|---|---|---|---|
| D-1 | INFO | The prompt asks for "a NEW chat". This is the existing LANE 4 chat (it also ran Q165). Both placement gates hold: `uname -s` = Linux, and this chat did not write Q162 or Q167. While running Q165 it saw the Q167 changes appear in the porcelain (recorded in Q165 REPORT §5) but did not read or use them before this run | `mvp6-q165-ver-q164-01/REPORT.md` §5 |

## 3. ASSUMPTIONs

- **A1:** For check 6, the baseline is this VM's porcelain snapshot at 08:26:39Z, taken before the Q167 apply (08:26:59Z). It has the known 29 ` M` + 2 UC-01 untracked + record folders, and matches the count in Q167 README §2 (535).
- **A2:** "New record folders (Q164–Q168)" means the Q164–Q168 record paths. Only Q165, Q167 and this folder are new since the baseline; Q164 and the Q166 verdict record were already there.

## 4. No-change verification

- Branch, HEAD, the 29 known ` M` paths and the 2 untracked UC-01 files are unchanged.
- This lane added only `?? docs/records/audits/2026-09/mvp6-q168-ver-q167-01/`.
- No `.git/index.lock`, no git write, no `git diff`, no rm in the repo.
- No edit to the SOP, `.antigravity`, the Q162/Q167 folders or any ledger. This lane wrote no ledger.

## 5. Files

`REPORT.md` · `SHA256SUMS`

Agent PASS ≠ CT ACCEPTED — returning to CT.
