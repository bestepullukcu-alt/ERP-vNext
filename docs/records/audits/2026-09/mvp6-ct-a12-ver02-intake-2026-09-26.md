# CT intake — A12 runtime independent VER attempt 2 (Q04) — 2026-09-26

Recorded by CT 2026-09-26T10:58+0300. New record (K4). **Intake only — not the CT disposition.** Per CT SOP and owner decision Q49, an independent check of this evidence (Q55) precedes the CT decision.

Writer: Terminal runtime lane (Claude Code on the Mac), 10:08:28–10:53:00 Istanbul, ~46 min agent run, no owner waits.
Evidence: `docs/records/audits/2026-09/mvp6-shipment-a12-runtime-independent-ver-02/` — ARTIFACTS.sha256 **187/187 OK** (CT, from the folder).

## Lane result (as reported)
All A12 criteria PASS (normal detail; cross-LE en/tr/ar; unknown; soft-deleted; hidden/inert surfaces; late async; 404 + correlation; zero writes 17 pairs; data/API isolation); negative controls PASS (pre-A12 details.js fails the same detectors); A08 422, A09 409, A09 422 regressions PASS; 16 PNG via Playwright `page.screenshot({path})` with sha256. Accessibility and A03/PRES-183-02 inherited (not counted). A10 NOT RUN (unauthorised).

## CT checks now
| Check | Result |
|---|---|
| Checksums | 187/187 OK |
| Tracked diff | 17 files, same set as before the run (includes `.claude/settings.local.json` changed by the security records); no unexpected tracked change |
| Secret pattern grep by CT over the evidence folder | 0 hits; lane exact-value scan PASS (8 values), rescan after cleanup present |
| Cleanup | Processes stopped, 8 ports free, Mongo dbpath and `/private/tmp` workspace removed (lane CLEANUP.md/tsv) |
| Native runtime / isolation | SDK 8.0.417, runtime 8.0.23; Mongo 127.0.0.1:34994 (not 27017); services bound to 127.0.0.1 |

## Items for decision / follow-up
1. **PNG method** — Playwright `page.screenshot` drives Chromium over CDP internally; earlier records treated CDP capture as unauthorised. Owner decision → Q56.
2. **Lane-DB fixture writes** (tenant record, two Auth roles, soft-deletes) done directly in the disposable lane DB because no product API exists. To be assessed by the independent verifier (Q55).
3. **Evidence-kit defects** found on the Mac: forced `BackgroundJobs__Enabled=false` crashes Platform; four internal keys must share one value; K01 wrapper depth; K04/K07 write secrets to files. → input to kit validation Q24.
4. **Permission-rule collision**: relative deny rules `Write/Edit(scripts/**)` and `(tests/**)` matched sub-folders of the evidence folder. Removed; the absolute repo-root rules remain. Four lane scripts were edited through Bash before the denial; evidence folder only.
5. EVIDENCE-REUSE rows from SOP-22 §9 added to the pilot ledger by CT.
