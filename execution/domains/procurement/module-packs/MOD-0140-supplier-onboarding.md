---
id: MOD-0140
name: Supplier Onboarding (KYC/Sanctions/Docs)
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

# MOD-0140 — Supplier Onboarding (KYC/Sanctions/Docs)

> **Status guard.** `ready-for-dev` — DCP-010 `approved` (Ali, 2026-09-16) + bu pack `ready-for-dev` → kod yetkili (CAP-001 §7). **FAZ 1: ilk scaffold edilecek modül** (`Diten.ProcurementService` doğar). runtime_code_allowed bu modül için AÇIK.
> **Contract authority.** Entity alanları `docs/analysis/contracts/supplier.openapi.yaml`'dan türetilir; uydurma yasak (K12). Ürün/tedarikçi kimliği consume edilir, yaratılmaz.
> **Değişiklik (2026-10-21, WP-MVP2-SUPPLIER-TENANT-01).** Yönetim kararı: **tedarikçi kiracıda tek kayıttır, kiracının bütün şirketleri onu paylaşır; onay, ödeme koşulu ve satın alma ilişkisi şirket (LE) bazında tutulur.** §2, §3, §4, §5, §7, §8, §12–§20 ve `## Teslim dilimleri` bu karara göre güncellendi. Değişen bölümlerin kodu bu sürümün CT-APR-01 onay satırı (yüksek risk: kiracı + şema/taşıma + izin; onaylayıcı paketi yazmamış ve `high: evet`) ve CT-APR-02 bağımsız incelemesi olmadan başlamaz; dilimler sırayla (DL-1 → DL-4) uygulanır. Öneri ve sahip kararları K-1..K-10: `docs/roadmap/plans/mvp2-supplier-tenant-01/PACK-CHANGE-PROPOSAL.md` (§5a).

## 1. Module Summary
Tedarikçi master + onboarding (KYC / sanctions / doküman / onay) SoR'u. Hedef kullanıcı: procurement operatörü + onaylayan. Supplier kimliği + durum + iletişim yüzeyi MVP-6 (0147 performance, 0148 portal) tarafından tüketilir (frozen slice). Blueprint W-1, Procurement Suite / P2P.

## 2. Ownership and Boundaries
- **In-scope:** supplier master (identity/status/contacts), onboarding case (KYC outcome, sanctions outcome, documents-by-reference, approval), supplier lifecycle (Active/OnHold/Blocked/Inactive).
- **Out-of-scope:** supplier **performance/risk skoru** (MOD-0147, MVP-6), **portal** (MOD-0148, MVP-6), ürün kimliği (MOD-0290/MDM), doküman binary saklama (MOD-0029/0031 Evidence — yalnız `evidenceRef` referansı tutulur), ödeme/AP (Finance/Treasury).
- **Owned contract:** `SUPPLIER` (`supplier.openapi.yaml`, x-owner MOD-0140). Producer surface additive; MVP-6 consumer slice'ı **superset** korunur (daraltma yasak, K16).
- **Sözleşme sürümü (2026-10-21):** `supplier.openapi.yaml` **1.2.0 (FROZEN) → 1.3.0, yalnız eklemeli (K16)**. Açıklamadaki "Tenant + Legal-Entity scoped" (:12) şöyle yeniden yazılır: kimlik kiracıda tek; görünürlük çağıranın şirketiyle ilişki üzerinden. Mevcut işlemler aynı şekil ve aynı şirket bazlı davranışta kalır (`status` = çağıran şirketin ilişki durumu). Eklenenler: ilişki uçları (yalnız kendi şirketi), kiracı geneli vergi no araması (ayrı izin), 409 `DUPLICATE_SUPPLIER` gövdesinde mevcut `supplierId`. Tek davranış farkı K-6 (başka şirkette aynı vergi no → 409); çağıranı yalnız Diten.Web tedarikçi ekranı. Tüketici sözleşmeleri (`requisition-po`, `contracting`, `sourcing`, `invoice-match` 1.1.0; `supplier-performance` 1.0.0) şekil değiştirmez. 1.3.0 dosya değişikliği bu pack'in onayından sonra, DL-2 ile yazılır.

