# MVP6-SHIPMENT-REAL-AUTH-BROWSER-VER-01 — SOP §22

**Date:** 2026-09-24  
**Role:** independent browser verifier; repository product source read-only  
**Verdict:** **REWORK**

## Exact inputs

- READY handoff SHA-256: `1d186726d47ce5d3f6456cb0087464303e589fb5d6a8981d46ea0c3f4afdf279`.
- Final Shipment source manifest: 354 entries, SHA-256 `7b6d2f6a679275b74ab88989f5ca943b139e7f1a365b5ab99d6f1285ceb1acae`; 354/354 materialized paths matched.
- Accepted Auth successor manifest: 22 entries, SHA-256 `b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731`; 22/22 paths matched.
- Shipment source was materialized from the prior 351-entry immutable snapshot plus the accepted three-file Root R2 archive. The mutable checkout was not used as runtime source.

## Environment and evidence chain

Native `/Users/natig/.dotnet/dotnet` SDK 8.0.417 and .NET 8.0.23 were used. Auth, Platform, MDM, SupplyChain, Gateway and Web were rebuilt from the hash-bound disposable source. The browser lane used Mongo replica set `rsShipmentBrowserVer01` on `127.0.0.1:41994`, API ports 5856/5857/5859/5861, Gateway 5800 and Web 5801. Operational Mongo 27017 was not used.

Gateway restore did not complete in the restricted lane. After verifying the exact project input, the disposable copy used the repository's existing `obj` restore assets and performed a fresh Release compilation; the limitation and failed restore logs are retained in the raw archive. Web restore completed with a NuGet advisory-source warning, and its fresh compilation succeeded.

The IAB reached localhost while the processes were active. Fresh Auth logins were performed for the full Shipment user and the read-only user; no bearer token, password or signing secret is retained. The first SupplyChain restart attempt used an incorrect test audience and produced a bounded 401; it was stopped and superseded by a restart using the actual Auth issuer/audience (`diten-auth-service` / `diten-erp`). The same source-bound binary and database then returned the persisted detail successfully.

## Fresh browser results

1. **Create and detail passed.** The Compact form rendered all 13 inputs. Browser submission through the same-origin MVC adapter and Gateway created shipment `1127143a-62bc-4b3a-aeba-b42ed823b787`. Detail rendered the persisted line and authoritative Root R2 value `e8c03ed7-69b1-4b30-873c-8776a30107a7`.
2. **Restart persistence passed.** After restarting the same SupplyChain binary against the same isolated database, a new real Auth login rendered the same shipment, `Draft` status and root. Scoped persistence contained one shipment, history, audit, receipt and Pending outbox record.
3. **UAS-001 create denial passed.** The read-only actor received only the access-denied heading and remediation text. The create form and save action were absent.
4. **List page failed.** `/SupplyChain/Shipments` returned HTTP 500 because `Index.cshtml` uses relative partial names such as `_Filter`, while the explicit view is under `Views/SupplyChain/Shipments` and MVC searched the controller-derived locations. The actual partial exists; this is a view-resolution defect, not a missing source file.
5. **Transition failed closed.** `details.js` creates a new `X-Correlation-Id` for every transition/POD request. The backend correctly requires the persisted lifecycle root for Shipment mutations and returned `400 INVALID_REQUEST`; the persisted aggregate remained `Draft` with no extra write. This violates UI183-A11 and prevents browser transition, POD and their replay paths.

## Findings requiring a separate DEV rework

| ID | Severity | Finding | Exact source evidence | Effect |
|---|---|---|---|---|
| SHIP-UI-B01 | High | List view uses unresolved relative partial names. | `frontend/Diten.Web/Views/SupplyChain/Shipments/Index.cshtml:20-24`; partial exists at `.../Shipments/_Filter.cshtml`. | UI183-A01/A02/A14 and normal list navigation fail with HTTP 500. |
| SHIP-UI-B02 | High | Mutation trace header is a fresh UUID rather than the persisted authoritative root. | `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/details.js:63,75-76`; backend root enforcement in `TransitionShipmentHandler.cs:18`. | Transition returns 400; POD/replay browser paths cannot be reached. |

No source fix was made. The Root R2 independent HTTP evidence remains inherited and is not relabeled as browser evidence.

## PNG disposition

**OPEN.** The available IAB interface did not expose a documented persistent PNG save/export path. Previously rejected methods and security workarounds were not retried. This open criterion did not stop the other browser checks.

## Scope boundary and recommendation

This verifies only the bounded, isolated Shipment UI/browser work package. The correct verdict is **REWORK** for `SHIP-UI-B01` and `SHIP-UI-B02`. It does not grant CT/full-module acceptance, G5, rollout, canonical publication or migration/backfill approval. Platform aggregate health remained 503 and is preserved as an observation rather than converted to a Platform PASS.
