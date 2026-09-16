---
id: MOD-0141
name: Requisition & Purchase Orders
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
form_field_count: 16
---

# MOD-0141 — Requisition & Purchase Orders

> **Status guard.** `draft` — DCP-010 `approved`/`ready-for-execution` + bu pack `ready-for-dev` olmadan kod YOK (CAP-001 §7). Bu pack ready-for-dev **hedefli** yazılmıştır; Ali DCP-010'u onaylayınca `ready-for-dev`'e alınır. DCP-010 §8 sırasında **üçüncü** dilim (0140→0145→**0141**→0142→0143→0144).
> **Contract authority.** Entity alanları `docs/analysis/contracts/requisition-po.openapi.yaml`'dan türetilir; uydurma yasak (K12). Ürün kimliği (0290) ve tedarikçi (0140) consume edilir, yaratılmaz; bilinmeyen referans fail-closed (404 `UNKNOWN_REFERENCE`).

## 1. Module Summary
Requisition (satın alma talebi) + Purchase Order SoR'u. Hedef kullanıcı: procurement operatörü (talep/PO oluşturan) + onaylayan. Akış: Requisition → (onay, MOD-0023) → PurchaseOrder → (onay) → GRN'e açık. PO, G2A golden flow'un **upstream belgesidir**: MOD-0142 GRN (`poId/poLineId`) ve MOD-0143 Invoice-Match bu belgeyi okur. MOD-0189 MRP requirement önerir ama PO SoR burada kalır. Stok TUTMAZ. Blueprint W-1, Procurement Suite / P2P, Min Integration Contract = PO-BUNDLE.

