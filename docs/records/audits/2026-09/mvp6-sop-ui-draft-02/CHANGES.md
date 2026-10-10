# Q158 — MOD-0190 S&OP UI draft overlay v2 (fixes UI-PM-01, UI-PM-05, UI-PM-10 + O-3)

**Agent verdict: DRAFT v2 written (archive only). Not built, not tested, not applied.** Agent PASS ≠ CT ACCEPTED.

## Metadata (SOP v2.5 §17.1)

| Field | Value |
|---|---|
| WP / Prompt / Template | Q158 · Prompt Q158 v1 · T1 v1 (SOP v2.5 §36.2) |
| CT-QUEUE row | line 234: `Q158 · S&OP draft UI fix (FAIL 01/05/10; O-3 note) · READY · frontend-ui-ux · Q169` (earlier rows 204, 215 for the same ID are superseded by the latest) |
| Lane / type / agent | Cowork LANE 2 (Linux VM; `uname -s` = Linux) / DEV (archive-only draft) / frontend-ui-ux |
| Module / pack | MOD-0190 S&OP workflow/sign-offs · pack `ae1be96954a84710d1adec9daa8e08ad76e0a40a799cd177804f1bde3dd804aa` |
| Branch / HEAD | `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched) |
| Preflight | 2026-09-28T10:07:33Z; no `.git/index.lock`; `git status --porcelain` only: 30 ` M` + 511 `??` = known baseline |
| Base Stack | n/a (draft only; the Mac build cites BASE-STACK-v2.md `ce8d60ab…`) |
| Inputs (all `sha256sum -c` OK) | v1 `mvp6-sop-ui-draft-01/` SHA256SUMS `0aafc340…` 55/55, archive `fcf52d82…`; checklist r2 `mvp6-q164-ui-checklist-02/` `a9024ac5…` 5/5; VER `mvp6-q165-ver-q164-01/` `c3c56ec6…` 2/2 (O-3 at REPORT.md:74); reference Claims v4 `mvp6-claims-ui-draft-04/` `cc3f4c7f…` 4/4, archive `2b34741a…` |
| Output | `sop-ui-draft-overlay-v2.tar.gz` `7a9666c7ee1dfab4f85c1324db38ab06ed87ed1c385de706629e60ed68e394e3` (47 files, same file set as v1; 11 changed) |

Paths below are inside the archive, under `overlay/frontend/Diten.Web/` (views) and `…/wwwroot/assets/` (JS).
Unchanged from v1 by design: area, module, golden_reference, shell/layout name of page views, columns, filters, L10n keys, pattern.

## Fix 1 — UI-PM-01: partials set no Layout

- **Finding:** Q164 CHECK-RUN.tsv, S&OP UI-PM-01 FAIL. Five partials set `Layout = "_LayoutTenantShell"` at line 7. Defect D-02 (`mvp6-q64b-claims-build-01/README.md:138`): a Layout in a partial renders a second complete tenant shell.
- **Change:** in `_CaptureSnapshotOffcanvas`, `_CreateOffcanvas`, `_DetailsL10n`, `_IndexL10n` and `_RecordSignOffOffcanvas`, lines 5–8 now hold a comment only (the Claims v4 wording). The page views `Index.cshtml:10` and `Details.cshtml:11` still state `"_LayoutTenantShell"`. The line count is unchanged.
- **Test:** `Diten.Web.Tests/Forms/SandopPlanFormContractTests.cs`. The v1 theory `Every_view_states_the_tenant_shell…` required the defect. It becomes `Page_views_state_the_tenant_shell_partials_do_not_and_no_view_has_an_inline_handler`, as in Claims v4 `ClaimFormContractTests.cs:167–175`. The inline-handler and `/Platform` checks stay.
- **Source:** checklist r2 UI-PM-01 (`CHECKLIST.md:20`); Claims v4 partial header (e.g. `Views/SupplyChain/Claims/_CreateEditOffcanvas.cshtml:6–8`).

## Fix 2 — UI-PM-05: skeletons become visible

- **Finding:** Q164 CHECK-RUN.tsv, S&OP UI-PM-05 FAIL. `details.js:189` (v1) only toggled `d-none` on `.backbone-skeleton`, and the shared rule is `display:none` (`css/backbone-custom.css:353`). Defect D1 (CU-05).
- **Change:** in `js/SupplyChain/SandopPlans/details.js:189–195`, `setSectionState` sets `skeleton.style.display = 'block' | 'none'` besides the `d-none` toggle. This is the Claims v4 `setListState` approach (`Claims/index.js:275–281`). No jQuery `stop()` is needed, because DtDefaults does not fade these skeletons (UI-PM-06 N/A).
- **Test:** `SandopPlanDetailsBehaviorTests.Section_skeletons_own_their_display_not_only_d_none`.

## Fix 3 — UI-PM-10: focus returns inside the panel after a rejected direct submit

- **Finding:** Q164 CHECK-RUN.tsv, S&OP UI-PM-10 FAIL. The create plan submit (`index.js` finally :230–232, v1) and the capture submit (`details.js` finally :501–503, v1) re-enabled the button without restoring focus. Defect D5 (Q122-D5).
- **Change in `index.js`:** `submitCreate` finally (:231–235) calls `restoreCreateFocus()` **before** `submit.disabled = false`. The helper (:238–247) acts only while the surface is open and not hiding, and only if focus is not already inside it. It focuses the first invalid field, else `planName`.
- **Change in `details.js`:** `postIntent` finally (:535–541) calls `restorePanelFocus(panelId, 'captureDemandPlanId')` for kind `capture`, before re-enabling. The helper (:462–471) follows the same rules.
- **Exempt:** the record sign-off submit reached through `window.showConfirm` (`details.js:687`) is exempt under OD-F-Q145-1 and is unchanged.
- **Pattern:** Claims v4 `restoreCreateFocus` (`Claims/index.js:180–188`, called in finally :573).
- **Test:** `SandopPlanDetailsBehaviorTests.Rejected_direct_submits_return_focus_inside_the_panel_before_re_enabling` checks the order (focus before re-enable) and the capture-only scope.

## Fix 4 — O-3: on a 403, focus does not fall to BODY

- **Finding:** Q165 REPORT.md:74. On a 403, S&OP removed the opener right after hiding the panel (`details.js:454`, v1), so the `hidden` handler focused a removed id and focus fell to BODY. `index.js` had the same pattern for the create CTA (v1 :243–244, :301–304).
- **Change in `details.js`:**
  - `deniedOpeners` and `focusStableIfLost` (:453–460).
  - The 403 branch (:477–491) marks the opener as denied and hides the panel, but does **not** remove the opener.
  - The hidden handler (:739–750) moves focus to `#planTitle` when the opener is denied, removed or hidden, and only then removes a denied opener. If the panel is not open at the time of the 403, the title is focused and the opener removed at once.
