# MOD-0186 Returns UI — O/M/P effort by delivery (person-hours)

O = optimistic, M = most likely, P = pessimistic. 8 h = 1 person-day. These are estimates, not commitments or
actuals. Baseline rows: `docs/roadmap/plans/mvp6-effort-shipment-ct-update-07/EFFORT.tsv` (SHA-256 `0b643ca9…d5`).
This draft does **not** edit that file. The rows below are a proposed **replacement** of existing REMAINING rows,
not an addition. Calibration comes from the Loads (list+create, M 84) and Carrier UI scope packages.

## Scope A — list + create + transition (recommended in APPROVAL-DECISION.md)

| Delivery | O | M | P | Replaces |
|---|---:|---:|---:|---|
| UI pack revision, Phase 1.5, dispatch closure | 4 | 8 | 16 | 0186-1-REMAINING 3.6/6/9.6 |
| Frontend: list, create (shipment resolve + line picker), transition modal (7 arrows, 3 conditional fields), 7 languages, module tests | 32 | 52 | 88 | 0186-4-REMAINING 28.8/48/96 |
| Shared integration: gateway routes, permission/nav registration, shared L10n, G-SHIPREAD, personalization check | 8 | 16 | 28 | UI sub-item of 0186-5-REMAINING |
| Independent UI VER: real Auth, 3 identities, browser profiles, DB before/after, restart/replay | 14 | 24 | 40 | UI sub-item of 0186-6-REMAINING |
| **Bounded UI deliveries** | **58** | **100** | **172** | 7.3 / 12.5 / 21.5 person-days |
| Left in 0186-5: backend uptake into the chosen target | 6 | 10 | 16 | remainder of 0186-5-REMAINING |
| Left in 0186-6: module E2E and narrow defect closure beyond UI VER | 6 | 10 | 16 | remainder of 0186-6-REMAINING |
| **New view of rows 1/4/5/6** | **70** | **120** | **204** | old 56.4 / 94 / 169.6 → net **+13.6 / +26 / +34.4** |

## Scope B — list + create only (transition UI deferred)

| Delivery | O | M | P |
|---|---:|---:|---:|
| UI pack revision, Phase 1.5, dispatch closure | 4 | 8 | 16 |
| Frontend list + create, 7 languages, module tests | 24 | 40 | 64 |
| Shared integration | 8 | 16 | 28 |
| Independent UI VER | 12 | 20 | 36 |
| **Bounded UI deliveries** | **48** | **84** | **144** |
| With the same two remainders (12/20/32) | 60 | 104 | 176 → net +3.6 / +10 / +6.4 vs old |

Scope B leaves Returns lifecycle management UI-less: every transition after Requested would need API access. So B
is a partial module UI, not a smaller version of A.

## Why the net increase in Scope A

The old 0186-4 reserve said "minimum final UI flows; reserve for unknown scope" (LOW confidence). Scope A adds a
7-target transition surface and the Shipment-resolve line picker, which that reserve did not specify. Delivered rows
0186-1/2/3/5/6-DELIVERED are unchanged, and 0186-2/3-REMAINING are kept as they are.

## Uncertainty drivers

M assumes the shared personalization, `_AccessDenied` and premium-modal wrappers exist in the target. P covers
route/provider preimage conflicts, G-SHIPREAD role design, and real-Auth/LE environment setup (the evidence
kit is not yet validated). Waiting time and native-executor availability are not included: recent lanes stopped at
STEP 0 because no native macOS executor was available, and that is calendar risk, not effort. If both modules are
delivered together, see README for the joint saving.
