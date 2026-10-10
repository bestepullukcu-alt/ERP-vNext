# CT disposition — Shipment UI183-A12 runtime + A08/A09 regression (Q04, Q55) — 2026-09-26

Recorded by CT 2026-09-26T11:14+0300. New record (K4). Base 4a8d4d4b; no commit/push/stash.
Flow: writer (Terminal runtime lane, 10:08–10:53) → independent VER (Lane-3, 11:04:56–11:12:33) → this CT decision.

| Input | Pointer | CT check |
|---|---|---|
| Writer evidence | `docs/records/audits/2026-09/mvp6-shipment-a12-runtime-independent-ver-02/` | ARTIFACTS 187/187 OK; intake `mvp6-ct-a12-ver02-intake-2026-09-26.md` |
| Independent VER | `docs/records/audits/2026-09/mvp6-shipment-a12-ver02-independent-review-01/SOP-22-VER.md` | ARTIFACTS 14/14 OK; 8 PASS, check 4 PARTIAL |
| PNG method | `docs/records/decisions/2026-09/mvp6-png-method-owner-decision-01.md` | first use |
| Check-4 treatment | Owner, question tool, 11:14: **accept on behaviour** | — |

## Decision

| Criterion | Disposition |
|---|---|
| UI183-A12 (normal detail; cross-LE en/tr/ar; unknown; soft-deleted; hidden/inert surfaces; late async; 404 SHIPMENT_NOT_FOUND + correlation; zero writes; data/API isolation) | **ACCEPTED — runtime, independent** (A12 successor source `details.js` 69626e61…, archive 7b6a0d1a…) |
| Negative control | ACCEPTED on behaviour (served-file hash not recorded; kit follow-up below) |
| UI183-A08 stale transition 422; A09 duplicate POD 409; A09 stale eligibility 422 — regression | **ACCEPTED — runtime regression on the A12 source** |
| Durable PNG for these rows | ACCEPTED (16 files, hashes = PNG-INDEX.tsv) |
| Accessibility PRES-183-01, A03/PRES-183-02 | Unchanged (inherited earlier acceptance; not re-counted) |
| A10 fault proxy | OPEN — unauthorised (DN-01) |
| A13 / A14 and other open Shipment rows | Unchanged (DN-02 / PC rows) |

Scope: disposable isolated run of the A12 successor; not an integrated-checkout acceptance and not module closure (Phase 4.5 in the integrated target still required).
Effort: credit treatment deferred to the effort successor update (Q51) with an O/M/P split; no number changed here.

## Follow-ups → kit validation Q24
Kit defects: forced BackgroundJobs__Enabled=false crashes Platform; four internal keys must share one value; K01 wrapper depth; K04/K07 write secrets to files.
Evidence gaps: record served-file hash for negative controls; complete COMMANDS.tsv; unique names per attempt (no overwrite); exact-value secret scan after the last write; assert outbox deltas for setup mutations.

Next: Q14 integration selection refresh is now unblocked (was HELD on the A12 disposition).