- **Change in `index.js`:** the `createDenied` flag (:56), `focusStableIfLost` (:249–254), the 403 branch (:261–274) and the hidden handler (:329–339) do the same with `#sandopPlansTitle`.
- **Views:** `Details.cshtml:25` `#planTitle` gets `tabindex="-1"`. `Index.cshtml:27` title gets `id="sandopPlansTitle" tabindex="-1"`.
- **Unchanged:** focus that is already on another element is left alone. For example, `closeWorkspace` focuses `#planAlert` (`details.js:244`), and that focus is not moved.
- **Test:** `SandopPlanDetailsBehaviorTests.Denied_openers_are_removed_only_after_focus_moves_to_the_page_title`.

## ASSUMPTIONs

- **A1 (pack text vs UI-PM-01):** MOD-0190 pack §23.2 (`MOD-0190-sop-workflow-signoffs.md:294`, pack `ae1be969…`) says every S&OP `.cshtml` states `Layout = "_LayoutTenantShell";`. That contradicts UI-PM-01 and this prompt ("partials must NOT set a Layout"). The prompt and checklist r2 were followed. Claims had the same conflict, closed by a pack text patch (Q114 P1, "layout rule (page views only)"). The MOD-0190 §23.2 wording stays as it is (the pack is protected) and is reported for CT.
- **A2 (UI-PM-10 order):** the prompt asks for focus inside the panel *before* the button is re-enabled. Claims v4 re-enables first and then refocuses, so it may land on Save. v2 therefore focuses the first invalid field, else the first field, and never the disabled button, before re-enabling. This meets both the prompt and checklist r2 ("finally re-enables and re-focuses inside the surface").
- **A3 (O-3 scope):** the fallback to the page title also covers an opener that is absent or hidden (`d-none` via `syncActions`) when the panel closes. It only acts when focus would otherwise be on BODY or inside the closed panel, so there is no other behaviour change. `index.js` got the same fix because it had the same 403 pattern.
- **A4:** a Playwright scenario for these fixes is not part of this WP's output list. The v1 `runtime-scenarios/` folder is outside the archive and unchanged. Runtime proof belongs to the Mac build (Q84b).
- **A5:** `css/backbone-custom.css` was read from the working tree, as in Q164/Q165 (A4 there).

## Static checks run in this lane (no build, no test run)

- `node --check` on v2 `index.js` and `details.js`: syntax OK. This is a parse only; nothing was executed.
- The v2 archive re-extracted and compared with `diff -rq` against the v2 tree: identical. 47 files, 0 AppleDouble members, owner 0/0, modes 0644/0755.
- `diff -rq` v1 vs v2: exactly the 11 files in `FILE-PLAN.tsv`.
- `grep "Layout = "` in the S&OP views: only `Index.cshtml:10` and `Details.cshtml:11`.
- `CHECK-RUN.tsv`: 12 items on v2 → 9 PASS, 3 N/A (UI-PM-03, -06, -07, with reasons), **0 FAIL**.
