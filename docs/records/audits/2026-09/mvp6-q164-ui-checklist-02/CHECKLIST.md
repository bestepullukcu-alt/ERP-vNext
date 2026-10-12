# Q164 — Pre-Mac UI checklist r2 (UI-PM-01…UI-PM-12) + static run on S&OP, Capacity, Returns and Claims v4

WP Q164 (CT-QUEUE line 213, READY at preflight) · LANE 3 (Cowork, Linux VM, repo via bridge; the Q144 writer chat) · frontend-ui-ux + testing-agent · SOP v2.4.
Start 2026-09-28 10:55:39 +03:00 · end 2026-09-28 11:13:42 +03:00 (Istanbul). HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (read from `.git`; no git diff).
Revises Q144 (`mvp6-q144-ui-checklist-01/`, SHA256SUMS `18edd238…`, unchanged, K4) after Q145 (`mvp6-q145-ver-q144-01/REPORT.md` `4cefb057…`).
Authority: CT-3 and OD-F-Q145-1 in `mvp6-ct-verdicts-q157-q142-q145-2026-09-28.md:40–41` (`39075081…`); owner reading of "stored opener" for UI-PM-09 (question tool, this chat, 28 Sep: "stable opener").
**Patches only; `.antigravity` is not changed.** Next: delta VER (Q165) → sign-off → exact-hash apply.

## Patches (against the same preimages as Q144)

| File | Target | Preimage SHA-256 | Postimage SHA-256 | Patch SHA-256 | `patch -p1 --dry-run` on a /tmp copy |
|---|---|---|---|---|---|
| `frontend-ui-ux.patch` (r2) | `.antigravity/agents/frontend-ui-ux.md` | `a8ba2cb5a6893a4dd82fdecd136be78e15e6d73c5a05fd2f1e1a7a291503d1fa` | `90247ddc689b1e06ab02ba33c9dd379ded45881f1c58fade0971c7ec3a78f044` | `4ce84c427996246fbf7b5cd867c7777d52327e5648f7ebcdc0f65bb62e0ffcd6` | exit 0; applied copy = postimage; +19/−0 |
| `test-workflow.patch` (unchanged from Q144, byte-identical) | `.antigravity/workflows/test.md` | `db207a9a510f8bf3bad61127a116fba533995289c90ef73961a8797b6a759177` | `0273cb3071b9a4fa10266edf0eb30d30d40313fc9fbc73d5a320b3064ae2b238` | `1c062665549879f4c14911295f3143f87c57ab56d42530bdcdbbdd843072122b` | exit 0; applied copy = postimage; +12/−0 |

## Checklist r2 (full text in `frontend-ui-ux.patch`; changes vs Q144 in bold)

| ID | Rule (short) | Defect | Defect source |
|---|---|---|---|
| UI-PM-01 | Only page views set `Layout`; partials never | D-02 | `mvp6-q64b-claims-build-01/README.md:138` |
| UI-PM-02 | Page JS only in the page view's `@section Scripts`; no `<script src>`/`@section` in partials | D-02 | same :138 |
| UI-PM-03 | DataTables `ajax` is never async / never returns a Promise | D-01 | `mvp6-q64b-claims-build-01/README.md:137` |
| UI-PM-04 | Reload after every create / transition / filter / retry | D-01 | same :137; `mvp6-q129-ver-claims-v4-01/REPORT.md:146` |
| UI-PM-05 | Skeleton shown via `display`/`fadeIn`, not only `d-none` | D1 (CU-05) | `mvp6-q64d-claims-runtime-01/README.md:114` |
| UI-PM-06 | `#skeleton-loader` + DtDefaults: first empty draw must not hide the skeleton | D1 (CU-05) | **Claims v4 fix `index.js:275-281` (comment :275, code :278-281) and `:1008`** (archive `2b34741a…`); Q129 REPORT:148 |
| UI-PM-07 | Focus moves inside on `shown.bs.*` **for surfaces opened from a row menu or with no focused opener**; surfaces opened by a focused button are exempt | D2 (CU-25) | `mvp6-q64d-claims-runtime-01/README.md:115`; **scope OD-F-Q145-1** |
| UI-PM-08 | Escape never disabled (`keyboard:false`) | D2 (CU-25) | same :115 |
| UI-PM-09 | **Every surface opened from a stable opener (toolbar/page control, e.g. Add) stores it and returns focus to it on `hidden.bs.*`; row-opened surfaces are exempt** | D4 | `mvp6-q64d-claims-runtime-01/README.md:117`; **scope OD-F-Q145-1 (stable-opener reading)** |
| UI-PM-10 | After a rejected submit, `finally` re-enables and re-focuses inside the surface; **submits reached through `window.showConfirm` are exempt** | D5 (Q122-D5, CU-25) | `mvp6-q122-ver-claims-v3-01/REPORT.md:186`; Q129 REPORT §5.1; **scope OD-F-Q145-1** |
| UI-PM-11 | RTL: logical classes only; `dir="ltr"` only on `<bdi>`/ID/number/date inputs | **preventive (no defect instance; CU-24/CU-25 ar PASS)** | Q122 REPORT:137; Q129 REPORT:128-138 |
| UI-PM-12 | Load failure → error state + callback `{data: []}`; retry reloads | D1 (CU-05) | Q64d README:114; Q129 REPORT:151 |

