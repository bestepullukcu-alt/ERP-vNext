# Findings

## SHIP-UI-POLICY-01 — unauthenticated direct adapter redirects

- Requirement: `docs/roadmap/plans/mvp6-shipment-pod-ui-scope-01/ACCEPTANCE.md:7` requires direct adapters to return 401/403.
- Request: `GET http://127.0.0.1:6041/SupplyChain/Shipments/api`, no cookie or bearer.
- Observed: `302 Found`, zero-byte body, `Location: http://127.0.0.1:6041/account/login?ReturnUrl=%2FSupplyChain%2FShipments%2Fapi`.
- Source: `frontend/Diten.Web/Controllers/SupplyChainShipmentsController.cs:11` applies `[Authorize]`; the adapter-local missing-token `401` at lines 148–149 is therefore unreachable for an unauthenticated request under the current cookie challenge.
- Disposition: **REWORK**. The verifier made no auth/middleware/controller change.

## UI183-A10 — runtime failure seam absent

- Create retains `intent.payload` and `intent.key` across network errors and non-`IDEMPOTENCY_KEY_REUSED` failures (`create.js:33-58`).
- Transition/POD retains body, key and authoritative lifecycle root and clears only on `IDEMPOTENCY_KEY_REUSED` (`details.js:74-86`).
- Controller maps timeout/refusal to 503 and unexpected exceptions to 500 (`SupplyChainShipmentsController.cs:126-140`).
- No authorized runtime injection seam was found for exact 500/503/unknown-response mutation evidence. This stays **OPEN**, rather than treating static/source tests as runtime evidence.

## Fixture correction retained

The first deleted-record setup updated `Id` rather than Mongo `_id`, so it returned 200. The immutable first result remains in `policy-error-cases.json`. `scope-safe-correction.json` records the corrected `_id` fixture and fresh 404 results. This is a fixture error, not a product defect.

