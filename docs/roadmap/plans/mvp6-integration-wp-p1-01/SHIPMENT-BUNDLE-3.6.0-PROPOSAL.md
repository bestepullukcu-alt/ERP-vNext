---
status: draft
kind: contract-change-proposal
contract: SHIPMENT-BUNDLE (docs/analysis/contracts/shipment-bundle.openapi.yaml)
from_version: 3.5.1
to_version: 3.6.0 (minor with a justified semver exception, O-R11-1 option C)
annex: returns-semantics-v3.0.0.md -> returns-semantics-v3.1.0.md (new file; v3.0.0 kept)
measured_at: HEAD a0c3c7194 (contract file 3.5.1 as published by MOD-0183 DL-8 20aaffc78; SHA-256 8f7564b3…e241, the DocsPathGuard pin)
---

STATUS: DRAFT — a proposal, not an approval. The frozen contract file is **not changed** here. The change is applied
exactly as `SHIPMENT-BUNDLE-3.6.0.patch` in this folder (MOD-0186 r12 §36.2, RC-9). Contract single writer: MVP-6 CT.

# SHIPMENT-BUNDLE 3.6.0 — Returns correlation root behind the Gateway (proposal)

Sources:

- MOD-0186 pack r12, §36 "Correlation root behind the Gateway (r11)": rules RR-1…RR-9, 36.2 items C-1…C-4, 36.5 RC-9,
  36.7 O-R11-1 (`execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md`).
- Decisions (Beste, MVP-6 CT): 2026-11-20 C path; 2026-11-24 O-R11-1 option C — both in
  `docs/records/decisions/2026-11/mod-0186-correlation-c-path-owner-decision-01.md`.
- Review that asked for this proposal: `docs/records/reviews/MOD-0186/2026-11-24-f533773b.md` (F-1).

## 1. What changes

| # | Place | 3.5.1 | 3.6.0 |
|---|---|---|---|
| V | `info.version` | 3.5.1 | 3.6.0 |
| V | `info.x-changelog` | — (the file has no changelog) | new list with one line for 3.6.0, which also states the semver exception (§3). Earlier versions are not back-filled |
| C-1 | `createReturn.description` | "SHIPMENT-BUNDLE 3.0.0 … returns-semantics-v3.0.0.md … inherited Shipment root" | annex v3.1.0, "the return's correlation root", plus a 3.6.0 paragraph: the received `X-Correlation-Id` is the return's root (RR-1), not compared with the shipment's `lifecycleCorrelationId` (RR-2), never `X-Client-Correlation-Id` (RR-6), link = `shipmentId`; replay with same key and body → original, `idempotentReplay=true`, correlation not compared, header = current request (RR-5); `CORRELATION_ROOT_MISMATCH` no longer a 409 here |
| C-1 | `transitionReturn.description` | same annex sentence; "overrides returns-semantics-v3.0.0.md" | annex v3.1.0 in both places, plus a 3.6.0 paragraph: the request's correlation is only the command's trace (RR-4), need not equal the root, no refusal, on every target incl. Received (§34, RR-2); events, envelopes and audit carry the stored root (RR-3); replay as above (RR-5); `CORRELATION_ROOT_MISMATCH` no longer a 409 here |
| C-1 (consequence) | `queryReturns.description` | same annex sentence (v3.0.0, "inherited Shipment root") | points to v3.1.0, "the return's correlation root". Not named in C-1; changed so no Returns operation points at the superseded annex |
| C-2 | `components/parameters/CorrelationId.description` | 3.5.1 sentence "Loads, Returns ve Claims yollari bu surumde degismez" | "… 3.5.1'de degismedi" (the 3.5.1 sentence kept as history) + 3.6.0: on Returns paths the "envelope correlation = request correlation" rule holds only for `createReturn`; later Returns commands (Received included) carry the stored root, the request's correlation is the trace; Loads and Claims unchanged |
| C-3 | `createReturn` 409 `examples` | `CORRELATION_ROOT_MISMATCH`, `IDEMPOTENCY_KEY_REUSED`, `RETURN_SOURCE_CHANGED` | the `CORRELATION_ROOT_MISMATCH` example removed (7 lines); status 409, its description and headers stay |
| C-3 | `transitionReturn` 409 `examples` | `CORRELATION_ROOT_MISMATCH`, `IDEMPOTENCY_KEY_REUSED`, `RETURN_RECEIPT_IN_PROGRESS` | the `CORRELATION_ROOT_MISMATCH` example removed (7 lines); the rest stays |
| C-4 | annex | `returns-semantics-v3.0.0.md` | new file `returns-semantics-v3.1.0.md` (§2) |

## 2. The annex `returns-semantics-v3.1.0.md`

A copy of v3.0.0 with these lines changed (v3.0.0 line numbers): `:1` title; `:3-5` authority (the v3.0.0 activation text
is replaced by the 3.1.0 authority: pack r12 §36 and both decisions); `:6-7` the shipment root is still read on create but is
not the return's root and is never compared; `:9` "mismatch409" removed (`Missing/null503; malformed502` kept — O-R11-2);
`:24` precedence without "root"; `:30-31` replay by fingerprint only, correlation not compared, header = current request;
`:33` returns created before DL-6 keep their root, no back-fill (RR-1); `:66-67` `CORRELATION_ROOT_MISMATCH` is not a
Returns code, `RETURN_SHIPMENT_ROOT_*` stay (create only); `:76` "Persist inherited Shipment root" → the local create root.
New section "3.1.0 — correlation root": root, trace, events/audit, replay (RR-1…RR-6) and the r12 F-5 sentence — the
`correlationId` of a return envelope carries no link to the shipment, the link is `shipmentId` only, returns created before
DL-6 carry the shipment's root, no consumer may use the envelope correlation to find the shipment.
`returns-semantics-v3.0.0.md` and `shipment-root-semantics-v3.0.0.md` are not changed.