## 3. Owned Objects
- **Entity:** `Supplier` (EntityBase; **kiracıda tek kayıt**, `LegalEntityId` boş), `SupplierContact` (embedded), `SupplierCompanyRelationship` (EntityBase; Tenant + LE + Supplier — şirket bazında durum/onboarding/onay/ödeme koşulu/satın alma ilişkisi), `OnboardingCase` (KYC/sanctions/docs/approval — ilişkiye bağlı; tarama sonucu Supplier'da, K-3).
- **Commands:** CreateSupplier (= **bağla-ya-da-oluştur**: kiracıda yoksa Supplier + ilişki, varsa 409 + "bu şirkete bağla", K-6), UpdateSupplier (ilişki alanları, kendi şirketi), UpdateSupplierMaster (ortak alanlar, K-1), LinkSupplierToCompany, ChangeRelationshipStatus, SetRelationshipPaymentTerms, SubmitOnboardingCase, (soft) DeleteSupplier (çağıran şirketin ilişkisi), BulkDeleteSupplier. Adlar öneridir; kesin adlar DL-2'de Golden Reference Compact naming'iyle verilir.
- **Queries:** GetSupplierList, GetSupplierById, GetOnboardingCase, ValidateSuppliers (bulk id doğrulama — consumer fail-closed; ilişki üzerinden), FindTenantSupplierByTaxId (kiracı geneli, ayrı izin), GetSupplierEligibility (tüketici uygunluk sorusu — "(kiracı, şirket, kod) için aktif ilişki var mı?").
- **DTOs:** `supplier.openapi.yaml` şemaları (Supplier, SupplierUpsert, OnboardingCase, OnboardingSubmit, KycOutcome, SanctionsOutcome) + 1.3.0'da eklenecek ilişki şemaları (additive).
- **API endpoints:** `GET/POST /api/suppliers`, `GET/PATCH /api/suppliers/{supplierId}`, `POST /api/suppliers/validate`, `GET/POST /api/suppliers/{supplierId}/onboarding` (mevcut; şekil değişmez) + 1.3.0'da `/api/suppliers/**` altında ilişki uçları (oku / bağla / durum / onay / ödeme koşulu — yalnız kendi şirketi) ve kiracı geneli vergi no araması.
- **Frontend route:** `/Procurement/Suppliers` (tenant shell).
- **Permissions:** `procurement.suppliers.read|create|update|delete|bulk-delete`, `procurement.suppliers.onboard`, `procurement.suppliers.master.update` (K-1), `procurement.suppliers.read-tenant-wide` (SEC-002).

## 4. Entity Fields

### 4.1 `Supplier` — kiracıda tek kayıt (tedarikçinin kendisi; koleksiyon `supplier_masters`)
| Field | Type | Rules | Index |
|---|---|---|---|
| SupplierId | string (public code) | server-assigned; biçim değişmez (`SUP-xxxxxxxx`); **unique/tenant** | `ux_tenant_supplierid` (TenantId, SupplierId) |
| Name | string | required, trim, max 200; ortak alan (K-1) | text |
| Country | string(lowercase code) | nullable; ortak alan | — |
| TaxId | string | nullable; ortak alan; `TaxIdNormalized` = boşluk, nokta, tire atılmış, büyük harf (K-2) | — |
| TaxIdNormalized | string | server-computed; payload'da YOK | `ux_tenant_country_taxid` partial (TenantId, Country, TaxIdNormalized; dolu + silinmemiş) — K-2 |
| Contacts[] | SupplierContact | type∈{primary,billing,quality,logistics}, email/phone nullable; ortak alan | — |
| Documents[] | {type, evidenceRef} | evidenceRef → MOD-0029/0031; binary yok; ortak alan | — |
| Screening | {KycOutcome, SanctionsOutcome, ScreenedAt} | KYC/sanctions sonucu firmanın olgusu, tedarikçide tek sonuç (K-3); KycOutcome enum(Pending/Passed/Failed/ManualReview), SanctionsOutcome enum(Pending/Clear/Hit/Override) | — |
| MergedIntoId | Guid? | yalnız göç yazar (birleşen kayıtlar) | — |
| TenantId | Guid | **server-resolved, payload'da YOK**; her sorguda filtre | Compound |
| LegalEntityId | Guid? | **kullanılmaz (boş kalır)** — EntityBase alanı; tedarikçi şirkete bağlı değildir | — |
| Id, Version, IsDeleted, DeletedAt, CreatedAt, UpdatedAt | (EntityBase/audit) | soft-delete zorunlu | — |

### 4.2 `SupplierCompanyRelationship` — şirket bazında ilişki (koleksiyon `supplier_company_relationships`)
| Field | Type | Rules | Index |
|---|---|---|---|
| TenantId, LegalEntityId | Guid | **server-resolved, payload'da YOK**; her sorguda filtre | Compound |
| SupplierRef | Guid (Supplier.Id) | required; aynı kiracıdaki Supplier | `ux_tenant_le_supplier` (TenantId, LegalEntityId, SupplierRef) — ilişki başına bir kayıt |
| Status | enum(Active/OnHold/Blocked/Inactive) | required, default Active; bugünkü `Supplier.Status` buraya taşınır; Blocked/OnHold → yeni işlem reddi (K-7) | — |
| OnboardingStatus | enum(Draft/InReview/Approved/Rejected) | onboarding case; şirket bazında | — |
| Approval | {WorkflowInstanceId, DecidedBy, DecidedAt} | onay şirket bazında (MOD-0023) | — |
| OnboardingCase | nesne? | KYC/docs/approval başvurusu — ilişkiye bağlı | — |
| ScreeningOverride | {Outcome, Reason, By, At}? | şirketin tarama sonucunu geçersiz kılması (şirket istisnası, K-3); Reason required | — |
| PaymentTermsCode | string? | ödeme koşulu kodu (K-8; ana tablo sonra); **banka bilgisi YOK** — Finance/Treasury'de kalır | — |
| PurchasingEnabled, PurchasingSince | bool, DateTime? | "bu şirket bu tedarikçiden alabilir mi" — satın alma ilişkisi | — |
| SourceSystem/ExternalRef | string | nullable (DEC-INV-19 dış besleme); şirketin kendi ERP eşlemesi | — |
| LegacySupplierCodes[] | string[] | göçte bu şirketin eski kodları — takma ad (K-5); geçmiş belgeler (PO/fatura/RFx/sözleşme) yeniden yazılmaz | unique (TenantId, LegalEntityId, kod) |
| Id, Version, IsDeleted, DeletedAt, CreatedAt, UpdatedAt | (EntityBase/audit) | soft-delete zorunlu | — |

> `form_field_count: 11` (Name, Status, Country, TaxId, primary Contact email/phone/type, KYC legalName/registrationNo, Document type, submitForApproval) > 8 → `golden_reference: compact`.
> Kod çözümü (tüketiciler, §7): önce `Supplier.SupplierId`, yoksa çağıran şirketin ilişkisindeki `LegacySupplierCodes` — eski belgelerdeki kodlar çözülmeye devam eder. `IdempotencyKey`/`OnboardingIdempotencyKey` teknik alanları taşındıkları komutun varlığında kalır.

## 5. Repo Scope
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Features/Supplier/**`
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Domain/**` (Supplier, OnboardingCase entities)
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Persistence/**` (repositories, Mongo index config, `supplier_masters` / `supplier_company_relationships` koleksiyonları ve göç işi — DL-1)
- DL-4 yalnız: Procurement içindeki beş tedarikçi doğrulama noktası, yalnız "Blocked / OnHold tedarikçiye yeni işlem yok" kontrolü için (K-7; XMC-001 sahip onayı 2026-10-23, MOD-0141/0143/0144/0145; başka davranış değişmez):
  `services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Features/PurchaseOrder/Handlers/CommandHandlers/CreatePurchaseOrderHandler.cs` (MOD-0141),
  `…/Features/InvoiceMatch/Handlers/CommandHandlers/CaptureInvoiceHandler.cs` (MOD-0143),
  `…/Features/Sourcing/Handlers/CommandHandlers/CreateRfxEventHandler.cs` + `SubmitBidHandler.cs` (MOD-0145),
  `…/Features/Contract/Handlers/CommandHandlers/CreateContractHandler.cs` (MOD-0144)
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Api/Controllers/SuppliersController.cs`
- `frontend/Diten.Web/Views/Procurement/Suppliers/**` + `wwwroot/assets/js/Procurement/Suppliers/**` + `Resources/Views/Procurement/Suppliers/**`

## 6. Protected Paths
- `.antigravity/**`
- `services/Diten.SupplyChainService/**`, `services/Diten.MdmService/**` ve diğer domain servisleri
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration-agent owned — ayrı task)
- `frontend/Diten.Web/Views/Archive/**`, `Views/Shared/_Layout.cshtml`
- `docs/analysis/contracts/inventory-bundle.openapi.yaml`, `product-master-bundle.openapi.yaml`, `location.openapi.yaml` (CONSUMED — redefine YASAK)

## 7. Dependencies
- **Backbone:** JWT/RBAC (MOD-0018), Audit (MOD-0021), Workflow/Approvals (MOD-0023, onboarding onayı), Evidence/Docs (MOD-0029/0031, doküman referansı), Notification (MOD-0027).
- **Consumed contracts:** yok (SUPPLIER upstream master; ürün/tedarikçi consume etmez — bu modül supplier'ın kendisidir).
- **Downstream consumers:** MOD-0141 PO (`supplierId`), MOD-0143 Invoice-Match, MOD-0145 Sourcing (`invitedSupplierIds`/`bid.supplierId`), MOD-0144 Contracting, MVP-6 0147/0148. Uygunluk tanımı (hepsi için): "(kiracı, şirket, kod) için **aktif ilişki** var mı?" — MVP-6 DC-02 "olumlu Tenant+LE uygunluğu" bu sonuçtur; MVP-6 okuyucusu 1.3.0 sözleşmesiyle yazılır. Procurement içi beş noktada DL-4 yalnız Blocked / OnHold reddini ekler; varlık denetimi bugünkü gibi kalır (şirketle ilişki yoksa bulunamaz — bugünkü şirket bazlı kayıtla aynı sonuç); MVP-6 (0147/0148) ve Inventory tüketicilerine sahip entegrasyon aşamasında bildirir (bu pack onların kodunu/paketini değiştirmez, XMC-001).
- **Denetim altyapısı (AUD-001):** DL-2'deki yeni yazma komutları BL-501 `Diten.BuildingBlocks.CentralAudit` iletimiyle denetlenir; iletim MVP-1 dalıyla main'e gelir. **DL-2 bu gelmeden başlamaz** (K-4). Bugün servis defterinde (`tests/architecture/audit-ledger/Diten.ProcurementService.md`) kabul edilmiş iz yok; Create/Update/Delete/BulkDelete "bilinen borç".

## 8. Runtime Constraints
- Gateway port **5065**; frontend yalnız Gateway (5000) üzerinden çağırır.
- **Tenant izolasyonu her sorguda; normal okuma Tenant + LE** (server-resolved). `Supplier` sorgusu `TenantId` + `!IsDeleted`; ilişki sorgusu `TenantId` + `LegalEntityId` + `!IsDeleted`. Liste, detay, onboarding ve `/validate` **ilişki üzerinden** gider: bir tedarikçi bir şirkette yalnız o şirketle **silinmemiş ilişkisi** varsa görünür (Blocked/OnHold ilişki yönetilebilsin diye görünür kalır; işlem uygunluğu ayrıca `Status = Active` ister, §7); ilişki yoksa 404 (bugünkü davranış). Cross-tenant → 404.
- Şirket A kullanıcısı B'nin ilişki verisini (durum, onay, ödeme koşulu, tarama istisnası, harici eşleme, eski kodlar) **hiçbir uçta** görmez. Ortak alanlar (ad, ülke, vergi no, iletişim, belgeler, tarama sonucu) ilişkili her şirkette aynıdır.
- Kiracı geneli okuma yalnız açıkça verilmiş `procurement.suppliers.read-tenant-wide` ile (SEC-002, `data-scope-enforcement.md` madde 5): ayrı üretici, boolean parametre yok; dönen en az alan `supplierId`, `name`, `country` — ilişki verisi dönmez. İzin yoksa 403.
- Ortak alanları yalnız `procurement.suppliers.master.update` değiştirir (K-1); ilişki alanları `update` / `onboard` ile ve yalnız çağıranın kendi şirketinde.
- **Blocked / OnHold ilişki yeni işlemi reddeder** (K-7): Procurement içi tüketiciler — PO, fatura, RFx/teklif, sözleşme (DL-4).
- **Göç (DL-1):** yeni koleksiyonlar `supplier_masters`, `supplier_company_relationships`; eski `suppliers` koleksiyonu göç boyunca salt okunur kaynak. Göç işi idempotent, kipleri `dry-run` / `apply` / `verify`; geri alma = okuma anahtarı `Suppliers:Model` (`PerLegalEntity` | `TenantShared`) eskiye döner, yeni koleksiyonlar silinmez, birleşen kayıtlar `MergedIntoId` + soft delete ile geri açılabilir. Birleştirme kuralı (grup anahtarı, hayatta kalan, çakışan ortak alanlar, kod çakışması) öneri §2.1'dedir. Gerçek veri yok — yalnız geliştirme/test verisi.
- Soft delete (`IsDeleted`/`DeletedAt`).
- Idempotent create/onboarding (`Idempotency-Key`); update optimistic concurrency (`If-Match`/rowVersion) → 409.
- Mongo V3 GUID subtype-4.

## 9. Layout & Shell Contract
- `shell: tenant` → tüm `Views/Procurement/Suppliers/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` **AÇIKÇA**.
- View klasörü: `Views/Procurement/Suppliers/` · Frontend route: `/Procurement/Suppliers`.

## 10. Backend File Convention
Golden Reference **Compact** birebir: `Features/Supplier/` altında Commands/, Queries/, Handlers/CommandHandlers/, Handlers/QueryHandlers/, Validators/, `SupplierModels.cs`. Naming: Command/Query record; Handler/Validator class **suffix YOK** (`CreateSupplierHandler`, `CreateSupplierValidator`).

## 11. Frontend File Contract
Compact seti: `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml` (`data-dt-standard="v2"`), `_IndexL10n.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml`, `{Module}Index.cs` + `index.js` + `index.l10n.js`. **YASAK:** `_CreateEditOffcanvas`, `_DetailsQuickView` (compact). L10n: tenant modülü → **7 dil** (en, tr, fr, es, zh, ar, ru).

## 12. Validation Rules
| Field | Required | Rule | DB | Pre-check |
|---|---|---|---|---|
| Name | Evet | trim, max 200 | — | — |
| TaxId | Hayır | format; normalize (boşluk/nokta/tire atılır, büyük harf); **kiracı + ülke + normalize vergi no** silinmemiş Supplier'da unique (K-2) | `ux_tenant_country_taxid` partial | ExistsByTaxIdAsync (kiracı geneli, normalize) |
| SupplierId | — (server) | kiracıda unique | `ux_tenant_supplierid` | — |
| İlişki (Tenant+LE+Supplier) | — | şirket başına tedarikçi için tek ilişki; ikinci bağlama 409 | `ux_tenant_le_supplier` | relationship exists |
| LegacySupplierCodes[] | — (yalnız göç) | (Tenant, LE, kod) unique | unique | — |
| Status (ilişki) | Evet | enum | — | — |
| ScreeningOverride | Hayır | Outcome enum + Reason NotEmpty | — | — |
| PaymentTermsCode | Hayır | kod; banka alanı kabul edilmez | — | — |
| Contact.type | Evet (satırda) | enum | — | — |
| Onboarding.submit | — | KYC legalName NotEmpty; en az 1 document | — | — |

- **Başka şirkette mevcut vergi no ile oluşturma (K-6):** kiracıda aynı (ülke, normalize vergi no) ile Supplier varsa create **409 `DUPLICATE_SUPPLIER`** döner; gövdede mevcut `supplierId`; ekran "bu tedarikçiyi bu şirkete bağla" yolunu sunar (bağlama ayrı, açık bir şirket kararıdır; sessiz otomatik bağlama YOK). **Uyumluluk anahtarı YOK** — eski "her şirkette ayrı kayıt" davranışı korunmaz (canlıda veri yok).
- Unique index yarışı (`DuplicateKey`) 409'a çevrilir, 500 dönmez.

## 13. Failure Path to Verify
- **Duplicate active TaxId** (kiracı + ülke + normalize vergi no; başka şirkette de) → 409 + mevcut `supplierId` + "bu şirkete bağla" + kayıt yok + reload temiz (K-6).
- **Aynı şirkete ikinci bağlama** → 409, ikinci ilişki yok.
- **Ortak alan güncellemesi `master.update` izni olmadan** → 403; **kiracı geneli arama `read-tenant-wide` olmadan** → 403.
- **Başka şirketin ilişkisini okuma/değiştirme** ya da ilişkisi olmayan şirkette erişim → 404.
- **Blocked / OnHold ilişkiyle yeni PO / fatura / RFx-teklif / sözleşme** → red, kayıt yok (K-7, DL-4).
- **Missing Name** → 400 + validator mesajı + save engellenir.
- **Concurrency conflict** (stale update) → 409 + "kayıt değişti, yeniden yükle" + sessiz overwrite YOK.
- **Unauthorized actor** (izin yok) → 403 + action disabled/permission-denied (UAS-001: iskelet çizilmez, yönlendirme yok).
- **Cross-tenant/LE erişim** → 404 (sızıntı yok).

## 14. Authorization Convention
- Policy: `[Authorize]` (tenant actor).
- Permission format (PKS-001, lowercase-dotted, ≥3 segment): `procurement.suppliers.{action}`.
- Actions: read, create, update, delete, bulk-delete, onboard, `master.update` (ortak alanlar — K-1), `read-tenant-wide` (kiracı geneli vergi no araması — SEC-002; yalnız açıkça verilir, hiçbir rolde varsayılan değil).
- `update` / `onboard` / `delete` yalnız çağıranın kendi şirketindeki ilişkiye etki eder; ortak alanı değiştiremez.
- Actor: tenant_user (procurement rolü); server-side `[HasPermission]` zorunlu.

## 15. Gateway / API Routing Decision
- Karar: Gateway değişikliği **gerekli** (`/api/suppliers` → 5065). `ocelot.json` protected; bu pack yazmaz — explicit Upstream/Downstream + OPTIONS içeren **integration-agent task**'ı olarak ayrı yürütülür.
- 2026-10-21 değişikliği: yeni ilişki ve kiracı geneli arama uçları `/api/suppliers/**` altında kalır; mevcut `/api/suppliers` + `/api/suppliers/{everything}` rotaları (`ocelot.json`) kapsar → **yeni gateway değişikliği gerekmez**. Uç bu önekin dışına çıkarsa integration-agent task'ı açılır.

## 16. Acceptance Criteria
- [ ] `POST /api/suppliers` idempotent create → 201; `GET` list/by-id tenant+LE filtreli.
- [ ] `PATCH` optimistic concurrency → stale update 409.
- [ ] `POST /api/suppliers/validate` bilinmeyen id'leri `known:false` işaretler (consumer fail-closed).
- [ ] Onboarding: KYC/sanctions outcome + document evidenceRef + approval (MOD-0023) kaydedilir; binary saklanmaz.
- [ ] SUPPLIER contract consumer slice (listSuppliers/getSupplier/validateSuppliers) şekli **değişmeden** yanıt verir (MVP-6 uyumu).
- [ ] Tenant+LE izolasyon: cross-LE 404; unauthorized 403 (UAS-001).
- [ ] Kiracıda tek Supplier: aynı (ülke, normalize vergi no) iki şirkette tek `supplier_masters` kaydı + iki ilişki; şirket B'de create → 409 + mevcut `supplierId`; "bağla" sonrası B'de görünür.
- [ ] Şirket A kullanıcısı B'nin ilişki alanlarını hiçbir uçta görmez; ilişkisi olmayan şirkette detay/validate 404/`known:false`.
- [ ] `master.update` olmadan ortak alan değişikliği 403; `read-tenant-wide` olmadan kiracı geneli arama 403; izinle dönen alanlar yalnız `supplierId`, `name`, `country`.
- [ ] Göç işi `dry-run` / `apply` / `verify` / geri alma: ikinci `apply` hiçbir şey değiştirmez; `verify` çözülemeyen (kiracı, şirket, kod) = 0; eski kodlar `LegacySupplierCodes` ile çözülür, geçmiş belgeler yeniden yazılmaz.
- [ ] Blocked / OnHold ilişkiyle PO, fatura, RFx/teklif, sözleşme oluşturma reddedilir (K-7).
- [ ] `supplier.openapi.yaml` 1.3.0 yalnız eklemeli: 1.2.0 (A) consumer slice ve (B) producer işlemleri aynı şekil.
- [ ] Tüm `Views/Procurement/Suppliers/*.cshtml` → `Layout = "_LayoutTenantShell"` açık; `verify_datatable_page.py --area Procurement --module Suppliers --reference compact` PASS.
- [ ] RESX 7-dil parite PASS.

## 17. Test Expectations
- Unit: validator (duplicate taxId, missing name), handler (create/update/onboarding).
- Integration: tenant isolation (cross-LE 404), soft-delete, idempotency replay (duplicate side-effect yok), concurrency 409.
- Integration (2026-10-21 değişikliği; gerçek replica set, 27017–27021 ve ≥49152 dışında geçici Mongo): birleştirme kuralı, vergi no normalizasyonu, farklı ülke aynı no, kod çakışması, idempotent ikinci koşu, verify sayımı, geri alma (DL-1); şirket A/B ilişki izolasyonu, kiracı geneli 409 + mevcut kod, eski kod çözümü, izinsiz kiracı geneli arama 403 (DL-2); her tüketicide ilişkisiz şirket 404 (bugünkü davranış, değişmedi) ve Blocked / OnHold reddi (DL-4).
- Sabotaj (üretim kodunda, guard kanıtı): birleştirme anahtarından Country çıkarılınca farklı-ülke testi kırmızı; idempotency kontrolü kaldırılınca ikinci koşu testi kırmızı; ilişki sorgusundan `LegalEntityId` kaldırılınca izolasyon testi kırmızı; legacy kod çözümü kaldırılınca eski PO kodu testi kırmızı; bir dilden RESX anahtarı silinince parite testi kırmızı; Blocked / OnHold kontrolü atlanınca tüketici testleri kırmızı.
- Contract: `supplier.openapi.yaml` mock (Prism) ile consumer slice uyumu.
- Frontend: browser smoke + DataTable verifier PASS.
- Build: ProcurementService + frontend + gateway 0-error.

## 18. Ready-for-dev Checklist
- [ ] Golden Reference Compact okundu
- [ ] Frontmatter tam (service/shell/golden_reference/entity_base)
- [ ] Layout & Shell Contract Razor Layout açık
- [ ] Backend/Frontend file convention golden reference ile birebir
- [ ] Validation her field için yazılı
- [ ] Failure Path ≥4 senaryo (duplicate/missing/unauthorized/concurrency) + cross-tenant
- [ ] Authorization permission listesi + policy + actor
- [ ] Gateway routing kararı (integration-agent task)
- [ ] Acceptance test edilebilir + G2A zincirinde supplier upstream rolü
- [ ] Test expectations build/verifier/RESX/smoke/contract-mock kapsıyor
- [x] DCP-010 approved (Ali, 2026-09-16) + bu pack ready-for-dev onayı (kod kapısı AÇIK)
- [ ] 2026-10-21 değişikliği: bu sürümün CT-APR-02 bağımsız incelemesi (`reviews.tsv`) + CT-APR-01 onay satırı (`approvals.tsv`, yüksek risk, paketi yazmamış `high: evet` onaylayıcı — K-10: Beste, `bestepullukcu-alt`; CT-APR-02 incelemesi MVP-1 Control Tower); bu olmadan değişen bölümlerin kodu başlamaz
- [ ] DL-2 öncesi: BL-501 `Diten.BuildingBlocks.CentralAudit` MVP-1 ile main'de (K-4) + §21 "Audited Events" yazılı

## 19. Implementation Notes
İlk scaffold edilecek modül (DCP-010 §8 sıra: 0140→0145→0141→0142→0143→0144). Servis scaffold bu pack + `@orchestrator /add-module` ile doğar (JWT + Mongo V3 GUID + TenantId izolasyonu + port 5065 + gateway route). SUPPLIER contract producer surface `x-producer-status: REVIEW` — freeze owner+consumer review sonrası; MVP-6 slice superset korunur.

**2026-10-21 — kiracıda tek tedarikçi, şirket bazında ilişki (WP-MVP2-SUPPLIER-TENANT-01).**
- Kaynak: `docs/roadmap/plans/mvp2-supplier-tenant-01/PACK-CHANGE-PROPOSAL.md` (hedef model §1, göç §2, sözleşme etkisi §3, dilimler §4, sahip kararları §5a); ölçüm `docs/records/audits/2026-10/mvp2-supplier-tenant-01/MEASUREMENT.md`. Emsal: MOD-0290 kalem ana kaydı (kiracı geneli ana veri, şirket bazında hareket).
- Sahip kararları (2026-10-21): K-1 ortak alanlar `procurement.suppliers.master.update` · K-2 tekillik kiracı + ülke + normalize vergi no · K-3 tarama sonucu tedarikçide, şirket istisnası ilişkide (`ScreeningOverride`) · K-4 istisna gerekmez, yeni yazma komutları BL-501 `Diten.BuildingBlocks.CentralAudit` ile denetlenir (MVP-1 main'e girince; DL-2 o zamana kadar başlamaz) · K-5 eski kodlar ilişkide takma ad, geçmiş belgeler yeniden yazılmaz · K-6 başka şirkette aynı vergi no → 409 + mevcut kod + "bu şirkete bağla", uyumluluk anahtarı yok · K-7 Blocked/OnHold ilişkiye yeni işlem reddi (Procurement içi beş nokta bu WP'de; 2026-10-23: kapsam yalnız bu kontrol); Blocked/OnHold şirkette görünür ve yönetilebilir kalır, yeni işleme yalnız Active girer (2026-10-23); `Suppliers:Model` geri alma anahtarı DL-1'de kalır (2026-10-23) · K-8 ilişkide `PaymentTermsCode`, banka bilgisi Finance'ta · K-9 karar ADR'ye yazılır · K-10 pack CT-APR-01'e göre (yüksek risk) paketi yazmamış onaylayıcı tarafından onaylanır — onaylayıcı Beste (`bestepullukcu-alt`, K2, `high: evet`; 2026-10-23).
- **ADR (K-9):** `docs/records/decisions/2026-10/mvp2-supplier-tenant-master-management-decision-01.md` — Natig (yönetim) kararı 2026-10-21; repodaki kaynak `docs/roadmap/plans/mvp6-development-plan-v10.0.md` C1 satırı (PR #177).
- **Mevcut veri:** tedarikçi modülü canlıda kullanılmadı, gerçek veri yok. Göç işi (dry-run / apply / verify / geri alma) yine yazılır, yalnız geliştirme/test verisi içindir; sayım sorgusu (öneri §2.3a) koşulmaz.
- **§21 "Audited Events"** ve `execution/approvals/approvals.tsv` / `reviews.tsv` satırları bu değişiklikte yazılmadı; ilk yazma komutu değişikliğiyle (DL-2) eklenir (K-4; `module-pack-standard.md` Bölüm 10.1).
- **Dış konu — Inventory karantina:** `quarantine.openapi.yaml` 1.2.0 `DecisionDisposition.SupplierId` **Guid/uuid** (`QuarantineEntities.cs:177`), Procurement kodu ise `SUP-…` metni — bugünkü tip uyuşmazlığı, bu değişiklikten bağımsız. Sahibi Inventory; entegrasyonda ölçülüp düzeltilir. **Bu pack değiştirmez** (XMC-001).
- **Bildirim:** MVP-6 (MOD-0147/0148, DC-02 "aktif ilişki" tanımı, "tenant-scoped" seam belgeleri) ve Inventory tüketicilerine değişikliği sahip entegrasyon aşamasında iletir; onların paketleri bu pack'le değişmez.

## 20. Follow-up Items
- Supplier performance/risk (0147) ve portal (0148) → MVP-6 (bu modül değil).
- Supplier bank/payment detayları → Finance/Treasury sınırı (EA-TBD). 2026-10-21: ödeme koşulu **kodu** ilişkiye alındı (K-8); banka bilgisi Finance/Treasury'de kalır; ödeme koşulu ana tablosu sonra.
- Dış ERP supplier senkron adapter (SourceSystem/ExternalRef; DEC-INV-19 deseni) → follow-up.
- Vergi no'suz kayıtların elle birleştirilmesi (göç bunları birleştirmez) → ayrı iş.
- Inventory karantina `supplierId` uuid / `SUP-` kod uyuşmazlığı → Inventory sahibi (XMC-001).

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | S1 model + göç: iki varlık, indeksler, göç işi (dry-run / apply / verify / geri alma) | Entity Fields, Validation Rules, Runtime Constraints, Repo Scope |
| DL-2 | S2 komutlar ve sorgular: iki repository, ilişki üzerinden okuma, bağla-ya-da-oluştur, ortak alan ve ilişki komutları, uygunluk sorusu | Owned Objects, Runtime Constraints, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision |
| DL-3 | S3 ekranlar (7 dil): şirketin tedarikçileri, "mevcut tedarikçi bulundu → bağla", ilişki paneli, ortak alanlar yalnız yetkiliye | Frontend File Contract, Layout & Shell Contract, Acceptance Criteria |
| DL-4 | S4 tüketiciler: Procurement içi beş doğrulama noktasına yalnız Blocked / OnHold reddi eklenir | Dependencies, Runtime Constraints, Test Expectations |

- DL-1 notu: model + göç işi `dry-run` / `apply` / `verify` / geri alma kipleriyle yazılır; **yalnız geliştirme/test verisi** — canlıda tedarikçi verisi yok.
- DL-2 notu: yeni yazma komutları BL-501 `Diten.BuildingBlocks.CentralAudit` ile denetlenir; **MVP-1 main'e girmeden başlamaz** (K-4). §21 "Audited Events" ve onay/inceleme satırları bu dilimle eklenir.
- DL-4 notu: yalnız Procurement içi tüketiciler (PO, fatura, RFx/teklif, sözleşme) ve yalnız "bloke tedarikçiye yeni işlem yok" kontrolü; başka davranış değişmez (XMC-001 onayı 2026-10-23). MVP-6 ve Inventory'ye sahip entegrasyonda bildirir.
- Her dilim ayrı commit, ayrı PR; push öncesi `origin/main` merge + ölçüm yeniden (main'e göre yeni kırmızı yok).
