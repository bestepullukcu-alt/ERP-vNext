# MOD-0186 DL-1 — implementation plan (customer return → quarantine)

> **Revizyon r2 (2026-10-26):** paket MVP-1 CT incelemesine göre değişti (deneme anahtarı `…:a{n}`, tek aktif deneme, 403 → 403, saklı ilk deneme, seri iade v1 dışı, ortak senaryo). Bu plan r2 paketine göre okunur; paketle çelişen satırda paket kazanır. §4/§5'teki açık noktalar r2'de kapandı.

DURUM: PLAN — kod izni değildir. Kod, MOD-0186 revizyonunun CT-APR-02 incelemesi (MVP-1 CT), `main`'deki CT-APR-01 satırı
(Beste) **ve MOD-0183 §23 DL-1'in `main`'e girmesinden** sonra başlar (pack "only DL-1; after MOD-0183 DL-1"). Kaynak:
`MOD-0186-reverse-logistics.md` §34 (pack sha256 `6811f18c…`), `SHIPMENT-BUNDLE-3.4.0.patch`, INVENTORY-BUNDLE 1.5.0
`postMovement`, `DL1-IMPLEMENTATION-PLAN.md` (MOD-0183). Kod tabanı: `origin/main` 2026-10-23. Yazan: MVP-6 CT.

## 1. Shape of the change