## 3. Version kind — minor 3.6.0 with a justified exception (O-R11-1 option C)

Removing a declared error example (C-3) is not additive; strict semver would make it 4.0.0. It is 3.6.0 because:

1. From MOD-0186 DL-6 on no Returns path sends `CORRELATION_ROOT_MISMATCH` (RR-7: `ReturnRepository.cs:31`, `:71`, `:109`
   and `ReturnReceiptCoordinator.cs:50` leave), so no consumer can receive it; removing a response the server never sends
   breaks no client.
2. The only code that names it for Returns is the Web Returns adapter and page, which drop it in the same DL-6 change (RR-9);
   no other module calls `/api/shipment-bundle/returns` and nothing consumes return events (§4).
3. Loads keeps the code (`LoadRepository.cs:17`, `:45`; 409 examples of `createLoadPlan` / `transitionLoad` unchanged), so
   `ContractErrorCodes.CorrelationRootMismatch` and `ContractErrorCodeParityTests` stay as they are.

The exception is written in the CT decision record (section "O-R11-1 — seçenek C") and in the `x-changelog` line.
Condition (CT proposal, see the decision record): the patch lands in the same change as DL-6, so there is no window in which
the contract omits a code the server still sends.

## 4. Consumers (measured on HEAD a0c3c7194)

| Consumer | Uses | Effect of 3.6.0 |
|---|---|---|
| Web `SupplyChainReturnsController` (only caller of `/api/shipment-bundle/returns`, `:45`) | code list `:55`, comment `:348`; root as `X-Correlation-Id` `:560`, `:573` | DL-6 (RR-8, RR-9) |
| Returns page `index.js:84`, `:876`, `:924`, `:1226`; `index.l10n.js:157`; `_IndexL10n.cshtml:130`; `ReturnsIndex.{en,tr,fr,es,zh,ar,ru}.resx:104` | the code and its text | DL-6 (RR-9) |
| Web tests `ReturnFormContractTests.cs:22`, `ReturnIndexBehaviorTests.cs:18`, `:177` | list the code | DL-6 (RR-9) |
| SupplyChain Returns (producer) `ReturnRepository.cs:31`, `:71`, `:109`; `ReturnReceiptCoordinator.cs:50`; tests `ReturnReplayTests.cs:13`, comments `ReturnsClaimsHttpHost.cs:43`, `ReturnsClaimsObservabilityTests.cs:72` | send / expect the code | DL-6 (RR-2, RR-5, RC-1…RC-6) |
| Contract tests `ShipmentBundleContract.cs:17`, `ContractErrorCodeParityTests.cs:12` | read the YAML | parity unchanged (code stays in Loads); Returns responses validated against 3.6.0 |
| Loads: `LoadRepository.cs:17`, `:45`, `SupplyChainLoadsController.cs:16`, Loads `index.js:274`, `:338`, `:450`, `LoadsIndex.*.resx:63`, Loads tests | Loads code | unchanged |
| `ReturnReferenceReader.cs:85`, `ClaimReferenceReader.cs:120`, `LoadReferenceReader.cs:66`, Inventory `ShipmentBundleHttpReader.cs:66`, `:118`, `:135` | `getShipment` / `queryShipments` | unchanged |
| Gateway `ocelot.json` `/returns` routes (`:6338`-`:6379`) | pass-through | unchanged |
| Return event consumers | none outside SupplyChain (Pending outbox, no publisher) | none |

## 5. Check

- `git apply --check SHIPMENT-BUNDLE-3.6.0.patch` and `git apply --check --cached …` pass on HEAD `a0c3c7194` (working tree
  CRLF and index LF). Base is 3.5.1: the patch's first hunk carries `version: 3.5.1`, so it does not apply to a line that
  still has 3.5.0 (`main` today) — inference from the context lines, not run.
- The patched YAML loads with YamlDotNet 16.3.0: `info.version` 3.6.0; `createReturn` 409 examples =
  `IDEMPOTENCY_KEY_REUSED`, `RETURN_SOURCE_CHANGED`; `transitionReturn` 409 examples = `IDEMPOTENCY_KEY_REUSED`,
  `RETURN_RECEIPT_IN_PROGRESS`; `code: CORRELATION_ROOT_MISMATCH` remains twice (Loads).
- SHA-256 (LF): patched YAML `6024d3f0738a4d55830ced9e97e7b4db41e4c6576ece3da6163e38ade615d7bb`; new annex
  `60ded8ddc5bb8c496086601a4edccdc1a9980c8c53b04e69aef08c0fc2df34bb`.
- Not run: contract example tests, Prism, service and Web builds; they belong to DL-6.

## 6. Approval path and follow-ups

MVP-6 CT (single writer) → applied byte-for-byte with DL-6 (RC-9) → DocsPathGuard re-pin of the YAML target from
`8f7564b3…e241` to the patched hash, as a separate owner record (the 3.5.0 / 3.5.1 pattern,
`docs/reference/architecture/docs-path-authority.json`). `docs/analysis/contracts/README.md` has no SHIPMENT-BUNDLE row, so
no index row is added (3.4.0, 3.5.0 and 3.5.1 added none either).
