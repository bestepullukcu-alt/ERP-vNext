# MVP6-SHIPMENT-A12-SAFE404-REWORK-01 — SOP §22 DEV Handoff

## Verdict

**DEV writer complete for A12 safe-not-found presentation rework.** This is a bounded Shipment UI presentation successor. It is not independent VER, CT acceptance, full Shipment UI acceptance, rollout, E5/G5, or repository PASS.

## Controlling Finding

Source: `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/evidence/a08-a09-a12-01/SOP-22.md` and `ACCEPTANCE.tsv`.

The controlling evidence closed A12 data/API isolation but left `UI183-A12-safe-not-found-DOM` as **FAIL / REWORK**: cross-LE, unknown, and soft-deleted detail calls returned safe 404 with zero writes, but the browser still showed `Loading...`, shipment summary labels, the empty lines table, POD placeholder, and related shipment surfaces. The expected surface is the localized safe-not-found message with support reference only.

## Authority And Scope

- Input source: current 360-source successor from `mvp6-shipment-shared-ui-exec-01`.
- User direction: make only the necessary Shipment-owned presentation change for A12; do not alter backend 404 behavior, permissions, tenant/LE policy, shared/governance, A10 proxy, or gateway.
- Actual changed files: two Shipment UI-owned/test files listed in `CHANGED-FILES.tsv`.
- Product checkout source files were not changed because the final Shipment UI source is carried as the immutable 360-source successor archive, not as checked-out frontend files in this repository.

## Fix Summary

`details.js` now has an explicit safe-not-found state:

- hides the detail row containing summary, schedule, lines and POD surfaces;
- hides and clears `#shipmentActions`;
- hides both transition and POD offcanvas roots;
- sets hidden surfaces `inert` and `aria-hidden`;
- replaces the `Loading...` header with the localized not-found text;
- preserves the localized support-reference message from the response correlation;
- does not redirect, invent fallback data, derive roots, change backend policy, or alter permissions;
- ignores stale late async detail responses through a load-version guard.

`ShipmentDetailActionTests.cs` adds focused static assertions for the safe-not-found state and late async response guard.

## RED To GREEN Evidence

| Check | Result | Evidence |
|---|---|---|
| RED: original 360-source `details.js` lacks `renderSafeNotFound`, inert safe-state handling and load-version guard | PASS as RED | `VALIDATION.txt` |
| GREEN: successor `details.js` parses with Node | PASS | `VALIDATION.txt` |
| GREEN: successor static assertions find safe 404 support-reference handling, hidden/inert/offcanvas/action cleanup and no redirect fallback | PASS | `VALIDATION.txt` |

No runtime/browser PASS is claimed here. The original A12 runtime failure requires independent browser/runtime VER on this successor archive.

## Exact Artifacts

| Artifact | SHA-256 |
|---|---|
| `shipment-a12-safe404.patch` | `b3c7dcbb68636b123f3ea3ad7c3c902709fef6a9775c66cc3d464073653ecefb` |
| `SUCCESSOR-360-SOURCE-MANIFEST.tsv` | `8ffa6c96d29f8004940f310ed704a2c05eaa38ffa501d6114a5f9251e6e00d36` |
| `successor-source.tar.gz` | `7b6a0d1abea649d920040a039972c5d4928825e9cf2cc185487b1fe5f0f3314d` |

## Acceptance Disposition

| Criterion | DEV disposition |
|---|---|
| A12 safe 404 must not show Loading | Addressed in successor; independent VER required |
| A12 safe 404 must not expose summary/line/POD/action surfaces | Addressed in successor; independent VER required |
| Safe 404 must keep support-reference presentation | Addressed in successor; independent VER required |
| Normal authorized detail flow | Source path preserved; independent VER must prove with runtime |
| Late async response behavior | Guard added; independent VER must prove if covered by runtime harness |
| Backend 404, permission, tenant/LE policy | Unchanged |
| A10 proxy | Not touched |

## Verification Limits

The minimal 360-source archive omits referenced backend projects, so a full `.NET build` from this artifact is not a meaningful source result. The attempted build failed on omitted project/type references. This package therefore provides exact source, patch and focused static validation only; runtime/browser verification is the next required lane.

## Writer Complete

Writer work is complete. Independent VER should use `INDEPENDENT-VER-HANDOFF.md` and `successor-source.tar.gz`.
