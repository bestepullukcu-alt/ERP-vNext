---
status: draft
kind: contract-change-proposal
contract: SHIPMENT-BUNDLE (docs/analysis/contracts/shipment-bundle.openapi.yaml)
from_version: 3.4.0
to_version: 3.5.0 (additive)
measured_at: branch docs/sce/mvp6-dilim-b-packs-01 @ 6fd5749af (contract file identical to 3.4.0 as published)
---

STATUS: DRAFT — a proposal, not an approval. The frozen contract file is **not changed** here. The change is applied by the
MOD-0183 DL-3 code change, exactly as `SHIPMENT-BUNDLE-3.5.0.patch` in this folder, under the "§24 DL-3 exception" of
MOD-0183's Repo Scope (the 3.4.0 pattern: proposal + patch here, applied with the code by DL-1).

# SHIPMENT-BUNDLE 3.5.0 — customer key, list search and list fields, return read and approval surface (proposal)

Sources:

- MOD-0183 r9, section "Screen slice B", item list "Contract changes needed" (24.7), items 1–4.
- MOD-0186 r10, section "Contract changes needed" (35.9), items 1–6 (items 1–4 joined 3.5.0 by Natig K1's decision of
  2026-11-03).
- Decision: the MVP-1 CT's answer on customer identity, as MOD-0177 owner, 2026-11-03, accepted by Natig (K1) the same day —
  `docs/records/decisions/2026-11/mvp6-screens-and-return-approval-owner-decisions-2026-11-02.md`, section
  "2026-11-03 — MVP-1 CT answer on customer identity (MOD-0177)", and Natig K1's four decisions of the same day appended
  there (sequencing: the whole of 3.5.0 lands with MOD-0183 DL-3; `getReturn` and MOD-0186 items 1–4 join 3.5.0).

Everything is **additive only**: no path, parameter, required field, type, enum value, status code or error code is removed or
changed; a 3.4.0 client keeps working unchanged. `info.version` goes 3.4.0 → 3.5.0.

## 1. `customerRef` — customer key on shipments and returns (decision points 1, 4)

New component schema `CustomerRef`: `type: [string, 'null']`.

| Rule | Text |
|---|---|
| Value | MOD-0149 CRM `Account.Id`, written as a Guid in "D" format, lower case |
| Comparison | case-insensitive, by every consumer |
| Service | stores it as given; never calls CRM; no format check on the wire (opaque, as `shipToReference`) |
| Who checks | the MVC create adapter (MOD-0183 24.1): lower-case Guid "D", and the account readable with the caller's token |
| Empty | null or absent on records written before 3.5.0, on Warehouse-intake shipments and on API calls that do not send it |
| Consumers when empty | keep their earlier rule; MOD-0177: `shipToReference`, then `"DOC:" + sourceDocumentId` |
| Old records | never matched to an account by a guess on `shipToReference` (SB-19, accepted limit) |
| Screens | never shown as a label |

Where it appears:

| Schema | Change | Note |
|---|---|---|
| `CreateShipmentCommand` | optional `customerRef` | the schema stays `additionalProperties: false`; `customerRef` is a declared property |
| `ShipmentSummary` | optional `customerRef` | `ShipmentDetail` (`allOf` of the summary) and every list row inherit it; MOD-0177 reads it through `getShipment` |
| `ReturnSummary` | optional, `readOnly` `customerRef` | `ReturnResponse` and the return list inherit it; MOD-0186 copies it from the shipment at create, never from the client (MOD-0186 35.6); `CreateReturnCommand` does not change |

`shipToReference` keeps its type and its required status. Only its description in `CreateShipmentCommand` and
`ShipmentSummary` says it is the delivery destination and carries no customer identity.

## 2. `queryShipments` — search and sort (MOD-0183 24.7 item 1)

| Addition | Type | Meaning |
|---|---|---|
| `search` (query) | string, max 100 | case-insensitive substring on `shipmentNumber` and `sourceDocumentId` only; names are not stored, so a customer or warehouse name is not searchable |
| `orderBy` (query) | enum `shipmentNumber`, `sourceDocumentId`, `status`, `plannedShipAt`, `plannedDeliverAt` | absent: today's order (by shipment id, `ShipmentRepository.cs:37`) is kept |
| `orderDir` (query) | enum `asc`, `desc`, default `asc` | read only with `orderBy` |
| `ShipmentListResponse.filteredTotal` | integer, optional | the count after `search` and the other filters; `total` keeps its 3.4.0 meaning |

## 3. `ShipmentSummary` — list fields (MOD-0183 24.7 item 2)

`warehouseReferenceId` and `shipToReference` are added to the summary, so list screens resolve the warehouse name and show an
old record's destination without a per-row `getShipment`. `ShipmentDetail` already requires both; its `allOf` is unchanged.

## 4. Examples

