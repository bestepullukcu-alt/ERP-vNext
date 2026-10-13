# SOP §22 — MVP6-EFFORT-SHIPMENT-CT-UPDATE-07

**Date:** 2026-09-24  
**Branch / HEAD:** `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
**Verdict:** **REPORTING PASS — exact CT evidence reconciled without duplicate credit**

## Changed files

Only `docs/roadmap/plans/mvp6-effort-shipment-ct-update-07/`.

## Evidence and calculation

The latest composite baseline is 1,432 ML delivered, 1,410 ML remaining and 2,842 ML total. Root R2 CT closes exact application and independent runtime allocations totaling 10/20/36 O/M/P. They are transferred from existing Shipment Backend/Test-VER reserves. B01/B02 functional closure is recorded qualitatively with zero additional numeric credit because it overlaps already credited Frontend delivery and the same unsplit Test/VER reserve.

Successor totals are 1,452 ML delivered, 1,390 ML remaining and 2,842 ML total. Shipment becomes 248/46/294 and 84.4%. Portfolio estimated scope index becomes 51.1%.

## Open gates

Durable PNG, broader Shipment UI/browser acceptance, common-target/full-module gates and Loads root producer/detail/lookup scope remain open. The Loads unestimated work is not silently removed from full MVP scope.

## Validation boundary

No runtime or product test was run. The calculation is reproduced by `RECALCULATE.py`; `VALIDATION.txt` records hash, row-count, unchanged-row, arithmetic, PNG and unestimated-scope checks. No readiness or production claim is made.

## Out-of-scope changes

None. No product, pack, contract, board, runtime or Git mutation occurred.

