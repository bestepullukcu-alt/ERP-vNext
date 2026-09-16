---
id: MOD-0145
name: Sourcing (RFQ/RFP)
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
form_field_count: 13
---

# MOD-0145 — Sourcing (RFQ/RFP)

> **Status guard.** `draft` — DCP-010 `approved`/`ready-for-execution` + bu pack `ready-for-dev` olmadan kod YOK (CAP-001 §7). Bu pack ready-for-dev **hedefli** yazılmıştır; Ali DCP-010'u onaylayınca `ready-for-dev`'e alınır. DCP-010 §8 sıra: 0140→**0145**→0141→0142→0143→0144 (2. dikey dilim, Supplier'dan sonra).
> **Contract authority.** Entity alanları `docs/analysis/contracts/sourcing.openapi.yaml`'dan (x-owner MOD-0145) türetilir; uydurma yasak (K12), boşluklar `ASSUMPTION-...` ile işaretlenir. Supplier kimliği (0140) ve ürün/UoM kimliği (0290) **consume** edilir, yaratılmaz; bilinmeyen referans fail-closed.

## 1. Module Summary
RFx (RFQ/RFP/RFI) → teklif (bid) → değerlendirme/skor → award (kazanan) SoR'u. Hedef kullanıcı: procurement sourcing operatörü + award onaylayan. Akış: RFx oluştur (Draft) → yayınla (Published) → davetli supplier'lar (0140 consume) teklif verir → skorla (Evaluating) → award (Awarded, kazanan bid). Award çıktısı MOD-0141 requisition/PO ve MOD-0144 contracting'e **downstream** beslenir. Blueprint W-3, Procurement Suite / P2P; Min Integration Contract = SOURCING-BUNDLE.

## 2. Ownership and Boundaries
- **In-scope:** RFx event (identity/type/status/closesAt/invited suppliers/lines), bid (supplier teklifi + satır fiyat/lead-time), evaluation score, award decision (kazanan bid + rationale + karar zamanı), RFx yaşam döngüsü (Draft/Published/Evaluating/Awarded/Closed/Cancelled).
- **Out-of-scope:** supplier master/onboarding (MOD-0140), ürün/UoM kimliği (MOD-0290/MDM), requisition/PO (MOD-0141, award downstream tüketici), procurement contract/clause (MOD-0144, award downstream tüketici), reverse auction / gelişmiş sourcing (follow-up), AP/ödeme (Finance/Treasury).
- **Owned contract:** `SOURCING` (`sourcing.openapi.yaml`, x-owner MOD-0145, x-min-contract SOURCING-BUNDLE). Tek yazıcı bu modül (K15); producer surface additive-only (K16).

## 3. Owned Objects
- **Entity:** `RfxEvent` (EntityBase) + `RfxLine` (embedded), `Bid` (EntityBase) + `BidLine` (embedded), `AwardDecision` (RfxEvent'e bağlı, embedded/1-1).
- **Commands:** CreateRfxEvent, PublishRfxEvent, SubmitBid, AwardRfxEvent, (soft) DeleteRfxEvent, BulkDeleteRfxEvent.
- **Queries:** GetRfxEventList, GetRfxEventById, GetBidList (rfxId'ye göre).
- **DTOs:** `sourcing.openapi.yaml` şemaları (RfxUpsert, RfxEvent, RfxLine, BidUpsert, Bid, BidLine, AwardDecision, RfxStatus, RfxType, Decimal, Error).
- **API endpoints (server `/api/sourcing`):** `POST/GET /events`, `GET /events/{rfxId}`, `POST /events/{rfxId}/publish`, `POST/GET /events/{rfxId}/bids`, `POST /events/{rfxId}/award`.
- **Frontend route:** `/Procurement/Sourcing` (tenant shell).
- **Permissions:** `procurement.sourcing.{read|create|publish|bid|award|delete|bulk-delete}`.

## 4. Entity Fields
| Field | Type | Rules | Index |
|---|---|---|---|
| RfxId | string (public code) | server-assigned; unique/tenant+LE | Unique (Tenant+LE) |
| Type | enum(RFQ/RFP/RFI) | required | — |
| Title | string | required, trim | text |
| Status | enum(Draft/Published/Evaluating/Awarded/Closed/Cancelled) | required, default Draft; server-driven geçiş | — |
| ClosesAt | datetime | nullable | — |
| InvitedSupplierIds[] | string | nullable; her id 0140'ta doğrulanır (fail-closed) | — |
| Lines[] (RfxLine) | {itemId, quantity, uomId} | ≥1 satır; itemId→0290 (uuid) consume, quantity Decimal string (float YASAK), uomId→0290 consume | — |
| BidId | string (public code) | server-assigned; unique/tenant+LE | Unique (Tenant+LE) |
| Bid.SupplierId | string | required; 0140'ta doğrulanır (fail-closed); RFx invited listesinde olmalı | — |
| Bid.Lines[] (BidLine) | {itemId, unitPrice, leadTimeDays?} | itemId→0290 consume, unitPrice Decimal string (float YASAK), leadTimeDays int nullable | — |
| Bid.EvaluationScore | Decimal (string) | nullable; Evaluating aşamasında set; okuma-yalnız yüzey | — |
| AwardDecision | {awardedBidId, awardedSupplierId, rationale?, decidedAt} | awardedBidId required; awardedSupplierId server-resolve (bid'den); rationale nullable | — |
| ContractVersion | string | response envelope (contract sürüm işareti) | — |
| SourceSystem/ExternalRef | string | nullable (DEC-INV-19 dış besleme) | — |
| TenantId, LegalEntityId | Guid | **server-resolved, payload'da YOK**; her sorguda filtre | Compound |
| Id, IsDeleted, DeletedAt, CreatedAt, UpdatedAt | (EntityBase/audit) | soft-delete zorunlu | — |

> `form_field_count: 13` > 8 → `golden_reference: compact`. Sayım (yalnız kullanıcı-giriş modül alanları; Id/TenantId/audit/DataTable kolonları hariç): RFx formu → Type, Title, ClosesAt, InvitedSupplierIds, RfxLine.itemId, RfxLine.quantity, RfxLine.uomId (7); Bid formu → SupplierId, BidLine.itemId, BidLine.unitPrice, BidLine.leadTimeDays (4); Award formu → awardedBidId, rationale (2) = **13**.

## 5. Repo Scope
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Features/Sourcing/**`
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Domain/**` (RfxEvent, Bid, AwardDecision entities)
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Persistence/**` (repositories, Mongo index config)
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Api/Controllers/SourcingController.cs`
- `frontend/Diten.Web/Views/Procurement/Sourcing/**` + `wwwroot/assets/js/Procurement/Sourcing/**` + `Resources/Views/Procurement/Sourcing/**`

## 6. Protected Paths
- `.antigravity/**`
- `services/Diten.SupplyChainService/**`, `services/Diten.MdmService/**` ve diğer domain servisleri
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration-agent owned — ayrı task)
- `frontend/Diten.Web/Views/Archive/**`, `Views/Shared/_Layout.cshtml`
- `docs/analysis/contracts/supplier.openapi.yaml` (CONSUMED reference — SUPPLIER, redefine YASAK), `product-master-bundle.openapi.yaml` (CONSUMED — MOD-0290 identity/UoM, redefine YASAK)
- `docs/analysis/contracts/sourcing.openapi.yaml` **OWNED** — bu modül yazar (additive-only, K16); consumed contract'lar salt-referans.

## 7. Dependencies
- **Backbone:** JWT/RBAC (MOD-0018), Audit (MOD-0021), Workflow/Approvals (MOD-0023, award onayı), Notification (MOD-0027, davet/award bildirimi), Reference Data (MOD-0048), Event Bus (MOD-0035, `RfxAwarded` yayını).
- **Consumed contracts:** `SUPPLIER` (MOD-0140 — `invitedSupplierIds`/`bid.supplierId` doğrulama, fail-closed), `PRODUCT-MASTER` (MOD-0290 — `itemId`/`uomId` doğrulama, fail-closed). Yalnız okur; yazıcı değildir.
- **Downstream consumers:** MOD-0141 Requisition/PO (award → PO kaynağı), MOD-0144 Contracting (award → sözleşme kaynağı).

## 8. Runtime Constraints
- Gateway port **5062**; frontend yalnız Gateway (5000) üzerinden çağırır.
- **Tenant + Legal-Entity izolasyonu her sorguda** (server-resolved; cross-LE fail-closed → 404).
- Soft delete (`IsDeleted`/`DeletedAt`); yalnız Draft RFx silinebilir (bkz. §12/§13).
- Idempotent create/publish/bid/award (`Idempotency-Key` header zorunlu — contract); replay yan-etkisiz.
- Geçersiz durum geçişi → 409 (INVALID_STATE): yayınlanmamış/kapanmış/awarded RFx'e teklif; Draft dışı RFx'e publish; kazanan bid seçilemeyen durumda award.
- Bilinmeyen supplier/item/uom → 404 (UNKNOWN_REFERENCE, fail-closed).
- Mongo V3 GUID subtype-4. Parasal alanlar (`unitPrice`, `evaluationScore`, `quantity`) **Decimal string** (`^-?\d+(\.\d+)?$`); **float YASAK**; para birimi LE base currency.
- **No shadow stock:** bu modül stok tutmaz (n/a); consume-don't-own duruşu — supplier (0140) ve item/UoM (0290) uydurulmaz.

## 9. Layout & Shell Contract
- `shell: tenant` → tüm `Views/Procurement/Sourcing/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` **AÇIKÇA**.
- View klasörü: `Views/Procurement/Sourcing/` · Frontend route: `/Procurement/Sourcing`.

## 10. Backend File Convention
Golden Reference **Compact** birebir: `Features/Sourcing/` altında Commands/, Queries/, Handlers/CommandHandlers/, Handlers/QueryHandlers/, Validators/, `SourcingModels.cs`. Naming: Command/Query record; Handler/Validator class **suffix YOK** (`CreateRfxEventHandler`, `SubmitBidHandler`, `AwardRfxEventHandler`, `CreateRfxEventValidator`). Tek dosyada tek `public` tip yasağı; `Requests/Commands/` gibi ekstra alt klasör yok.

## 11. Frontend File Contract
Compact seti: `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml` (`data-dt-standard="v2"`), `_IndexL10n.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml`, `{Module}Index.cs` + `index.js` + `index.l10n.js`. **YASAK:** `_CreateEditOffcanvas`, `_DetailsQuickView` (compact). Bid submit + award actions Details sayfasından yürür (publish/bid/award state-driven aksiyonlar). L10n: tenant modülü → **7 dil** (en, tr, fr, es, zh, ar, ru).

## 12. Validation Rules
| Field | Required | Rule | DB | Pre-check |
|---|---|---|---|---|
| Type | Evet | enum(RFQ/RFP/RFI) | — | — |
| Title | Evet | trim, NotEmpty | text | — |
| Lines | Evet | ≥1 satır (422 VALIDATION_FAILED); her satır itemId+quantity+uomId | — | ValidateItems (0290) |
| RfxLine.quantity | Evet | Decimal regex; > 0 | — | — |
| InvitedSupplierIds[] | Hayır | her id 0140'ta var | — | ValidateSuppliers (0140) |
| Bid.supplierId | Evet | 0140'ta var + RFx invited listesinde | — | ValidateSuppliers (0140) |
| BidLine.unitPrice | Evet | Decimal regex; ≥ 0 | — | — |
| Bid submit | — | RFx status ∈ {Published} ve closesAt geçmemiş | — | RFx state gate |
| Award.awardedBidId | Evet | RFx'e ait mevcut bid; RFx status ∈ {Published, Evaluating} | — | Bid ownership gate |

## 13. Failure Path to Verify
- **Bid on unpublished/closed RFx** (Draft/Closed/Awarded/Cancelled) → 409 INVALID_STATE + teklif kaydı yok + reload temiz.
- **Unknown supplier/item/uom** (invited/bid/line) → 404 UNKNOWN_REFERENCE (fail-closed) + kayıt yok.
- **Empty lines / missing Title** → 422 VALIDATION_FAILED + validator mesajı + save engellenir.
- **Award with foreign/nonexistent bidId** (başka RFx'in bid'i) → 404/409 + award yok + sessiz overwrite YOK.
- **Delete non-Draft RFx** → 409 INVALID_STATE (yalnız Draft soft-delete).
- **Unauthorized actor** (izin yok) → 403 + action disabled/permission-denied (UAS-001: iskelet çizilmez, yönlendirme yok).
- **Cross-tenant/LE erişim** → 404 (sızıntı yok).

## 14. Authorization Convention
- Policy: `[Authorize]` (tenant actor).
- Permission format (PKS-001, lowercase-dotted, ≥3 segment): `procurement.sourcing.{action}`.
- Actions: read, create, publish, bid, award, delete, bulk-delete.
- Actor: tenant_user (procurement sourcing rolü); server-side `[HasPermission]` zorunlu.

## 15. Gateway / API Routing Decision
- Karar: Gateway değişikliği **gerekli** (`/api/sourcing` → 5062). `ocelot.json` protected; bu pack yazmaz — explicit Upstream/Downstream (POST/GET `/events`, `/events/{rfxId}`, `/events/{rfxId}/publish`, `/events/{rfxId}/bids`, `/events/{rfxId}/award`) + OPTIONS içeren **integration-agent task**'ı olarak ayrı yürütülür.

## 16. Acceptance Criteria
- [ ] `POST /api/sourcing/events` idempotent create → 201 (Draft); `GET /events` list + `GET /events/{rfxId}` tenant+LE filtreli.
- [ ] `POST /events/{rfxId}/publish` idempotent → Draft→Published; Draft dışı → 409 INVALID_STATE.
- [ ] `POST /events/{rfxId}/bids` yalnız Published + closesAt öncesi kabul (aksi 409); unknown supplier/item → 404; idempotent replay yan-etkisiz.
- [ ] `POST /events/{rfxId}/award` → Awarded; `awardedSupplierId` server tarafından bid'den çözülür; rationale + evaluationScore audit'e yazılır (E4, L3 persistence).
- [ ] RFx lifecycle Draft→Published→Evaluating→Awarded doğrulanır; award decision rationale + evaluation skoruyla persist edilir.
- [ ] SUPPLIER (0140) ve PRODUCT-MASTER (0290) referansları consume-only; fail-closed 404; lokal supplier/item kimliği yaratılmaz.
- [ ] Money alanları Decimal string; float serileştirme yok.
- [ ] Tenant+LE izolasyon: cross-LE 404; unauthorized 403 (UAS-001).
- [ ] Tüm `Views/Procurement/Sourcing/*.cshtml` → `Layout = "_LayoutTenantShell"` açık; `verify_datatable_page.py --area Procurement --module Sourcing --reference compact` PASS.
- [ ] RESX 7-dil parite PASS.

## 17. Test Expectations
- Unit: validator (empty lines, missing title, decimal/float reddi), handler (create/publish/bid/award state geçişleri).
- Integration: tenant isolation (cross-LE 404), soft-delete (yalnız Draft), idempotency replay (create/publish/bid/award duplicate side-effect yok), state gate (unpublished/closed RFx'e bid 409), fail-closed (unknown supplier/item 404), award→`RfxAwarded` event.
- Contract: `sourcing.openapi.yaml` mock (Prism) ile owned surface uyumu (RfxEvent/Bid/AwardDecision şekli + error kodları NOT_FOUND/UNKNOWN_REFERENCE/INVALID_STATE/VALIDATION_FAILED).
- Frontend: browser smoke + DataTable verifier PASS.
- Build: ProcurementService + frontend + gateway 0-error.

## 18. Ready-for-dev Checklist
- [ ] Golden Reference Compact okundu
- [ ] Frontmatter tam (service/shell/golden_reference/entity_base)
- [ ] Layout & Shell Contract Razor Layout açık
- [ ] Backend/Frontend file convention golden reference ile birebir
- [ ] Validation her field için yazılı
- [ ] Failure Path ≥4 senaryo (state-conflict/unknown-ref/validation/unauthorized) + cross-tenant
- [ ] Authorization permission listesi + policy + actor
- [ ] Gateway routing kararı (integration-agent task)
- [ ] Acceptance test edilebilir + G2A zincirinde award→PO(0141)/Contracting(0144) upstream rolü
- [ ] Test expectations build/verifier/RESX/smoke/contract-mock kapsıyor
- [ ] DCP-010 approved + bu pack ready-for-dev onayı (kod kapısı)

## 19. Implementation Notes
DCP-010 §8 sıra: 0140→**0145**→0141→0142→0143→0144 (Sourcing 2. dikey dilim, Supplier'dan sonra). Servis 0140 ile scaffold edilir (JWT + Mongo V3 GUID + TenantId izolasyonu + port 5062); bu modül mevcut servise `Features/Sourcing` olarak eklenir. SOURCING contract producer surface `x-status: REVIEW` — freeze owner+consumer review sonrası. Award decision **upstream** çıktıdır: PO (0141) ve Contracting (0144) tüketir.
- **ASSUMPTION-SRC-01 (id üretimi):** `rfxId`/`bidId` contract'ta server-assigned public code olarak varsayılır (0140 `SupplierId` deseni); alan tipi string, tenant+LE unique.
- **ASSUMPTION-SRC-02 (state geçiş sahipliği):** Contract yalnız `publish`/`award` endpoint'i verir; `Evaluating`/`Closed`/`Cancelled` geçişleri için ayrı endpoint yok → `Evaluating` ilk bid/skorlama ile, `Closed` `closesAt` geçince (server-driven), award yalnız Published/Evaluating'den yapılır varsayıldı. Cancel akışı follow-up.
- **ASSUMPTION-SRC-03 (evaluation skoru):** `Bid.evaluationScore` contract'ta okuma-yalnız alan; skorlama algoritması/ağırlıkları koda gömülmez (policy-driven, EA/procurement-TBD) — sabit varsayılan yok.
- **ASSUMPTION-SRC-04 (uom sahipliği):** `RfxLine.uomId` MOD-0290/MDM UoM kataloğundan consume edilir; lokal UoM açılmaz.
- **ASSUMPTION-SRC-05 (award onayı):** Award, Workflow/Approvals (MOD-0023) ile onaya bağlanabilir; onay eşiği tenant policy — contract'ta zorunlu değil, additive.

## 20. Follow-up Items
- Reverse auction / çok-turlu sourcing → follow-up (bu modül değil).
- RFx `Cancelled` explicit endpoint + gerekçe → follow-up (SRC-02).
- Evaluation skorlama motoru (ağırlıklı kriter/TCO) → policy-driven follow-up (SRC-03).
- Award → PO (0141) / Contracting (0144) otomatik seam (event/handoff) → downstream modül packs.
- Dış e-sourcing/EDI adapter (SourceSystem/ExternalRef; DEC-INV-19 deseni) → follow-up.
