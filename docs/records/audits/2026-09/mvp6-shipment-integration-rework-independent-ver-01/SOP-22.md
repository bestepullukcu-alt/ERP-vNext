# MVP6-SHIPMENT-INTEGRATION-REWORK-INDEPENDENT-VER-01 — SOP §22

**Date:** 2026-09-24  
**Role:** independent verifier; writer source was not used as a mutable workspace  
**Verdict:** **PARTIAL — BUILD/ROUTE/UNAUTHENTICATED RUNTIME PASS; REAL-AUTH SHIPMENT E2E AND BROWSER OPEN**

## Immutable inputs

- Base HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- Writer `ARTIFACTS.sha256`: `3842230d59a650f5842e344abad4a34711b28390a740fd07539e847f2fb1d183`; every listed artifact passed `sha256sum -c`.
- Writer overlay: `8aba3d890f160cbed88eecc284cdcba78eda03b6b0955cf3061199a62171112e`.
- Writer final manifest: `a469678926c0b085075e6c14d8977789e26eb2e787558acf86905081030fa51e`; fresh disposable materialization matched **351/351** paths.
- Current Carrier Auth-chain source: the independently accepted 22-path manifest was materialized from its durable successor archives and matched **22/22** target hashes. The current Carrier handoffs remain bounded PASS evidence; no token or secret was copied.

The verifier created `/private/tmp/mvp6-shipment-integration-ver-01/source` from base HEAD, the durable Auth successor source, and the exact writer overlay. The writer worktree was not read as the runtime source.

## Fresh results

| Area | Result | Evidence |
|---|---|---|
| SupplyChain Release build | PASS, 0 warnings / 0 errors | `raw/supplychain-build-final.log` |
| Gateway Release build | PASS, 0 warnings / 0 errors | `raw/gateway-build-final.log` |
| CRM Release build | PASS, 2 inherited nullability warnings / 0 errors | `raw/crm-build-final.log` |
| Web Release build | PASS, 15 inherited warnings / 0 errors | `raw/web-build-final.log` |
| Gateway route tests | PASS, 24/24 | `raw/route-tests-recorded.log`, `.trx` |
| Shipment UI focused tests | PASS, 14/14 | `raw/frontend-focused-final.log`, `.trx` |
| Route ownership | PASS | 35 CRM routes at 5065; 6 ShipmentBundle routes at 5061; zero mismatch in `raw/route-ownership.tsv` |
| Native runtime | PASS, bounded | SupplyChain/Gateway/Web exact Release binaries listened at 5061/5800/5801; health 200 and source-to-binary hashes recorded |
| DB-010 | PASS | isolated replica set `rsShipmentIntegrationVer01` at 127.0.0.1:39994; no 27017 use |
| Authentication rejection | PASS | anonymous MVC request redirected to login; anonymous Gateway Shipment list returned contract-shaped 401 with correlation |
| Process restart | PASS, infrastructure only | same SupplyChain binary restarted and returned health 200; Shipment collections remained zero |
| Real-Auth Shipment flows | OPEN | see boundary below |
| Browser render/action flows | OPEN | CUA could not reach the approval-isolated localhost process namespace (`ERR_CONNECTION_REFUSED`) |
| Durable PNG | OPEN | no supported browser export reached a rendered Shipment page; no alternate capture bypass was attempted |

The first build attempt supplied a repository-root `NuGet.Config` path that does not exist. It is retained as discarded setup evidence. The cache-based native .NET 8 builds above are controlling. A sandboxed test-host attempt also hit local named-pipe permission restrictions; the controlling focused tests were rerun successfully in the approved local test context.

## Real-Auth and browser boundary

The current Carrier Auth handoffs were verified and their accepted behavior was not re-opened. They intentionally archive no bearer token, session cookie, signing secret, reusable credential, or live process, and their fixture processes/databases were cleaned. The Shipment writer handoff also contains no exact replayable Auth fixture. Therefore this verifier could not independently issue a fresh Auth session for Shipment without inventing a new identity/permission fixture outside the handed-off bytes.

The exact Shipment/Gateway/Web processes were approval-isolated. Terminal HTTP probes could reach them; the browser provider ran outside that localhost namespace and returned `ERR_CONNECTION_REFUSED`. Consequently list/create/detail/transition/POD, replay/conflict, tenant/LE isolation, independent permissions, real-row actions, UAS-001 browser rendering and durable PNG remain OPEN. No diagnostic bearer was minted and no Carrier result was relabelled as Shipment acceptance.

## Disposition

The integration successor closes the former compilation and route-port blockers: exact source builds and the CRM/SupplyChain route split pass independently. It does **not** yet satisfy the controlling composed real-Auth browser acceptance. A successor evidence run needs an exact, reproducible real-Auth fixture handoff for Shipment and a browser surface able to reach the isolated localhost processes. No product defect was observed in the scope actually executed.

This technical PARTIAL is not CT acceptance, full MOD-0183 acceptance, rollout, E5 or G5.