`createShipment` request, `getShipment` 200 and `queryShipments` 200 examples gain `customerRef`
(`14900000-0000-4000-8000-000000000001`, lower case); the list example also gains `warehouseReferenceId`, `shipToReference`
and `filteredTotal`. Existing example values are unchanged.

## 4a. Returns read and approval surface (MOD-0186 35.9 items 1–4; Natig K1, 2026-11-03)

Declaring is not serving. Each new operation carries `x-served-from`; until that slice ships, the service and the Gateway have
no route and the call answers 404. DL-5 stays outside the DL-3 / DL-4 approval.

| Item | Change | Served from |
|---|---|---|
| 1 | `GET /returns/{returnId}` `getReturn` → new `ReturnDetail` (`ReturnSummary` + `lines[]` with `shipmentLineNumber`, `quantity`, `uomId`, `inventoryTransactionReferenceId`, `inventoryPosting`, `inventoryPostingReason`, + `workflowInstanceId`, `contractVersion`); 200, 404 (`RETURN_NOT_FOUND`, one text for foreign and unknown) | MOD-0186 DL-4 |
| 2 | `POST /returns/{returnId}/submit-authorization` `submitReturnAuthorization`, `Idempotency-Key`, no body; 200 `ReturnResponse`, 400, 403, 404, 409, 422, 503 | MOD-0186 DL-5 |
| 3 | `ReturnSummary` gains optional `reasonCode`, `createdAt`, and read-only `authorizationState` (`None`, `PendingApproval`), `lastAuthorizationOutcome` (value set bound when DL-5 opens); `ReturnDetail` has `workflowInstanceId` | `reasonCode`, `createdAt`: DL-4; workflow fields: DL-5 |
| 4 | Codes `RETURN_APPROVAL_REQUIRED` (422 example on the shared `Unprocessable`), `RETURN_WORKFLOW_TEMPLATE_MISSING`, `RETURN_APPROVERS_NOT_CONFIGURED` (409), `RETURN_WORKFLOW_FORBIDDEN` (403), `RETURN_WORKFLOW_REJECTED` (400) as examples on the submit operation; `transitionReturn` description: "once MOD-0186 DL-5 is served" Authorized / Rejected come only from the workflow outcome; until then the 3.4.0 text stands | DL-5 |

The five codes enter `ContractErrorCodes` with the MOD-0183 DL-3 change (constants only), so `ContractErrorCodeParityTests`
stays green (MOD-0183 Repo Scope, §24 DL-3 exception).

## 5. Not changed

`ShipmentResponse` (the create acknowledgement carries no header field today), `ShipmentEventPayload`,
`ReturnEventPayload` and every event envelope, `CreateReturnCommand`, `TransitionReturnCommand`, every existing path, status code
and error code, and both lifecycles. The MVC adapter code `INVALID_CUSTOMER_ACCOUNT` is not a wire code and is not added.

## 6. Consumers measured on this branch

| Consumer | Reads | Effect of 3.5.0 |
|---|---|---|
| MOD-0186 `ReturnReferenceReader.ParseShipment` (`services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/Returns/ReturnReferenceReader.cs`) | `getShipment`: named top-level fields plus a duplicate check, no top-level allow-list; lines have an allow-list (`OptionalLineTexts`) | a new top-level `customerRef` is accepted; lines are unchanged. MOD-0186 DL-4 adds the read of the field |
| MOD-0177 `ShipmentBundleHttpReader` (`services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Recall/ShipmentBundleHttpReader.cs`) | `getShipment`: `status`, `actualDeliverAt`, `shipToReference` | unchanged until the MOD-0177 FU (MVP-1) reads `customerRef`; XMC-001: not touched here |
| MOD-0183 Web adapters | list, details, create | DL-3 / DL-4 per MOD-0183 24.9 (SB-2, SB-9, SB-20…SB-22) |

## 7. Check

`git apply --check docs/roadmap/plans/mvp6-integration-wp-p1-01/SHIPMENT-BUNDLE-3.5.0.patch` passes against the contract at
3.4.0 on this branch. The patched file was loaded with a strict duplicate-key YAML loader: no duplicate key; `info.version`
3.5.0; `CustomerRef` and `ReturnDetail` present; `ShipmentSummary`, `ReturnSummary`, `ShipmentListResponse` and the
`queryShipments` parameters carry the new fields; `/returns/{returnId}` and `/returns/{returnId}/submit-authorization` exist;
every `#/components/...` reference resolves. Not run here: the contract example tests (`ShipmentBundleContractExampleTests`), Prism, the
service build; they belong to the DL-3 change.

## 8. Approval path

Contract single writer (MOD-0183 Phase A, MVP-6 CT) → CT-APR-02 independent review together with MOD-0183 r9 and MOD-0186 r10 →
CT-APR-01 row → applied by MOD-0183 DL-3 with its code; the DocsPathGuard hash binding is a separate step (Q26 / Q30 pattern).
MVP-1 opens its MOD-0177 FU after reading this draft (decision point 6).
