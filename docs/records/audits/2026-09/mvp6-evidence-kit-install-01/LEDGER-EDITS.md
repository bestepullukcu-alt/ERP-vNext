# Ledger edits (single ledger writer: AL-MVP6-KIT-INSTALL-01)

## docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv (state column only for existing rows; appended rows use the existing 8-column layout)

Before sha256 `5501c11afacef974e18a25a86382ccb6d81739952d012f30c3c37bf846a22981` → after `dbe4623b823b03f83591e3a7e7384aa23a153f9a5006751f8aa3dfc1d73364d0`. No row removed or reordered.

| Row | State set | How |
|---|---|---|
| Q09 | READY (decision C granted, option A; estimate first) | changed |
| Q24 | SPLIT: Q24a IN-PROGRESS (chat), Q24b READY (local Mac) → at Step Z: SPLIT: Q24a WRITER DONE (2026-09-26T13:45+03:00, uncommitted), Q24b READY (local Mac) | changed |
| Q51 | DONE (update 08 CT ACCEPTED) | changed |
| Q57 | DONE (superseded by v1.2) | changed |
| Q58 | DONE (V1 approved) | changed |
| Q59 | DONE (CT ACCEPTED as analysis) | appended (row was missing) |
| Q60 | DONE (CT ACCEPTED) | appended (row was missing) |
| Q61 | READY (local Mac; v1.1 Linux NOT RUNNABLE) | appended (row was missing) |
| Q62 | READY — Carrier UI source archive (local Mac) | appended |
| Q63 | DONE (CT ACCEPTED; V2) | appended (row was missing) |
| Q64 | READY — Claims UI DEV (isolated, local Mac) | appended |
| Q65 | READY — Returns UI DEV (isolated, local Mac) | appended |
| Q66 | DONE — Evidence kit v1.2 proposal | appended |
| Q67 | DONE (PASS) — Evidence kit v1.2 independent re-check | appended |
| Q24b | READY — Kit v1.2 Mac validation run + commit | appended |

## docs/roadmap/plans/mvp6-process-pilot-01/MILESTONE-EVENTS.tsv (append only)

Before `fb5b91ea2584f13f3d4f3dbd9243c0009811e255902f984a44ef144667e35b6a` → after `e3d7ddd6f7d72d6c3dad37148283ca2b2dfac6eed063a5f1eb252f4111751a10`. 14 lines appended, none changed. Approximate owner-decision times carry a "~" prefix in the timestamp column:

- 2026-09-26T12:03+03:00 | Q59 writer done — module readiness inventory | Q59 | docs/roadmap/plans/mvp6-module-readiness-01/ | lane report | 
- ~2026-09-26T12:10+03:00 | Owner decision SR-D4: self-registration built with each module as an overlay | Q59,Q64,Q65 | docs/records/audits/2026-09/mvp6-ct-verdicts-q59-srd4-2026-09-26.md | CT conversation | 
- 2026-09-26T12:13+03:00 | Q61 v1.1 Linux chat lane NOT RUNNABLE (proxy blocks .NET/MongoDB/Playwright hosts; /tmp 4.2 GB) | Q61 | docs/records/audits/2026-09/mvp6-shipment-iso-final-ver-lnx-01/ | lane report | 
- 2026-09-26T12:15+03:00 | Q60 writer done — decision-prep package | Q60 | docs/roadmap/plans/mvp6-decision-prep-01/ | lane report | 
- ~2026-09-26T12:20+03:00 | Owner decisions: Phase 1.5 Claims/Returns approved; Shipment/Carrier retroactive tables accepted; Loads decision C granted (option A) | Q60,Q64,Q65,Q09 | docs/roadmap/plans/mvp6-decision-prep-01/ | CT conversation | 
- 2026-09-26T12:23+03:00 | Q63 done — kit v1.1 independent review | Q63 | docs/records/audits/2026-09/mvp6-evidence-kit-v1-1-independent-review-01/ | lane report | 
- ~2026-09-26T12:30+03:00 | Owner decision V2: revise kit to v1.2 (proposal-03) | Q57,Q58,Q63,Q66 | docs/records/audits/2026-09/mvp6-ct-verdict-q63-kit-v2-2026-09-26.md | CT conversation | 
- 2026-09-26T12:54+03:00 | Q66 done — kit v1.2 proposal | Q66 | docs/roadmap/plans/mvp6-evidence-kit-proposal-03/ | lane report | 
- 2026-09-26T13:01+03:00 | Q51 done — effort update 08 | Q51 | docs/roadmap/plans/mvp6-effort-update-08/ | lane report | 
- 2026-09-26T13:08+03:00 | Q67 done — kit v1.2 independent re-check PASS | Q67 | docs/records/audits/2026-09/mvp6-evidence-kit-v1-2-recheck-01/ | lane report | 
- ~2026-09-26T13:22+03:00 | Owner decision V1: install kit v1.2 under A1 terms | Q58,Q24 | docs/records/decisions/2026-09/mvp6-evidence-kit-v1-2-adoption-owner-decision-01.md | CT conversation | 
- ~2026-09-26T13:43+03:00 | Owner decision: split Q24 into Q24a (chat install) and Q24b (Mac validation + commit) | Q24 | docs/records/decisions/2026-09/mvp6-evidence-kit-v1-2-adoption-owner-decision-01.md | CT conversation | 
- 2026-09-26T13:45+03:00 | Task start Q24a (kit v1.2 install, chat lane) | Q24 | docs/records/audits/2026-09/mvp6-evidence-kit-install-01/ | agent | 
- 2026-09-26T13:45+03:00 | Q24a writer hand-off — kit v1.2 installed (27 files) + A1 §5 line; not validated; uncommitted | Q24 | docs/records/audits/2026-09/mvp6-evidence-kit-install-01/ | agent | 
