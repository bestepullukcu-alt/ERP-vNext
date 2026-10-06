---
id: MOD-0143
name: Invoice Capture & 3-Way Match
domain: procurement
service: Diten.ProcurementService
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: ready-for-dev
owner: procurement / control-tower
branch: feature/procurement-mvp-2
started: 2026-09-16
target: 2026-10-15
form_field_count: 11
---

# MOD-0143 — Invoice Capture & 3-Way Match

> **Status guard.** `ready-for-dev` — DCP-010 `approved` (Ali) + bu pack `ready-for-dev` (Ali, 2026-09-17) → kod yetkili (CAP-001 §7). DCP-010 §8: 0142 GRN'den sonra (W-2). 3-way match GoodsReceived (0142) + PO (0141) tüketir; build 0142'den sonra yürür.
> **Contract authority.** Entity alanları/objeleri/endpoint'leri `docs/analysis/contracts/invoice-match.openapi.yaml` (OWNED, x-owner MOD-0143, x-min-contract MATCH-BUNDLE) türetilir; uydurma yasak (K12). Gap'ler açıkça `ASSUMPTION-...:` ile işaretlenir. PO/GRN/ürün kimliği **consume edilir, yaratılmaz** (K16). Bu modül **AP/payment SoR DEĞİLDİR** — yalnız eşleşme sonucunu üretir.

## 1. Module Summary
Fatura yakalama + 3-yönlü eşleştirme (PurchaseOrder ↔ GoodsReceipt ↔ Invoice) SoR'u. Hedef kullanıcı: procurement/AP-önü operatörü + exception çözen onaylayan. Modül supplier faturasını PO'ya referanslı yakalar, PO (0141) ↔ GRN (0142) ↔ Invoice üçlüsünü policy-driven tolerans profiline göre eşleştirir ve sonucu (`Matched | MatchedWithinTolerance | Exception`) üretir; eşleşmeyenleri exception kuyruğuna düşürür ve approval/audit ile çözer. Blueprint canonical adı **"Invoice Capture & 3-Way Match"**, Wave **W-2**, Suite **Procurement / P2P**, Min Integration Contract **MATCH-BUNDLE**. Ödeme YÜRÜTMEZ.

## 2. Ownership and Boundaries
- **In-scope:** invoice capture (supplier faturası, PO-referanslı), 3-way match yürütme (`runThreeWayMatch`), match outcome (Matched/MatchedWithinTolerance/Exception + variances), invoice exception kuyruğu + çözüm (approve/reject/tolerance-override), invoice yaşam döngüsü (Captured/Matched/MatchedWithinTolerance/Exception/Rejected/ClearedForPayment).
- **Out-of-scope:** **AP/ödeme SoR** (Finance/Treasury; `ClearedForPayment` yalnız bir eşleşme-durumu işaretidir, ödeme tetiklemez), **Payment Run Support (MOD-0146 — bu domain dışı)**, PO/requisition kimliği (MOD-0141), GRN/mal kabul (MOD-0142), ürün/SKU kimliği (MOD-0290/MDM), stok balance/ledger (MOD-0173/SCE — shadow stock YASAK), tolerans sayısal değerlerinin sahipliği (EA/Finance-TBD; bkz ASSUMPTION-P2P-01).
- **Owned contract:** `MATCH` (`invoice-match.openapi.yaml`, x-owner MOD-0143, x-min-contract MATCH-BUNDLE). Tek-yazıcı (K15). Producer surface additive-only (K16).

