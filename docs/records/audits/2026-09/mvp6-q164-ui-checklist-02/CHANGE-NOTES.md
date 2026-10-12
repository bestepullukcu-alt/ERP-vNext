# Q164 — change notes vs Q144 (every change → finding)

| # | Artifact | Change vs Q144 | Finding / decision |
|---|---|---|---|
| R1 | `frontend-ui-ux.patch` row UI-PM-07 | Scope narrowed: only surfaces opened from a row menu or with no focused opener; surfaces opened by a focused button are exempt (Bootstrap focus trap). The static check lists each surface's opener first; N/A when no surface is in scope. | F-Q145-1 → OD-F-Q145-1 (A) |
| R2 | `frontend-ui-ux.patch` row UI-PM-09 | Scope: every surface opened from a stable opener (toolbar/page control) stores and restores it; row-opened surfaces are exempt | F-Q145-1 → OD-F-Q145-1 (A); owner reading "stable opener" (28 Sep, this chat) |
| R3 | `frontend-ui-ux.patch` row UI-PM-10 | Exemption: submits reached through `window.showConfirm`; the static check adds `grep -n "showConfirm"` | F-Q145-1 → OD-F-Q145-1 (A) |
| R4 | `frontend-ui-ux.patch` row UI-PM-11 | Defect column → "preventive (no defect instance; CU-24/CU-25 ar PASS)" | F-Q145-2 |
| R5 | `CHECKLIST.md` UI-PM-06 source | → Claims v4 `index.js:275-281` (comment :275, code :278-281) and `:1008` | F-Q145-3 |
| R6 | `CHECK-RUN.tsv` S&OP UI-PM-07 | Citation → generic listener `details.js:695-701` (was `:701`); result N/A under R1 (all openers are focused page buttons) | F-Q145-3; R1 |
| R7 | `CHECK-RUN.tsv` UI-PM-07/09/10 rows (S&OP, Capacity, Returns) | Re-evaluated under R1–R3 with opener/exemption evidence. Result changes: S&OP and Capacity UI-PM-07 PASS → N/A. All 13 FAILs unchanged. | R1–R3 |
| R8 | `CHECK-RUN.tsv` Defect column | UI-PM-07/09/10 add "scope OD-F-Q145-1"; UI-PM-11 → preventive | R1–R4 |
| R9 | `CHECK-RUN.tsv` new column block | Claims v4 (`2b34741a…`) × 12 = 12 PASS (required by OD-F-Q145-1) | OD-F-Q145-1 |
| — | `test-workflow.patch` | Unchanged (byte-identical, `1c062665…`) | no finding touches `test.md` |
| — | Other rows UI-PM-01…06, 08, 12 | Text unchanged | — |

Unchanged inputs: preimages `a8ba2cb5…` / `db207a9a…`; draft archives `fcf52d82…` / `dd290891…` / `35dd1489…`; Q144 and Q145 folders untouched (K4).
