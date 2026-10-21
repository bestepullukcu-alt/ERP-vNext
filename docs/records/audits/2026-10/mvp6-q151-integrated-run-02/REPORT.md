---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: pass
commit: 1ace4f30e
modules: [MOD-0183, MOD-0184, MOD-0185, MOD-0186, MOD-0187]
---

# Q151 integrated run 2 — PASS: all five golden flows through the gateway, persistence + audit 1:1

    run       : 2026-10-18 by CT (gmg-10), same guarded lane as run 1
                (`mvp6-q151-integrated-run-01`, decision: fail — not edited, K4; this record supersedes its verdict)
    fix under test : `1ace4f30e` — D1 canonical edge UUIDs + D2 gateway-signed root adoption (owner-approved
                under XMC-001; gateway suite 254/254)
    stack     : gateway 5000 (explicit routes `8f6efe3a9` + fix `1ace4f30e`, lane signing key via
                `Observability__Correlation__SigningKey`) · supplychain 5066 · mdm 5059 · lane mongod 35151 (rsq151).
                27017 never touched (guarded; DBs confirmed on 35151).
    actor     : lane-minted HS256 JWT, fresh tenant, ACTIVE legal entity created + activated through MDM's API.
    verdict   : **PASS — the Stage-2 gate (OD-Q15 A / Q151).** Supersedes Q435's INTEGRATION_STALE.

## The five flows — all through `http://127.0.0.1:5000/api/shipment-bundle`

| # | module | flow | result |
|---|---|---|---|
| 1 | MOD-0183 Shipment | `POST /shipments` | **201** Draft · root `697badce-…` (36-char canonical, gateway-signed) |
| 1b | | `POST /{id}/transition` Planned → Dispatched → Delivered, echoing the root | **200, 200, 200** |
| 2 | MOD-0184 Carrier | `POST /carriers` | **201** Active |
| 3 | MOD-0185 Load | `POST /loads` (fresh shipment, its root echoed) | **201** Draft |
| 4 | MOD-0186 Return | `POST /returns` against the Delivered shipment, root echoed | **201** Requested |
| 5 | MOD-0187 Claim | `POST /claims` against the Delivered shipment, root echoed | **201** Open |
| — | all five | `GET /shipments｜carriers｜loads｜returns｜claims` | **200** each (run 1: 400 for four) |
| — | gateway | undeclared `/api/shipment-bundle/not-a-route` | **404 at the gateway** (NET-001) |

## Persistence and audit — 1:1, no orphans (lane DB `Q151_SupplyChain`)

```
sce_shipments 3 · sce_shipment_receipts 7 · _history 7 · _audit 7 · _outbox 7
   (7 mutations = run-1 shipment: create + 1 direct transition; run-2 shipment: create + 3; load shipment: create)
carriers 1 · carrier_audit 1 · carrier_idempotency 1
loads 1 · loads_audit 1 · loads_outbox 1 · loads_receipts 1 · load_assignments 1
returns 1 · returns_audit 1 · returns_outbox 1 · returns_receipts 1 · return_entitlements 1
claims 1 · claims_audit 1 · claims_outbox 1 · claims_receipts 1
```

Every run-1 rejection (the three 400 transitions, four 400 lists) wrote **nothing** — fail-closed held. The
**lifecycle chain is bound through the gateway**: `returns_audit.CorrelationRoot` and `claims_audit.CorrelationRoot`
both equal the shipment's gateway-signed root `697badce-9369-4f52-a5dd-3d2067b46a73`.

## The UI path

Already designed for root echo: `Shipments/details.js` sends `authoritativeRoot()` (= `lifecycleCorrelationId`) on
transitions; the Returns and Claims MVC proxies resolve the shipment root server-side and send it on POST. They
were blocked only by D1/D2, so they are unblocked once the gateway runs with a signing key. Not driven in a
browser here (the local tenant-birth wall still applies).

## Runtime requirement this creates (carry into deploy/dev config)

The gateway **must** run with `Observability:Correlation:SigningKey` set (a secret, like `JwtSettings:Secret`;
never committed). Without it the gateway behaves as INTX FIX2 exactly — it never adopts, and multi-step
lifecycles through the gateway fail again (D2). Recommended follow-up: fail-fast in non-Development when the key
is absent, as the gateway already does for the JWT secret. Shipments created before `1ace4f30e` carry an unsigned
N-format root and cannot be continued through the gateway — lane/dev data only; production holds none.

## Not covered

BL-652 (Admin role grant) is still unapplied — the run used a minted token; real tenant Admins need BL-652 for
`mdm.legal-entities.read`. Platform registration and the MDM event bus were not running (best-effort side
channels). Wave 6.1 (0190/0192) is out of this gate.
