# MOD-0187 Claims UI — O/M/P effort by delivery (person-hours)

Same basis as Returns: estimates, not commitments; proposed **replacement** of rows in
`mvp6-effort-shipment-ct-update-07/EFFORT.tsv` (SHA-256 `0b643ca9…d5`), which this draft does not edit.

## Scope A — list + create + transition (recommended)

| Delivery | O | M | P | Replaces |
|---|---:|---:|---:|---|
| UI pack revision, Phase 1.5, dispatch closure | 4 | 8 | 16 | 0187-1-REMAINING 3.6/6/9.6 |
| Frontend: list with exact amounts, create (resolve, carrier link, lexical amount/currency), transition modal (7 arrows, conditional approvedAmount), 18 localized error codes × 7 languages, module tests | 30 | 50 | 84 | 0187-4-REMAINING 33.6/56/112 |
| Shared integration: gateway, permission/nav, shared L10n, G-SHIPREAD, personalization | 8 | 16 | 28 | UI sub-item of 0187-5-REMAINING |
| Independent UI VER: real Auth, 3 identities, lexical/approval matrices, DB before/after, restart/replay | 14 | 24 | 40 | UI sub-item of 0187-6-REMAINING |
| **Bounded UI deliveries** | **56** | **98** | **168** | 7 / 12.3 / 21 person-days |
| Left in 0187-5: backend uptake into the chosen target | 6 | 10 | 16 | remainder |
| Left in 0187-6: module E2E and narrow defect closure | 6 | 10 | 16 | remainder |
| **New view of rows 1/4/5/6** | **68** | **118** | **200** | old 61.2 / 102 / 185.6 → net **+6.8 / +16 / +14.4** |

## Scope B — list + create only

| Delivery | O | M | P |
|---|---:|---:|---:|
| Pack revision / Phase 1.5 | 4 | 8 | 16 |
| Frontend list + create | 22 | 38 | 62 |
| Shared integration | 8 | 16 | 28 |
| Independent UI VER | 12 | 20 | 36 |
| **Bounded UI deliveries** | **46** | **82** | **142** |
| With the two remainders (12/20/32) | 58 | 102 | 174 → net −3.2 / 0 / −11.6 vs old |

Scope B leaves Claims stuck at Open in the UI (no investigate/decide/settle), which removes most of the module's operational value.

## Uncertainty drivers

M assumes the shared wrappers and personalization exist in the target. P covers lexical-amount handling across 7
locales (decimal-comma users), the approval matrix, and route/provider conflicts. Native-executor availability is calendar risk and is not counted here.
