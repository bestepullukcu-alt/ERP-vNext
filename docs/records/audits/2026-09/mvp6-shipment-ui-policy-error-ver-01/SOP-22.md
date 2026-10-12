# MVP6-SHIPMENT-UI-POLICY-ERROR-VER-01 — SOP §22

Date: 2026-09-24  
Role: independent verifier, repository source read-only  
Branch / HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
Verdict: **REWORK**

## Scope and source binding

This lane examined only the policy/error gaps left open by `mvp6-shipment-ui-functional-ct-consolidation-01`. Root R2 and B01/B02 were treated as inherited, content-bound evidence and were not reopened.

- Final 354-entry source manifest: `e6551f4552bd05682dcd9f2753044aa0b803dd4df90875952cca89608ab98ab3`; 354/354 current disposable inputs matched.
- Build source manifest: `516ba828bed602e4355a5cc6871501cbb3b3c72d60473fa7ec73d135bbadf911`; 3333/3333 matched.
- Auth successor 22-path manifest: `b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731`; 22/22 matched.
- Native runtime: SDK `8.0.417`, runtime `8.0.23`; no major roll-forward.
- Isolated Mongo: `127.0.0.1:42314`, replica set `rsShipmentPolicy01`, PRIMARY before probes. Operational `27017` was not used.

Fresh Release builds of Auth, Platform, MDM, SupplyChain, Gateway and Web completed with exit 0. The exact runtime binaries are recorded in `SOURCE-BINARY-PROCESS-BROWSER.tsv` and the raw archive.

## Result

### Closed or materially advanced

- **UI183-A07 PASS:** real Auth tokens proved dispatch-only cancel denial, cancel-only dispatch denial, and read-only transition/POD denial as `403 INVALID_REQUEST`; every denied mutation had zero delta in shipments, receipts, audit and outbox. The browser action surface independently showed only `Dispatched` for a dispatch-only actor and did not expose cancel or POD.
- **UI183-A08 PARTIAL:** a stale second transition produced exact `422 INVALID_SHIPMENT_TRANSITION` with zero persisted delta. The final UI maps that code and reloads detail, but this lane did not manufacture a live browser race; the browser-specific stale refresh remains unclosed.
- **UI183-A09 PARTIAL:** POD from Draft produced `422 INVALID_SHIPMENT_TRANSITION`; second POD after successful capture produced `409 POD_ALREADY_CAPTURED`; both had zero persisted delta. The final UI maps both codes and reloads detail. A live browser stale-POD sequence was not executed.
- **UI183-A12 PARTIAL:** deleted, foreign-tenant and unknown detail pages showed the same safe not-found message with a support reference and no record data. Cross-LE returned the same backend `404 SHIPMENT_NOT_FOUND`. Scoped before/after counts for shipments, receipts, audit and outbox were unchanged. Cross-LE was not repeated as a separate browser session.
- Focused final-source tests: **9/9 PASS** (`SupplyChainShipmentsControllerTests` plus `ShipmentDetailActionTests`). They bind permission selection, scope/header fail-closed behavior, frozen query forwarding, authoritative lifecycle root separation and stable mutation intent.

### Findings and open criteria

1. **SHIP-UI-POLICY-01 — UI183-A03 REWORK.** The controlling requirement says direct adapters return `401/403`. A fresh unauthenticated `GET /SupplyChain/Shipments/api` returned `302 Found` with `Location: /account/login?...`. `[Authorize]` at `SupplyChainShipmentsController.cs:11` is using the cookie challenge surface before the adapter-local `401` path at lines 148–149 can run. No source was changed.
2. **UI183-A10 OPEN (runtime portion).** Final source retains one payload/key for create and one payload/key/root tuple for transition/POD, and deletes the intent only for `IDEMPOTENCY_KEY_REUSED`. No authorized process-boundary 500/503/unknown-response injection seam exists in this scope. The verifier did not add one and does not claim runtime retry PASS.
3. **UI183-A11 PARTIAL.** The browser displayed correlation-based support references on safe 404 surfaces. A dedicated copy interaction and the full error-precedence matrix were not independently run.
4. **PNG OPEN.** No supported durable PNG export was available. No restricted capture workaround was attempted.
5. **Platform aggregate health remains observed 503.** Successful Auth issuance and consumer traffic do not convert aggregate Platform health to PASS.

## Browser/operator disclosure

The root verifier operated the browser for the fresh real-Auth session, permission-filtered action surface, and safe detail presentations. The verifier probe separately measured HTTP status/body/header/correlation and Mongo before/after deltas. This is not represented as a second independent browser run.

An initial browser session created before the Platform-to-MDM resolver configuration was corrected lacked `legal_entity_id`; it is retained as a setup failure and is not acceptance evidence. A fresh Auth login after the lane-owned Platform/MDM restart supplied the accepted session.

## Evidence boundary

This verdict is limited to the policy/error lane on the exact 354-source snapshot. It does not alter the prior Root R2 or B01/B02 decisions and does not grant CT acceptance, full-module acceptance, G5, rollout, or PNG waiver.

