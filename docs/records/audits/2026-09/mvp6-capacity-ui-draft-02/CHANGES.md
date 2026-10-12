# Q159 — MOD-0192 Capacity UI draft overlay v2 (checklist r2 FAIL fixes + Q88c)

WP Q159 · Prompt Q159 v1 · Template T1 v1 (SOP v2.5 §36.2) · LANE 3 (Cowork, Linux VM, repo via bridge) · frontend-ui-ux (draft overlay writer).
Start 2026-09-28 13:17:15 +03:00 · end 2026-09-28 13:24:12 +03:00 (Istanbul). Branch `feature/mvp6-logistics`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (read from `.git`; no git diff).
**DRAFT — not built, not run.** The Mac build (Q88b) cites BASE-STACK v2 `ce8d60ab…`.

## Inputs (all verified before work)

| Input | Hash / result |
|---|---|
| v1 draft `mvp6-capacity-ui-draft-01/` | SHA256SUMS `f4e18ab9…` 55/55; archive `dd290891d67eb1ccb6943acd25f855902b86b00af6384d0ec43525e1ee162818` |
| UI checklist r2 `mvp6-q164-ui-checklist-02/` (live in `.antigravity` since Q170) | SHA256SUMS `a9024ac5…` 5/5 |
| Q165 VER `mvp6-q165-ver-q164-01/` | SHA256SUMS `c3c56ec6…` 2/2 |
| Claims v4 reference `mvp6-claims-ui-draft-04/` | SHA256SUMS `cc3f4c7f…` 4/4; archive `2b34741a…` |
| Q88c decision `docs/records/decisions/2026-09/mvp6-capacity-ui-ids-in-address-owner-decision-01.md` | `52228ea78f45f55b9e2377f4a9c471c6e6175a0e908b33d19727d93ecf34fd4a` |
| Pack MOD-0192 | `a1df34c15abff77b291b4a4ca3c79e8e3f775e6c671c76bedbd24b291b55593b` |

## Output

`capacity-ui-draft-overlay-v2.tar.gz` — `48b60b782f1bbd84a74a95c3cbe01f90aec9d0062c528d748726b266db1028b3`. It has 47 files, the same set as v1, and 9 of them changed (`FILE-PLAN.tsv`). It was built with the v1 recipe (`tar --sort=name --mtime=@0 --owner=0 --group=0 --numeric-owner --mode=u=rwX,go= --format=gnu | gzip -n -9`). That recipe re-creates the v1 archive byte-for-byte (`dd290891…`). Extracting the v2 archive gives the v2 tree exactly.

## Changes (each with its source line)

| # | Change | Files | Source |
|---|---|---|---|
| C1 | **UI-PM-01:** the `@{ Layout = "_LayoutTenantShell"; }` block is removed from the 5 partials and replaced by a Razor comment. The page views keep their Layout (Index.cshtml:10, Details.cshtml:11). | `_CreatePlanOffcanvas`, `_CreateScenarioOffcanvas`, `_DetailsL10n`, `_EvaluateOffcanvas`, `_IndexL10n` | checklist r2 UI-PM-01 (live `.antigravity/agents/frontend-ui-ux.md`); Q64b D-02 (`mvp6-q64b-claims-build-01/README.md:138`); Claims v4 `ClaimFormContractTests.cs:169–174` |
| C2 | **UI-PM-05:** `setSectionState` sets `skeleton.style.display` (`block`/`none`) as well as the `d-none` toggle. | `details.js:206–215` | checklist r2 UI-PM-05; Q64d-D1 (`mvp6-q64d-claims-runtime-01/README.md:114`); Claims v4 `index.js:278–281` |
| C3 | **UI-PM-10:** a new `restorePanelFocus`. In the `finally` block, after the submit is re-enabled, focus moves to the first invalid field, else the submit, else the first field, but only while the panel is still open and not hiding. No Capacity submit goes through the shared confirm, so none is exempt. | `index.js:201–209, :250–253`; `details.js:597–607, :668–672` | checklist r2 UI-PM-10; Q122-D5 (`mvp6-q122-ver-claims-v3-01/REPORT.md:186`); Claims v4 `index.js:179–188, :573–577` |
| C4 | **Q88c — scenario and evaluation IDs in the Details address.** Details below. | `details.js:17–21, :343–354, :400–405, :517, :970–994, :1006` | see below |
| C5 | **Tests:** the Layout theory is now "page views state the shell, partials must not". Three static facts were added for C2, C3 and C4. | `CapacityPlanFormContractTests.cs`, `CapacityPlanDetailsBehaviorTests.cs` | Claims v4 `ClaimFormContractTests.cs:166–176` and `ClaimIndexBehaviorTests.cs:152–158, :183–203` (pattern) |

