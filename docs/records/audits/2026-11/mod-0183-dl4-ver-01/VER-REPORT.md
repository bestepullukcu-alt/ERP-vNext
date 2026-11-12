# MOD-0183 DL-4 — independent VER (read-only-auditor) · 2026-11-05

Lane: read-only-auditor (SOP §17.4 row), worktree-read-only, strict; it did not see CT notes. Scope: commit `e36a5fe06`
(parent `53d05b598`) on branch `feature/sce/mod0183-dl3-01`. Recorded by the MVP-6 CT from the auditor's final message,
layout condensed. Only the status column of the findings is the CT's ("kapandı" = closed by the commit after this record's
parent, with a test); every other word is the auditor's.

**Verdict: DL-4: ACCEPTABLE WITH CONDITIONS.** Conditions: close or accept F-1 and F-2; the Web suite finishes green.

## Per SB row (auditor)

- SB-9 met — customerRef removed by the adapter; `data_mode: server` in pack, `_DataTable.cshtml` and `index.js`; sabotage red.
- SB-10 partly — list correct; Details showed the reference as plain text (F-3); the exception path dropped the reference (F-4).
- SB-11a met — old record marked, shipToReference as stored, no CRM call; sabotage red; node test covers the badge.
- SB-12 met — summary and lines with names; `issueTransactionId` gone; card map = form + POD (owner-declared). Only an Lsku line exercised.
- SB-13 met — localized status in filter, badge and details; notes 1000.
- SB-14 met — checker re-run by the auditor: 102 PASS, 2 FAIL, both declared (no `Edit.cshtml`; POD card).
- SB-15 partly — one resolver per request, semaphore 4, no static state; static-cache sabotage red; no test for the bound (F-5).
- SB-17 met — no `services/**` change; SupplyChain suite on `e36a5fe06` 1027 passed, 1 skipped, 0 failed.
- SB-18 met — no write path changed.
- SB-20 (list/details) partly — adapter output carries no account id; rendered pages in 7 languages not measured; lifecycle-root, carrier and load GUIDs still shown on Details (F-8).
- SB-21 (list/details) met — capturing-logger test on the list; details and export log status and counts only (read, no own test).
- SB-26 (list/details) met — no new keys; every used key non-empty in 7/7.

Export (owner decision 2026-11-05): read permission, tenant and company via the shared request builder, 10,000-row cap with
413, column whitelist (no customerRef), formula guard, XML escape, localized headers; tested. Repo scope clean; no regression
found in other callers.

## Findings

F-1 | ORTA | pack §24.4 asks for list latency in the slice evidence; none recorded for the per-row CRM and location reads | açık | MVP-6 CT | 2026-11-10
F-2 | ORTA | export resolves names for up to 10,000 rows in one request (50 sequential pages, by-id reads at concurrency 4); cost and timeout unmeasured, no multi-page test | açık | MVP-6 CT | 2026-11-10
F-3 | DÜŞÜK | Details shows an unresolved reference as plain text, not the small LTR block of §24.4 (details.js:117-118) | kapandı | MVP-6 CT | 2026-11-10
F-4 | DÜŞÜK | Gate turns a thrown read into unavailable with no reference, so a warehouse or location cell loses it (controller :641) | kapandı | MVP-6 CT | 2026-11-10

CT note on F-4 (measured): the INVENTORY and TRACE gateway clients catch transport errors and return a failed reply, so location and lot reads keep their reference without reaching Gate. The Gate change in `b7776f405` is defence in depth; its sabotage (passing no reference) stays green, so it is not counted as a guard. The SB-10 behaviour itself is tested.
F-5 | DÜŞÜK | the concurrency bound has no test; the token and tenant check covers CRM reads only | kapandı | MVP-6 CT | 2026-11-10
F-6 | DÜŞÜK | page = start / length + 1; a start that is not a multiple of length would misalign the pager (not measured in a browser) | açık | MVP-6 CT | 2026-11-10
F-7 | DÜŞÜK | xlsx cells do not strip XML-illegal control characters; no styles.xml; not opened in a spreadsheet application | kapandı | MVP-6 CT | 2026-11-10
F-8 | DÜŞÜK | Details still shows lifecycleCorrelationId, carrierId and loadId GUIDs as text; not an account id, against the §24.4 aim | açık | MVP-6 CT | 2026-11-10
F-9 | DÜŞÜK | DL-4 writes frontend/Diten.Web.Tests/**, which §5 does not grant (earlier slices did the same) | açık | MVP-6 CT | 2026-11-10

## Not measured (auditor)

Web full suite on `e36a5fe06` (still running at the time); any real browser run (SB-20 rendered, factory `beforeSend`,
export download, Accept-Language headers); the workbook in Excel or LibreOffice; list and export latency; Item, Gsku and
FinishedGood lines; the company-name row shape; pack r9 approval state not re-verified.
