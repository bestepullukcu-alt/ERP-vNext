---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: fail
commit:
modules: [MOD-0183, MOD-0184, MOD-0185, MOD-0186, MOD-0187]
---

# Q151 integrated run (through the gateway) — FAIL: two gateway-correlation defects block 4 of 5 modules

    run       : 2026-10-18 by CT (gmg-10), guarded lane bring-up (owner decision
                mvp6-next-steps-owner-decisions-2026-10-18 #1)
    stack     : gateway 5000 (explicit routes applied, 8f6efe3a9) · supplychain 5066 · mdm 5059 ·
                lane mongod 127.0.0.1:35151 replica set rsq151 (fresh scratchpad dbpath). The operational
                mongod on 27017 was never touched: every service's mongo URI was overridden by env var and a
                guard refused to start any service pointing at 27017; both services' DBs (Q151_SupplyChain,
                Q151_Mdm) were confirmed created on 35151. Platform 5057 / Auth 5056 / RabbitMQ not run — their
                connection errors are best-effort side channels (module registration, MDM event bus) and
                blocked no request.
    actor     : lane-minted HS256 JWT (issuer diten-auth-service, audience diten-erp, lane-only secret, never
                written to the repo) for a fresh tenant, carrying the 26 supplychain.* keys + mdm.legal-entities.*.
    scope     : a new ACTIVE legal entity created + activated through MDM's own API; lookup-validation 200.
    verdict   : **FAIL.** The routing layer works; the gateway's correlation policy breaks the downstream
                contracts. This is the first through-the-gateway run — the 2026-10-17 golden flows went direct
                to 5066 and could not see either defect.

## What works through the gateway

| request | result |
|---|---|
| `GET /api/shipment-bundle/shipments` | **200** |
| `POST /api/shipment-bundle/shipments` | **201** `Draft` (shipment 84202b87…) |
| undeclared `/api/shipment-bundle/not-a-route` | **404 at the gateway** (NET-001, the explicit-route intent) |

## D1 — the edge correlation is not a canonical UUID (blocks Carrier, Load, Return, Claim entirely)

`services/Diten.Platform.Common/src/Diten.Platform.Common/Observability/CorrelationIdMiddleware.cs:62` mints the
gateway's correlation as `Guid.NewGuid().ToString("N")` — 32 hex, **no hyphens**. The gateway replaces the
caller's `X-Correlation-Id` with it (INTX FIX2/FIX3, `CorrelationPropagationDelegatingHandler.cs:24-27`).
Carrier/Load/Return/Claim validate it with `Length == 36 && Guid.TryParseExact(text, "D")`
(`CarrierContextMiddleware.cs:14`, `LoadContextMiddleware.cs:14`; 6 such strict parses in the Api) and answer
**400 `X-Correlation-Id must contain exactly one UUID value`** on every request. Shipments parses with lenient
`Guid.TryParse` and passes. The SupplyChain contract requires the canonical form: "X-Correlation-Id: current
valid request UUID (**lowercase hyphenated**)" (`shipment-bundle.openapi.yaml:324`).

| module | direct to 5066 | through gateway |
|---|---|---|
| carriers / loads / returns / claims `GET` | 200 | **400** |

## D2 — the edge re-mints correlation per request, but lifecycle mutations must carry the root (blocks every follow-up mutation)

SupplyChain binds a shipment's lifecycle to its **root correlation** (the create request's) and requires later
transitions — and Returns/Claims against that shipment — to carry it (recorded 2026-10-17;
`ShipmentTests.cs:179` asserts 400 for a foreign root). The gateway never adopts a caller's correlation and
mints a fresh one for every request, so a client **cannot** present the root through the gateway.

Measured: the shipment's root is the gateway-minted `01e5b5d4…889e` (32-char, N-format). Transition to
`Planned` **through the gateway → 400**; the same transition **direct to 5066 carrying that root → 200**.

D1 compounds D2: even once a root could be presented, it is stored in N-format, which the D-strict
Return/Claim paths could not accept.

## Known design, already on the backlog

The gateway policy is deliberate (INTX FIX2: stop a client choosing another request's correlation). The open
backlog item (product-backlog.md:9967, CT/gateway, 2026-10-03) already proposes the fix for its HCM/CRM
consequence: the gateway **HMAC-signs** the id it mints and adopts an inbound correlation **only if it carries
its own valid signature**; services forward it unchanged. That design also resolves D2 without touching the
frozen shipment-bundle contract. D1 is a one-line, contract-mandated format fix in a shared library.

## Lane state

Left running for the rerun after the fix: mongod 35151 (rsq151), supplychain 5066, mdm 5059, gateway 5000.
Lane secret + tenant live only in the scratchpad lane env. No repo file was changed by this run.
