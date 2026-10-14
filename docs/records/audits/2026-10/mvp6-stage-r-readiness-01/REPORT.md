# Stage R readiness — the nine browser cells, mapped before the session instead of during it

**Date:** 2026-10-13 · **Branch:** `feature/sce/mod-0190-0192-bounded-slice`
**Scope:** gate 18.0 rows 2 (×4), 7 (×2) and 12 (×3) — everything left that only a browser can close.

## Why the stack was not started today

Measured before deciding, not after failing:

| | measured | bearing |
|---|---|---|
| free disk | **2.1 GiB** | one service is built (SupplyChain, 35 MB `bin`); **five are not** — web, gateway, auth, platform, mdm. `launch.json` runs them `--no-build` |
| today's ENOSPC | the volume hit **zero** at 22:30 with one service's suite and one mongod running | the lane mongod terminated itself: `"s":"F"` FTDC id 9399800, *"Terminating the process because diagnostics cannot be captured"* (Q490) |
| free memory | **30%**, `memory_pressure` | memory is not the constraint; swap counters are cumulative since boot and do not indicate current pressure |

So the blocker is disk headroom, and it is specific: today's crash happened at a materially *smaller* footprint
than six service builds plus a web app plus four mongods. Starting it at 2.1 GiB would most likely reproduce
the crash, and an ENOSPC mid-session also endangers the record seals being written. The time went into making
the eventual session cheap instead.

## The dependency map

All six `launch.json` services are needed, and no seventh:

| process | port | why these cells need it |
|---|---:|---|
| web | 5001 | the pages themselves |
| gateway | 5000 | `/api/shipment-bundle/{everything}` → **5066**, measured correct after OD-P1 renumbered SupplyChain from 5061; and `/api/personalization/views` → 5057 |
| auth | 5056 | the login that mints the token the pages carry |
| platform | 5057 | the §22 menu, the tenant entitlement, **and saved views** — `SavedViewsController.cs:13` owns `api/personalization/views` |
| mdm | 5059 | every `*ContextMiddleware` asks MDM whether the legal entity is the token tenant's and Active, fail-closed (R-2, PR #134) |
| supplychain | 5066 | the four modules |

Plus a mongod per data-owning service, and from the R-2 lane's record: a tenant with a **commercial plan**
(the default tenant answers `QUOTA_CONFIGURATION_MISSING`), a Platform-admin token minted by Q358's
`mint.py`, and a throwaway JWT secret.

## Row 2 is viable — the feature is implemented, not a label

This was worth checking first, because "Save View and column visibility were never exercised" would read the
same way whether the controls exist or not. They exist, and the whole chain is measured:

```
Views/SupplyChain/<M>/_IndexL10n.cshtml      SaveView, ColumnVisibility l10n keys
wwwroot/assets/js/SupplyChain/<M>/index.js   window.personalizationClient, colvisColumns,
                                             saveViewArmed, the Save View block
wwwroot/assets/js/personalization-client.js  window.personalizationClient = (function () { …
Controllers/PersonalizationProxyController   [Route("api/personalization/views")] → gateway
gateway/ocelot.json                          /api/personalization/views → localhost:5057
Platform SavedViewsController.cs:13          api/personalization/views, GET/POST moduleKey+pageKey
```

Returns' Save View block carries its own constraint in a comment — *"shared personalizationClient only; no
browser storage"* — so the saved view is server state, which is what makes row 2 measurable at all rather
than a per-browser convenience.

## The finding: four modules, two personalization key conventions, and A6 was never confirmed

`index.js` in each module carries `// ASSUMPTION A6: module/page codes of the §33 manifest; the integration
owner confirms the personalization codes.` Nobody did, and the four modules invented **two different
answers**:

| module | `moduleKey` | `pageKey` | convention |
|---|---|---|---|
| MOD-0184 Carriers | `SupplyChain` | `Carriers` | service name + view name |
| MOD-0185 Loads | `SupplyChain` | `Loads` | service name + view name |
| MOD-0186 Returns | `reverse-logistics` | `RETURNS` | module slug + §22 PageCode |
| MOD-0187 Claims | `claims-management` | `CLAIMS` | module slug + §22 PageCode |

Measured consequence, and it is not a crash: `SavedViewsController.GetViews` takes `moduleKey` and `pageKey`
as **free query strings** with no manifest validation in that controller, so both conventions store and read
successfully. What the inconsistency costs is a split key space inside one service — Carriers and Loads share
the `SupplyChain` module namespace and are told apart only by `pageKey`, while Returns and Claims each own a
namespace — and nothing reconciles the two. Row 2 can therefore be **exercised and ticked** in all four; the
convention is a separate defect, raised as **Q493** rather than folded into the cell.

This is the kind of drift K6 names: the same decision answered twice because the assumption that would have
settled it was left open.

## What the session has to produce, per cell

Rows 7 and 12's server halves are already evidenced in the suites; only the browser halves are open.

| cell | modules | exercise |
|---|---|---|
| 2 No-shell | 0184, 0185, 0186, 0187 | Save View (toolbar), column visibility (`colvisColumns` = indices 1–3), then a **reload** showing the view persisted server-side |
| 7 Validation | 0184, 0187 | one client-side rejection before any request, and one server 400/422 whose response **keeps the entered inputs** |
| 12 UX states | 0184 | an empty list, and a validation-error state |
| 12 UX states | 0186 | an empty list (the other five states are measured) |
| 12 UX states | 0187 | a validation-error state (the other five are measured) |

An empty list needs a tenant/legal-entity scope with zero rows — cheapest as a freshly seeded legal entity,
not by deleting data.

## Not blocked on this

Rows 3 for MOD-0186/0187 stay with the integration owner (`gateway/**` is Protected Paths in every pack).
MOD-0184 row 13 is MET as of 2026-10-12 on observation, which `:376` never withheld.
