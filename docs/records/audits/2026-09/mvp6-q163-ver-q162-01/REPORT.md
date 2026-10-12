# Q163 — delta VER of Q162 (SOP v2.5 patch r2) — SOP §37

| Field | Value |
|---|---|
| WP | Q163 (CT-QUEUE line 212, READY at preflight) · read-only-auditor (`/read-only-audit`, strict repository-read-only) |
| Where | LANE 4, Cowork Linux VM (`uname -s` = `Linux`). Not the writer's chat: Q141/Q162 were LANE 2 (`CHANGE-NOTES-r2.md:12`). This chat wrote no Q141/Q162 file |
| Start gate | `docs/records/audits/2026-09/mvp6-q162-sop-v25-r2-01/SHA256SUMS` exists (writer hand-off) |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; `GIT_OPTIONAL_LOCKS=0`; `git status --porcelain` only (no `git diff`); no `.git/index.lock` at start or end |
| Baseline | 29 ` M` (19 known + 10 UC-01) + 2 untracked UC-01 + record folders; 532 porcelain lines at start |
| Time | 2026-09-28 11:00:18 → 11:02:52 +03:00 (Istanbul) |
| Scratch | `/tmp/q163` in the VM only: three copies of the live SOP (dry-run, r2 apply, Q141 apply) |

## 0. Verification report

```text
VERIFICATION REPORT

WP ID:               Q163 (delta VER of Q162)
Verifier:            LANE 4 (Cowork, Linux VM) — read-only-auditor; independent of the Q141/Q162 writer (LANE 2)
Verification date:   2026-09-28
Branch/HEAD:         feature/mvp6-logistics @ 4a8d4d4b… (unchanged start → end)

Agent Verdict:       Q162 CHANGE-NOTES-r2.md "PATCH r2 PREPARED — not applied"
Verification Verdict: PASS (6/6)
CT Status:           pending (Agent PASS ≠ CT ACCEPTED)

Evidence level achieved: E1 (file/hash, patch dry-run and apply on /tmp copies, independent diff)
Required evidence level: E1

Checks:
- 1 integrity:                 PASS
- 2 dry-run + apply = r2 hash: PASS
- 3 delta vs Q141 postimage:   PASS
- 4 F-Q142-1 place wording:    PASS (one wording note, O-1)
- 5 F-Q142-2 no fixed stack:   PASS
- 6 placement / no live change: PASS

Failed criteria:     none
Rework required:     no
Next gate:           CT disposition → owner sign-off of r2 → exact-hash apply
```

## 1. Checks

