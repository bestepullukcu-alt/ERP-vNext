---
id: MOD-0142
name: Receiving (GRN)
domain: procurement
service: Diten.ProcurementService
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: draft
owner: procurement / control-tower
branch: feature/procurement-mvp-2
started: 2026-09-16
target: 2026-10-15
form_field_count: 12
---

# MOD-0142 — Receiving (GRN)

> **Status guard.** `draft` — DCP-010 `approved`/`ready-for-execution` + bu pack `ready-for-dev` olmadan kod YOK (CAP-001 §7). Bu pack ready-for-dev **hedefli** yazılmıştır; Ali DCP-010'u onaylayınca `ready-for-dev`'e alınır. DCP-010 §8 sırasında 0140→0145→0141'den sonra scaffold edilir (W-2).
> **Contract authority.** Entity alanları OWNED `docs/analysis/contracts/grn-event.openapi.yaml`'dan (FROZEN v1) türetilir; uydurma yasak (K12). Ürün/tedarikçi/stok/lokasyon kimliği consume edilir, yaratılmaz.
> **No shadow stock.** GRN envanteri **kendi balance'ı olarak tutmaz**; yalnız frozen INVENTORY-BUNDLE `POST /api/inventory/movements` (`GOODS_RECEIPT_PO`) ile post eder ve dönen `inventoryTransactionId` referansını saklar. İkinci stok balance/ledger YASAK (AD-2, rapor §21.1 rule 3).

## 1. Module Summary
Mal kabul (Goods Receipt Note / GRN) SoR'u. Hedef kullanıcı: depo/receiving operatörü + procurement kontrolör. PO'ya karşı fiziksel teslim alma kaydı; her satır frozen INVENTORY-BUNDLE'a `GOODS_RECEIPT_PO` movement olarak post edilir ve `GoodsReceived` event yayınlanır (0143 Invoice-Match / 0175 QC dinler). Blueprint W-2, Procurement Suite / P2P; Min Integration Contract = GRN-BUNDLE. Envanter gerçeği **MOD-0173'te** kalır; bu modül yalnız mal kabul dokümanını + dönen transaction referansını sahiplenir.

