# Q173 — independent static VER of the Capacity overlay v2 (Q159, + Q88c) — SOP §37

| Field | Value |
|---|---|
| WP | Q173 / v1 (T3 v1, SOP v2.5 §36.2) · CT-QUEUE `mvp6-process-pilot-01/CT-QUEUE.tsv:244` (READY at preflight) · read-only-auditor (`/read-only-audit`, worktree-read-only) + frontend-ui-ux perspective |
| Where | LANE 4, Cowork Linux VM (`uname -s` = `Linux`). Not the Q159 writer (LANE 3, `CHANGES.md:3`); this chat wrote no Capacity file |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; `GIT_OPTIONAL_LOCKS=0`; `git status --porcelain` only (no `git diff`); no `.git/index.lock` at start, at resume or at end |
| Baseline | 549 porcelain lines at 07:25Z: 32 ` M` + 2 untracked UC-01 + `?? .antigravity/workflows/dispatch-wp.md` + record folders |
| Time | 2026-09-29 10:25 → 10:32 +03:00 (07:25 → 07:32 UTC); device bridge lost 07:32–10:32 UTC; resumed 13:32 +03:00 (10:32 UTC) → end, gates re-run on resume (D-1) |
| Scratch | `/tmp/q173` in the VM only: v1 and v2 extracts, emulation script |
| Static only | No build, no test, no browser. Evidence is file/hash, diff, grep and textual emulation of test assertions |

## 0. Verification report

```text
VERIFICATION REPORT

WP ID:               Q173 (independent static VER of Capacity overlay v2, Q159 + Q88c)
Verifier:            LANE 4 (Cowork, Linux VM) — read-only-auditor; independent of the Q159 writer (LANE 3)
Verification date:   2026-09-29
Branch/HEAD:         feature/mvp6-logistics @ 4a8d4d4b… (unchanged start → end)

Agent Verdict:       Q159 CHANGES.md "9 PASS · 3 N/A · 0 FAIL; Q88c implemented"
Verification Verdict: PASS (7/7)
CT Status:           pending (Agent PASS ≠ CT ACCEPTED)

Evidence level achieved: E1 (static: hashes, byte-exact re-pack, diff, grep, emulated test assertions, node --check)
Required evidence level: E1 (static); runtime CP-21…CP-23 and UI-PM focus behaviour → Q88b (Mac)

Checks:
- 1 integrity (4/4, archive, v1 55/55):      PASS
- 2 changed files = FILE-PLAN (9):          PASS
- 3 12 checklist items on v2:              PASS (9 PASS · 3 N/A valid · 0 FAIL)
- 4 UI-PM-01/05/10 fixes:                   PASS
- 5 Q88c vs decision + pack lines:          PASS (observations O-1, O-2)
- 6 tests match v2; only Layout changed:    PASS
- 7 node --check:                          PASS

Failed criteria:     none
Rework required:     no
Next gate:           CT disposition → Mac build Q88b (runtime CP-21…CP-23, UI-PM-05/10 behaviour)
```

## 1. Checks

