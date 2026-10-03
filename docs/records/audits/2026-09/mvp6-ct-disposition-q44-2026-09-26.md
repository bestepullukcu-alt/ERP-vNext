# CT disposition — Q44 self-registration patches (2026-09-26)

Recorded by: Control Tower, 2026-09-26T09:21+0300. New record (K4).
Base: feature/mvp6-logistics @ 4a8d4d4b; no commit, push or stash. Authority: mvp6-self-registration-design-owner-decision-01.md (D1–D5 = A).

| Check | Result |
|---|---|
| Output | `docs/roadmap/plans/mvp6-self-registration-patches-01/` — SHA256SUMS 11/11 OK (checked from the folder); SHA256SUMS sha256 `2acd3241cffe1dcfdcc03c1d39cc9acc6912541db6c76f782dea0ebeca822230` |
| Apply check | CT re-ran `git apply --check` on the live repository: 6/6 exit 0 |
| Base files | All six targets still match "Before" hashes in BASE-HASHES.tsv (DCP-009, MOD-0183…0187); none edited |
| Scope | Append-only sections (308 added lines in total); existing permission keys only; D3 names; D4 ship rule; D5 ForTarget; gaps carried open |
| Conflict | Patch 5 (MOD-0186) and the Returns sign-off (Q39) share base `07a8a015…`. Only one can apply first; the other must be rebased. CT order: Q39 first, then rebase patch 5 |
| Tree | Tracked diff still 14 files; git lock removed by owner |
| Effort | No credit (pack preparation). AG-01 stays OPEN until code. |

Result: **DONE (prepared)**. Sign-off → new queue item Q45 (DECISION-REQUIRED).

Runtime lanes Q04, Q24, Q25: no output folders at this time.
