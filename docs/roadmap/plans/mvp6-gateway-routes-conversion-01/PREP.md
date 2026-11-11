---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# Gateway explicit-routes re-derivation — prep (so the patch is mechanical once the owner confirms)

> **RULED 2026-10-18 (owner ny@gmgroup.ch): A accepted (explicit swap) · B reserve now (404-until-composed).**
> The two patches are built and verified under `patches/` and handed to the integration-agent. See
> `docs/records/decisions/2026-10/mvp6-gateway-explicit-routes-owner-decision-01.md`. The "no patch is
> written" line below was the pre-ruling state.

    item      : v9.6 §5 item 5 (gateway explicit routes), after ASSESSMENT.md found the old proposal
                superseded by OD-P1 · author: CT (gmg-10) · measured 2026-10-18 at HEAD
    purpose    : the authoritative route set derived from the CURRENT contracts on port 5066, the OD-7/8/9
                resolutions, and the two decisions the owner/integration-agent still holds. No ocelot.json
                patch is written — `gateway/**` is Protected, and two behaviour choices below are the
                owner's; with them settled, generating the routes from the table is mechanical.

## Target and current state (measured)

- Port is **5066** (OD-P1; the current catch-all already routes `/api/shipment-bundle` there). No port
  question remains.
- Current gateway at 5066: **two catch-all routes** — `/api/shipment-bundle` and
  `/api/shipment-bundle/{everything}` (GET POST OPTIONS). **Zero** `/api/supply-chain` routes.
- Route object schema (from the existing route) — the template every new route follows:

```json
{ "DownstreamPathTemplate": "<path>", "DownstreamScheme": "http",
  "DownstreamHostAndPorts": [ { "Host": "localhost", "Port": 5066 } ],
  "UpstreamPathTemplate": "<path>", "UpstreamHttpMethod": [ ... , "OPTIONS" ], "UpstreamScheme": "http" }
```

## A. shipment-bundle — 12 explicit routes, LIVE, ready now

Shipments/Carriers/Loads/Returns/Claims are **composed in production** (`Program.cs:67-78`, and `:169`
"controllers outside Features (Shipments) are always kept"), so these 12 replace the two catch-all routes
and every one resolves. Methods are from SHIPMENT-BUNDLE 3.3.0 + `OPTIONS` (OD-8 / NET-001). Upstream =
downstream = `/api/shipment-bundle` + path.

| # | path (under /api/shipment-bundle) | methods |
|---|---|---|
| 1 | `/shipments` | GET POST OPTIONS |
| 2 | `/shipments/{shipmentId}` | GET OPTIONS |
| 3 | `/shipments/{shipmentId}/transition` | POST OPTIONS |
| 4 | `/shipments/{shipmentId}/pod` | POST OPTIONS |
| 5 | `/carriers` | GET POST OPTIONS |
| 6 | `/carriers/{carrierId}/status` | POST OPTIONS |
| 7 | `/loads` | GET POST OPTIONS |
| 8 | `/loads/{loadId}/transition` | POST OPTIONS |
| 9 | `/returns` | GET POST OPTIONS |
| 10 | `/returns/{returnId}/transition` | POST OPTIONS |
| 11 | `/claims` | GET POST OPTIONS |
| 12 | `/claims/{claimId}/transition` | POST OPTIONS |

## B. supply-chain — 10 routes, but DEFER (they would 404 in production today)

SANDOP-CAPACITY 3.1.0 declares these ten, but **S&OP (MOD-0190) and Capacity (MOD-0192) are NOT composed in
production** (`Program.cs:83, :87`; Q484 keeps Capacity to the test host). Q381 drops both controllers, so
every `/api/supply-chain` route would route to 5066 and get a **404**. Adding them now advertises ten routes
that 404 until the §22/Q292 composition gate opens.

**Decision (owner/integration):** defer these ten until S&OP/Capacity are composed, or add them now as a
documented "reserved, 404-until-composed" set. CT recommends **defer** — the gateway should not publish
routes the production service cannot serve. For reference when they are added (base `/api/supply-chain`):

| path | methods | note |
|---|---|---|
| `/sandop-plans` | **POST** OPTIONS | **OD-9 resolved**: 3.1.0 declares POST only — no GET on the collection (the pack's GET claim is not in the contract) |
| `/sandop-plans/{sandopPlanId}` | GET OPTIONS | |
| `/sandop-plans/{sandopPlanId}/snapshots` | GET POST OPTIONS | |
| `/sandop-plans/{sandopPlanId}/sign-offs` | GET POST OPTIONS | |
| `/capacity-plans` | POST OPTIONS | Capacity: composed only in the test host (Q484) |
| `/capacity-plans/{capacityPlanId}` | GET OPTIONS | |
| `/capacity-plans/{capacityPlanId}/scenarios` | POST OPTIONS | |
| `/capacity-plans/{capacityPlanId}/scenarios/{scenarioId}` | GET OPTIONS | |
| `/capacity-plans/{capacityPlanId}/scenarios/{scenarioId}/evaluations` | POST OPTIONS | |
| `/capacity-plans/{capacityPlanId}/evaluations/{evaluationId}` | GET OPTIONS | |

## The open decisions (from the proposal's OD list), resolved or routed

- **OD-8 (OPTIONS):** resolved — every explicit route carries OPTIONS (NET-001), as the table shows.
- **OD-9 (sandop-plans GET):** resolved by the current contract — 3.1.0 has POST only on the collection, so
  the route is POST+OPTIONS. No owner call needed.
- **OD-7 (route-count guard):** `OcelotConfigurationTests` today counts only `/api/shipment-bundle`
  upstreams. CT recommends the guard assert **exactly 12** `/api/shipment-bundle` explicit routes on 5066,
  each carrying OPTIONS (and, when B lands, 10 `/api/supply-chain`). CT can draft this guard change in the
  same patch — it is `tests/**`, so handed over, not applied.
- **OD-12 (permission keys):** platform-owner, unrelated to routing — out of scope for this patch.

## The behaviour change the owner must accept

Replacing the catch-all with explicit routes changes gateway behaviour: an **undeclared** path under
`/api/shipment-bundle/...` currently proxies to 5066 via `{everything}`; with explicit routes it **404s at
the gateway**. That is the NET-001 intent (only declared operations are routed), but it is an
integration-owner decision, and `gateway/**` is a Protected Path — so applying any of this is the
integration-agent's, not CT's.

## What unblocks the patch

1. Confirm **5066** (already the tree's port) and accept the catch-all → explicit swap for A.
2. Decide **B: defer or reserve** the ten supply-chain routes.
3. Accept the **OD-7** guard shape (12 + OPTIONS; +10 when B lands).

With 1–3 settled, CT generates the ocelot.json routes from table A (and B if reserved) plus the OD-7 guard,
as a `git apply`-ready patch for the integration-agent — the same safe transcription the other lane
packages used.

## Files

- `PREP.md` — this. `ASSESSMENT.md` beside it records why the old 5061 proposal was superseded.