| # | Check | Verdict | Evidence |
|---|---|---|---|
| 1 | `sha256sum -c` 4/4; archive hash; v1 still 55/55 | **PASS** | `mvp6-capacity-ui-draft-02/SHA256SUMS` `6aba548d8f4f7f44caa17bcea05929bf7d161a73685751490bea4c668349074e` 4/4 OK (at start and at resume); archive `48b60b782f1bbd84a74a95c3cbe01f90aec9d0062c528d748726b266db1028b3`. v1 `f4e18ab9c4953870f2b6cf7deb7441c7daead673903672ab90e943c13933dbd7` 55/55 OK, archive `dd290891…`. Other inputs equal: decision `52228ea7…`, pack MOD-0192 `a1df34c1…` (also at end), live checklist `90247ddc…`, Claims v4 `2b34741a…`. Extra: re-packing the extracted v1 and v2 trees with the recipe in `CHANGES.md` reproduces `dd290891…` and `48b60b78…` byte-for-byte |
| 2 | Plain `diff` of v1 and v2 extracts: changed files = FILE-PLAN.tsv (9) | **PASS** | 47/47 files, identical entry list. `diff -rq` → exactly 9 files, set-equal to `FILE-PLAN.tsv`; every v1/v2 hash and added/removed count in FILE-PLAN matches (5 partials 2/4, `details.js` 74/4, `index.js` 15/0, FormContract 5/2, DetailsBehavior 47/0). Controllers, 14 resx, `services/` and `_shared-integration/` identical |
| 3 | All 12 items on v2 → 0 FAIL; N/A reasons for 03/06/07 valid | **PASS** | 9 PASS / 3 N/A / 0 FAIL = `CHECK-RUN.tsv` (`RECHECK.tsv` group 3). **03 N/A:** no `ajax` option; the table is client-side (`details.js:550` `data: []`, `:571` `rows.add`) — the rule's own N/A case. **06 N/A:** 0 `skeleton-loader` in views/JS; section skeletons only. **07 N/A:** the 3 offcanvas `show()` calls (`index.js:291`, `details.js:748`, `:834`) are each behind a focused page button, 0 `dropdown-item`; the Q88c address opens page sections, not surfaces; `shown` listeners exist anyway (`index.js:321`, `details.js:960`) |
| 4 | UI-PM-01, 05, 10 fixed as CHANGES states; focus never on a disabled control | **PASS** | **01:** the 5 partials swap the `@{ Layout = … }` block for a Razor comment; only `Index.cshtml:10`, `Details.cshtml:11` set Layout. **05:** `setSectionState` sets `skeleton.style.display` (`details.js:214`) besides `d-none` (`:213`). **10:** `restorePanelFocus` (`index.js:201-209`; `details.js:598-607`) runs in `finally` after `submit.disabled = false` (`index.js:252-253`; `details.js:670-672`); targets are `[aria-invalid="true"]:not([disabled])`, the submit only when `!submit.disabled`, else `planName` / `scenarioName` / `evaluationMode` (no `disabled` attribute on those fields). 0 `showConfirm`, so no exemption applies |
| 5 | Q88c maps to decision :9-12 and pack :321, :337-341, :464-466, :488; replaceState; one request each; invalid ID; no new endpoint/route/key/storage | **PASS** | **Address:** `writeAddress` (`details.js:343-352`) sets `scenarioId`/`evaluationId` only when UUID, else deletes, and uses `history.replaceState` (`:352`); 0 `pushState`. Scenario written on every successful `loadScenario` (`:403`; covers open-by-ID `:468`, create read-back `:779`, retry `:933`, address `:984`); evaluation on every successful `loadEvaluation` (`:517`; covers submit read-back `:859`, refresh `:926`, retry `:934`). **Reload:** `init` calls `loadPlan()` (`:1005`) then `openFromAddress()` (`:1006`) → one get-scenario (`:984`) and, after it succeeds, one get-evaluation (`:400-405`); `evaluationId` without `scenarioId` → one get-evaluation (`:992`). **Invalid ID:** `scenarioId` → existing localized field error `openScenarioIdError` (`Details.cshtml:111`, `ScenarioIdInvalid`) + `aria-invalid`, no request (`:985-988`); `evaluationId` → evaluation error state with `ErrInvalidRequest` (ERROR_KEY `:54`, `failureText` `:158`), retry hidden, no request (`:991`); the plan loads independently. **Safe-not-found:** unchanged per-resource 404 handling (`:378-382`, `:498-502`). **Bounds:** `fetch(` count 2 (details) / 1 (index) in v1 and v2; the same 19 `t('…')` keys; ERROR_KEY unchanged; controllers and resx identical; 0 storage APIs, 0 timers. **Pack :488:** header comment `details.js:18-21` rewritten. Decision :9-10 (route or query) → query; :12 bounds → met |
| 6 | New/changed test assertions match v2; only the Layout assertion weakened | **PASS** | `CapacityPlanFormContractTests.cs:83-92`: the one changed assertion — page views must state the shell, partials must not (renamed theory; inline-handler and `/Platform` checks kept). `CapacityPlanDetailsBehaviorTests.cs`: +47/−0, 3 new facts. Emulated over v1 and v2: all assertions of the 3 new facts and the new Layout rule hold on v2 and fail on v1; the existing bans (storage, timers, `?page`, `search=`, verbs, native dialogs, hard-coded text, `fetch(` count 2, `window.location.assign(detailsBase…)`) still hold on v2 |
| 7 | `node --check` on the changed JS | **PASS** | `details.js` OK, `index.js` OK (node v22.23.2) |

**Overall: PASS (7/7).** v2 changes exactly the 9 planned files, fixes UI-PM-01/05/10 with focus never landing on a disabled control, and implements Q88c inside the decision and pack bounds.

## 2. Observations (no rework required; runtime-checkable at Q88b)

| # | Level | Observation | Evidence |
|---|---|---|---|
| O-1 | LOW | The evaluation ID is written to the address only after a successful read-back. If the 202 read-back GET fails (network/503), the evaluation was submitted but the address has no `evaluationId` until a retry succeeds. Pack :337-338 reads "when an evaluation is submitted … writes its ID" | `details.js:856-859`, `:496-513`, `:517` |
| O-2 | LOW | A valid address `evaluationId` is queued until the address scenario loads (`addressEvaluationId`). If that scenario load fails (404 or error), the queue is not cleared; a scenario the user opens next by ID would then load that evaluation. `isEvaluation` does not compare `evaluation.scenarioId` with the open scenario, so a hand-edited link with mismatched IDs shows that evaluation under the other scenario (its own scenario ID is displayed) | `details.js:354`, `:376-388`, `:400-405`, `:297-300`, `:983` |
| O-3 | INFO | The writer's F-Q159-1 is confirmed: pack MOD-0192 :303 still says every Capacity `.cshtml` states the Layout, which v2 deviates from by design (UI-PM-01). The pack hash is unchanged at end | pack :303 (`a1df34c1…`) |

## 3. Deviations

| # | Level | Deviation |
|---|---|---|
| D-1 | INFO | The device bridge dropped at 07:32 UTC during check 5 and returned at 10:32 UTC. On resume, `uname -s`, `index.lock`, HEAD and the v2 `sha256sum -c` were re-run (all unchanged) and the `/tmp/q173` extracts were intact. Nothing was written before the drop |
| D-2 | INFO | Another lane added `?? docs/records/audits/2026-09/mvp6-q176-pack-layout-patch-01/` during the outage. Not read or used; the pack MOD-0192 hash is unchanged |

## 4. ASSUMPTIONs

- **A1:** "one request each" is judged per resource on reload: 1 get-scenario and 1 get-evaluation (plus the plan GET that always runs).
- **A2:** Test assertions were emulated textually (string contains / regex / slice) with the same start/end markers as the C#; the C# tests run at Q88b.

## 5. No-change verification

- Branch, HEAD, the 32 ` M` paths and the 2 untracked UC-01 files are unchanged.
- This lane added only `?? docs/records/audits/2026-09/mvp6-q173-ver-capacity-v2-01/` (other lane: D-2).
- No `.git/index.lock`, no git write, no `git diff`, no rm in the repo. No edit to the v1/v2 folders, the pack, the decision, `.antigravity` or any ledger.

## 6. Files

`REPORT.md` · `RECHECK.tsv` · `SHA256SUMS`

Agent PASS ≠ CT ACCEPTED — returning to CT.
