# Loads producer uptake — DEV preparation (Q68, MVP6-WP-185-UPTAKE-PREP-01)

🤖 Applying knowledge of @backend-architect + @product-manager.
Lane AL-MVP6-185-PREP-01 (INS), chat lane on the linked Mac folder. Repo `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Start 2026-09-26T14:17:54+03:00. Read-only; no code; no git writes.

**Preparation only — NOT APPROVED.** Authority used: decisions A (`mvp6-loads-root-amendment-owner-decision-a-01.md`, `e12d5975…`), B (`…-b-01.md`, `36fe774a…`), and the C grant, option A, from `docs/roadmap/plans/mvp6-decision-prep-01/LOADS-ROOT-OPTIONS.md` (as stated in the Q68 prompt; ASSUMPTION A1).

## Summary

| Item | Result |
|---|---|
| Contract delta for the producer | One observable change: optional/nullable `LoadSummary.lifecycleCorrelationId` on `queryLoads` (YAML 3197, annex 207–219). Everything else in 3.1.0 is metadata or unchanged behaviour (CONTRACT-DELTA.tsv, 12 rows) |
| Current code | Field MISSING (`LoadModels.cs:8`, `GetLoadListHandler.cs:8`); the stored source exists (`LoadPlan.cs:15`). The list reads typed `LoadPlan`, which would turn a missing root into a synthesized nil and fail the whole list on null/malformed data — so a presence-aware read is needed (pattern already in `ShipmentDetailMaterializer.cs`) |
| Work | 6 source changes (2 new files), 3 test changes (2 new files), 2 evidence-tool changes; 11 owned paths; no endpoint, no backfill, no migration, no shared-seam overlay |
| Evidence tooling | `tests/loads/verify_evidence.py` still pins SHIPMENT-BUNDLE 2.0.0 (`93c696e2…`) and the v2 annex (`a2187c93…`) — must be repinned to 3.1.0 |
| Isolated env | `git archive` HEAD + BC-SOURCE `ebd5d80c…` (all 42 Loads files; byte-identical to the working tree today) + published contracts; Auth 22 `f50350b8…` only for real-Auth runtime |
| Estimate | **11.5 / 19 / 35 h** O/M/P (Backend 3/5/9, Test/VER 7/11/20, Contract 0.5/1/2, Integration 1/2/4); confidence MEDIUM-LOW |
| Phase 1.5 | 9-row table prepared, recommended Yes (PH15-185-UPTAKE.md) |

## Top risks

1. **List failure on legacy data today:** typed deserialization of a document with null/malformed `CorrelationRoot` would fail `queryLoads`; a missing field would be emitted as nil. The presence-aware read fixes this for the list only.
2. **Finding F-1 (outside decision C):** the transition path (`LoadRepository.cs:43-44`) also reads typed `LoadPlan`; a legacy document without a stored root becomes `Guid.Empty`, which matches a valid nil inbound correlation (annex line 178). Needs a separate CT/owner decision; this package does not touch it.
3. **Evidence pins stale:** without the repin, the runtime evidence verifier fails against 3.1.0.
4. **Environment effort:** as in A12 VER-02, composing the isolated env and the kit dominate runtime VER time (EST-03 LOW confidence).
5. **Annex header still reads "NOT PUBLISHED"** although the bytes are the CT-accepted published version (hash-bound publication); cosmetic, not a producer change — recorded only.

## Ledger mapping (for CT)

RS-05 was outside the 2,842 h denominator. Option 1 (recommended): add EST-TOTAL as new forecast rows. Option 2: absorb into `0185-3-REMAINING` 9.6/16/25.6 (backend reserve) and `0185-6-LIVE-REMAINING` 8/16/28. This package does not edit any ledger.

## ASSUMPTIONS

- **A1** Decision C / option A is taken as granted from the Q68 prompt; no separate decision record for it was found in `docs/records/decisions/2026-09/` by this lane.
- **A2** The published contract bytes are the working-tree files `6dc1dd48…` / `9d8a3706…` (CT ACCEPTED Q25/Q32, not yet committed).
- **A3** For a malformed or wrong-type stored root, the list emits `null` and does not fail: the schema forbids a malformed value on the wire (RP08), the contract makes the field nullable for legacy records, and the consumer already treats null as "transition unavailable". The Shipment detail route instead fails with 500 for its single record; a list failing on one bad row would hide every Load. Put to the owner through the Phase 1.5 table.
- **A4** The value is serialized as the canonical lowercase UUID string (existing web JSON defaults, nulls written; `Program.cs:50-54` sets no ignore-null condition).
- **A5** Loads source in the common checkout equals BC-SOURCE (42/42 files byte-identical, checked 2026-09-26); the writer still builds from the archive + overlay, never from the checkout.
- **A6** Estimates are agent estimates by analogy to existing files, not measured time.
- **A7** "add-endpoint-cqrs" rules apply as far as they concern file layout; no new endpoint means no new controller action.

## Files

`README.md`, `CONTRACT-DELTA.tsv`, `WORK-BREAKDOWN.tsv`, `OWNED-PATHS.md`, `ACCEPTANCE.tsv`, `ISOLATED-ENV.md`, `ESTIMATE.tsv`, `PH15-185-UPTAKE.md`, `SHA256SUMS`.
