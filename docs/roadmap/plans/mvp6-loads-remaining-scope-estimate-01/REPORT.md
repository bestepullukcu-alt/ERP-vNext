# MVP6-LOADS-REMAINING-SCOPE-ESTIMATE-01

**Date:** 2026-09-24  
**Profile:** scope/effort review only  
**Baseline:** `mvp6-effort-shipment-loads-update-06`  
**Result:** Loads transition UI is mandatory and sufficiently defined for an O/M/P estimate; root access is mandatory but its producer-owned contract shape is undecided; separate detail and searchable lookup surfaces remain owner scope decisions.

## Disposition

The full MVP brief owns Routing/Load and names `warehouse shipment → carrier/load → POD` as part of the golden flow. The controlling pack publishes list, create and lifecycle transition as the three Loads operations. Therefore the transition surface cannot be removed from the full-MVP denominator merely because the first UI slice stopped at list/create.

The transition command requires the immutable Load root. Current `LoadSummary` and `LoadResponse` do not expose it, and there is no published Loads detail read operation. The UI must not derive the root from request trace, identity, cache, DB/audit, or user entry. A producer/contract owner must choose and publish an authoritative access shape. This report does not choose a new endpoint or schema field.

An independent detail page and searchable Carrier/Shipment/location selectors are not required by the current published operation set or MVP brief. They are not silently excluded: both remain explicit owner scope decisions with scenario bounds in `SCENARIOS.tsv`.

## Defined mandatory remaining delivery

| Delivery | O | M | P | Existing-reserve overlap | Net new O/M/P |
|---|---:|---:|---:|---|---:|
| Transition UI: current-row action, allowed-target presentation, exact note/time payload, permission gating and published 404/409/422/replay remediation | 16 | 28 | 48 | None; first-slice frontend estimate is list/create only | 16/28/48 |
| Transition shared integration: existing published route, transition permission and root handoff after producer seam exists | 4 | 8 | 16 | Fully within `0185-5-LIVE-REMAINING` 12/20/32 | 0/0/0 |
| Independent real-Auth/browser transition verification: lifecycle pairs, root/replay precedence, restart/current-row behavior | 8 | 16 | 28 | Fully within `0185-6-LIVE-REMAINING` 8/16/28 | 0/0/0 |
| Root seam disposition/candidate/repin preparation | 4 | 8 | 16 | M covered by `0185-2-REMAINING` 4.8/8/12.8; pessimistic excess remains uncertainty, not an approved implementation estimate | 0/0/unallocated 3.2 |
| **Gross defined work envelope** | **32** | **60** | **108** | **Existing reserves absorb integration, VER and M root-decision work** | **16/28/48 counted; root-decision P excess remains unallocated** |

The estimate is delivery-based. It does not use file counts, test counts or equal module weights. It assumes the published `transitionLoad` operation and approved lifecycle/replay/error policy remain unchanged. It excludes the still-undecided producer implementation used to retrieve the root.

## Portfolio effect

Update 06 had 1,432 most-likely delivered hours and 1,382 remaining hours, totaling 2,814. Adding only the non-overlapping transition frontend delivery produces:

- Estimated remaining: **838.8 / 1,410 / 2,628 hours** O/M/P.
- Estimated total: **1,891 / 2,842 / 4,487.8 hours** O/M/P.
- Loads most-likely: **136 delivered / 172 remaining / 308 estimated**, an estimated-scope index of **44.2%**.
- Portfolio most-likely estimated-scope index: **1,432 / 2,842 = 50.4%**.

This is named **estimated scope index**, not full-MVP completion, readiness or schedule percentage. The denominator still excludes undecided detail/lookup implementation, the selected producer-owned root-access implementation, and the new Shipment Root R2 defect.

## Explicitly unestimated dependency

`GAP-SHIPMENT-ROOT-EMISSION-01` is a separate current Shipment product defect: fresh create persists `CorrelationId` but not `LifecycleCorrelationId`, and detail emits null before/after restart. Its rework lane has not completed. This report records it as **effort not yet determined** and assigns no hours to Loads or Shipment. The Loads root-access decision and the Shipment producer defect are related prerequisites but are not the same deliverable and must not be double-counted.

## Limits

No runtime, contract, pack, board or Git state changed. No owner decision, new endpoint, root-selection policy, consumer consent or DEV authorization is created by this estimate.
