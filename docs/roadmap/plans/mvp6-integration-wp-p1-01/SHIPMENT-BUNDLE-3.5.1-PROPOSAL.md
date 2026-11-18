---
status: draft
kind: contract-change-proposal
contract: SHIPMENT-BUNDLE (docs/analysis/contracts/shipment-bundle.openapi.yaml)
from_version: 3.5.0
to_version: 3.5.1 (description only)
measured_at: main @ 6ef2c884b (contract file 3.5.0 as published)
---

STATUS: DRAFT — a proposal, not an approval. The frozen contract file is **not changed** here. The change is applied by the
MOD-0183 DL-8 code change, exactly as `SHIPMENT-BUNDLE-3.5.1.patch` in this folder, under the "§26 DL-8 exception" of
MOD-0183's Repo Scope (the 3.4.0 / 3.5.0 pattern).

# SHIPMENT-BUNDLE 3.5.1 — Shipments correlation root behind the Gateway (proposal)

Sources:

- MOD-0183 r11, section 26 "Correlation root behind the Gateway (r11)", rules R11-1, R11-2, R11-5.
- Decision: Natig, 2026-11-15, option C (the service decides the root; the request's correlation is the command trace).
- Cause: WP-PLATFORM-AUDIT-INTX-01 FIX2/FIX3 — the Gateway mints its own `X-Correlation-Id` and forwards the caller's value
  apart on `X-Client-Correlation-Id` (`gateway/Diten.ApiGateway/Observability/CorrelationPropagationDelegatingHandler.cs:24-36`).

## 1. What changes (text only)

| Place | 3.5.0 | 3.5.1 |
|---|---|---|
| `info.version` | 3.5.0 | 3.5.1 |
| `components/parameters/CorrelationId.description` | "event envelope correlationId equals the request's" for every operation | same sentence, plus: on Shipments paths it holds only for `createShipment`; later Shipments commands' events carry the stored root; Loads, Returns, Claims unchanged (their annexes govern) |
| `createShipment.description` | none | the received `X-Correlation-Id` is the shipment's root (`lifecycleCorrelationId`); behind the Gateway it is the Gateway's; replay with same key + body returns the original with `idempotentReplay=true`, correlation not compared, header = current request |
| `transitionShipment.description` | lifecycle table | + request correlation is only the command's trace, need not equal the root and is no refusal reason; events carry the stored root; replay as above |
| `captureProofOfDelivery.description` | "only from Dispatched or InTransit" | + same correlation and replay text |

## 2. Not changed

Every path, parameter, required field, schema, enum, status code, error code and example; every event payload and envelope
schema; Loads, Returns and Claims operations and the annexes `returns-semantics-v3.0.0.md`, `claims-semantics-v3.0.0.md`,
`loads-semantics-v3.1.0.md`, `shipment-root-semantics-v3.0.0.md`. `x-status` stays FROZEN. Wire `contractVersion` stays `v1`.

Why it is a version and not a silent edit: the parameter description is normative ("birebir ayni tasinir"), and r11 changes what
a later Shipments command's event carries relative to its request. A 3.5.0 client is not broken: it may still send the root as
its correlation; the service no longer requires it.

## 3. Consumers

| Consumer | Effect |
|---|---|
| Web `SupplyChainShipmentsController` + `details.js` / `create.js` | DL-8 R11-7: details stops sending the root as correlation |
| MOD-0186 `ReturnReferenceReader`, MOD-0187 `ClaimReferenceReader`, MOD-0185 `LoadReferenceReader` | read `getShipment` incl. `lifecycleCorrelationId`; unchanged (the root value and its meaning are unchanged) |
| MOD-0177 `ShipmentBundleHttpReader` (Inventory) | unchanged |
| Event consumers | unchanged: events already carried the stored root |
| r10 planned 3.6.0 (MOD-0183 §25.3.1) | not yet a patch file on its branch; cut from 3.5.1 instead of 3.5.0 |

## 4. Check

`git apply --check SHIPMENT-BUNDLE-3.5.1.patch` passes against the contract at 3.5.0 on `main` @ `6ef2c884b`. The patched file
loads as YAML (Ruby Psych): `info.version` 3.5.1; the four descriptions read as in §1. Not run here: the contract example tests
(`ShipmentBundleContractExampleTests`), Prism, the service build; they belong to the DL-8 change.

## 5. Approval path

Contract single writer (MOD-0183 Phase A, MVP-6 CT) → CT-APR-02 independent review together with MOD-0183 r11 → CT-APR-01 row →
applied by MOD-0183 DL-8 with its code; the DocsPathGuard hash binding (docs-path repin) is a separate step, as for 3.4.0 / 3.5.0.