| # | Check | Verdict | Evidence |
|---|---|---|---|
| 1 | `sha256sum -c SHA256SUMS` | **PASS** | `mvp6-q162-sop-v25-r2-01/SHA256SUMS` sha256 `00c03274acf6f8a7c2533b89d7e6d7be6a5218692c68c025e12df59f1d07bfa0`, 4/4 OK. Files: `SOP-v2.5-r2.patch` `3e8a0dba…f5f6`, `dispatch-wp.md` `af0a080e…3e50`, `CHANGE-NOTES-r2.md` `9c76445b…2b12`, `DELTA.diff` `df6eddd5…2c2d` |
| 2 | Live SOP copy `e85854bd…` → `patch --dry-run -p1` exit 0; real apply on a 2nd copy = r2 hash | **PASS** | Live `docs/guides/operations/control-tower-sop.md` = `e85854bd1ecb4895406d8e5a1276adbc82a2af250dc304abad796a2ae7a4de36`. Dry-run: "checking file docs/guides/operations/control-tower-sop.md", exit 0. Apply on a 2nd copy: exit 0 → `c1afe981c8f432087c5220035e49a0fc4ff9369df29f7c60a4c77badbcccf032`, 2,327 lines = `CHANGE-NOTES-r2.md:19`. 0 `.orig`/`.rej` |
| 3 | Rebuild the Q141 postimage `814dc29d…`; `diff -u` against r2: every change maps to F-Q142-1/-2; removed lines vs v2.4 still 7 | **PASS** | Q141 folder `SHA256SUMS` `914dcaeb…` 3/3 OK. Q141 `SOP-v2.5.patch` on a 3rd copy → `814dc29d06df046fbc0532a19003e064932b2e4cf50443e7cadf674a6da3246c`, 2,322 lines (= `CHANGE-NOTES-r2.md:18`). Independent `diff -u` 814dc29d → r2 has 5 hunks, line-for-line equal to the SOP part of `DELTA.diff`. The dispatch-wp `diff -u` (Q141 `614f33f0…` → r2 `af0a080e…`) has 1 hunk, equal to `DELTA.diff:57-`. Mapping: §0.1 r2 note (r2 :31-38, +8), which lists F-Q142-1/-2; §17.1 "Current at v2.5" pointer removed (Q141 :873-875) → F-Q142-2; T2 NEREDE (r2 :1997) → F-Q142-1; T2 Base Stack (r2 :2012) → F-Q142-2; T3 runtime branch (r2 :2048-2049) → F-Q142-1; dispatch G2 (`dispatch-wp.md:41`) → F-Q142-1; G4 (`:43`) → F-Q142-2. No other change. Lines removed vs v2.4: r2 7, Q141 7 |
| 4 | F-Q142-1: T2 NEREDE = OD-PLACE; T3 runtime branch and dispatch-wp G2 match | **PASS** | OD-PLACE (`mvp6-ct-verdicts-q154-q131-q141-q144-2026-09-27.md:42`): "Claude app → </> Code tab → Local → ERP-vNext-recovery (Mac Claude Code; Darwin evidence unchanged). Terminal is not required." r2 T2 NEREDE (:1997): "Claude app → </> Code tab → Local → ERP-vNext-recovery (Mac Claude Code). Terminal is not required."; the Darwin gate follows at :1999 (O-1). T3 runtime branch (:2048-2049) and G2 (`dispatch-wp.md:41`) use the same place name, "Terminal not required", Darwin. The old "Mac → Claude Code (local session) … Not a Cowork LANE" no longer occurs anywhere in r2 |
| 5 | F-Q142-2: no fixed BASE-STACK version, hash or layer chain in §17.1, T2 or dispatch-wp G4; rule still one versioned record + SHA256 | **PASS** | grep over the whole r2 SOP and `dispatch-wp.md` for `BASE-STACK v[0-9]`, `base-stack-v[0-9]`, `80e31c15`, `ce8d60ab`, `a8a236de`, `83e6322c`, `93bf1c07`, `4a4a0860` → **0 hits**. The rule stays: §17.1 r2 :878-880 ("names one versioned BASE-STACK record and its SHA256 (K4 …) … required for Mac build/test/runtime WPs … checks … SHA256SUMS and the quoted hash"); T2 :2012 "{BASE-STACK record path + SHA256 + layer chain as written in that record}"; T2 :2025 check → STOP; T3 :2065 runtime VER required; G4 `dispatch-wp.md:43` "names one BASE-STACK record + SHA256" |
| 6 | `dispatch-wp.md` r2 still outside `.antigravity/workflows/`; live SOP still `e85854bd…`; no change under `.antigravity/` or `docs/guides/` | **PASS** | `.antigravity/workflows/` has no `dispatch-wp*`. Live SOP `e85854bd…` at start and end. The `.antigravity`/`docs/guides` entries in `git status --porcelain` (` M .antigravity/rules/docs-organization.md` — one of the 19 known paths; `?? docs/guides/operations/mvp6-development-process-v1.0.md`, `?? …/mvp6-evidence-kit-v1.0.md`) are the same at start and end |

**Overall: PASS (6/6).** The r2 patch changes exactly the lines that F-Q142-1 and F-Q142-2 name, plus the §0.1 revision note. It applies cleanly to the live v2.4 SOP.

## 2. Observations (no rework required)

| # | Level | Observation | Evidence |
|---|---|---|---|
| O-1 | INFO | T2 NEREDE drops the OD-PLACE parenthetical "; Darwin evidence unchanged". The Darwin requirement is kept in the placement gate on the next line, so the meaning is the same. For a word-exact match with OD-PLACE, the parenthetical could be restored at sign-off | r2 :1997, :1999; OD-PLACE :42 |
| O-2 | INFO | `dispatch-wp.md` still calls itself "PROPOSAL v1 (Q141)" (:2, :7). The writer kept this on purpose (`CHANGE-NOTES-r2.md:53`); the status text changes when the file is copied into `.antigravity/workflows/` | `dispatch-wp.md:2`, `:7` |
| O-3 | INFO | The §0.1 r2 paragraph (+8 lines) is an audit note listing F-Q142-1/-2, not a rule change | r2 :31-38 |

## 3. ASSUMPTIONs

- **A1:** "every changed line maps to F-Q142-1 or F-Q142-2" includes the §0.1 r2 note, because that note only lists those two findings (`CHANGE-NOTES-r2.md:38`).
- **A2:** The OD-PLACE comparison is on meaning plus the place string. The place string matches word for word; the Darwin note moved to the gate line (O-1).

## 4. No-change verification

- Branch, HEAD, 29 ` M` and the 2 untracked UC-01 files are unchanged.
- `git status --porcelain` differs from the start only by `?? docs/records/audits/2026-09/mvp6-q163-ver-q162-01/`.
- No `.git/index.lock`, no git write, no `git diff`, no rm in the repo.
- No edit to the SOP, `.antigravity`, the Q141/Q162 folders or the ledgers.

## 5. Files

`REPORT.md` · `SHA256SUMS`

Agent PASS ≠ CT ACCEPTED — returning to CT.
