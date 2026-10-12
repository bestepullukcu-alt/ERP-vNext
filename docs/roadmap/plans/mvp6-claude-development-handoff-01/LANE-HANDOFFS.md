# Lane state and predecessor chain

All paths below are relative to `docs/records/audits/2026-09/`. These are historical writer/verifier/CT roles, not active transferred agents.

| Lane | Writer / technical evidence | CT / next dependency |
|---|---|---|
| Root R2 | mvp6-shipment-root-r2-emission-exec-01; mvp6-shipment-root-r2-emission-independent-ver-01 | mvp6-shipment-root-r2-ct-disposition-01; bounded mutation accepted |
| B01/B02 | mvp6-shipment-ui-b01-b02-rework-01; mvp6-shipment-ui-b01-b02-independent-ver-01 | mvp6-shipment-ui-functional-ct-consolidation-01 |
| Accessibility | mvp6-shipment-line-accessibility-rework-01; mvp6-shipment-line-accessibility-independent-ver-01 | mvp6-shipment-line-accessibility-ct-disposition-01; bounded accepted |
| Shared UI | mvp6-shipment-shared-ui-exec-01; mvp6-shipment-shared-ui-independent-ver-01 | mvp6-shipment-shared-ui-ct-disposition-01; A03/PRES-183-02 accepted |
| A08/A09/A12 original | mvp6-shipment-remaining-acceptance-disposition-01/evidence/a08-a09-a12-01 | mvp6-shipment-a08-a09-a12-ct-disposition-01; exact stale cases closed, A12 presentation failed |
| A12 correction | mvp6-shipment-a12-safe404-rework-01; mvp6-shipment-a12-safe404-independent-ver-01 | CONDITIONAL STATIC PASS / RUNTIME NOT RUN; complete runnable baseline required |
| A10 / DataTable | mvp6-shipment-remaining-acceptance-disposition-01 | Proxy authority and four policy decisions missing; no execution permission inferred |
| Carrier | mvp6-carrier-auth-chain-recovery-01; mvp6-carrier-real-auth-e2e-exec-01 | mvp6-carrier-real-auth-ct-review-01; PARTIAL, PNG open |
| Loads amendment | mvp6-loads-root-acquisition-amendment-candidate-02; mvp6-loads-root-amendment-independent-rever-01 | mvp6-loads-root-amendment-release-prep-01; publication/runtime HELD |
| Capacity/S&OP | mvp6-bc-successor-exec-02; mvp6-bc-successor-independent-ver-01 | mvp6-bc-successor-ct-handoff-01; isolated bounded acceptance |

Supplier concurrence package lives under `docs/roadmap/plans/mvp6-supplier-concurrence-close-prep-01/`. DC-01 ownership, DC-02 Supplier/LE, DC-03 security binding/revocation, DC-04 Metric Registry and DC-05 Risk/source remain separate owner decisions. Policy selection does not supply their concurrence.

## A12 exact identity
- Patch: `b3c7dcbb68636b123f3ea3ad7c3c902709fef6a9775c66cc3d464073653ecefb`
- 360-source successor manifest: `8ffa6c96d29f8004940f310ed704a2c05eaa38ffa501d6114a5f9251e6e00d36`
- Archive: `7b6a0d1abea649d920040a039972c5d4928825e9cf2cc185487b1fe5f0f3314d`
- Delta: `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/details.js` and `frontend/Diten.Web.Tests/JavaScript/ShipmentDetailActionTests.cs` only.

Accepted shared predecessor 360 manifest: `7d7bec63a1f2e9e906864e5dea6dc96a00e94a280b830dd06f95a8b68246b4fc`; source archive `490d51be87d249265a2cc9fe6d1f8f3b23bd6976f4e5830d733bff2dfff613a3`.

The earlier 354-source identities are historical predecessors, not interchangeable execution targets. Recover complete build inputs separately; do not silently combine arbitrary current shared source with these manifests.
