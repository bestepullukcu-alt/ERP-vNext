# MVP6 decision preparation (Q60, WP MVP6-WP-DECISION-PREP-01)

🤖 Applying knowledge of @orchestrator (Phase 1.5 table author) + @module-pack-author + @read-only-auditor.
Lane AL-MVP6-DECPREP-01 (INS), chat lane on the linked Mac folder. Repo `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
Start 2026-09-26T12:08:37+03:00. Text only; no git writes.

**Everything here is prepared text — NOT APPROVED.** Put to the owner through CT, one decision at a time, in the order below.

## Decisions prepared

| Order | ID | File | Owner question | Recommended |
|---|---|---|---|---|
| 1 | PH15-UI-187 | `PH15-UI-187.md` | Approve the Claims UI Phase 1.5 table so the UI can be built in its isolated environment? | **Yes** |
| 2 | PH15-UI-186 | `PH15-UI-186.md` | Approve the Returns UI Phase 1.5 table (incl. the bound error-code set)? | **Yes** |
| 3 | PH15-UI-183 | `PH15-UI-183.md` | Accept the retroactive table for the built Shipment UI, findings left open? | **Yes** |
| 4 | PH15-UI-184 | `PH15-UI-184.md` | Accept the retroactive table for the built Carrier UI, findings left open? | **Yes** |
| 5 | LOADS-ROOT | `LOADS-ROOT-OPTIONS.md` | Grant producer uptake as published in 3.1.0 (option A = decision C), no backfill, no new endpoint? | **Yes (A)** |

Each PH15 file has the 9 add-module Phase 1.5 rows in the original wording and order, each answered with a source pointer.

## Main findings (in scope)

1. **Pack wording vs modules first:** Returns and Claims pack §32.14 still say UI code waits for an integrated target. The decision texts state the isolated build explicitly; the pack sentence stays until a pack patch.
2. **Shared packs lack the UI revisions of the built UIs:** MOD-0183 and MOD-0184 still say `shell: none`, `golden_reference: none`, `form_field_count: 0` (F-183-1, F-184-1).
3. **Shipment Create/Details section map differs** (F-183-2). **Carrier UI rows 8–9 cannot be verified** because its source is not in the repo (F-184-2); its start/Phase 1.5 approval record was not found (F-184-3).
4. **Shipment UI did receive an owner approval covering Phase 1.5** on 2026-09-24, but not in the 9-row format (F-183-4).
5. **Loads root:** the contract design is already decided and published (D185-ROOT-ACQ-01, A, B). RS-05 is now only the producer implementation, which is decision C (Q09).
6. The Returns 422/409/5xx code set, which pack §32.13 left to Phase 1.5, is bound from the accepted source (22 codes).

## ASSUMPTIONS

- **A1** Phase 1.5 row 4's `BaseEntity` is read as this service's `EntityBase` (same role: TenantId + soft delete); recorded, not treated as a deviation.
- **A2** Backend rows 1–5 cite the accepted archive `normal-source.tar.gz` (`edb759a0…`) for Returns/Claims and the common checkout for Shipment/Carrier; the BC 422 successor carries the same Returns/Claims paths.
- **A3** Returns error codes are the string literals found under `Features/Returns` in the accepted source; HTTP status per code is left to the writer's vertical slice.
- **A4** Nav-key values in 7 languages do not exist yet; they are listed as "NOT FOUND" and belong to the SR-D4 overlay.
- **A5** The first-open tracker progress expectation (row 8) is not in the Returns/Claims packs; left as a finding, not invented.
- **A6** LOADS-ROOT effort stays UNESTIMATED; existing ledger reserves are named but not assumed to cover it.
- **A7** "Accept retroactive table" is framed as recording, not approving new work.

## Files

`PH15-UI-187.md`, `PH15-UI-186.md`, `PH15-UI-183.md`, `PH15-UI-184.md`, `LOADS-ROOT-OPTIONS.md`, this `README.md`, `SHA256SUMS` (relative paths).
Step A record (separate path): `docs/records/audits/2026-09/mvp6-ct-verdicts-q59-srd4-2026-09-26.md`.