## 2. Ownership and Boundaries
- **In-scope:** requisition master + satırlar (itemId/skuId consume, quantity/uom/needBy), requisition lifecycle (Draft/Submitted/Approved/Rejected/Converted/Cancelled), purchase order master + satırlar (unitPrice/lineAmount), PO lifecycle (Draft/Approved/PartiallyReceived/Received/Closed/Cancelled), approval trail + PO lifecycle event'leri, submit/approve geçişleri.
- **Out-of-scope:** ürün/SKU kimliği (MOD-0290/MDM — consume, `itemId/skuId` uydurulmaz), tedarikçi kimliği/master (MOD-0140 — consume, `supplierId`), stok balance/ledger (MOD-0173/SCE — **shadow stock YASAK**), mal kabul/GRN (MOD-0142 — PO'yu okur), fatura 3-yönlü eşleştirme (MOD-0143), MRP requirement üretimi (MOD-0189 — öneri girer, SoR burada), AP/ödeme (Finance/Treasury).
- **Owned contract:** `REQUISITION-PO` (`requisition-po.openapi.yaml`, x-owner MOD-0141, x-min-contract PO-BUNDLE). Producer surface additive; PO downstream consumer slice'ı (`getPurchaseOrder` → `poId/poLineId/status`) **superset** korunur (daraltma yasak, K16).

## 3. Owned Objects
- **Entity:** `Requisition` (EntityBase) + `RequisitionLine` (embedded), `PurchaseOrder` (EntityBase) + `PoLine` (embedded).
- **Commands:** CreateRequisition, SubmitRequisition, (soft) DeleteRequisition, BulkDeleteRequisition, UpdateRequisition; CreatePurchaseOrder, ApprovePurchaseOrder, (soft) DeletePurchaseOrder, BulkDeletePurchaseOrder, UpdatePurchaseOrder.
- **Queries:** GetRequisitionList, GetRequisitionById; GetPurchaseOrderList, GetPurchaseOrderById.
- **DTOs:** `requisition-po.openapi.yaml` şemaları (Requisition, RequisitionUpsert, RequisitionLine, RequisitionStatus, PurchaseOrder, PurchaseOrderUpsert, PoLine, PoStatus, Decimal, Error).
- **API endpoints (server root `/api`):** `POST/GET /api/requisitions`, `GET /api/requisitions/{requisitionId}`, `POST /api/requisitions/{requisitionId}/submit`, `POST/GET /api/purchase-orders`, `GET /api/purchase-orders/{poId}`, `POST /api/purchase-orders/{poId}/approve`.
- **Frontend routes:** `/Procurement/Requisitions` ve `/Procurement/PurchaseOrders` (tenant shell).
- **Permissions:** `procurement.requisitions.{read|create|update|delete|bulk-delete|submit}`, `procurement.purchase-orders.{read|create|update|delete|bulk-delete|approve}`.

## 4. Entity Fields
### Requisition (+ RequisitionLine)
| Field | Type | Rules | Index |
|---|---|---|---|
| RequisitionId | string (public code) | server-assigned; unique/tenant+LE | Unique (Tenant+LE) |
| Status | enum(Draft/Submitted/Approved/Rejected/Converted/Cancelled) | required, default Draft | — |
| Lines[] | RequisitionLine | required, ≥1 satır | — |
| Line.ItemId | Guid (uuid) | required; **MOD-0290 consume**, uydurulmaz; fail-closed | — |
| Line.SkuId | Guid (uuid) | nullable; MOD-0290 consume | — |
| Line.Quantity | Decimal (string, `^-?\d+(\.\d+)?$`) | required; float YASAK; > 0 | — |
| Line.UomId | string | required | — |
| Line.NeedBy | date | nullable | — |
| Justification | string | nullable | — |
| WorkflowInstanceId | string | nullable; submit'te MOD-0023'ten atanır | — |
| TenantId, LegalEntityId | Guid | **server-resolved, payload'da YOK**; her sorguda filtre | Compound |
| Id, IsDeleted, DeletedAt, CreatedAt, UpdatedAt | (EntityBase/audit) | soft-delete zorunlu | — |

### PurchaseOrder (+ PoLine)
| Field | Type | Rules | Index |
|---|---|---|---|
| PoId | string (public code) | server-assigned; unique/tenant+LE | Unique (Tenant+LE) |
| SupplierId | string | required; **MOD-0140 SUPPLIER consume**; fail-closed 404 | index |
| RequisitionId | string | nullable; onaylı requisition referansı | index |
| Status | enum(Draft/Approved/PartiallyReceived/Received/Closed/Cancelled) | required, default Draft | — |
| Currency | string | required; **LE base currency** | — |
| Lines[] | PoLine | required, ≥1 satır | — |
| Line.PoLineId | string | server-assigned; **GRN 0142 `poLineId` ile eşleşir** | — |
| Line.ItemId | Guid (uuid) | required; MOD-0290 consume | — |
| Line.SkuId | Guid (uuid) | nullable; MOD-0290 consume | — |
| Line.Quantity | Decimal (string) | required; float YASAK; > 0 | — |
| Line.UomId | string | required | — |
| Line.UnitPrice | Decimal (string) | required; float YASAK; ≥ 0 | — |
| Line.LineAmount | Decimal (string) | **server-computed** (quantity × unitPrice); kullanıcı girmez | — |
| TotalAmount | Decimal (string) | **server-computed** (Σ lineAmount); kullanıcı girmez | — |
| SourceSystem / ExternalRef | string | nullable (DEC-INV-19 dış besleme) | — |
| WorkflowInstanceId | string | nullable; approve'da MOD-0023'ten atanır | — |
| TenantId, LegalEntityId | Guid | **server-resolved, payload'da YOK**; her sorguda filtre | Compound |
| Id, IsDeleted, DeletedAt, CreatedAt, UpdatedAt | (EntityBase/audit) | soft-delete zorunlu | — |

> `form_field_count: 16` = Requisition formu **6** (Justification + satır: ItemId/SkuId/Quantity/UomId/NeedBy) + PurchaseOrder formu **10** (SupplierId/RequisitionId/Currency/SourceSystem/ExternalRef + satır: ItemId/SkuId/Quantity/UomId/UnitPrice). PO formu tek başına 10 > 8 → `golden_reference: compact`. LineAmount/TotalAmount server-computed, sayılmaz.
>
> `ASSUMPTION-P2P-0141-01:` Contract `requisitionId/poId` alanlarını `string` olarak verir ama public kod üretim kuralını (ör. `REQ-####`/`PO-####`) tanımlamaz; MOD-0140 `SupplierId` deseniyle uyumlu server-assigned tenant+LE-unique public code varsayılır (additive, contract-bozan değil).
> `ASSUMPTION-P2P-0141-02:` Contract yalnız POST/GET + submit/approve tanımlar; `update`/`delete`/`bulk-delete` permission'ları verilen yetki setinden gelir. Standart DataTable CRUD (`PATCH /api/{res}/{id}`, soft-delete `DELETE`, bulk-delete) golden reference compact + MOD-0140 deseniyle **additive** eklenir; contract freeze kapısında owner+consumer review'a additive-safe olarak sunulur.
> `ASSUMPTION-P2P-0141-03:` `Idempotency-Key` create/submit/approve akışlarında zorunlu header (contract `IdempotencyKey` parametresi); update optimistic concurrency `If-Match`/rowVersion ile yönetilir (contract 409 `StateConflict` ile uyumlu).

## 5. Repo Scope
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Features/Requisition/**`
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Features/PurchaseOrder/**`
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Domain/**` (Requisition, RequisitionLine, PurchaseOrder, PoLine entities)
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Persistence/**` (repositories, Mongo index config)
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Api/Controllers/RequisitionsController.cs`, `PurchaseOrdersController.cs`
- `frontend/Diten.Web/Views/Procurement/Requisitions/**` + `Views/Procurement/PurchaseOrders/**`
- `frontend/Diten.Web/wwwroot/assets/js/Procurement/Requisitions/**` + `.../PurchaseOrders/**`
- `frontend/Diten.Web/Resources/Views/Procurement/Requisitions/**` + `.../PurchaseOrders/**`

## 6. Protected Paths
- `.antigravity/**`
- `services/Diten.SupplyChainService/**`, `services/Diten.MdmService/**` ve diğer domain servisleri
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration-agent owned — ayrı task)
- `frontend/Diten.Web/Views/Archive/**`, `Views/Shared/_Layout.cshtml`
- `docs/analysis/contracts/inventory-bundle.openapi.yaml`, `product-master-bundle.openapi.yaml`, `location.openapi.yaml`, `supplier.openapi.yaml` (CONSUMED referans — redefine YASAK)

## 7. Dependencies
- **Backbone:** JWT/RBAC (MOD-0018), Audit (MOD-0021), Workflow/Approvals (MOD-0023, requisition submit + PO approve), Reference Data (MOD-0048, UoM/currency), Notification (MOD-0027), Event Bus (MOD-0035, PO lifecycle event).
- **Consumed contracts (frozen — redefine YASAK):** PRODUCT-MASTER (`itemId/skuId` — MOD-0290/MDM; kimlik uydurulmaz), SUPPLIER (`supplierId` — MOD-0140; bilinmeyen → fail-closed 404). Bilinmeyen referans `UNKNOWN_REFERENCE` ile reddedilir, uydurma yok.
- **Downstream consumers:** MOD-0142 GRN (PO'yu `poId/poLineId` ile okur — G2A upstream), MOD-0143 Invoice-Match (3-way match PO'yu okur), MOD-0189 MRP (requirement önerir; PO SoR burada kalır).

## 8. Runtime Constraints
- Gateway port **5062**; frontend yalnız Gateway (5000) üzerinden çağırır.
- **Tenant + Legal-Entity izolasyonu her sorguda** (server-resolved; cross-LE fail-closed → 404).
- Soft delete (`IsDeleted`/`DeletedAt`).
- Idempotent create/submit/approve (`Idempotency-Key`); update optimistic concurrency (`If-Match`/rowVersion) → 409.
- Para/decimal: parasal alanlar `Decimal` (string, `^-?\d+(\.\d+)?$`), **float YASAK**; para birimi LE base currency.
- **No shadow stock:** bu modül hiçbir stok balance/ledger tutmaz; envanter yalnız MOD-0142 GRN üzerinden MOD-0173'e post edilir.
- Mongo V3 GUID subtype-4.

## 9. Layout & Shell Contract
- `shell: tenant` → tüm `Views/Procurement/Requisitions/*.cshtml` ve `Views/Procurement/PurchaseOrders/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` **AÇIKÇA**.
- View klasörleri: `Views/Procurement/Requisitions/`, `Views/Procurement/PurchaseOrders/` · Frontend route'ları: `/Procurement/Requisitions`, `/Procurement/PurchaseOrders`.

## 10. Backend File Convention
Golden Reference **Compact** birebir; her entity kendi feature klasöründe: `Features/Requisition/` ve `Features/PurchaseOrder/` altında Commands/, Queries/, Handlers/CommandHandlers/, Handlers/QueryHandlers/, Validators/, `RequisitionModels.cs` / `PurchaseOrderModels.cs`. Naming: Command/Query record; Handler/Validator class **suffix YOK** (`CreateRequisitionHandler`, `CreatePurchaseOrderValidator`, `ApprovePurchaseOrderHandler`, `SubmitRequisitionHandler`).

## 11. Frontend File Contract
Compact seti — **her iki modül için ayrı**: `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml` (`data-dt-standard="v2"`), `_IndexL10n.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml`, `{Module}Index.cs` + `index.js` + `index.l10n.js`. **YASAK:** `_CreateEditOffcanvas`, `_DetailsQuickView` (compact). L10n: tenant modülü → **7 dil** (en, tr, fr, es, zh, ar, ru).

## 12. Validation Rules
| Field | Required | Rule | DB | Pre-check |
|---|---|---|---|---|
| Requisition.Lines | Evet | ≥1 satır | — | — |
| Line.ItemId | Evet | uuid; MOD-0290'da mevcut | — | ValidateItemsAsync (consume) |
| Line.Quantity | Evet | Decimal regex; > 0; float YASAK | — | — |
| Line.UomId | Evet | reference-data (MOD-0048) | — | — |
| PO.SupplierId | Evet | MOD-0140'da mevcut aktif supplier | index | ValidateSupplierAsync (consume, fail-closed) |
| PO.Currency | Evet | LE base currency | — | — |
| PoLine.UnitPrice | Evet | Decimal regex; ≥ 0; float YASAK | — | — |
| Requisition.submit | — | Status=Draft; ≥1 satır; geçerli geçiş | — | — |
| PO.approve | — | Status=Draft→Approved; geçersiz geçiş 409 | — | — |

## 13. Failure Path to Verify
- **Unknown supplier/item** (0140/0290'da yok) → 404 `UNKNOWN_REFERENCE` + kayıt yok + uydurma yok (fail-closed).
- **Empty lines / missing quantity** → 422 `VALIDATION_FAILED` + validator mesajı + save engellenir.
- **Invalid state transition** (onaylı PO tekrar approve, onaya gitmiş requisition tekrar submit) → 409 `INVALID_STATE`.
- **Concurrency conflict** (stale update) → 409 + "kayıt değişti, yeniden yükle" + sessiz overwrite YOK.
- **Unauthorized actor** (izin yok) → 403 + action disabled/permission-denied (UAS-001: iskelet çizilmez, yönlendirme yok).
- **Cross-tenant/LE erişim** → 404 (sızıntı yok).

## 14. Authorization Convention
- Policy: `[Authorize]` (tenant actor).
- Permission format (PKS-001, lowercase-dotted, ≥3 segment): `procurement.requisitions.{action}`, `procurement.purchase-orders.{action}`.
- Actions: read, create, update, delete, bulk-delete + `submit` (requisitions) / `approve` (purchase-orders).
- Actor: tenant_user (procurement rolü); server-side `[HasPermission]` zorunlu.

## 15. Gateway / API Routing Decision
- Karar: Gateway değişikliği **gerekli** (`/api/requisitions` + `/api/purchase-orders` → 5062). `ocelot.json` protected; bu pack yazmaz — explicit Upstream/Downstream (submit/approve alt-path'leri + OPTIONS dahil) içeren **integration-agent task**'ı olarak ayrı yürütülür.

## 16. Acceptance Criteria
- [ ] `POST /api/requisitions` idempotent create → 201; `GET` list/by-id tenant+LE filtreli; `POST /submit` → onaya gönderir (MOD-0023 workflowInstanceId).
- [ ] `POST /api/purchase-orders` idempotent create → 201; unknown supplier/item → 404 `UNKNOWN_REFERENCE` (fail-closed, uydurma yok).
- [ ] `POST /api/purchase-orders/{poId}/approve` → Draft→Approved; geçersiz geçiş 409 `INVALID_STATE`.
- [ ] `totalAmount`/`lineAmount` server-computed (Decimal-string); float yok; currency = LE base currency.
- [ ] **G2A golden flow'daki rol:** approve edilmiş PO, MOD-0142 GRN ve MOD-0143 Invoice-Match'in `poId/poLineId` ile okuduğu **upstream belgedir** (PO → GRN → 0173 posting → invoice 3-way match); consumer slice şekli değişmeden yanıt verir.
- [ ] `PATCH` optimistic concurrency → stale update 409; soft-delete + bulk-delete tenant+LE filtreli.
- [ ] Tenant+LE izolasyon: cross-LE 404; unauthorized 403 (UAS-001).
- [ ] Tüm `Views/Procurement/{Requisitions,PurchaseOrders}/*.cshtml` → `Layout = "_LayoutTenantShell"` açık; `verify_datatable_page.py --area Procurement --module Requisitions --reference compact` ve `--module PurchaseOrders` PASS.
- [ ] RESX 7-dil parite PASS (her iki modül).

## 17. Test Expectations
- Unit: validator (empty lines, missing quantity, unknown supplier/item, invalid currency), handler (create/submit/approve, totalAmount hesap).
- Integration: tenant isolation (cross-LE 404), soft-delete, idempotency replay (duplicate side-effect yok), concurrency 409, state-machine (submit/approve geçersiz geçiş 409), fail-closed unknown reference.
- Contract: `requisition-po.openapi.yaml` mock (Prism) ile producer/consumer slice uyumu (özellikle `getPurchaseOrder` → `poId/poLineId`).
- Frontend: browser smoke + DataTable verifier PASS (her iki modül).
- Build: ProcurementService + frontend + gateway 0-error.

## 18. Ready-for-dev Checklist
- [ ] Golden Reference Compact okundu
- [ ] Frontmatter tam (service/shell/golden_reference/entity_base)
- [ ] Layout & Shell Contract Razor Layout açık (her iki view klasörü)
- [ ] Backend/Frontend file convention golden reference ile birebir (iki feature klasörü)
- [ ] Validation her field için yazılı
- [ ] Failure Path ≥4 senaryo (unknown-reference/validation/state/concurrency/unauthorized) + cross-tenant
- [ ] Authorization permission listesi + policy + actor (submit/approve dahil)
- [ ] Gateway routing kararı (integration-agent task)
- [ ] Acceptance test edilebilir + G2A golden flow'da PO'nun upstream rolü
- [ ] Test expectations build/verifier/RESX/smoke/contract-mock kapsıyor
- [ ] DCP-010 approved + bu pack ready-for-dev onayı (kod kapısı)

## 19. Implementation Notes
DCP-010 §8 sırasında üçüncü dilim (0140 Supplier → 0145 Sourcing → **0141 Req/PO** → 0142 GRN → 0143 Invoice → 0144 Contracting). Servis scaffold MOD-0140 ile doğmuş olur (JWT + Mongo V3 GUID + TenantId izolasyonu + port 5062); bu pack o servise iki yeni feature (Requisition, PurchaseOrder) ekler. REQUISITION-PO contract `x-status: REVIEW` — freeze owner+consumer review sonrası; PO downstream slice (`poId/poLineId/status`) superset korunur. MRP (MOD-0189) yalnız requirement önerir, PO SoR burada kalır — seam tek-yazıcı (K15). Update/delete/bulk-delete additive (ASSUMPTION-P2P-0141-02); freeze kapısında additive-safe sunulur.

## 20. Follow-up Items
- MRP (MOD-0189) → PO requirement önerisi seam'i (öneri girişi; PO SoR bu modülde) → entegrasyon FU.
- PartiallyReceived/Received/Closed durum geçişleri GRN (MOD-0142) goods-receipt event'i ile tetiklenir → 0142 ready-for-dev'de netleşir.
- Dış ERP requisition/PO senkron adapter (SourceSystem/ExternalRef; DEC-INV-19 deseni) → follow-up.
- 3-way match tolerance (ASSUMPTION-P2P-01) MATCH contract'ında (MOD-0143) modellenir; bu modül yalnız PO tutarını üretir.