## CHECK-RUN r2 summary (`CHECK-RUN.tsv`, 48 rows = 4 modules × 12, read-only on the CT-accepted archives, extracted to VM /tmp)

| Module | Archive | PASS | FAIL | N/A | FAIL items |
|---|---|---|---|---|---|
| MOD-0187 Claims v4 (reference) | `mvp6-claims-ui-draft-04/claims-ui-draft-overlay-v4.tar.gz` `2b34741a…` | **12** | 0 | 0 | — (required 12/12: met) |
| MOD-0190 S&OP | `mvp6-sop-ui-draft-01/sop-ui-draft-overlay.tar.gz` `fcf52d82…` | 6 | **3** | 3 | UI-PM-01, UI-PM-05, UI-PM-10 |
| MOD-0192 Capacity | `mvp6-capacity-ui-draft-01/capacity-ui-draft-overlay.tar.gz` `dd290891…` | 6 | **3** | 3 | UI-PM-01, UI-PM-05, UI-PM-10 |
| MOD-0186 Returns | `mvp6-returns-ui-draft-01/returns-ui-draft-overlay.tar.gz` `35dd1489…` | 5 | **7** | 0 | UI-PM-01, UI-PM-03, UI-PM-05, UI-PM-06, UI-PM-07, UI-PM-09, UI-PM-10 |

The draft FAIL set is the same 13 as Q144 (reproduced by Q145). The narrowed rules change only S&OP/Capacity UI-PM-07 (PASS → N/A, since every surface has a focused-button opener) and the scope text of the UI-PM-10 FAILs (S&OP sign-off and Returns transition are now exempt; the direct create/capture submits still fail).
Evidence paths are relative to `overlay/frontend/Diten.Web/` (`Views/…`) and `overlay/frontend/Diten.Web/wwwroot/assets/` (`js/…`) inside each archive.

## ASSUMPTIONs

- **A1:** "Row menu" means a dropdown menu inside a table row (`.dropdown-item`). A row **button** such as QuickView (`js-quick-view`) is a focused opener, so it is exempt from UI-PM-07, and as a row control it is also exempt from UI-PM-09.
- **A2:** UI-PM-09 "stored opener" follows the owner's reading given in this chat (28 Sep): a stable toolbar/page control whose opener can be stored. A surface opened from it must store the opener and return focus to it. The literal alternative ("only where code already stores an opener") would have made Returns create N/A and dropped the D4 pattern.
- **A3:** A module's item is N/A when no surface is in the item's scope. For S&OP and Capacity UI-PM-07, the focus-on-open listeners exist anyway and are cited.
- **A4:** `dt-defaults.js` and `backbone-custom.css` were read from the working tree, as in Q144 and Q145.

Agent PASS ≠ CT ACCEPTED — returning to CT.