Only the `InTransit → Received` transition changes; every other return transition stays as today ("Transitions must not
read external references", `TransitionReturnHandler`). Like the dispatch, the INVENTORY calls cannot sit inside the Mongo
transaction:

| Step | Where | What |
|---|---|---|
| A — read source | HTTP (existing shipment seam) | read the shipment detail through `ReturnReferenceReader` and, per return line, the original issue `transactionId` + `lotId` stored by MOD-0183 (see §4 point 1 — not on the wire yet). A line without one → 422 `RETURN_SOURCE_MOVEMENT_MISSING`, no movement (R34-6). |
| B — post | outside transaction | per return line `POST` `CUSTOMER_RETURN` into `QUARANTINE` with the §34.1 fields, deterministic key `{returnId}:{shipmentLineNumber}:customer-return:v1`, caller token, correlation id, `transactionDate`/`postingDate` from the validated `occurredAt`. `idempotentReplay: true` = success. Posted lines **stay posted** on a later refusal (no REVERSAL, §34.2). |
| C — commit | Mongo transaction | all lines posted → `Received`, per-line `InventoryTransactionReferenceId`, receipt, central-audit enlistment, frozen `ReturnReceived` event — one commit. |

A refusal (any INVENTORY 422) or 503 answers without a transition; the retry resends the same keys and the same body, so
INVENTORY replays the posted lines and posts only the missing ones (R34-3). Unlike the dispatch, no attempt document is
needed **if** the dates come from the command: a retry with a different `occurredAt` changes the INVENTORY fingerprint →
`IDEMPOTENCY_KEY_REUSED` (§4 point 3).

## 2. Files

| Layer | File (under `services/Diten.SupplyChainService/src/`) | Change |
|---|---|---|
| Contract | `shipment-bundle.openapi.yaml` (3.4.0, shipped with MOD-0183 DL-1) | `TransitionReturnCommand.toWarehouseId` / `toLocationId`, `RETURN_SOURCE_MOVEMENT_MISSING` are already in 3.4.0; the per-line issue fields need a further additive version (§4 point 1) |
| Domain | `Domain/Features/Returns/ReturnLine.cs`, `ReturnOrder.cs` | per-line `InventoryTransactionReferenceId`; `ToWarehouseId`, `ToLocationId` |
| Application | new `Application/Features/Returns/Receipt/ReturnReceiptCoordinator.cs`, `CustomerReturnMovementMapper.cs` | steps A–C; reuses MOD-0183's `IInventoryMovementClient` |
| Application | `Handlers/CommandHandlers/TransitionReturnHandler.cs`, `ReturnModels.cs`, `ReturnRequestFingerprint.cs`, `Validators/TransitionReturnValidator.cs` | route `Received` to the coordinator; new body fields (required for `Received`); fingerprint includes them |
| Infrastructure | `Infrastructure/Features/Returns/ReturnReferenceReader.cs` | accept the 3.4.0 line fields (§4 point 2) and read the issue fields |
| Persistence | `Persistence/Features/Returns/ReturnRepository.cs` | commit step C with per-line references |
| Web / l10n | `Resources/Views/SupplyChain/Returns/*.{en,tr,fr,es,zh,ar,ru}.resx` | localized text for the INVENTORY refusal codes and `RETURN_SOURCE_MOVEMENT_MISSING` (R34-3, 7 languages) |

## 3. Tests and sabotage proofs

Fake INVENTORY handler and fake shipment detail (both via `HttpMessageHandler`), owned replica set for the commit:

| AC | Test | Sabotage |
|---|---|---|
| R34-1 | 1-line and 3-line receipt → N `CUSTOMER_RETURN` requests, `toStockStatus = QUARANTINE`, item/SKU/level/lot from the issue | send `AVAILABLE`; take `lotId` from the request |
| R34-2 | replay of the receipt command → no second request, no second event | write the receipt before posting |
| R34-3 | line 2 refused → state unchanged, line 1 kept; retry → line 1 replay, line 2 posted, one event; refusal text in 7 languages | send a REVERSAL for line 1 |
| R34-4 | INVENTORY 503 / timeout → 503, no transition, no event | map 503 to 422 |
| R34-5 | cross-tenant / cross-LE return → 404, zero requests | drop the scope predicate |
| R34-6 | shipment line without an issue movement → 422 `RETURN_SOURCE_MOVEMENT_MISSING`, zero requests | default the lot to the return body |
| (seam) | shipment detail with the 3.4.0 line fields → parsed, not 502 | keep the old strict line whitelist |

Then the full SupplyChain assembly unfiltered, the contract parity test, and the Gateway tour extended "dispatch → return →
quarantine" on lane ports 6100–6199 (MVP-1 live tour "iade → karantina" is the acceptance).

## 4. Points to settle before code (raised by this plan; not decided)

1. **The issue transactionId / lotId is not readable over the seam.** §34.1 says MOD-0186 reads what MOD-0183 stored, but the
   return reads the shipment only through the HTTP shipment detail (`ReturnReferenceReader`: "Shipment detail is the only
   HTTP seam"), and SHIPMENT-BUNDLE 3.4.0 adds only `dispatchState`, not the per-line issue fields. Proposal: add read-only
   `ShipmentLine.issueTransactionId` (and the issued `lotId` is already the line's `lotId`) to the 3.4.0 patch **before** it
   ships with MOD-0183 DL-1, so no 3.5.0 is needed. Alternative (reading MOD-0183's collection directly) bypasses the seam's
   authorization — not recommended.
2. **MOD-0183 DL-1 breaks today's return reader.** `ReturnReferenceReader.Line` rejects every line property except the five
   required ones and `inventoryReferenceId`, so the 3.4.0 fields (`skuLevel`, `lotId`, `fromLocationId`,
   `fefoBypassReason`) make every return read answer 502 `DEPENDENCY_RESPONSE_INVALID`. The reader fix must ship **in MOD-0183
   DL-1** (same change as the contract), not wait for MOD-0186 DL-1. Add to `DL1-IMPLEMENTATION-PLAN.md` and its test list.
3. **Retry dates.** A retry with a different `occurredAt` would change the INVENTORY fingerprint. Proposal: the receipt stores
   the first attempt's dates on the return (as MOD-0183 §23.4 does) and resends them; the pack's §34.2 should say so.
4. **Which contract.** §34.1 / §34.2 say the warehouse fields and the missing-movement code come in a "RETURNS contract
   addition, separately versioned / to be named"; the 3.4.0 patch already carries both (`toWarehouseId`, `toLocationId`,
   `RETURN_SOURCE_MOVEMENT_MISSING`). Proposal: align the pack text to "SHIPMENT-BUNDLE 3.4.0".
5. **The client-supplied `inventoryTransactionReferenceId`** on the transition body (order level, today stored as given)
   conflicts with the per-line references the service now sets at `Received`. Proposal: on `Received` the client value is
   refused (400) and the service value wins; other transitions unchanged.

Points 1, 2 and 4 belong with the MVP-1 CT's findings on MOD-0183 / MOD-0186 (they change the pack hashes and the 3.4.0 patch).
