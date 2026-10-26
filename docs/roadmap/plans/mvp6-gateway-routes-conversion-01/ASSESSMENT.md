---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# Gateway-routes proposal → apply-ready patch: cannot be converted, it is superseded — measured

    item      : v9.6 §5 item 5 (rows-3 gateway explicit routes) · owner: integration-agent
    author    : CT (gmg-10)
    measured  : 2026-10-17 on feature/mvp6-logistics at HEAD, against gateway/Diten.ApiGateway/ocelot.json
    verdict   : NO apply-ready patch produced — and that is the correct outcome. The existing proposal
                (`docs/records/audits/2026-10/mvp6-q221-q209-prework-01/OCELOT-PROPOSAL.md`) is superseded
                on its central fact (the ports) by owner decision OD-P1, and mechanically applying it would
                misroute the gateway.

## What was asked, and why it stops here

The task was to convert the prose `OCELOT-PROPOSAL.md` into a `git apply`-ready patch, as was done for
Q498 and BL-650. Measuring the proposal against the live tree first — as every other package in this lane
did — shows the proposal can no longer be applied: its port scheme is the reverse of what the tree now
carries, so a patch built from it would route logistics traffic to the wrong service. Converting it anyway
would be shipping a misroute as finished work.

## The supersession, measured three ways

| | proposal (2026-10-02 / BASE) | live tree today (HEAD) |
|---|---|---|
| total routes | 269 (working tree) / 275 (BASE) | **323** |
| `/api/shipment-bundle` → port | add explicit routes on **5061** | **5066**, via a 2-route catch-all (`/api/shipment-bundle` + `/{everything}`) |
| `/api/supply-chain` → port | add explicit routes on **5061** | not routed (0) |
| CRM | **move 5061 → 5065** (35 routes) | **stays on 5061** (47 routes) |
| port 5065 | the free slot CRM would move into | **suppliers / sourcing** (14 routes) |

The proposal predates **owner decision OD-P1**, which renumbered SupplyChain to **5066** and kept CRM on
**5061** (CLAUDE.md port table; `TrackedConfigurationGuardTests` asserts 5066; the handoff NEXT-PROMPTS §5
states it). So:

- the proposal's 20 new routes all target **5061** — which is the **CRM** service in the live tree. Applied
  as written, every logistics request would reach CRM.
- the proposal's "CRM 5061 → 5065" would move CRM onto the **suppliers** port.

Both are the opposite of OD-P1 and of the tree. The proposal's BASE (`67060bf9…`, 275 routes, from a
2026-09 tarball) and its route indices (`121-155` for CRM) no longer exist in the 323-route current file.

## Even re-based, it is not a CT conversion — it is an integration work package

The valid residual intent survives — **replace the 2-route `/api/shipment-bundle` catch-all with explicit
per-endpoint routes (NET-001), on 5066, and add the `/api/supply-chain` routes** — but turning that into a
correct patch requires decisions this lane cannot make, which the proposal itself booked as **OD-1…OD-12**:

- **OD-7** — no count/port guard exists for the `/api/supply-chain/` family; `OcelotConfigurationTests`
  only counts `/api/shipment-bundle/` upstreams. A route patch must land with its guard (integration-agent).
- **OD-8 / OD-9** — which routes carry `OPTIONS` (NET-001), and whether the S&OP collection keeps a `GET`
  the controller doesn't implement (pack owners).
- **OD-12** — the four modules' permission keys are in no catalogue or seed (platform owner).
- **Capacity routes specifically** collide with the open **Q484** state ("Capacity is NOT composed; routes
  404 by decision") and with **OD-1 / OD-2** (the executor's lease renewal and an unscoped tenant scan).
  Routing Capacity at the gateway is a reachability decision, not a transcription.

None of these is mine to decide, and several (Q484, the Capacity executor) are the same owner decisions
already open elsewhere in the queue. A gateway route set authored over them, unrunnable here (the gateway
and `OcelotConfigurationTests` cannot be exercised in this lane), would be low-confidence by construction.

## Recommendation

Treat item 5 as an **integration-agent work package**, not a CT prose→patch conversion. Its one cheap,
decision-free prerequisite is already settled — the port is **5066** (OD-P1), not the proposal's 5061 — so
the first concrete step is to **re-base the route list from `OCELOT-PROPOSAL.md` §2 onto 5066 against the
current 323-route tree**, then resolve OD-7/8/9/12 and the Capacity/Q484 question as part of the same
change. When the owner confirms the port and rules OD-7/8/9 (the mechanical ones), **I can then produce the
apply-ready additive patch** — at that point it is transcription against a known base, which is exactly the
kind of thing the other four packages did safely.

## What this package contains

- `ASSESSMENT.md` — this measurement. No `ocelot.json` patch, by design: the source proposal is
  superseded, and a patch from it would misroute the gateway.
