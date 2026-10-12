<!-- verify from: this folder -->
# Q446 re-checked by date: eight of its nineteen open cells have moved, and CT had been reading the queue as if none had

    lane      : mvp6-q446-date-recheck-01
    measured  : 2026-10-12
    basis     : docs/records/audits/2026-10/mvp6-q446-gate-18-0-four-modules-01/GATE-18-0.tsv
    verdict   : Q446 is correct as measured on 2026-10-04/05 and stale as a verdict on EIGHT of nineteen cells.
    cause     : its preflight is `Sun Oct 4 22:08 UTC 2026`; five decisions and one commit land on 10-05 and 10-06.

## Why this lane ran

CT found two of Q446's cells stale by accident — row 13 while acting on it, row 1 while preparing to
put it to the owner — and then put three already-decided items into the owner's queue. The failure was
method, not luck: CT read `CT-QUEUE.tsv`'s narrative rows as current state and never cross-checked
`docs/records/decisions/`. This lane checks all nineteen.

## Result

| cells | count | state after the re-check |
|---|---:|---|
| moved by an owner decision | 4 | rows 3 and 10, MOD-0186 and MOD-0187 |
| moved by code | 1 | MOD-0185 row 1 |
| code complete, needs only a re-measure | 3 | row 13, MOD-0185/0186/0187 |
| NOT MET and not actionable by CT | 1 | MOD-0184 row 13 |
| NOT MEASURED, unchanged | 10 | rows 2, 5, 7, 12 |

**Eight of nineteen moved.** None of Q446's measurements was wrong; all eight were overtaken.

## Cell by cell, where it moved

### Rows 3 — MOD-0186, MOD-0187 · decided, sequence gate now open

`mvp6-gateway-explicit-routes-owner-decision-01.md`, 2026-10-05, ACCEPTED, `ledger: Q397, Q446, Q456`.
The five modules get explicit gateway routes and the `/api/shipment-bundle/{everything}` catch-all is
removed, because MOD-0186:749 and MOD-0187:699 require `/returnsXYZ` **not** to match and the catch-all
forwards it downstream instead of answering 404.

CT wrote in plan v9.3 that this ruling "was assigned 2026-09-27 and has never been made". **Wrong — it
was made on 2026-10-05.**

The decision was sequenced after the origin/main merge because Q435 measured that `ocelot.json`
auto-merges silently with no conflict marker. **That gate is now open:** `origin/main` is `a814bad0f`
(PR #134, 2026-10-10) and this branch contains it (`HEAD..origin/main` = 0).

So the cells are still NOT MET, but for a different reason than Q446 recorded: not a missing ruling, a
pending gateway edit. `gateway/**` is in every pack's Protected Paths, so it is the **integration
owner's**, not CT's.

### Rows 10 — MOD-0186, MOD-0187 · waived, and the verdict should not read NOT MET

`mvp6-returns-claims-evidence-row10-waiver-owner-decision-01.md`, 2026-10-05, ACCEPTED,
`ledger: Q446, Q455`, precedent `mvp6-shipment-pod-evidence-row10-waiver-owner-decision-01.md`.
§18.0's audit/evidence row is waived for the `evidenceReferenceIds` surface only, on MOD-0183's
reasoning: the row prescribes a shared evidence service and the packs instruct opaque references, so
the modules were measured against something they were never built to do.

Q446's TSV reads NOT MET because the waiver is same-day. Per the MOD-0183:364 precedent the cell reads
**CLOSED UNDER WAIVER**, which is not MET and not a gap.

CT asked the owner to grant this waiver again on 2026-10-12. It already existed.

### Row 1 — MOD-0185 · closed by code the day after Q446

Q446: *"no tracked config sets `Loads:ReferenceBaseUrl`, so every clone fails the golden flow"*, and it
recommended adding it to `appsettings.Development.example.json`. That file does not exist. The key is in
the tracked `appsettings.Development.json` at `http://127.0.0.1:5066/`, added by **`c74ea4839`,
2026-10-06**, *"revert(mvp6): R-3 the CRM secret pattern is kept — 11 Development configs are tracked
again"*. Q446's preflight was 10-04 22:08.

### Rows 13 — MOD-0185, MOD-0186, MOD-0187 · code complete

Recorded in full in `mvp6-q446-row13-recheck-01`. All four modules log replay on their controllers'
outcome lines; R-2 measured Returns a day before that line existed. MOD-0187's genuinely missing half —
`ClaimContextMiddleware` with zero log calls — was closed on 2026-10-12.

### Row 13 — MOD-0184 · real, and not CT's

Pack `:302`'s list size and timings plus a conflict/transaction-failure signal are genuinely unlogged.
`MOD-0184:376` accepts the §23 owned paths "as design; **no runtime write**", so CT cannot write it.

### Rows 2, 5, 7, 12 — ten cells, unchanged

No decision and no code can move these: they ask for behaviour produced live. Row 2 wants Save View,
column visibility and Reset exercised with a reload; row 5 one conflicting concurrent mutation; row 7 a
client-side rejection and a server 400/422 that keeps inputs; row 12 the states not yet produced,
notably an empty list. **One live session covers all ten**, and measuring MOD-0184 is permitted —
`:376` withholds runtime write, not observation.

## What this changes for the plan

| v9.3 said | corrected |
|---|---|
| Row 3 ruling "assigned 2026-09-27, never made" | made 2026-10-05; the gateway edit is the integration owner's |
| Row 10 waiver pending | granted 2026-10-05 |
| Q414 "one line, highest impact" | done 2026-10-06 by `c74ea4839` |
| 0147/0148 five concurrences awaiting a CT ruling | ruled 2026-10-05 (`mvp6-ct-ruling-dc01-dc05-concurrence-01.md`, which says the ruling "was owed for eight days") |
| 0190/0192 fixture-bound status quo unrecorded | recorded 2026-10-05 (`mvp6-0190-0192-fixture-bound-owner-decision-01.md`) |
| Q399 as an undecided 2026-11-03 threat | `mvp6-shipment-admin-baseline-temporary-widening-owner-decision-01.md`, 2026-10-03, `:38` already carries the expiry **and its condition**: the widening is re-approved or retracted, not lapsed quietly |

## Standing correction to CT method

`CT-QUEUE.tsv` rows and audit reports are **snapshots**. `docs/records/decisions/` is newer than both.
Nothing goes into the owner's queue before that directory is read.

## Compliance

Read-only. No pack line ticked by this lane, no code changed, nothing pushed by it.
