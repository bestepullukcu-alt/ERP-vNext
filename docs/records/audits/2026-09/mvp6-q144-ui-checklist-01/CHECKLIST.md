# Q144 — Pre-Mac UI checklist (UI-PM-01…UI-PM-12) + static run on S&OP, Capacity, Returns

WP Q144 (CT-QUEUE line 170, READY at preflight) · LANE 3 (Cowork, Linux VM, repo via bridge) · frontend-ui-ux + testing-agent · SOP v2.4.
Start 2026-09-27 19:58:10 +03:00 · end 2026-09-27 20:04:39 +03:00 (Istanbul). HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (read from `.git`, no git diff).
Authority: owner decision OD-R6 (Q139 rec. 6: YES, as a separate patch + independent VER), `docs/records/audits/2026-09/mvp6-ct-verdicts-q138-q139-owner-recs-2026-09-27.md` §4.
**Patches only — `.antigravity` is not changed by this WP.** Apply needs its own sign-off, exact-hash apply and VER (Q145).

## Patches

| File | Target (preimage) | Preimage SHA-256 | Postimage SHA-256 | Patch SHA-256 | `patch -p1 --dry-run` on a /tmp copy |
|---|---|---|---|---|---|
| `frontend-ui-ux.patch` | `.antigravity/agents/frontend-ui-ux.md` | `a8ba2cb5a6893a4dd82fdecd136be78e15e6d73c5a05fd2f1e1a7a291503d1fa` | `328e54a5b01c0ec8f988c86a74e7ae2944aa132645ce76643c7db2c7c6f0dac3` | `a5a00fcfec43a6c66b7cb33eeed7e1a09b32b9f9efe1399bc710eee45b06860c` | exit 0; applied copy = postimage; +19/−0 (new section at the end) |
| `test-workflow.patch` | `.antigravity/workflows/test.md` | `db207a9a510f8bf3bad61127a116fba533995289c90ef73961a8797b6a759177` | `0273cb3071b9a4fa10266edf0eb30d30d40313fc9fbc73d5a320b3064ae2b238` | `1c062665549879f4c14911295f3143f87c57ab56d42530bdcdbbdd843072122b` | exit 0; applied copy = postimage; +12/−0 (`/test pre-mac-ui` line + section) |

## Checklist (full text in `frontend-ui-ux.patch`)

| ID | Rule (short) | Defect | Defect source |
|---|---|---|---|
| UI-PM-01 | Only page views set `Layout`; partials never | D-02 | `mvp6-q64b-claims-build-01/README.md:138` |
| UI-PM-02 | Page JS only in the page view's `@section Scripts`; no `<script src>`/`@section` in partials | D-02 | same :138 (6× `main.js`) |
| UI-PM-03 | DataTables `ajax` is never async / never returns a Promise | D-01 | `mvp6-q64b-claims-build-01/README.md:137` |
| UI-PM-04 | Reload after every create / transition / filter / retry | D-01 | same :137; `mvp6-q129-ver-claims-v4-01/REPORT.md:146` |
| UI-PM-05 | Skeleton shown via `display`/`fadeIn`, not only `d-none` (`.backbone-skeleton{display:none}`) | D1 (CU-05) | `mvp6-q64d-claims-runtime-01/README.md:114` |
| UI-PM-06 | `#skeleton-loader` + DtDefaults: first empty draw must not hide the skeleton | D1 (CU-05) | Claims v4 fix `index.js:275-281, :1008` (archive `2b34741a…`); Q129 REPORT:148 |
| UI-PM-07 | Focus moves inside every surface on `shown.bs.*` (incl. row-dropdown openers) | D2 (CU-25) | `mvp6-q64d-claims-runtime-01/README.md:115` |
| UI-PM-08 | Escape never disabled (`keyboard:false`) | D2 (CU-25) | same :115 |
| UI-PM-09 | Focus returns to the opener on `hidden.bs.*` | D4 | `mvp6-q64d-claims-runtime-01/README.md:117` |
| UI-PM-10 | After a rejected submit, `finally` re-enables and re-focuses inside the surface | D5 (Q122-D5, CU-25) | `mvp6-q122-ver-claims-v3-01/REPORT.md:186` (`69c77a23…`); fixed per `mvp6-q129-ver-claims-v4-01/REPORT.md` §5.1 (`a5f91c7d…`) |
| UI-PM-11 | RTL: logical classes only; `dir="ltr"` only on `<bdi>`/ID/number/date inputs | CU-24 / CU-25 (ar) | Q129 REPORT §5.1 (ar run) |
| UI-PM-12 | Load failure → error state + callback `{data: []}`; retry reloads | D1 (CU-05) | Q64d README:114; Q129 REPORT:151 |