### C4 — how each Q88c element matches its source line

| Q88c element (decision / pack) | Source line | Implementation |
|---|---|---|
| The scenario ID and evaluation ID are carried in the Details page address (route or query); a reload or shared link reopens the same view | decision `…-owner-decision-01.md:9–10`; pack MOD-0192:321 (optional query `?scenarioId={uuid}&evaluationId={uuid}`) | query parameters `scenarioId` / `evaluationId`; `openFromAddress()` reads them at init (`details.js:970–994`, called at `:1006`) |
| When a scenario is opened or created and when an evaluation is submitted or opened, the page writes its ID into the query with `history.replaceState` | pack MOD-0192:337–338 | `writeAddress()` (`:343–352`, `replaceState` at `:350`). Scenario write on every successful `loadScenario`, which covers open-by-ID, create read-back and retry (`:403`). Evaluation write on every successful `loadEvaluation`, which covers submit read-back, reopen and refresh (`:517`). |
| Reopens through the get-scenario and get-evaluation adapters (one request each) | pack MOD-0192:338–339; CP-21 (`:464`) | the address scenario is loaded first; the address evaluation is loaded once after it (`addressEvaluationId`, `:354`, `:400–405`); no new request type (still 2 `fetch(`) |
| Each value must be a UUID; an invalid value is ignored with a localized message and sends no request | pack MOD-0192:339–340; CP-23 (`:466`) | `isId()` gate; invalid `scenarioId` → the existing localized `ScenarioIdInvalid` field error on the Open-scenario input; invalid `evaluationId` → evaluation panel error with the localized `ErrInvalidRequest`, no retry, no request (`:982–992`); the plan always loads (`loadPlan()` first) |
| Unknown, foreign-scope or soft-deleted IDs give the safe-not-found of §23.8 | pack MOD-0192:340; CP-22 (`:465`) | unchanged: the existing per-resource 404 handling of `loadScenario` / `loadEvaluation` applies to address IDs |
| No new endpoint, field, permission or route segment; no browser storage; manifest RoutePath unchanged | decision `:12`; pack MOD-0192:341 | query only; `replaceState` (no `pushState`); the storage-free test still applies; no controller, route, manifest, resx or L10n key change |
| Session memory gap resolved | pack MOD-0192:488 (§23.13) | header comment `details.js:17–21` rewritten (the v1 text said nothing is written to the URL) |

## Checklist r2 on v2 (`CHECK-RUN.tsv`)

9 PASS · 3 N/A (UI-PM-03 no `ajax` option; UI-PM-06 no `#skeleton-loader`; UI-PM-07 no row-menu or code-opened surface) · **0 FAIL**. The v1 FAILs were UI-PM-01, UI-PM-05 and UI-PM-10.

## Static checks run in this lane (no build or test)

- `node --check` on `details.js` and `index.js`: syntax OK.
- The literal assertions of the 3 unchanged test files still hold against v2, except the ones deliberately changed in C5.
- 60 assertions from the new and changed tests were emulated in Python against the v2 sources: 0 false. This includes the existing test-file bans on `search=`, `?page`, storage APIs and timers.

## ASSUMPTIONs

- **A1:** UI-PM-10 order follows the live checklist r2 text: `finally` re-enables the submit and then restores focus, as Claims v4 does. The prompt says "focus returns inside the panel before re-enable". Doing it in that order would put focus on a still-disabled button.
- **A2:** Invalid address IDs reuse existing localized keys (`ScenarioIdInvalid`, `ErrInvalidRequest`), so there is no new L10n key and no resx change (§17.4: L10n keys unchanged).
- **A3:** When a different scenario opens, its evaluation panel is cleared (unchanged v1 behaviour). The address then drops `evaluationId`, unless the session or the address supplies one for that scenario. Invalid values are dropped from the address on the next successful write.
- **A4:** An address `evaluationId` without a `scenarioId` opens the evaluation panel alone. The pack does not require both.

## Findings (in scope)

- **F-Q159-1 (MEDIUM, pack text):** MOD-0192 §23.2 (pack line 303) still says "every Capacity `.cshtml` states `Layout = "_LayoutTenantShell";` explicitly". That is the wording that produced the partial Layouts (D-02), and v2 now deviates from it by design (UI-PM-01). A pack text correction like Q114 P1 for MOD-0187 is needed.

Agent PASS ≠ CT ACCEPTED — returning to CT.