## 2. Ownership and Boundaries
- **In-scope:** goods receipt master (GRN başlık + satır), receiving exceptions (item/lokasyon/PO uyuşmazlığı — exception kaydı), match artifacts (GRN↔PO satır eşleşme referansları), GRN lifecycle (Draft/Posted/Reversed), INVENTORY'ye post + `GoodsReceived` event üretimi.
- **Out-of-scope / SHADOW STOCK YASAK:** **stok balance/ledger/valuation (MOD-0173/SCE — GRN ikinci balance AÇMAZ; yalnız `POST /movements` çağırır ve `inventoryTransactionId` saklar)**, ürün/SKU kimliği (MOD-0290/MDM), fiziksel lokasyon master (DEC-INV-03/SCE), lot/serial master yaşam döngüsü (MOD-0174 TRACE — GRN yalnız `lotNumber` verir, lot'u TRACE oluşturur), 3-yönlü eşleştirme kararı (MOD-0143), QC dispozisyon (MOD-0175), AP/ödeme (Finance/Treasury).
- **Owned contract:** `GRN-BUNDLE` (`grn-event.openapi.yaml`, x-owner MOD-0142, **x-status FROZEN v1**). Değişiklik yalnız additive (K16); mevcut şekil daraltılmaz/değişmez.

## 3. Owned Objects
- **Entity:** `GoodsReceipt` (EntityBase), `GrnLine` (embedded), `ReceivingException` (GoodsReceipt'e bağlı — item/lokasyon/PO iş kuralı ihlali kaydı).
- **Commands:** RecordGrn (create + INVENTORY post), ReverseGrn (INVENTORY `REVERSAL`/`SUPPLIER_RETURN` ile — **ASSUMPTION-GRN-02**), (soft) DeleteGrn, BulkDeleteGrn.
- **Queries:** GetGrnList, GetGrnById.
- **DTOs:** `grn-event.openapi.yaml` şemaları (GrnRequest, GrnResponse, GrnLine, GoodsReceivedEvent, Error). CONSUMED tipler (MovementRequest/MovementType) **redefine edilmez**, yalnız çağrı için map edilir.
- **API endpoints:** `POST /api/grn` (recordGrn, contract), `GET /api/grn/{grnId}` (getGrn, contract), `POST /api/grn/{grnId}/reverse` (**ASSUMPTION-GRN-02**), `DELETE /api/grn/{grnId}` + `POST /api/grn/bulk-delete` (**ASSUMPTION-GRN-03**, soft-delete; contract-additive).
- **Frontend route:** `/Procurement/Grn` (tenant shell).
- **Permissions:** `procurement.grn.read|create|delete|bulk-delete`, `procurement.grn.reverse` (**ASSUMPTION-GRN-02**).

## 4. Entity Fields
| Field | Type | Rules | Index |
|---|---|---|---|
| GrnId | string (public code, ör. `GRN-10001`) | server-assigned; unique/tenant+LE | Unique (Tenant+LE) |
| PoId | string | nullable; upstream MOD-0141 PO referansı (yaratılmaz) | — |
| WarehouseId | string | required; consumed LOCATION (yaratılmaz) | — |
| LocationId | string | nullable; consumed LOCATION | — |
| Status | enum(Draft/Posted/Reversed) | required; default Draft → başarılı INVENTORY post sonrası Posted; reverse → Reversed | — |
| Lines[] | GrnLine (embedded) | ≥1 satır (contract `lines` required) | — |
| Lines[].PoLineId | string | nullable | — |
| Lines[].ItemId | Guid (uuid) | required; consumed MOD-0290 (fail-closed 422 UNKNOWN_ITEM) | — |
| Lines[].SkuId | Guid (uuid) | required; consumed MOD-0290 | — |
| Lines[].SkuLevel | enum(Gsku/Lsku/FinishedGood) | required | — |
| Lines[].LotNumber | string | nullable; lot MOD-0174 TRACE `createLot` ile oluşur (GRN saklamaz) | — |
| Lines[].SerialIds[] | string[] | nullable | — |
| Lines[].Quantity | Decimal (string, `^-?\d+(\.\d+)?$`) | required; **float YASAK** | — |
| Lines[].UomId | string | required | — |
| Lines[].ToStockStatus | enum(AVAILABLE/QUALITY_INSPECTION/QUARANTINE) | required | — |
| Lines[].InventoryTransactionId | string | **INVENTORY `POST /movements` sonucu — salt referans, balance DEĞİL** | — |
| Lines[].LotId | string | nullable; INVENTORY/TRACE post sonucu referans | — |
| ReceivingExceptions[] | ReceivingException {code, message, correlationId} | UNKNOWN_ITEM/UNKNOWN_LOCATION/PO mismatch (**ASSUMPTION-GRN-01**) | — |
| ReceivedAt | date-time | server-assigned | — |
| ContractVersion | string | sabit `v1` (contract yüzeyi) | — |
| SourceSystem/ExternalRef | string | nullable (DEC-INV-19 dış besleme) | — |
| TenantId, LegalEntityId | Guid | **server-resolved, payload'da YOK**; her sorguda filtre | Compound |
| IdempotencyKey | string | `Idempotency-Key` header; replay-safe (balance mükerrer artmaz) | Unique (Tenant+LE) |
| Id, IsDeleted, DeletedAt, CreatedAt, UpdatedAt | (EntityBase/audit) | soft-delete zorunlu | — |

> `form_field_count: 12` > 8 → `golden_reference: compact`. Sayım (kullanıcının create formunda doldurduğu modül alanları): başlık **poId, warehouseId, locationId** (3) + satır bazlı **poLineId, itemId, skuId, skuLevel, lotNumber, serialIds, quantity, uomId, toStockStatus** (9) = **12**. `idempotencyKey` teknik header (sayılmaz); `TenantId/LegalEntityId/GrnId/Status/ReceivedAt/ContractVersion/InventoryTransactionId/LotId` server-resolved/türev (sayılmaz).

## 5. Repo Scope
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Features/Grn/**`
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Domain/**` (GoodsReceipt, GrnLine, ReceivingException entities)
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Persistence/**` (repositories, Mongo index config, INVENTORY/TRACE contract client'ları)
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Api/Controllers/GrnController.cs`
- `frontend/Diten.Web/Views/Procurement/Grn/**` + `wwwroot/assets/js/Procurement/Grn/**` + `Resources/Views/Procurement/Grn/**`

## 6. Protected Paths
- `.antigravity/**`
- `services/Diten.SupplyChainService/**`, `services/Diten.MdmService/**` ve diğer domain servisleri
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration-agent owned — ayrı task)
- `frontend/Diten.Web/Views/Archive/**`, `Views/Shared/_Layout.cshtml`
- `docs/analysis/contracts/inventory-bundle.openapi.yaml`, `product-master-bundle.openapi.yaml`, `location.openapi.yaml`, `trace.openapi.yaml` (CONSUMED — redefine YASAK, K16)
- `docs/analysis/contracts/grn-event.openapi.yaml` (OWNED-FROZEN — sessizce şekil değiştirilmez; yalnız additive)

## 7. Dependencies
- **Backbone:** JWT/RBAC (MOD-0018), Audit (MOD-0021), Event Bus (MOD-0035, `GoodsReceived` yayını), Reference Data (MOD-0048), Notification (MOD-0027).
- **Consumed contracts (frozen, redefine YASAK):** INVENTORY-BUNDLE (MOD-0173, `POST /api/inventory/movements` `GOODS_RECEIPT_PO`/`SUPPLIER_RETURN` — mock şimdi, gerçek 0173 sonra; **GRN kodu değişmez**), PRODUCT-MASTER (MOD-0290, `itemId/skuId` doğrulama — fail-closed), LOCATION (`warehouseId/locationId`), TRACE (MOD-0174, `lotNumber` → lot oluşturma). Upstream: MOD-0141 PO (`poId/poLineId`).
- **Downstream consumers:** MOD-0143 Invoice-Match (`GoodsReceived` event + GRN referansı), MOD-0175 QC (`GoodsReceived` event, `QUALITY_INSPECTION` statüsü).

## 8. Runtime Constraints
- Servis portu **5062**; frontend yalnız Gateway (5000) üzerinden çağırır; servis portuna doğrudan gitmez.
- **Tenant + Legal-Entity izolasyonu her sorguda** (server-resolved; cross-LE fail-closed → 404; INVENTORY post'unda cross-LE → 403 CROSS_LE_FORBIDDEN).
- **Shadow stock YASAK:** GRN miktarı yalnız INVENTORY `POST /movements` ile post edilir; GRN kendi balance kolonu tutmaz, yalnız `inventoryTransactionId` referansı saklar.
- **Idempotent posting** (`Idempotency-Key`); replay'de mükerrer INVENTORY hareketi YOK (aynı key → orijinal `transactionId`).
- **Reversal via INVENTORY** (`REVERSAL`/`SUPPLIER_RETURN`) — GRN kaydı **edit edilmez**; düzeltme yeni reverse hareketiyle (append-only, DEC-INV-07).
- Soft delete (`IsDeleted`/`DeletedAt`). Mongo V3 GUID subtype-4.
- Money/quantity = `Decimal` (string), **float YASAK**.

## 9. Layout & Shell Contract
- `shell: tenant` → tüm `Views/Procurement/Grn/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` **AÇIKÇA**.
- View klasörü: `Views/Procurement/Grn/` · Frontend route: `/Procurement/Grn`.

## 10. Backend File Convention
Golden Reference **Compact** birebir: `Features/Grn/` altında Commands/, Queries/, Handlers/CommandHandlers/, Handlers/QueryHandlers/, Validators/, `GrnModels.cs`. Naming: Command/Query record; Handler/Validator class **suffix YOK** (`RecordGrnHandler`, `RecordGrnValidator`, `GetGrnByIdHandler`). INVENTORY/TRACE çağrıları Persistence/Infrastructure katmanındaki contract client'ları üzerinden (iç tip paylaşımı yok, yalnız contract).

## 11. Frontend File Contract
Compact seti: `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml` (`data-dt-standard="v2"`), `_IndexL10n.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml` (başlık + satır grid), `GrnIndex.cs` + `index.js` + `index.l10n.js`. **YASAK:** `_CreateEditOffcanvas`, `_DetailsQuickView` (compact). L10n: tenant modülü → **7 dil** (en, tr, fr, es, zh, ar, ru).

## 12. Validation Rules
| Field | Required | Rule | DB | Pre-check |
|---|---|---|---|---|
| WarehouseId | Evet | consumed LOCATION; boş değil | — | LOCATION contract |
| Lines | Evet | ≥1 satır | — | — |
| Line.ItemId | Evet (satırda) | uuid; MOD-0290'da mevcut (fail-closed) | — | PRODUCT-MASTER doğrulama |
| Line.SkuId | Evet (satırda) | uuid; MOD-0290 | — | PRODUCT-MASTER doğrulama |
| Line.SkuLevel | Evet (satırda) | enum(Gsku/Lsku/FinishedGood) | — | — |
| Line.Quantity | Evet (satırda) | Decimal-string `^-?\d+(\.\d+)?$`, >0; **float YASAK** | — | — |
| Line.UomId | Evet (satırda) | boş değil | — | — |
| Line.ToStockStatus | Evet (satırda) | enum(AVAILABLE/QUALITY_INSPECTION/QUARANTINE) | — | — |
| IdempotencyKey | Evet (header) | boş değil; replay-safe | Unique (Tenant+LE) | ExistsByIdempotencyKeyAsync |

## 13. Failure Path to Verify
- **Unknown item/SKU** (MOD-0290'da yok) → 422 `UNKNOWN_ITEM` + GRN post edilmez (fail-closed, silent-pass YOK) + INVENTORY hareketi oluşmaz.
- **Unknown location** → 422 `UNKNOWN_LOCATION` + post yok.
- **Idempotency replay** (aynı `Idempotency-Key`) → orijinal `GrnResponse`/`transactionId`; **mükerrer INVENTORY hareketi YOK**, ikinci balance artışı YOK.
- **Cross-LE posting** → 403 `CROSS_LE_FORBIDDEN` (INVENTORY seam) + GRN başka LE'ye yazamaz.
- **Unauthorized actor** (izin yok) → 403 + action disabled (UAS-001: iskelet çizilmez, yönlendirme yok).
- **Cross-tenant/LE okuma** → 404 (sızıntı yok).
- **Unknown GRN** (getGrn) → 404 `UNKNOWN_GRN`.

## 14. Authorization Convention
- Policy: `[Authorize]` (tenant actor).
- Permission format (PKS-001, lowercase-dotted, ≥3 segment): `procurement.grn.{action}`.
- Actions: read, create, delete, bulk-delete, reverse (**ASSUMPTION-GRN-02**).
- Actor: tenant_user (procurement/receiving rolü); server-side `[HasPermission]` zorunlu.

## 15. Gateway / API Routing Decision
- Karar: Gateway değişikliği **gerekli** (`/api/grn` → 5062). `ocelot.json` protected; bu pack yazmaz — explicit Upstream/Downstream + OPTIONS içeren **integration-agent task**'ı olarak ayrı yürütülür. GRN'in çağırdığı `/api/inventory/movements` route'u MOD-0173 tarafında (mock/gerçek); procurement yalnız consumer.

## 16. Acceptance Criteria
- [ ] `POST /api/grn` idempotent create → 201 `GrnResponse`; her satır INVENTORY `POST /movements` (`GOODS_RECEIPT_PO`) ile post edilir ve `inventoryTransactionId` GRN satırında saklanır.
- [ ] **Shadow stock yok:** GRN hiçbir yerde kendi stok balance kolonu tutmaz; miktar yalnız INVENTORY'de artar (E5 kanıt: INVENTORY `getBalance`/`transactions` GRN post'unu gösterir, GRN kaydında balance alanı YOK).
- [ ] **G2A golden flow segmenti:** `PO → GRN → 0173 inventory posting → invoice 3-way match` — GRN'in **gerçekten 0173'e (mock/gerçek) post ettiği** kanıtlanır (idempotent, L3, evidence **E4** state-changing + **E5** cross-module GRN→INVENTORY seam). Replay ikinci hareket üretmez.
- [ ] `GoodsReceived` event yayınlanır (0143/0175 tüketir); event payload contract `GoodsReceivedEvent` şekliyle (`inventoryTransactionId` dahil).
- [ ] Fail-closed: unknown item → 422 `UNKNOWN_ITEM`; unknown location → 422 `UNKNOWN_LOCATION`; INVENTORY erişilemezse post yok (silent-pass YOK).
- [ ] Reverse (**ASSUMPTION-GRN-02**): INVENTORY `REVERSAL`/`SUPPLIER_RETURN` ile; GRN edit edilmez, Status→Reversed; balance düzeltmesi INVENTORY'de.
- [ ] Tenant+LE izolasyon: cross-LE 404 (okuma) / 403 (post); unauthorized 403 (UAS-001).
- [ ] GRN-EVENT contract yüzeyi (`recordGrn`/`getGrn`) **değişmeden** yanıt verir (FROZEN v1; additive-only).
- [ ] Tüm `Views/Procurement/Grn/*.cshtml` → `Layout = "_LayoutTenantShell"` açık; `verify_datatable_page.py --area Procurement --module Grn --reference compact` PASS.
- [ ] RESX 7-dil parite PASS.

## 17. Test Expectations
- Unit: validator (missing warehouse/lines, invalid quantity/decimal, empty idempotency-key), handler (recordGrn map → MovementRequest, reverse).
- Integration: tenant isolation (cross-LE 404/403), soft-delete, **idempotency replay (mükerrer INVENTORY hareketi YOK)**, fail-closed unknown item/location.
- **Cross-module (E5):** GRN→INVENTORY seam — GRN post'u INVENTORY mock/gerçek `POST /movements`'a `GOODS_RECEIPT_PO` gönderir, `inventoryTransactionId` döner ve saklanır; **GRN'de shadow balance olmadığı** doğrulanır (INVENTORY `getBalance` tek gerçek).
- Contract: `grn-event.openapi.yaml` mock (Prism) ile producer uyumu; `inventory-bundle.openapi.yaml` mock ile consumer seam uyumu (redefine YOK).
- Frontend: browser smoke + DataTable verifier PASS.
- Build: ProcurementService + frontend + gateway 0-error.

## 18. Ready-for-dev Checklist
- [ ] Golden Reference Compact okundu
- [ ] Frontmatter tam (service/shell/golden_reference/entity_base)
- [ ] Layout & Shell Contract Razor Layout açık
- [ ] Backend/Frontend file convention golden reference ile birebir
- [ ] Validation her field için yazılı
- [ ] Failure Path ≥4 senaryo (unknown item/location, idempotency replay, unauthorized, concurrency/cross-LE) + cross-tenant
- [ ] Authorization permission listesi + policy + actor
- [ ] Gateway routing kararı (integration-agent task)
- [ ] Acceptance test edilebilir + G2A zincirinde GRN→0173 post rolü (E4/E5) + shadow-stock-yok kanıtı
- [ ] Test expectations build/verifier/RESX/smoke/contract-mock + cross-module seam kapsıyor
- [ ] DCP-010 approved + bu pack ready-for-dev onayı (kod kapısı)

## 19. Implementation Notes
DCP-010 §8 sırasında W-2 dilimi (0140→0145→0141→**0142**→0143→0144). **Progressive integration:** GRN, frozen INVENTORY-BUNDLE'a post eder — mock şimdi (`prism mock inventory-bundle.openapi.yaml`), gerçek MOD-0173 sonra; contract sınır olduğu için GRN kodu değişmez (AD-2/§8). GRN-EVENT OWNED-FROZEN v1; şekli değiştirilmez, gerekirse additive-only. Kritik tasarım kuralı: GRN **doküman + dönen `inventoryTransactionId`** sahibidir, **stok gerçeği değil** — envanter tek SoR MOD-0173. INVENTORY `MovementRequest.sourceModule=MOD-0142`, `sourceType=GOODS_RECEIPT`, `sourceDocumentId=GrnId`, `sourceLineId=poLineId` map edilir (contract örneğiyle uyumlu).

## 20. Follow-up Items
- **ASSUMPTION-GRN-01:** GRN-EVENT contract "receiving exceptions" ve "match artifacts" SoR objelerini (domain-config §In-Scope) explicit şema olarak vermiyor; bu pack `ReceivingException` embedded modelini contract `Error` (UNKNOWN_ITEM/UNKNOWN_LOCATION) kodlarından türetir. Exception queue kalıcılık modeli freeze kapısında netleşir (additive-safe açıklık).
- **ASSUMPTION-GRN-02:** GRN-EVENT `status` enum'u `Reversed` içerir ama **reverse endpoint/permission** contract'ta tanımlı değil. `POST /api/grn/{grnId}/reverse` + `procurement.grn.reverse` additive olarak eklendi (INVENTORY `REVERSAL`/`SUPPLIER_RETURN` seam'i mevcut). Freeze owner review'da contract'a additive yazılır.
- **ASSUMPTION-GRN-03:** Permission seti `delete|bulk-delete` içeriyor ama contract yalnız `recordGrn`/`getGrn` tanımlar. Soft-delete endpoint'leri (`DELETE /api/grn/{grnId}`, `POST /api/grn/bulk-delete`) golden-reference compact deseni gereği additive eklendi; contract yüzeyi FROZEN v1 korunur.
- MOD-0143 Invoice-Match `GoodsReceived` event tüketimi (0143 pack) ve MOD-0175 QC dispozisyonu → ayrı modül.
- Gerçek MOD-0173 devreye girince mock→gerçek geçiş doğrulaması (contract sınırı, kod değişmez) → integration smoke follow-up.
