# CT verdicts — 2026-09-26 (Q64a, Q77a, Q78) — recorded by AL-MVP6-Q79-REC-01 (Q79, chat lane on the Linux VM bridge) at 2026-09-26T17:30:43+0300 on CT instruction; CT writes no files

## Verdict text (verbatim from the CT instruction to Q79)

> - Q64a Claims UI draft overlay: CT ACCEPTED as DRAFT (not writer-complete). Independent CT check: SHA256SUMS 45/45, archive bc0f5819…, 46 files, tracked diff unchanged (19 files), no index.lock.
>   F1: The Claims Phase 1.5 (PH15-UI-187) owner approval was given in the CT session on 2026-09-26 but never recorded. Record it now (Step A.3).
>   F2: The owner decisions "modules first" and "draft overlays" (~16:17) take precedence over the pack §32.14 wording "UI code not authorized". The wording is corrected in the next pack revision of 0187 (not in this task).
>   F3: Accepted. The Q64b isolated-environment recipe = HEAD archive + A12 360 overlay + the Claims draft archive.
>   F6: The transition inputs in an offcanvas in the owned Index.cshtml, with final confirmation via showConfirm, are accepted.
>   F8: Accepted. The lifecycle root never reaches the browser; the adapter substitutes the trace id and logs the pair.
>   F11: The pack name ClaimsManagementManifestProvider is correct.
>   F12: Localizing all 18 codes and using _shared-integration/ is accepted.
>   F4/F5/F7/F9/F10/F13: noted, no change; re-check in Q64b.
> - Q77a Loads uptake + F-1 draft overlay: CT ACCEPTED as DRAFT (not writer-complete). SHA256SUMS 19/19.
>   A-01: 409 CORRELATION_ROOT_MISMATCH for a missing or unusable stored root is accepted (existing contract code).
>   A-02: Only the strict 8-4-4-4-12 lexical rule counts as a stored root.
>   FN-02: 500→409 for bad stored roots is the expected consequence of the owner decision "F-1: Uptake ile birlikte düzelt".
>   A-07: F-1 estimate +1.5/3/5 h is recorded as an agent estimate.
> - Q78 S&OP/Capacity UI scope: CT ACCEPTED as analysis. SHA256SUMS 7/7. CT confirmed that the contract has create-only on /sandop-plans and /capacity-plans (no list ops).

## Hash binding (re-checked by this lane at recording time)

| Item | File | sha256 | Re-check |
|---|---|---|---|
| Q64a | `docs/records/audits/2026-09/mvp6-claims-ui-draft-01/SHA256SUMS` | `80da27c94c6e8a124e4636e679f95b39ef7e67ad5ea489a097e84d801df608d3` | 45/45 OK (from the folder) |
| Q64a | `docs/records/audits/2026-09/mvp6-claims-ui-draft-01/claims-ui-draft-overlay.tar.gz` | `bc0f5819de94273ebca162ed6ede2a62555f888f314c799c27e56a1c75b82e60` | = `bc0f5819…` as stated; folder holds 46 files |
| Q77a | `docs/records/audits/2026-09/mvp6-loads-uptake-draft-01/SHA256SUMS` | `8c472c27c93cb4c506470572d59528d83af67bcbe75fa4460a75960ddb2c92b2` | 19/19 OK (from the folder) |
| Q78 | `docs/roadmap/plans/mvp6-ui-scope-190-192-01/SHA256SUMS` | `83c6b7e96331e246cacc9f75890b8282c32fb6bda066b2d504d66e22e0f1e985` | 7/7 OK (from the folder) |
| Tracked diff | `git diff --stat HEAD` | — | `19 files changed, 6704 insertions(+), 316 deletions(-)` |

"CT ACCEPTED as DRAFT" does not make Q64a or Q77a writer-complete: build, tests and runtime follow on the local Mac (Q64b, Q77b).
"CT ACCEPTED as analysis" (Q78) approves no decision text inside that package; the owner's UI-scope answers are recorded
separately in `docs/records/decisions/2026-09/mvp6-sop-capacity-ui-scope-owner-decision-01.md`, and F1 is recorded in
`docs/records/decisions/2026-09/mvp6-claims-ui-ph15-owner-decision-01.md`. F2 (pack §32.14 wording of MOD-0187) is left for the
next MOD-0187 pack revision; this record does not change the pack.