Q139 RECOMMENDATIONS row 6 (`077424ca…` package) names the defect classes: focus 3/6 (D2, D4, D5), async/loading 2/6 (D-01, D1), layout 1/6 (D-02). Every class is covered. Q64d-D3 (Enter on Add) is template-owned and outside this checklist.

## CHECK-RUN summary (`CHECK-RUN.tsv`, 36 rows, read-only on the CT-accepted draft archives, extracted to VM /tmp)

| Module | Draft archive (verified) | PASS | FAIL | N/A | FAIL items |
|---|---|---|---|---|---|
| MOD-0190 S&OP | `mvp6-sop-ui-draft-01/sop-ui-draft-overlay.tar.gz` `fcf52d82…` (folder SHA256SUMS 55/55) | 7 | **3** | 2 | UI-PM-01, UI-PM-05, UI-PM-10 |
| MOD-0192 Capacity | `mvp6-capacity-ui-draft-01/capacity-ui-draft-overlay.tar.gz` `dd290891…` (55/55) | 7 | **3** | 2 | UI-PM-01, UI-PM-05, UI-PM-10 |
| MOD-0186 Returns | `mvp6-returns-ui-draft-01/returns-ui-draft-overlay.tar.gz` `35dd1489…` (44/44) | 5 | **7** | 0 | UI-PM-01, UI-PM-03, UI-PM-05, UI-PM-06, UI-PM-07, UI-PM-09, UI-PM-10 |

Evidence paths in `CHECK-RUN.tsv` are relative to `overlay/frontend/Diten.Web/` (`Views/…`) and `overlay/frontend/Diten.Web/wwwroot/assets/` (`js/…`) inside each archive.

## Findings (in scope)

- **F-Q144-1 (HIGH, all three drafts):** every draft repeats D-02. Each has 5 partials with `Layout = "_LayoutTenantShell"` (UI-PM-01), so each would show stacked shells on the Mac, as Claims did.
- **F-Q144-2 (HIGH, Returns):** Returns repeats D-01 (`ajax: loadReturns`, async), which breaks every list reload. It also repeats D1 (UI-PM-05/06), D2 (UI-PM-07) and D4 (UI-PM-09). It was cut from the Claims pattern before the Claims fixes.
- **F-Q144-3 (MEDIUM, all three):** each repeats the Q122-D5 pattern (UI-PM-10): the submit is disabled while pending, and `finally` re-enables it without restoring focus.
- **F-Q144-4 (MEDIUM, S&OP + Capacity):** the Details section skeletons can never become visible (UI-PM-05): they toggle only `d-none` on `.backbone-skeleton`.
- The drafts were not changed. The fixes belong to the draft owners before Q84b / Q88b / Q65b (OD-R6).

## ASSUMPTIONs

- **A1:** The checklist runs on the CT-accepted draft archives, not on the open `overlay/` folders (D6: records keep draft code as `.tar.gz`). The archive hashes match the CT records.
- **A2:** A FAIL means the defect pattern is present in the source. Runtime confirmation is the Mac §32.11 run, which the checklist does not replace (Q139 rec. 6 risk).
- **A3:** The checklist text is in English, and the `test.md` section is in Turkish to match that file's language.
- **A4:** Q88c (Capacity IDs in the address, HELD) was not applied here; the Capacity run uses the Q88 archive `dd290891…`.

Agent PASS ≠ CT ACCEPTED — returning to CT.