## 3. Owned Objects
- **Entity:** `Invoice` (EntityBase), `InvoiceLine` (embedded), `MatchException` (Invoice'a bağlı; exception kuyruğu). `MatchOutcome` = eşleştirme sonucu projeksiyonu (Invoice + variances + exceptionRef üzerinden türetilir/persist edilir).
- **Commands:** CaptureInvoice (`captureInvoice`), RunThreeWayMatch (`runThreeWayMatch`), ResolveMatchException (`resolveMatchException`), (soft) DeleteInvoice, BulkDeleteInvoice.
- **Queries:** GetInvoiceById (`getInvoice`), GetInvoiceList (liste; tenant+LE filtreli), ListMatchExceptions (`listMatchExceptions` — reasonCode + cursor sayfalı).
- **DTOs:** `invoice-match.openapi.yaml` şemaları (InvoiceUpsert, Invoice, InvoiceLine, MatchOutcome, MatchException, InvoiceStatus, ExceptionReason, Decimal, Error).
- **API endpoints (server `/api/invoice-match`):** `POST /invoices` (captureInvoice), `GET /invoices/{invoiceId}` (getInvoice), `POST /invoices/{invoiceId}/match` (runThreeWayMatch), `GET /exceptions` (listMatchExceptions), `POST /exceptions/{exceptionId}/resolve` (resolveMatchException).
- **Frontend route:** `/Procurement/InvoiceMatch` (tenant shell).
- **Permissions:** `procurement.invoice-match.read|create|match|resolve-exception` (+ `delete`, `bulk-delete`).

## 4. Entity Fields
| Field | Type | Rules | Index |
|---|---|---|---|
| InvoiceId | string (public code) | server-assigned; unique/tenant+LE | Unique (Tenant+LE) |
| SupplierId | string | required; consume MOD-0140 (fail-closed → 404 UNKNOWN_REFERENCE); **redefine yok** | — |
| PoId | string | required; consume MOD-0141 (fail-closed → 404 UNKNOWN_REFERENCE); **redefine yok** | — |
| InvoiceNumber | string | required, trim; (SupplierId+InvoiceNumber) unique/tenant+LE → 409 | Unique (Tenant+LE+Supplier) |
| Currency | string | required; **PO currency ile eşleşmeli** (uyumsuz → CurrencyMismatch/422) | — |
| Status | enum(Captured/Matched/MatchedWithinTolerance/Exception/Rejected/ClearedForPayment) | default Captured (yakalamada); geçiş `runThreeWayMatch`/`resolve` ile | — |
| Lines[] | InvoiceLine | required, ≥1; her satır: itemId, quantity, unitPrice zorunlu | — |
| Lines[].PoLineId | string | nullable; MOD-0141 PO satır referansı | — |
| Lines[].ItemId | Guid (uuid) | required; consume MOD-0290 (fail-closed → 404); lokal ürün açılmaz | — |
| Lines[].Quantity | Decimal (string, `^-?\d+(\.\d+)?$`) | required; **float YASAK** | — |
| Lines[].UnitPrice | Decimal (string) | required; **float YASAK** | — |
| Lines[].LineAmount | Decimal (string) | nullable; qty×unitPrice tutarlılık kontrolü | — |
| TotalAmount | Decimal (string) | server-hesaplı (Σ lineAmount); float YASAK | — |
| ToleranceProfileId | string | nullable; policy-driven; verilmezse LE/tenant default profil uygulamada çözülür (ASSUMPTION-P2P-01; contract sayı gömmez) | — |
| SourceSystem/ExternalRef | string | nullable (DEC-INV-19 dış besleme) | — |
| ContractVersion | string | response'ta yayılır | — |
| TenantId, LegalEntityId | Guid | **server-resolved, payload'da YOK**; her sorguda filtre | Compound |
| Id, IsDeleted, DeletedAt, CreatedAt, UpdatedAt | (EntityBase/audit) | soft-delete zorunlu; Mongo V3 GUID subtype-4 | — |

`MatchException`: ExceptionId (unique/tenant+LE), InvoiceId, PoId, ReasonCode (enum: QtyMismatch/PriceMismatch/AmountMismatch/NoReceipt/NoPo/DuplicateInvoice/CurrencyMismatch), Status (enum: Open/Resolved), ContractVersion, + EntityBase/audit.

> `form_field_count: 11` (supplierId, poId, invoiceNumber, currency + satır alanları poLineId/itemId/quantity/unitPrice/lineAmount + sourceSystem, externalRef) > 8 → `golden_reference: compact`. `runThreeWayMatch`/`resolveMatchException` ayrı aksiyon akışları olup capture formu alan sayısına dahil değildir.

## 5. Repo Scope
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Features/InvoiceMatch/**`
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Domain/**` (Invoice, InvoiceLine, MatchException entities)
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Persistence/**` (repositories, Mongo index config)
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Api/Controllers/InvoiceMatchController.cs`
- `frontend/Diten.Web/Views/Procurement/InvoiceMatch/**` + `wwwroot/assets/js/Procurement/InvoiceMatch/**` + `Resources/Views/Procurement/InvoiceMatch/**`

## 6. Protected Paths
- `.antigravity/**`
- `services/Diten.SupplyChainService/**`, `services/Diten.MdmService/**` ve diğer domain servisleri
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration-agent owned — ayrı task)
- `frontend/Diten.Web/Views/Archive/**`, `Views/Shared/_Layout.cshtml`
- **CONSUMED contract'lar (redefine YASAK, K16):** `docs/analysis/contracts/inventory-bundle.openapi.yaml`, `product-master-bundle.openapi.yaml`, `requisition-po.openapi.yaml` (consumed reference), `grn-event.openapi.yaml` (consumed reference)
- `invoice-match.openapi.yaml` → **OWNED** (bu modül yazar; additive-only)

## 7. Dependencies
- **Backbone:** JWT/RBAC (MOD-0018), Audit (MOD-0021), Workflow/Approvals (MOD-0023 — exception resolve onay trail), Notification (MOD-0027), Event Bus (MOD-0035 — `GoodsReceived` seam).
- **Consumed contracts (frozen/owned-by-others, redefine YASAK):** REQUISITION-PO (`poId`, PO currency/satır — MOD-0141), GRN-EVENT (goods receipt — MOD-0142; `GoodsReceived` tetikleyici + eşleşen GRN referansları), PRODUCT-MASTER (`itemId` — MOD-0290). Bilinmeyen PO/receipt/item → **fail-closed** (404 UNKNOWN_REFERENCE).
- **Upstream master:** SUPPLIER (`supplierId` — MOD-0140).
- **Downstream consumers:** Finance/Treasury (match outcome → AP/payment; MOD-0146 dışarıda). Match outcome üretir, ödeme yürütmez.

## 8. Runtime Constraints
- Gateway port **5065**; frontend yalnız Gateway (5000) üzerinden çağırır (`/api/invoice-match`).
- **Tenant + Legal-Entity izolasyonu her sorguda** (server-resolved; cross-LE fail-closed → 404).
- Soft delete (`IsDeleted`/`DeletedAt`).
- **Idempotent capture/match/resolve** (`Idempotency-Key` header zorunlu — replay yan-etki üretmez).
- **Duplicate invoice** (SupplierId+InvoiceNumber) → 409 DUPLICATE_INVOICE.
- Geçersiz durum geçişi (zaten Matched faturaya tekrar match; kapalı exception resolve) → 409 INVALID_STATE.
- **Para = Decimal string, float YASAK**; Currency PO currency ile uyumlu olmalı (uyumsuz → CurrencyMismatch/422).
- **Shadow stock YASAK; payment execution YASAK** — modül yalnız match outcome üretir.
- Mongo V3 GUID subtype-4.

## 9. Layout & Shell Contract
- `shell: tenant` → tüm `Views/Procurement/InvoiceMatch/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` **AÇIKÇA**.
- View klasörü: `Views/Procurement/InvoiceMatch/` · Frontend route: `/Procurement/InvoiceMatch`.

## 10. Backend File Convention
Golden Reference **Compact** birebir: `Features/InvoiceMatch/` altında Commands/, Queries/, Handlers/CommandHandlers/, Handlers/QueryHandlers/, Validators/, `InvoiceMatchModels.cs`. Naming: Command/Query record; Handler/Validator class **suffix YOK** (`CaptureInvoiceHandler`, `RunThreeWayMatchHandler`, `ResolveMatchExceptionHandler`, `CaptureInvoiceValidator`). Tek dosyada birden fazla public type YASAK; `*CommandHandler`/`*QueryHandler` suffix YASAK.

## 11. Frontend File Contract
Compact seti: `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml` (`data-dt-standard="v2"`), `_IndexL10n.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml`, `{Module}Index.cs` + `index.js` + `index.l10n.js`. **YASAK:** `_CreateEditOffcanvas`, `_DetailsQuickView` (compact). Exception kuyruğu + match sonucu görünümü Details/Index içinde compact sayfa deseniyle sunulur. L10n: tenant modülü → **7 dil** (en, tr, fr, es, zh, ar, ru).

## 12. Validation Rules
| Field | Required | Rule | DB | Pre-check |
|---|---|---|---|---|
| SupplierId | Evet | consume; bilinmeyen → 404 | — | ValidateSupplier (0140) |
| PoId | Evet | consume; bilinmeyen → 404 | — | ValidatePo (0141) |
| InvoiceNumber | Evet | trim; (Supplier+InvoiceNumber) unique | Unique (Tenant+LE+Supplier) | ExistsByInvoiceNumberAsync |
| Currency | Evet | PO currency ile eşleşmeli; uyumsuz → 422 CurrencyMismatch | — | PO currency karşılaştırma |
| Lines | Evet | ≥1 satır | — | — |
| Lines[].ItemId | Evet (satırda) | uuid; consume 0290; bilinmeyen → 404 | — | ValidateItem (0290) |
| Lines[].Quantity / UnitPrice | Evet (satırda) | Decimal regex; float YASAK; >0 | — | — |
| Lines[].LineAmount | Hayır | Decimal; qty×unitPrice tutarlılık | — | — |
| ToleranceProfileId | Hayır | policy-driven; verilmezse default profil uygulamada çözülür (sayı gömülmez) | — | — |
| resolve.decision | Evet | enum(approve/reject/tolerance-override) | — | Exception Open olmalı |

## 13. Failure Path to Verify
- **Duplicate invoice** (aynı Supplier+InvoiceNumber) → 409 DUPLICATE_INVOICE + field-level error + kayıt yok + reload temiz.
- **Unknown reference** (bilinmeyen PO/supplier/item) → 404 UNKNOWN_REFERENCE (fail-closed) + kayıt yok.
- **Currency mismatch** (fatura currency ≠ PO currency) → 422 VALIDATION_FAILED + CurrencyMismatch + capture engellenir.
- **Invalid state transition** (zaten Matched faturaya tekrar match; kapalı exception resolve) → 409 INVALID_STATE + sessiz overwrite YOK.
- **Missing required (ItemId/Quantity/UnitPrice)** → 422 + validator mesajı + save engellenir.
- **Unauthorized actor** (izin yok) → 403 + action disabled/permission-denied (UAS-001: iskelet çizilmez, yönlendirme yok).
- **Cross-tenant/LE erişim** → 404 (sızıntı yok).

## 14. Authorization Convention
- Policy: `[Authorize]` (tenant actor).
- Permission format (PKS-001, lowercase-dotted, ≥3 segment, kebab-case multiword): `procurement.invoice-match.{action}`.
- Actions: read, create, match, resolve-exception, delete, bulk-delete.
- Actor: tenant_user (procurement / AP-önü rolü); server-side `[HasPermission]` zorunlu.

## 15. Gateway / API Routing Decision
- Karar: Gateway değişikliği **gerekli** (`/api/invoice-match` → 5065). `ocelot.json` protected; bu pack yazmaz — explicit Upstream/Downstream + OPTIONS içeren **integration-agent task**'ı olarak ayrı yürütülür. Frontend servis portuna (5065) doğrudan gitmez; yalnız Gateway (5000).

## 16. Acceptance Criteria
- [ ] `POST /invoices` idempotent capture → 201 (Captured); duplicate (Supplier+InvoiceNumber) → 409; bilinmeyen PO/supplier/item → 404 (fail-closed).
- [ ] `POST /invoices/{id}/match` (`runThreeWayMatch`) PO↔GRN↔Invoice'u policy-driven tolerans profiline göre değerlendirir; sonuç `Matched | MatchedWithinTolerance | Exception` + variances; **kodda/contract'ta sabit sayısal tolerans YOK** (ASSUMPTION-P2P-01).
- [ ] **G2A golden flow kuyruğu:** GRN 0173'e post ettikten sonra invoice 3-way match çalışır; `MatchedWithinTolerance` yolunda variance `withinTolerance:true`; `Exception` yolunda exception kuyruğa düşer.
- [ ] **Exception queue + tolerance-override yolu kanıtlanır:** `GET /exceptions` reasonCode+cursor filtreli listeler; `POST /exceptions/{id}/resolve` (approve/reject/tolerance-override) approval trail (MOD-0023) + audit ile durumu günceller (Evidence E4, state-changing; L3 persistence — match outcome/approval kalıcı).
- [ ] Currency PO ile uyumsuz → 422 CurrencyMismatch; para alanları Decimal-string (float yok).
- [ ] Tenant+LE izolasyon: cross-LE 404; unauthorized 403 (UAS-001).
- [ ] Ödeme YÜRÜTÜLMEZ; modül yalnız match outcome üretir (AP/payment Finance/Treasury sınırı).
- [ ] MATCH contract consumer/producer shape'i değişmeden yanıt verir (additive-only, K16).
- [ ] Tüm `Views/Procurement/InvoiceMatch/*.cshtml` → `Layout = "_LayoutTenantShell"` açık; `verify_datatable_page.py --area Procurement --module InvoiceMatch --reference compact` PASS.
- [ ] RESX 7-dil parite PASS.

## 17. Test Expectations
- Unit: validator (duplicate invoiceNumber, currency mismatch, missing item/qty/price), handler (capture, runThreeWayMatch matched/within-tolerance/exception, resolveMatchException).
- Integration: tenant isolation (cross-LE 404), soft-delete, idempotency replay (capture/match/resolve yan-etki yok), invalid-state 409, fail-closed unknown reference (PO/GRN/item), G2A tail (GRN post → 3-way match → exception queue → tolerance-override resolve).
- Contract: `invoice-match.openapi.yaml` mock (Prism) ile producer shape uyumu; consumed REQUISITION-PO/GRN-EVENT/PRODUCT-MASTER mock'a karşı fail-closed.
- Frontend: browser smoke + DataTable verifier PASS (exception kuyruğu görünümü dahil).
- Build: ProcurementService + frontend + gateway 0-error.

## 18. Ready-for-dev Checklist
- [ ] Golden Reference Compact okundu
- [ ] Frontmatter tam (service/shell/golden_reference/entity_base/form_field_count)
- [ ] Layout & Shell Contract Razor Layout açık
- [ ] Backend/Frontend file convention golden reference ile birebir
- [ ] Validation her field için yazılı
- [ ] Failure Path ≥4 senaryo (duplicate/unknown-ref/currency/invalid-state/missing/unauthorized) + cross-tenant
- [ ] Authorization permission listesi + policy + actor
- [ ] Gateway routing kararı (integration-agent task)
- [ ] Acceptance test edilebilir + G2A golden flow kuyruğu (GRN post → 3-way match → exception/tolerance-override) + E4/L3
- [ ] Test expectations build/verifier/RESX/smoke/contract-mock kapsıyor
- [ ] CONSUMED contract redefine yok (0141/0142/0290 fail-closed consume)
- [x] DCP-010 approved + 0141/0142 dilimleri + bu pack ready-for-dev onayı (Ali, 2026-09-17; kod kapısı AÇIK)

## 19. Implementation Notes
DCP-010 §8 sırasında W-2 dilimi: 0142 GRN'den sonra, 0144 Contracting'den önce (0140→0145→0141→0142→**0143**→0144). Servis scaffold MOD-0140 ile doğar; bu modül mevcut `Diten.ProcurementService`'e feature olarak eklenir (port 5065, JWT + Mongo V3 GUID + TenantId izolasyonu). MATCH contract producer surface `x-status: REVIEW` — freeze owner+consumer review sonrası.

**ASSUMPTION-P2P-01 (carry-forward, DCP-010 AD-7 / domain-config §Key Decisions ile hizalı):** 3-yönlü eşleştirme toleransı **POLICY-DRIVEN** modellenir (`toleranceProfileId` + qty/price/amount toleransları); mismatch → exception queue. Koda/contract'a **sabit sayısal varsayılan GÖMÜLMEZ**; gerçek tolerans değeri/sahibi tenant policy/Finance kararıdır (EA/Finance-TBD). Bu **contract-bozan değil additive-safe** bir açıklıktır; contract `runThreeWayMatch` request'inde `toleranceProfileId` opsiyonel ve verilmezse LE/tenant default profil uygulama tarafından çözülür.

**ASSUMPTION-P2P-02 (yeni, capture kimliği):** `invoiceId` public code server-assigned; contract `Invoice.invoiceId` string olduğundan format (ör. `INV-...`) uygulama/registry kararı, contract sayı/format gömmez.

## 20. Follow-up Items
- 3-way match tolerance profil sahipliği/değeri (0143-FU) → EA/Finance-TBD (ASSUMPTION-P2P-01).
- Match outcome → Finance/Treasury AP/payment seam (MOD-0146 out; `ClearedForPayment` işareti bu domainde kalır, ödeme başka domain) → cross-domain follow-up.
- Dış ERP fatura besleme adapter (SourceSystem/ExternalRef; DEC-INV-19 deseni) → follow-up.
- `GoodsReceived` event tüketimi (0142 → auto-match trigger) detay tasarımı → Event Bus (MOD-0035) follow-up.
