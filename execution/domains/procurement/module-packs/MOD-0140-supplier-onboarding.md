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
> **Değişiklik (2026-10-21, WP-MVP2-SUPPLIER-TENANT-01).** Yönetim kararı: **tedarikçi kiracıda tek kayıttır, kiracının bütün şirketleri onu paylaşır; onay, ödeme koşulu ve satın alma ilişkisi şirket (LE) bazında tutulur.** §2, §3, §4, §5, §7, §8, §12–§20 ve `## Teslim dilimleri` bu karara göre güncellendi. Değişen bölümlerin kodu bu sürümün CT-APR-01 onay satırı (yüksek risk: kiracı + şema/taşıma + izin; onaylayıcı paketi yazmamış ve `high: evet`) ve CT-APR-02 bağımsız incelemesi olmadan başlamaz; dilimler sırayla uygulanır (Revizyon 2 numaralandırmasıyla DL-1 → DL-7). Öneri ve sahip kararları K-1..K-10: `docs/roadmap/plans/mvp2-supplier-tenant-01/PACK-CHANGE-PROPOSAL.md` (§5a).
> **Revizyon 2 (2026-10-26, WP-MVP2-SUPPLIER-TENANT-01).** Sahip/onaylayıcı kararları (Beste, MVP-1 CT, 2026-10-26): (1) `supplier.openapi.yaml` 1.3.0 yalnız eklemeli — `POST /validate` sonuç satırına uygunluk alanları, `GET /api/suppliers/{supplierId}/eligibility`, `GET /api/suppliers/eligible`, şema `SupplierEligibility` (§3.1); (2) dar izin `procurement.suppliers.eligibility.read` (§14); (3) Treasury isteği: çağıran şirketle **aktif** ilişkisi olan tedarikçi listesi; (4) contract-first — 1.3.0 ilk dilimde (DL-1) yazılır ve donar; (5) onboarding onayı gerçek MOD-0023 iş akışıyla, sahte `WF-` kimliği kalkar; (6) başlık LE'si MDM'den doğrulanır (C2, servis geneli ara katman); (7) `validate` / uygunluk okuması için servis token'ı son dilim (DL-7, Auth sahibinin BL-652 Option 2'sine bağlı). §2, §3, §4 (Approval satırı + kod çözümü notu), §5, §6, §7, §8, §12–§20 ve `## Teslim dilimleri` güncellendi. Dilim numaraları değişti: eski DL-1 → **DL-2**, eski DL-2 → **DL-3**, eski DL-3 → **DL-5**, eski DL-4 → **DL-6**; yeni dilimler DL-1 (sözleşme), DL-4 (MOD-0023 onayı + LE doğrulaması), DL-7 (servis token'ı). Bu revizyon da yüksek risklidir (izin, kiracı/LE, sözleşme, entegrasyon): bu sürümün CT-APR-02 bağımsız incelemesi ve CT-APR-01 onay satırı olmadan değişen bölümlerin kodu ve sözleşme dosyası yazılmaz. Onay/inceleme satırları ve §21 "Audited Events" bu revizyonda yazılmadı (§21 ilk yazma komutu dilimiyle, DL-3).

## 1. Module Summary
Tedarikçi master + onboarding (KYC / sanctions / doküman / onay) SoR'u. Hedef kullanıcı: procurement operatörü + onaylayan. Supplier kimliği + durum + iletişim yüzeyi MVP-6 (0147 performance, 0148 portal) tarafından tüketilir (frozen slice). Blueprint W-1, Procurement Suite / P2P.

## 2. Ownership and Boundaries
- **In-scope:** supplier master (identity/status/contacts), onboarding case (KYC outcome, sanctions outcome, documents-by-reference, approval), supplier lifecycle (Active/OnHold/Blocked/Inactive).
- **Out-of-scope:** supplier **performance/risk skoru** (MOD-0147, MVP-6), **portal** (MOD-0148, MVP-6), ürün kimliği (MOD-0290/MDM), doküman binary saklama (MOD-0029/0031 Evidence — yalnız `evidenceRef` referansı tutulur), ödeme/AP (Finance/Treasury).
- **Owned contract:** `SUPPLIER` (`supplier.openapi.yaml`, x-owner MOD-0140). Producer surface additive; MVP-6 consumer slice'ı **superset** korunur (daraltma yasak, K16).
- **Sözleşme sürümü (2026-10-21):** `supplier.openapi.yaml` **1.2.0 (FROZEN) → 1.3.0, yalnız eklemeli (K16)**. Açıklamadaki "Tenant + Legal-Entity scoped" (:12) şöyle yeniden yazılır: kimlik kiracıda tek; görünürlük çağıranın şirketiyle ilişki üzerinden. Mevcut işlemler aynı şekil ve aynı şirket bazlı davranışta kalır (`status` = çağıran şirketin ilişki durumu). Eklenenler: ilişki uçları (yalnız kendi şirketi), kiracı geneli vergi no araması (ayrı izin), 409 `DUPLICATE_SUPPLIER` gövdesinde mevcut `supplierId`. Tek davranış farkı K-6 (başka şirkette aynı vergi no → 409); çağıranı yalnız Diten.Web tedarikçi ekranı. Tüketici sözleşmeleri (`requisition-po`, `contracting`, `sourcing`, `invoice-match` 1.1.0; `supplier-performance` 1.0.0) şekil değiştirmez. 1.3.0 dosya değişikliği bu pack'in onayından sonra, **DL-1** ile yazılır (Revizyon 2: contract-first, aşağıda).
- **Sözleşme 1.3.0 — Revizyon 2 eklemeleri ve contract-first kuralı (2026-10-26):**
  - 1.3.0 yukarıdaki ilişki uçlarına, kiracı geneli vergi no aramasına ve 409 gövdesindeki `supplierId`'ye ek olarak **uygunluk yüzeyini** taşır: `POST /validate` sonuç satırına eklemeli alanlar (§3.2), `GET /{supplierId}/eligibility` (`getSupplierEligibility`), `GET /eligible` (`listEligibleSuppliers`) ve `SupplierEligibility` şeması (§3.1). Şema ve alanlar pack'te birebir yazılıdır; sözleşme bu metinden kopyalanır, yorumla genişletilmez. İlişki uçlarının ve kiracı geneli aramanın kesin yol/işlem adları da DL-1'de, §4.2 alanlarından türetilerek sözleşmeye yazılır (ya da 1.4.0'a bırakılır — §20 S-6).
  - **Tek yazar bu modül** (`x-owner: MOD-0140`). Dosya yazımı yalnız bu pack'in onayını ister (sözleşmenin sahibi bu modül; tüketici pack'lerinin onayı gerekmez, onlar bildirilir).
  - **Yalnız eklemeli (K16):** 1.2.0'daki hiçbir yol, işlem, parametre, şema alanı, enum değeri, `required` listesi ya da yanıt kodu değişmez, silinmez, daraltılmaz; yeni alanlar mevcut nesnelere yalnız `properties` eklemesi olarak girer (mevcut satır şeması `allOf`/`$ref` ile yeniden kurulmaz). 1.2.0 FROZEN metni git geçmişinde aynen kalır.
  - DL-1 dosyayı yazar ve aynı commit'te `version: 1.3.0`, `x-status: FROZEN`, `x-slice-status: FROZEN`, `x-producer-status: FROZEN <DL-1 birleşme tarihi>` yapar; 1.3.0 o tarihten sonra yalnız yeni eklemeli sürümle (1.4.0+) genişler.
  - **Tüketiciler** — MVP-6 MOD-0147 / MOD-0148 (SCE, `Diten.SupplyChainService`) ve Treasury girişi MOD-0119 Accounts Payable / MOD-0289 Bank Master — DL-1 FROZEN olduktan sonra 1.3.0'a karşı (Prism mock) kod yazabilir. `x-front-loaded-for` listesine MOD-0119 ve MOD-0289 eklenir (eklemeli).
  - **Sunum:** uçları uygulayan dilim (DL-3) birleşene kadar 1.3.0'ın yeni uçları **sunulmaz** — feature flag ya da 501 yoktur. Gateway rotası `/api/suppliers/**` zaten Procurement'a (5065) yönlendirdiği için istek servise ulaşır ve bugünkü davranışı alır: `/validate` 1.2.0 şeklinde yanıt verir (yeni alanlar yok), `GET /eligible` bugünkü `GET /{supplierId}` aksiyonuna düşer (`procurement.suppliers.read` yoksa 403, varsa 404 `UNKNOWN_SUPPLIER`), `GET /{supplierId}/eligibility` için aksiyon yok → 404. Tüketici canlı bağlantıyı DL-3 birleşmeden açmaz; yeni alan yoksa uygun saymaz (fail-closed).
  - 1.3.0 Revizyon 2 hata yanıtları (eklemeli yanıt kodları; mevcut kodlar değişmez): bütün işlemlere `404 UnknownLegalEntity` (`UNKNOWN_LEGAL_ENTITY`, C2) ve `503 DependencyUnavailable` (`DEPENDENCY_UNAVAILABLE`, MDM / MOD-0023 sorulamadı); `submitOnboardingCase`'e `409 WorkflowTemplateMissing` (`WORKFLOW_TEMPLATE_MISSING`) ve `403`. Gövde mevcut `Error` şeması.
  - Servis token'ı güvenlik şeması 1.3.0'a **girmez**; DL-7 ile ayrı eklemeli sürümde (1.4.0) eklenir — audience adı BL-652 Option 2'de Auth sahibince kesinleşir.

## 3. Owned Objects
- **Entity:** `Supplier` (EntityBase; **kiracıda tek kayıt**, `LegalEntityId` boş), `SupplierContact` (embedded), `SupplierCompanyRelationship` (EntityBase; Tenant + LE + Supplier — şirket bazında durum/onboarding/onay/ödeme koşulu/satın alma ilişkisi), `OnboardingCase` (KYC/sanctions/docs/approval — ilişkiye bağlı; tarama sonucu Supplier'da, K-3).
- **Commands:** CreateSupplier (= **bağla-ya-da-oluştur**: kiracıda yoksa Supplier + ilişki, varsa 409 + "bu şirkete bağla", K-6), UpdateSupplier (ilişki alanları, kendi şirketi), UpdateSupplierMaster (ortak alanlar, K-1), LinkSupplierToCompany, ChangeRelationshipStatus, SetRelationshipPaymentTerms, SubmitOnboardingCase, (soft) DeleteSupplier (çağıran şirketin ilişkisi), BulkDeleteSupplier; Revizyon 2: RecordOnboardingDecision (MOD-0023 sonucunu ilişkiye yazan iç komut — HTTP ucu yok, okuma anı uzlaştırmasından çağrılır, DL-4). Adlar öneridir; kesin işlem/yol adları DL-1'de sözleşmede, sınıf adları DL-3'te Golden Reference Compact naming'iyle verilir.
- **Queries:** GetSupplierList, GetSupplierById, GetOnboardingCase, ValidateSuppliers (bulk id doğrulama — consumer fail-closed; ilişki üzerinden; Revizyon 2: satıra uygunluk alanları, §3.2), FindTenantSupplierByTaxId (kiracı geneli, ayrı izin), GetSupplierEligibility (tüketici uygunluk sorusu — "(kiracı, şirket, kod) için aktif ilişki var mı?"; kod ya da `supplierMasterId` uuid ile, §3.1), ListEligibleSuppliers (Revizyon 2, Treasury isteği: çağıran şirketle **aktif** ilişkisi olan tedarikçiler, sayfalı).
- **DTOs:** `supplier.openapi.yaml` şemaları (Supplier, SupplierUpsert, OnboardingCase, OnboardingSubmit, KycOutcome, SanctionsOutcome) + 1.3.0'da eklenecek ilişki şemaları ve `SupplierEligibility` (additive, §3.1).
- **API endpoints:** `GET/POST /api/suppliers`, `GET/PATCH /api/suppliers/{supplierId}`, `POST /api/suppliers/validate`, `GET/POST /api/suppliers/{supplierId}/onboarding` (mevcut; 1.2.0 alanları aynı şekil) + 1.3.0'da `/api/suppliers/**` altında ilişki uçları (oku / bağla / durum / onay / ödeme koşulu — yalnız kendi şirketi), kiracı geneli vergi no araması ve Revizyon 2 uygunluk uçları: `GET /api/suppliers/{supplierId}/eligibility` (`getSupplierEligibility`), `GET /api/suppliers/eligible` (`listEligibleSuppliers`); `POST /api/suppliers/validate` satırına eklemeli alanlar.
- **Frontend route:** `/Procurement/Suppliers` (tenant shell).
- **Permissions:** `procurement.suppliers.read|create|update|delete|bulk-delete`, `procurement.suppliers.onboard`, `procurement.suppliers.master.update` (K-1), `procurement.suppliers.read-tenant-wide` (SEC-002; **bu pack'te Tier-3 eylem olarak kayıtlı**, §14), `procurement.suppliers.eligibility.read` (Revizyon 2; dar okuma, §14).

### 3.1 `SupplierEligibility` — uygunluk şeması (1.3.0, Revizyon 2)
Sözleşmeye bu metin birebir yazılır (`components.schemas.SupplierEligibility`; dosyanın kendi `nullable: true` deseniyle). Null olabilen alanlar da her yanıtta **yazılır** (`required`; değer `null`) — tüketici alanın yokluğunu uygunluk sanamaz; yalnız `contractVersion` isteğe bağlıdır (liste öğelerinde yazılmayabilir, zarfta vardır). Bütün değerler **yalnız çağıran şirketin** (server-resolved Tenant + LE) ilişkisinden gelir; başka şirketin ilişki verisi hiçbir alanda dönmez.

| alan | tip | null | anlam |
|---|---|---|---|
| `supplierId` | string | hayır | kanonik tedarikçi kodu (`Supplier.SupplierId`, `SUP-xxxxxxxx`). Uca eski kodla ya da uuid ile sorulsa da kanonik kod döner |
| `supplierMasterId` | string, `format: uuid` | evet | kiracı geneli tedarikçi kimliği (`Supplier.Id`); çağıran şirketle ilişki yoksa `null` (kiracıdaki varlık sızdırılmaz) |
| `relationshipExists` | boolean | hayır | çağıran şirketle **silinmemiş** ilişki var mı |
| `status` | `SupplierStatus` (Active, OnHold, Blocked, Inactive) | evet | ilişkinin durumu; ilişki yoksa `null` |
| `onboardingStatus` | `OnboardingStatus` (Draft, InReview, Approved, Rejected — mevcut enum) | evet | ilişkinin onboarding durumu; ilişki ya da onboarding kaydı yoksa `null`. **Onay sonucu bu alandadır** (MOD-0023 kararı `Approved` / `Rejected` olarak yazılır, §4.2); ayrı bir `approval` alanı eklenmez — onay ayrıntısı (`workflowInstanceId`, `decidedBy`, `decidedAt`) yalnız `GET /{supplierId}/onboarding` (`OnboardingCase.approval`) ile, `procurement.suppliers.read` izniyle okunur |
| `purchasingEnabled` | boolean | evet | ilişkideki `PurchasingEnabled`; ilişki yoksa `null` |
| `eligibleForTransactions` | boolean | hayır | **yalnız** `relationshipExists == true` **ve** ilişki silinmemiş **ve** `status == Active` iken `true`; başka her durumda `false`. `onboardingStatus` ve `purchasingEnabled` bu hesaba girmez (sahip tanımı, 2026-10-26; §20 açık soru S-1) |
| `contractVersion` | string | hayır (isteğe bağlı) | mevcut yanıtlarla aynı değer (`v1`) |

```yaml
SupplierEligibility:
  type: object
  required: [supplierId, supplierMasterId, relationshipExists, status, onboardingStatus, purchasingEnabled, eligibleForTransactions]
  properties:
    supplierId: { type: string, description: "kanonik tedarikçi kodu (Supplier.SupplierId)" }
    supplierMasterId: { type: string, format: uuid, nullable: true, description: "kiracı geneli tedarikçi kimliği; ilişki yoksa null" }
    relationshipExists: { type: boolean, description: "çağıran şirketle silinmemiş ilişki var mı" }
    status: { allOf: [{ $ref: '#/components/schemas/SupplierStatus' }], nullable: true, description: "çağıran şirketin ilişki durumu; ilişki yoksa null" }
    onboardingStatus: { allOf: [{ $ref: '#/components/schemas/OnboardingStatus' }], nullable: true, description: "çağıran şirketin onboarding/onay durumu; yoksa null" }
    purchasingEnabled: { type: boolean, nullable: true, description: "çağıran şirketin satın alma ilişkisi; ilişki yoksa null" }
    eligibleForTransactions: { type: boolean, description: "relationshipExists && !IsDeleted && status == Active" }
    contractVersion: { type: string }
```

- **`GET /{supplierId}/eligibility`** (`getSupplierEligibility`): yol parametresi `supplierId` (string) **kod ya da uuid** kabul eder — `Guid` olarak ayrışırsa `supplierMasterId` (`Supplier.Id`), değilse kod: önce `Supplier.SupplierId`, yoksa çağıran şirketin ilişkisindeki `LegacySupplierCodes` (§4 kod çözümü). Entegrasyon E-3 "uuid ile oku" ihtiyacı bu uçla karşılanır. Yanıt `200` → `SupplierEligibility` (bu uçta `relationshipExists` her zaman `true`); çağıran şirkette silinmemiş ilişki yoksa / kimlik çözülmezse / başka kiracı → `404 UnknownSupplier` (bugünkü izolasyon davranışı, varlık sızdırılmaz).
- **`GET /eligible`** (`listEligibleSuppliers`): çağıran şirketle **silinmemiş ve `status = Active`** ilişkisi olan tedarikçiler (her öğede `eligibleForTransactions = true`). Parametre yalnız `cursor` (string, opsiyonel; `listSuppliers` ile aynı imleç — son `supplierId`); sayfa boyu `listSuppliers` ile aynı (50, `GetSupplierListHandler.cs:12`); ek filtre yok (gerekirse sonraki eklemeli sürüm). Yanıt `200` → `{ items: SupplierEligibility[], nextCursor: string (nullable), contractVersion: string }` (`listSuppliers` zarfıyla aynı). Kiracı + LE kapsamlı; başka kiracı/LE verisi hiçbir zaman listelenmez (yabancı/pasif LE başlığı → 404, §8 C2). Yol sırası: `/eligible` somut yoldur, `/{supplierId}`'den önce eşleşir (OpenAPI somut-önce kuralı; ASP.NET literal segment önceliği); sunucunun verdiği kodlar `SUP-` önekli olduğundan `eligible` bir tedarikçi kodu olamaz.

### 3.2 `POST /validate` — 1.3.0 eklemeli satır alanları (Revizyon 2)
- İstek şekli **değişmez** (`{ supplierIds: string[] }`; öğe = kod — kanonik ya da çağıran şirketin eski kodu, §4). uuid bu uçta kimlik olarak çözülmez (uuid ile okuma `GET /{supplierId}/eligibility`).
- Satır (`results[]` öğesi) 1.2.0 alanlarını aynen korur: `supplierId` (istenen değerin aynısı — 1.2.0 anlamı), `known`, `status`. 1.3.0 aynı satıra **yalnız şu özellikleri ekler**, tip ve anlamları §3.1 ile birebir: `supplierMasterId`, `relationshipExists`, `onboardingStatus`, `purchasingEnabled`, `eligibleForTransactions`. Böylece satır `SupplierEligibility` alan kümesinin üst kümesidir (yalnız `supplierId` istenen değeri yansıtır ve ek olarak `known` vardır). 1.2.0 satırının `required` listesi yoktur ve eklenmez (yalnız `properties` eklemesi); 1.3.0 sunucusu yine de her satırda bu beş alanı yazar (değer `null` olabilir).
- Değişmez kural: `known == relationshipExists`. İlişki yoksa (kod kiracıda başka şirkette olsa bile): `known: false`, `relationshipExists: false`, `status`, `supplierMasterId`, `onboardingStatus`, `purchasingEnabled` = `null`, `eligibleForTransactions: false` — **404 değil**, çünkü `validate` toplu bir denetimdir (fail-closed tüketici satır satır karar verir).

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
| Approval | {WorkflowInstanceId, DecidedBy, DecidedAt} | onay şirket bazında (MOD-0023). Revizyon 2: `WorkflowInstanceId` = MOD-0023'ün döndürdüğü `workflowInstanceId` (Guid metni) — sahte `WF-` kimliği üretilmez; `DecidedBy` = onaylayan/ret eden history satırının aktörü, `DecidedAt` = örneğin `completedAt`'i (DL-4) | — |
| OnboardingCase | nesne? | KYC/docs/approval başvurusu — ilişkiye bağlı | — |
| ScreeningOverride | {Outcome, Reason, By, At}? | şirketin tarama sonucunu geçersiz kılması (şirket istisnası, K-3); Reason required | — |
| PaymentTermsCode | string? | ödeme koşulu kodu (K-8; ana tablo sonra); **banka bilgisi YOK** — Finance/Treasury'de kalır | — |
| PurchasingEnabled, PurchasingSince | bool, DateTime? | "bu şirket bu tedarikçiden alabilir mi" — satın alma ilişkisi | — |
| SourceSystem/ExternalRef | string | nullable (DEC-INV-19 dış besleme); şirketin kendi ERP eşlemesi | — |
| LegacySupplierCodes[] | string[] | göçte bu şirketin eski kodları — takma ad (K-5); geçmiş belgeler (PO/fatura/RFx/sözleşme) yeniden yazılmaz | unique (TenantId, LegalEntityId, kod) |
| Id, Version, IsDeleted, DeletedAt, CreatedAt, UpdatedAt | (EntityBase/audit) | soft-delete zorunlu | — |

> `form_field_count: 11` (Name, Status, Country, TaxId, primary Contact email/phone/type, KYC legalName/registrationNo, Document type, submitForApproval) > 8 → `golden_reference: compact`.
> Kod çözümü (tüketiciler, §7): önce `Supplier.SupplierId`, yoksa çağıran şirketin ilişkisindeki `LegacySupplierCodes` — eski belgelerdeki kodlar çözülmeye devam eder. Revizyon 2: sözleşmedeki `supplierMasterId` = `Supplier.Id` (yeni alan değil); uygunluk alanları ilişkiden türetilir, kalıcı yeni alan yoktur. `IdempotencyKey`/`OnboardingIdempotencyKey` teknik alanları taşındıkları komutun varlığında kalır.

## 5. Repo Scope
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Features/Supplier/**`
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Domain/**` (Supplier, OnboardingCase entities)
- `docs/analysis/contracts/supplier.openapi.yaml` — **DL-1 yalnız** (1.3.0, yalnız eklemeli; x-owner MOD-0140); DL-7'de servis token'ı şeması için ayrı eklemeli sürüm (1.4.0)
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Persistence/**` (repositories, Mongo index config, `supplier_masters` / `supplier_company_relationships` koleksiyonları ve göç işi — DL-2)
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Infrastructure/Authorization/**` — DL-3: `HasAnyPermissionAttribute` (Inventory deseni, `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Authorization/HasAnyPermissionAttribute.cs:16`; DI kaydı `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/DependencyInjection.cs:29`)
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Infrastructure/Workflow/**` (MOD-0023 Gateway istemcisi + çağıranın başlıklarını ileten handler), `…/Infrastructure/LegalEntities/**` (MDM LE doğrulayıcı), `…/Infrastructure/Middleware/**` (LE doğrulama ara katmanı) ve `…/Infrastructure/DependencyInjection.cs` — DL-4
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Api/Program.cs` — DL-4 (ara katman sırası), DL-7 (servis token'ı kimlik doğrulama şeması); `…/Infrastructure/Security/**` — DL-7
- `services/Diten.ProcurementService/tests/**` — her dilimin testleri
- DL-6 yalnız: Procurement içindeki beş tedarikçi doğrulama noktası, yalnız "Blocked / OnHold tedarikçiye yeni işlem yok" kontrolü için (K-7; XMC-001 sahip onayı 2026-10-23, MOD-0141/0143/0144/0145; başka davranış değişmez):
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
- Revizyon 2: Procurement içindeki diğer modüllerin sahte `WF-` üreticileri (`ApprovePurchaseOrderHandler.cs:61`, `ActivateContractHandler.cs:64`, `SubmitRequisitionHandler.cs:61`) bu pack'le **değişmez** (XMC-001; §20). MOD-0023 / MDM / Auth servis kodu bu pack'le yazılmaz.

## 7. Dependencies
- **Backbone:** JWT/RBAC (MOD-0018), Audit (MOD-0021), Workflow/Approvals (MOD-0023, onboarding onayı), Evidence/Docs (MOD-0029/0031, doküman referansı), Notification (MOD-0027).
- **MOD-0023 Workflow (Revizyon 2, DL-4):** Gateway üzerinden `/api/v1/workflow/*` (mevcut gateway rotası) — `POST /api/v1/workflow/instances` (başlat), `GET /api/v1/workflow/instances/by-objects` (okuma anı uzlaştırması), `GET /api/v1/workflow/instances/{id}/history` (karar veren). Yayımlanmış bir onboarding şablonu (önerilen `templateCode`: `PROC-SUPPLIER-ONBOARDING`; kesin ad ve atama kuralı DL-4'te MOD-0023 sahibiyle, §20 S-3) gerekir; şablon yoksa onaya gönderim 409 döner, sessiz onay YOK.
- **MDM legal entities (Revizyon 2, DL-4, C2):** `GET /api/legal-entities/{id}/lookup-validation` (`services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:48-49`, `[HasPermission("mdm.legal-entities.read")]`) Gateway üzerinden, çağıranın token'ıyla. **Sonuç:** çağıran kullanıcı `mdm.legal-entities.read` taşımazsa MDM 403 döner → `Unavailable` → Procurement 503 (Manufacturing'de bugün aynı; BL-652'nin SupplyChain'de ölçtüğü sorun). Procurement rollerine bu iznin verilmesi ya da Procurement için bir MDM servis kimliği DL-4'ün canlı öncesi koşuludur (§20 S-2).
- **Auth BL-652 Option 2 (Revizyon 2, DL-7):** yeni audience + servis istemci kaydı (Auth sahibi; desen `docs/roadmap/plans/mvp6-bl652-coupling-decision-01/OPTION2-S2S-SCOPE-v0.1.md` §2–§3). DL-7 bu kayıt olmadan başlamaz. Servis token'lı isteklerin LE doğrulaması için Procurement → MDM servis kimliği de gerekir (çağıranın kullanıcı token'ı yok; §8).
- **Consumed contracts:** yok (SUPPLIER upstream master; ürün/tedarikçi consume etmez — bu modül supplier'ın kendisidir).
- **Downstream consumers:** MOD-0141 PO (`supplierId`), MOD-0143 Invoice-Match, MOD-0145 Sourcing (`invitedSupplierIds`/`bid.supplierId`), MOD-0144 Contracting, MVP-6 MOD-0147 Supplier Performance & Risk / MOD-0148 Supplier Portal (SCE, `Diten.SupplyChainService`), Treasury girişi MOD-0119 Accounts Payable (finance-treasury) / MOD-0289 Bank Master & Bank Account Registry (master-data-management) — Revizyon 2: Treasury `GET /eligible` listesini ister. Uygunluk tanımı (hepsi için): "(kiracı, şirket, kod) için **aktif ilişki** var mı?" = `eligibleForTransactions` (§3.1) — MVP-6 DC-02 "olumlu Tenant+LE uygunluğu" bu sonuçtur; MVP-6 ve Treasury okuyucuları 1.3.0 sözleşmesiyle (DL-1 FROZEN sonrası, Prism mock) yazılır, canlı bağlantı DL-3 birleşmeden açılmaz. Procurement içi beş noktada DL-6 yalnız Blocked / OnHold reddini ekler; varlık denetimi bugünkü gibi kalır (şirketle ilişki yoksa bulunamaz — bugünkü şirket bazlı kayıtla aynı sonuç); MVP-6 (0147/0148), Treasury (0119/0289) ve Inventory tüketicilerine sahip entegrasyon aşamasında bildirir (bu pack onların kodunu/paketini değiştirmez, XMC-001).
- **Denetim altyapısı (AUD-001):** DL-3'teki yeni yazma komutları BL-501 `Diten.BuildingBlocks.CentralAudit` iletimiyle denetlenir. Revizyon 2 (2026-10-26): BL-501 **main'de** (`services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit` + `.Abstractions` + testleri `origin/main`'de mevcut) — K-4 ön koşulu karşılandı; DL-3 bu nedenle beklemez, yalnız onay kapısını bekler. Bugün servis defterinde (`tests/architecture/audit-ledger/Diten.ProcurementService.md`) kabul edilmiş iz yok; Create/Update/Delete/BulkDelete "bilinen borç".

## 8. Runtime Constraints
- Gateway port **5065**; frontend yalnız Gateway (5000) üzerinden çağırır.
- **Tenant izolasyonu her sorguda; normal okuma Tenant + LE** (server-resolved). `Supplier` sorgusu `TenantId` + `!IsDeleted`; ilişki sorgusu `TenantId` + `LegalEntityId` + `!IsDeleted`. Liste, detay, onboarding ve `/validate` **ilişki üzerinden** gider: bir tedarikçi bir şirkette yalnız o şirketle **silinmemiş ilişkisi** varsa görünür (Blocked/OnHold ilişki yönetilebilsin diye görünür kalır; işlem uygunluğu ayrıca `Status = Active` ister, §7); ilişki yoksa 404 (bugünkü davranış). Cross-tenant → 404.
- Şirket A kullanıcısı B'nin ilişki verisini (durum, onay, ödeme koşulu, tarama istisnası, harici eşleme, eski kodlar) **hiçbir uçta** görmez. Ortak alanlar (ad, ülke, vergi no, iletişim, belgeler, tarama sonucu) ilişkili her şirkette aynıdır.
- Kiracı geneli okuma yalnız açıkça verilmiş `procurement.suppliers.read-tenant-wide` ile (SEC-002, `data-scope-enforcement.md` madde 5): ayrı üretici, boolean parametre yok; dönen en az alan `supplierId`, `name`, `country` — ilişki verisi dönmez. İzin yoksa 403.
- Ortak alanları yalnız `procurement.suppliers.master.update` değiştirir (K-1); ilişki alanları `update` / `onboard` ile ve yalnız çağıranın kendi şirketinde.
- **Blocked / OnHold ilişki yeni işlemi reddeder** (K-7): Procurement içi tüketiciler — PO, fatura, RFx/teklif, sözleşme (DL-6).
- **Uygunluk okumaları (Revizyon 2, DL-3):** `validate`, `GET /{supplierId}/eligibility` ve `GET /eligible` yalnız çağıranın Tenant + LE ilişkisini okur (ilişki sorgusu `TenantId` + `LegalEntityId` + `!IsDeleted`); `supplierMasterId` yalnız ilişki varsa döner. `eligibleForTransactions` sunucuda tek bir yerde hesaplanır (`relationshipExists && !IsDeleted && Status == Active`) ve üç uçta aynı fonksiyonla üretilir. Bu okumalar MOD-0023'e gitmez (uzlaştırma onboarding okumalarında yapılır; `onboardingStatus` bir sonraki onboarding okumasına kadar `InReview` görünebilir — `eligibleForTransactions` buna bağlı değildir).
- **Onboarding onayı — MOD-0023 (Revizyon 2, DL-4):** Inventory `GatewayWorkflowClient` deseni birebir: `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Workflow/GatewayWorkflowClient.cs:19-28` (çağıranın `Authorization` ve korelasyon kimliği `ForwardCallerHeadersHandler` ile — `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Upstream/ForwardCallerHeadersHandler.cs` — iletilir; kiracı `X-Tenant-Id` olarak istemcide yazılır, `:250-253`; bu yolda servis token'ı yok), kaydı `services/Diten.InventoryService/src/Diten.InventoryService.Api/Quarantine/QuarantineServiceCollectionExtensions.cs:75-85` (`Gateway:BaseUrl`, zaman aşımı). `SubmitOnboardingCase` (`submitForApproval = true`) bir MOD-0023 örneği başlatır (`POST /api/v1/workflow/instances`, `GatewayWorkflowClient.cs:51-98`): `objectType = procurement.supplier-relationship`, `objectId` = ilişki `Id`, `idempotencyKey = procurement:supplier-relationship:{id}:r{n}` (aynı gönderim aynı örneği alır). Dönen `workflowInstanceId` (Guid) `Approval.WorkflowInstanceId`'ye yazılır ve durum `InReview` olur. Başlatma hata eşlemesi Inventory'deki gibi (`GatewayWorkflowClient.cs:88-96`; `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Quarantine/Services/QuarantineWorkflow.cs:13-20`): şablon yok → 409 `WORKFLOW_TEMPLATE_MISSING`, yetkisiz → 403, diğer 4xx → 422 `SupplierUnprocessable` (mevcut yanıt; Inventory 400 döner), 5xx / ağ / zaman aşımı → 503 `DEPENDENCY_UNAVAILABLE` — bu durumlarda ilişki `InReview` olmaz, kimlik yazılmaz.
- **Kararın gelişi (Revizyon 2, DL-4):** Inventory'de olduğu gibi **garantili yol okuma anı uzlaştırmasıdır** (yoklama; geri çağırma yok): `QuarantineWorkflow.cs:180-187` (`QuarantineWorkflowReconciler`, `:188`; `ReconcileAsync` `:215`; `InstancesByObjectsAsync` çağrısı `:238`). Procurement'ta `GET /{supplierId}/onboarding`, `GET /{supplierId}` ve ekrandaki "Durumu yenile" `InReview` ilişkiler için okuyanın token'ıyla `GET /api/v1/workflow/instances/by-objects` sorar (`GatewayWorkflowClient.cs:100-125`); örnek terminalse `RecordOnboardingDecision` sonucu `Approved` / `Rejected` olarak yazar, `DecidedBy` = onay/ret history satırının aktörü (`HistoryAsync`, `:127-140`), `DecidedAt` = `completedAt`. Yalnız ilişkinin **kendi sakladığı** `WorkflowInstanceId`'ye ait sonuç uygulanır; uygulama tam bir kez (sürüm/eşzamanlılık kontrolü). MOD-0023 okunamazsa okuma başarısız olmaz (durum `InReview` kalır). Inventory'deki olay tüketicisi (`platform.workflow.instance.completed.v1`, `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Eventing/QuarantineWorkflowOutcomeConsumer.cs:14-26`, yalnız `Eventing:Transport = RabbitMQ`) bu pack'te yoktur → §20.
- **Legal entity doğrulaması (C2, Revizyon 2, DL-4, sahip onayı 2026-10-26):** başlık/JWT LE'si (`X-Legal-Entity-Id` / `legal_entity_id`, `services/Diten.ProcurementService/src/Diten.ProcurementService.Infrastructure/Middleware/TenantResolutionMiddleware.cs:17-19`) kiracıya ait ve ACTIVE mi, her istekte MDM'e sorulur — önbellek yok. Desen Manufacturing: `services/Diten.ManufacturingService/src/Diten.ManufacturingService.Infrastructure/LegalEntities/MdmLegalEntityReferenceValidator.cs:11-22` (3 sn bütçe, tek geçici yeniden deneme, Authorization / X-Tenant-Id / X-Correlation-Id iletimi; `:49-107` sonuç eşlemesi: 404 / ACTIVE değil / referanslanamaz / farklı id → `NotReferenceable`; zaman aşımı / 5xx / 401-403 / bozuk gövde → `Unavailable`) ve ara katman `services/Diten.ManufacturingService/src/Diten.ManufacturingService.Infrastructure/Middleware/LegalEntityValidationMiddleware.cs:7-46`; sıra `UseAuthentication → UseTenantResolution → UseAuthorization → UseLegalEntityValidation` (`services/Diten.ManufacturingService/src/Diten.ManufacturingService.Api/Program.cs:74-78`; Procurement'ta `Program.cs:149-151`'in ardına). **Fark (sahip kararı):** `NotReferenceable` (yabancı kiracı, bilinmeyen, pasif LE) → Procurement'ta **404** `UNKNOWN_LEGAL_ENTITY` (varlık sızdırmaz; Manufacturing 422 döner); `Unavailable` → 503 `DEPENDENCY_UNAVAILABLE`, hiçbir şey okunmaz/yazılmaz (fail-closed). **Kapsam:** ara katman düzeyinde olduğu için **Procurement servisinin bütün kiracı uçlarını** etkiler (MOD-0141..0145 dahil), yalnız bu pack'in uçlarını değil; sahip C2'yi bu kapsamla onayladı (2026-10-26). Başka modülün dosyası değişmez; davranış değişikliği MOD-0141..0145 sahiplerine bildirilir. Sağlık ve OPTIONS kapsam dışı; geliştirme bypass'ı (`TenantResolution:DevBypassLegalEntityId`) ile çözülen LE de doğrulanır.
- **Servis token'ı (Revizyon 2, DL-7, BL-652 Option 2'ye bağlı):** Procurement güvenilir servis token'ını **yalnız** `POST /validate` ve `GET /{supplierId}/eligibility` için kabul eder (`GET /eligible` ve diğer bütün uçlar kapalı; §20 S-4). Biçim Platform'unki: `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedServiceTokenValidationExtensions.cs:14-38` (audience başına bir şema; zorunlu claim kümesi tam olarak `iss, aud, sub, actor_type, service_name, tenant_id, jti, iat, nbf, exp`) ve `:239-277` (`HasExactServiceClaims`: tek audience, `actor_type = service`, `service_name` ordinal tam eşleşme, `tenant_id` Guid, `exp = nbf + 300`). Procurement audience'ı (öneri `TRUSTED_SUPPLIER_ELIGIBILITY_CONSUMER`) ve kabul edilen `service_name` listesi (açık yapılandırma, joker yok) Auth sahibince kaydedilir. Kiracı ve LE açıkça gelir: `X-Tenant-Id` token'ın `tenant_id`'sine eşit olmalı (değilse 401), `X-Legal-Entity-Id` zorunlu ve C2 ile **yine doğrulanır** — çağıranın kullanıcı token'ı olmadığı için bu doğrulama Procurement → MDM servis kimliğiyle yapılır; o kimlik yoksa servis token'lı istek 503 alır (LE kontrolü atlanmaz). Servis token'ı izin claim'i taşımaz; bu iki uç için şema kendisi yetkidir, başka uca bağlanmaz.
- **Göç (DL-2):** yeni koleksiyonlar `supplier_masters`, `supplier_company_relationships`; eski `suppliers` koleksiyonu göç boyunca salt okunur kaynak. Göç işi idempotent, kipleri `dry-run` / `apply` / `verify`; geri alma = okuma anahtarı `Suppliers:Model` (`PerLegalEntity` | `TenantShared`) eskiye döner, yeni koleksiyonlar silinmez, birleşen kayıtlar `MergedIntoId` + soft delete ile geri açılabilir. Birleştirme kuralı (grup anahtarı, hayatta kalan, çakışan ortak alanlar, kod çakışması) öneri §2.1'dedir. Gerçek veri yok — yalnız geliştirme/test verisi.
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
| Eligibility `{supplierId}` yol parametresi (Revizyon 2) | Evet | NotEmpty; `Guid` olarak ayrışırsa `supplierMasterId`, değilse kod (kanonik, sonra çağıran şirketin eski kodu) | — | ilişki (Tenant + LE + Supplier, silinmemiş) var mı → yoksa 404 |
| `validate.supplierIds[]` (Revizyon 2) | Evet (1.2.0 gibi) | istek şekli değişmez; öğe kod olarak çözülür (uuid çözülmez); boş öğe atlanır, tekrar eden öğe tek satır döner (bugünkü davranış, `ValidateSuppliersHandler.cs:22-25`) | — | — |
| `listEligibleSuppliers.cursor` (Revizyon 2) | Hayır | `listSuppliers` imleciyle aynı kural (son `supplierId`) | — | — |
| LE başlığı (C2, Revizyon 2) | Evet (kiracı uçları) | kiracıya ait + ACTIVE + referanslanabilir (MDM `lookup-validation`) | — | her istekte MDM, önbellek yok |
| Onboarding onaya gönderim (Revizyon 2) | — | yayımlanmış MOD-0023 onboarding şablonu; `InReview` ilişkide tekrar gönderim aynı idempotency anahtarıyla aynı örneği alır | — | şablon var mı (MOD-0023 yanıtı) |
| Servis token'ı (DL-7) | — | Platform biçimi claim kümesi tam; audience ve `service_name` listede; `X-Tenant-Id` = `tenant_id`; `X-Legal-Entity-Id` zorunlu | — | LE yine C2 ile doğrulanır |

- **Başka şirkette mevcut vergi no ile oluşturma (K-6):** kiracıda aynı (ülke, normalize vergi no) ile Supplier varsa create **409 `DUPLICATE_SUPPLIER`** döner; gövdede mevcut `supplierId`; ekran "bu tedarikçiyi bu şirkete bağla" yolunu sunar (bağlama ayrı, açık bir şirket kararıdır; sessiz otomatik bağlama YOK). **Uyumluluk anahtarı YOK** — eski "her şirkette ayrı kayıt" davranışı korunmaz (canlıda veri yok).
- Unique index yarışı (`DuplicateKey`) 409'a çevrilir, 500 dönmez.

## 13. Failure Path to Verify
- **Duplicate active TaxId** (kiracı + ülke + normalize vergi no; başka şirkette de) → 409 + mevcut `supplierId` + "bu şirkete bağla" + kayıt yok + reload temiz (K-6).
- **Aynı şirkete ikinci bağlama** → 409, ikinci ilişki yok.
- **Ortak alan güncellemesi `master.update` izni olmadan** → 403; **kiracı geneli arama `read-tenant-wide` olmadan** → 403.
- **Başka şirketin ilişkisini okuma/değiştirme** ya da ilişkisi olmayan şirkette erişim → 404.
- **Blocked / OnHold ilişkiyle yeni PO / fatura / RFx-teklif / sözleşme** → red, kayıt yok (K-7, DL-6).
- **Uygunluk — ilişki yok (Revizyon 2, karar):** `POST /validate` → **200**, satırda `known: false`, `relationshipExists: false`, diğer uygunluk alanları `null`, `eligibleForTransactions: false` (toplu denetim; kod kiracıda başka şirkette olsa da `supplierMasterId` dönmez). `GET /{supplierId}/eligibility` → **404 `UNKNOWN_SUPPLIER`** (bugünkü izolasyon davranışı; kod ya da uuid fark etmez). `GET /eligible` → o tedarikçi listede yok.
- **Uygunluk — ilişki var ama Blocked / OnHold / Inactive** → `relationshipExists: true`, `status` gerçek değer, `eligibleForTransactions: false`; `/eligible` listesinde yok.
- **Uygunluk — silinmiş ilişki** → `GET /{supplierId}/eligibility` 404; `validate` satırı `relationshipExists: false`.
- **Dar izinle tam kayıt okuma:** yalnız `procurement.suppliers.eligibility.read` taşıyan çağıran `GET /api/suppliers`, `GET /{supplierId}`, `GET /{supplierId}/onboarding` → 403 (iletişim, vergi no, onay ayrıntısı açılmaz).
- **Yabancı / pasif / bilinmeyen LE başlığı (C2)** → her Procurement kiracı ucunda 404 `UNKNOWN_LEGAL_ENTITY`, hiçbir okuma/yazma yok; **MDM sorulamadı** (zaman aşımı, 5xx, 401/403) → 503 `DEPENDENCY_UNAVAILABLE`, hiçbir okuma/yazma yok (fail-closed).
- **Onboarding onaya gönderim — MOD-0023 şablonu yok / MOD-0023 erişilemez** → 409 / 503, ilişki `InReview` olmaz, `WorkflowInstanceId` yazılmaz; tekrar gönderim aynı idempotency anahtarıyla ikinci örnek açmaz.
- **Workflow sonucu başka bir örneğe ait** (ilişkinin sakladığı `WorkflowInstanceId` değil) → uygulanmaz; MOD-0023 okunamazsa onboarding okuması 200 döner, durum `InReview` kalır.
- **Servis token'ı (DL-7):** yanlış audience / `service_name` / süresi dolmuş / eksik-fazla claim → 401; `X-Tenant-Id` ≠ `tenant_id` → 401; izin verilen iki uç dışındaki uç → reddedilir (401/403); yabancı LE → 404; Procurement → MDM servis kimliği yoksa → 503.
- **Missing Name** → 400 + validator mesajı + save engellenir.
- **Concurrency conflict** (stale update) → 409 + "kayıt değişti, yeniden yükle" + sessiz overwrite YOK.
- **Unauthorized actor** (izin yok) → 403 + action disabled/permission-denied (UAS-001: iskelet çizilmez, yönlendirme yok).
- **Cross-tenant/LE erişim** → 404 (sızıntı yok).

## 14. Authorization Convention
- Policy: `[Authorize]` (tenant actor).
- Permission format (PKS-001, lowercase-dotted, ≥3 segment): `procurement.suppliers.{action}`.
- Actions: read, create, update, delete, bulk-delete, onboard, `master.update` (ortak alanlar — K-1), `read-tenant-wide` (kiracı geneli vergi no araması — SEC-002; yalnız açıkça verilir, hiçbir rolde varsayılan değil), `eligibility.read` (Revizyon 2; uygunluk izdüşümü).
- **PKS-001 uyumu (Revizyon 2):**

| anahtar | segment (≥3) | gramer `^[a-z][a-z0-9-]*$` her segmentte | namespace | kaynak yolu | eylem / katman |
|---|---|---|---|---|---|
| `procurement.suppliers.eligibility.read` | 4 | evet (küçük harf, `_` yok) | `procurement` | `suppliers.eligibility` | `read` — Tier 1 |
| `procurement.suppliers.master.update` | 4 | evet | `procurement` | `suppliers.master` | `update` — Tier 1 |
| `procurement.suppliers.read-tenant-wide` | 3 | evet (`read-tenant-wide`: tireli tek segment) | `procurement` | `suppliers` | `read-tenant-wide` — **Tier 3, bu pack'te kayıtlı** |
| `procurement.suppliers.onboard` (mevcut) | 3 | evet | `procurement` | `suppliers` | `onboard` — **Tier 3, bu pack'te kayıtlı** (yayımlı, `SuppliersController.cs`) |

  - Uzunluk her anahtarda ≤ 200; hepsi küçük harfle yazılır ve saklanır.
  - **Tier-3 kaydı (PKS-001 §5):** bu modül şu alana özgü eylemleri kaydeder — `onboard` (tedarikçi onboarding başvurusu ve onaya gönderim) ve `read-tenant-wide` (kiracı geneli vergi no araması; dönen alan yalnız `supplierId`, `name`, `country`; SEC-002, hiçbir rolde varsayılan değil). Başka Tier-3 eylem yoktur; `eligibility.read` ve `master.update` Tier 1 eylem + iç içe kaynak yoludur (PKS-001 §1 "deeper resource paths").
  - **Namespace (dürüst not):** PKS-001 §4 tablosunda `procurement.*` **henüz listelenmemiştir**. Bu Revizyon 2'den önceki bir durumdur: `procurement.*` anahtarları Procurement servisinde zaten yayımlıdır (`procurement.suppliers.*`, `procurement.purchase-orders.*`, `procurement.contracts.*`, `procurement.invoice-match.*` …). Sahibi `Diten.ProcurementService` (DCP-010, MOD-0140..0145); §4 tablosuna kaydı AG-STEP-004B işidir (`.antigravity/**` korunan yol — bu pack yazmaz; §20).
- **Uç → izin (Revizyon 2):**
  - `POST /validate` → `procurement.suppliers.read` **veya** `procurement.suppliers.eligibility.read` (`[HasAnyPermission]`).
  - `GET /{supplierId}/eligibility`, `GET /eligible` → `procurement.suppliers.eligibility.read` **veya** `procurement.suppliers.read` (`[HasAnyPermission]`).
  - Diğer bütün uçlar değişmez; `eligibility.read` onlara erişim vermez (dar anahtar yalnız bu üç uç).
  - "Veya" kuralı Inventory'nin `HasAnyPermissionAttribute`'u ile kurulur (`services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Authorization/HasAnyPermissionAttribute.cs:6-16`: her anahtar gerçek `HasPermissionAttribute` ile sırayla değerlendirilir, ilk geçen yetkilendirir); Procurement'ın bugünkü `HasPermissionAttribute`'u tek anahtar alır ve birden çok öznitelik VE'dir.
- **`procurement.suppliers.read` sahipleri — karar (Revizyon 2): yeni uygunluk uçlarını da alırlar.** Gerekçe (PKS-001 §2 değişmez anahtar kuralı): `procurement.suppliers.read`'in anlamı "çağıranın şirketindeki tedarikçi verisini oku"dur ve **genişlemez**. Uygunluk izdüşümü bu verinin alt kümesidir: ilişki varlığı = `validate.known` (bugün), `status` = `Supplier.status` (bugün), `onboardingStatus` = `GET /{supplierId}/onboarding` (bugün), `purchasingEnabled` = 1.3.0 ilişki okuması (aynı izin), `supplierMasterId` = zaten okunabilen kaydın kimliği, `/eligible` = `GET /?status=Active` (bugün). Yani read sahibi yeni bir **yetenek** kazanmaz, aynı veriyi başka bir uçtan okur; anahtarın verdiği erişim değişmez. Yeni yetenek yalnız yeni anahtardır: `eligibility.read`, tam kaydı (iletişim, vergi no, onay ayrıntısı) açmadan yalnız izdüşümü verir — MVP-6 ve Treasury rollerine bu verilir. Ters yön (eligibility.read'in read'i ima etmesi) yoktur.
- Servis token'ı (DL-7): izin claim'i taşımaz; yalnız `POST /validate` ve `GET /{supplierId}/eligibility` üzerinde, ayrı kimlik doğrulama şemasıyla kabul edilir; bu iki uç "kullanıcı izni (yukarıdaki) **veya** geçerli servis token'ı" kuralıyla korunur.
- `update` / `onboard` / `delete` yalnız çağıranın kendi şirketindeki ilişkiye etki eder; ortak alanı değiştiremez.
- Actor: tenant_user (procurement rolü); server-side `[HasPermission]` zorunlu.

## 15. Gateway / API Routing Decision
- Karar: Gateway değişikliği **gerekli** (`/api/suppliers` → 5065). `ocelot.json` protected; bu pack yazmaz — explicit Upstream/Downstream + OPTIONS içeren **integration-agent task**'ı olarak ayrı yürütülür.
- 2026-10-21 değişikliği: yeni ilişki ve kiracı geneli arama uçları `/api/suppliers/**` altında kalır; mevcut `/api/suppliers` + `/api/suppliers/{everything}` rotaları (`ocelot.json`) kapsar → **yeni gateway değişikliği gerekmez**. Uç bu önekin dışına çıkarsa integration-agent task'ı açılır.
- Revizyon 2 (2026-10-26): `GET /api/suppliers/eligible` ve `GET /api/suppliers/{supplierId}/eligibility` `/api/suppliers/{everything}` rotasıyla kapsanır (`ocelot.json`; GET dahil) → **gateway değişikliği gerekmez**. Procurement'ın giden çağrıları mevcut rotalardan gider: MOD-0023 `/api/v1/workflow/{everything}`, MDM `/api/legal-entities/{everything}` → değişiklik yok. DL-7: tedarikçi rotalarında gateway kimlik doğrulaması (`AuthenticationOptions`) tanımlı değil, token olduğu gibi iletilir; servis token'ı gateway'den geçecekse rota değişmez. Bu durum değişirse (gateway'de audience denetimi eklenirse) integration-agent task'ı açılır.

## 16. Acceptance Criteria
- [ ] `POST /api/suppliers` idempotent create → 201; `GET` list/by-id tenant+LE filtreli.
- [ ] `PATCH` optimistic concurrency → stale update 409.
- [ ] `POST /api/suppliers/validate` bilinmeyen id'leri `known:false` işaretler (consumer fail-closed).
- [ ] Onboarding: KYC/sanctions outcome + document evidenceRef + approval (MOD-0023) kaydedilir; binary saklanmaz.
- [ ] SUPPLIER contract consumer slice (listSuppliers/getSupplier/validateSuppliers) 1.2.0 alanları **değişmeden** yanıt verir (MVP-6 uyumu); Revizyon 2'de `validateSuppliers` satırına yalnız eklemeli alanlar gelir.
- [ ] Tenant+LE izolasyon: cross-LE 404; unauthorized 403 (UAS-001).
- [ ] Kiracıda tek Supplier: aynı (ülke, normalize vergi no) iki şirkette tek `supplier_masters` kaydı + iki ilişki; şirket B'de create → 409 + mevcut `supplierId`; "bağla" sonrası B'de görünür.
- [ ] Şirket A kullanıcısı B'nin ilişki alanlarını hiçbir uçta görmez; ilişkisi olmayan şirkette detay/validate 404/`known:false`.
- [ ] `master.update` olmadan ortak alan değişikliği 403; `read-tenant-wide` olmadan kiracı geneli arama 403; izinle dönen alanlar yalnız `supplierId`, `name`, `country`.
- [ ] Göç işi `dry-run` / `apply` / `verify` / geri alma: ikinci `apply` hiçbir şey değiştirmez; `verify` çözülemeyen (kiracı, şirket, kod) = 0; eski kodlar `LegacySupplierCodes` ile çözülür, geçmiş belgeler yeniden yazılmaz.
- [ ] Blocked / OnHold ilişkiyle PO, fatura, RFx/teklif, sözleşme oluşturma reddedilir (K-7).
- [ ] `supplier.openapi.yaml` 1.3.0 yalnız eklemeli: 1.2.0 (A) consumer slice ve (B) producer işlemleri aynı şekil.
- [ ] Tüm `Views/Procurement/Suppliers/*.cshtml` → `Layout = "_LayoutTenantShell"` açık; `verify_datatable_page.py --area Procurement --module Suppliers --reference compact` PASS.
- [ ] RESX 7-dil parite PASS.
- [ ] **(Revizyon 2, DL-1)** `supplier.openapi.yaml` 1.3.0 yazılı ve `x-status: FROZEN` (tarihli); 1.2.0'a karşı fark yalnız ekleme (yol/işlem/özellik/yanıt kodu ekleme; silme, tip değişikliği, enum daraltma, mevcut nesneye yeni `required` yok); `SupplierEligibility` §3.1 ile alan, tip ve `required` olarak birebir; Prism mock ile üç uç yanıt verir.
- [ ] **(DL-3)** `POST /validate` her satırda §3.2'deki beş eklemeli alanı yazar; ilişki varsa değerler çağıran şirketin ilişkisinden gelir; ilişki yoksa `known: false`, `relationshipExists: false`, `supplierMasterId` / `status` / `onboardingStatus` / `purchasingEnabled` = `null`, `eligibleForTransactions: false`.
- [ ] **(DL-3)** `GET /{supplierId}/eligibility` kanonik kod, çağıran şirketin eski kodu ve `supplierMasterId` uuid ile aynı `SupplierEligibility`'yi döner (`supplierId` her zaman kanonik kod); ilişki yoksa 404.
- [ ] **(DL-3)** `eligibleForTransactions` yalnız ilişki var + silinmemiş + `status = Active` iken `true`; Blocked / OnHold / Inactive için `false`.
- [ ] **(DL-3)** `GET /eligible` yalnız çağıran şirketle silinmemiş ve Active ilişkisi olan tedarikçileri, sayfa 50 ve `nextCursor` ile döner; başka şirketin Active ilişkisi listede görünmez; başka kiracının tedarikçisi hiçbir sayfada yok.
- [ ] **(DL-3)** İzin: yalnız `procurement.suppliers.eligibility.read` ile üç uygunluk ucu 200, tam kayıt uçları 403; yalnız `procurement.suppliers.read` ile üç uç 200; ikisi de yoksa 403 (UAS-001).
- [ ] **(DL-4)** `SubmitOnboardingCase` (`submitForApproval = true`) MOD-0023'te tek örnek başlatır; ilişkide Guid `WorkflowInstanceId` saklanır, `WF-` önekli kimlik hiçbir yerde üretilmez; şablon yok → 409, MOD-0023 yok → 503, ilişki değişmez.
- [ ] **(DL-4)** MOD-0023 örneği onay/ret ile bitince bir sonraki onboarding okuması (ya da "Durumu yenile") ilişkiyi `Approved` / `Rejected` yapar; `DecidedBy` = karar veren history aktörü, `DecidedAt` = `completedAt`; iki paralel okuma tek kez uygular.
- [ ] **(DL-4)** C2: yabancı kiracının LE'si, bilinmeyen LE ve pasif LE ile her Procurement kiracı ucu 404 `UNKNOWN_LEGAL_ENTITY` döner ve hiçbir veri okunmaz/yazılmaz; MDM erişilemezse 503; geçerli LE ile davranış değişmez.
- [ ] **(DL-7)** Geçerli servis token'ı (doğru audience, listedeki `service_name`, `tenant_id` = `X-Tenant-Id`) + geçerli `X-Legal-Entity-Id` ile `POST /validate` ve `GET /{supplierId}/eligibility` 200; aynı token başka her uçta reddedilir; yabancı LE 404.

## 17. Test Expectations
- Unit: validator (duplicate taxId, missing name), handler (create/update/onboarding).
- Integration: tenant isolation (cross-LE 404), soft-delete, idempotency replay (duplicate side-effect yok), concurrency 409.
- Integration (2026-10-21 değişikliği; gerçek replica set, 27017–27021 ve ≥49152 dışında geçici Mongo): birleştirme kuralı, vergi no normalizasyonu, farklı ülke aynı no, kod çakışması, idempotent ikinci koşu, verify sayımı, geri alma (DL-2); şirket A/B ilişki izolasyonu, kiracı geneli 409 + mevcut kod, eski kod çözümü, izinsiz kiracı geneli arama 403 (DL-3); her tüketicide ilişkisiz şirket 404 (bugünkü davranış, değişmedi) ve Blocked / OnHold reddi (DL-6).
- Contract (Revizyon 2, DL-1): 1.2.0 ↔ 1.3.0 yapısal fark testi (ör. `oasdiff breaking` ya da eşdeğer betik) **sıfır kırıcı değişiklik**; 1.2.0'daki her yol/işlem/özellik/enum değeri 1.3.0'da aynı; `SupplierEligibility` alan + tip + `required` kümesi §3.1 tablosuyla karşılaştırılır; Prism mock'ta `validateSuppliers`, `getSupplierEligibility`, `listEligibleSuppliers` örnek yanıtları şemaya uyar.
- Integration (Revizyon 2, DL-3; gerçek Mongo): A/B şirketi aynı tedarikçi — A'da Active, B'de Blocked: A'dan `eligibility` → `eligibleForTransactions: true`, B'den → `false` + `status: Blocked`; ilişkisi olmayan C'den → 404, `validate` → `known` / `relationshipExists: false`, `supplierMasterId: null`; `/eligible` A'da var, B'de yok, C'de yok; uuid / kanonik kod / eski kod aynı sonucu verir; silinmiş ilişki → 404 ve `validate` `relationshipExists: false`; başka kiracı → 404 / boş; izin matrisi (yalnız `eligibility.read`, yalnız `read`, ikisi de yok).
- Integration (Revizyon 2, DL-4): MOD-0023 HTTP ucunda bellek içi sahte ağ geçidi (Inventory `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/TestInfrastructure/FakeWorkflowGateway.cs` deseni — gerçek istemci + başlık iletici çalışır): başlatma isteği gövdesi (`objectType`, `objectId`, `idempotencyKey`), şablon yok → 409 ve ilişki değişmedi, 5xx → 503, onay/ret sonucu okuma anında yazılır, başka örneğin sonucu uygulanmaz, paralel okuma tek uygulama. C2: sahte MDM (Manufacturing `services/Diten.ManufacturingService/tests/Diten.ManufacturingService.Tests/Tenancy/MdmLegalEntityReferenceValidatorTests.cs` deseni) — 404 / pasif / farklı id / 403 / zaman aşımı → 404 / 404 / 404 / 503 / 503; tedarikçi uçlarından en az biri ve başka bir Procurement modül ucu (ör. satın alma siparişi listesi) aynı yanıtı verir.
- Integration (Revizyon 2, DL-7): Platform biçiminde imzalı test token'ları — doğru token 200; yanlış audience, yanlış `service_name`, eksik/fazla claim, `exp ≠ nbf + 300`, `X-Tenant-Id` ≠ `tenant_id` → 401; izinli iki uç dışındaki uç reddedilir; yabancı LE 404.
- Sabotaj (üretim kodunda, guard kanıtı): birleştirme anahtarından Country çıkarılınca farklı-ülke testi kırmızı; idempotency kontrolü kaldırılınca ikinci koşu testi kırmızı; ilişki sorgusundan `LegalEntityId` kaldırılınca izolasyon testi kırmızı; legacy kod çözümü kaldırılınca eski PO kodu testi kırmızı; bir dilden RESX anahtarı silinince parite testi kırmızı; Blocked / OnHold kontrolü atlanınca tüketici testleri kırmızı.
- Sabotaj (Revizyon 2, üretim kodunda): uygunluk sorgusundan `LegalEntityId` filtresi kaldırılınca A/B uygunluk izolasyon testi kırmızı; `eligibleForTransactions` `status`'u yok sayınca (yalnız ilişki varlığı) Blocked / OnHold testi kırmızı; `!IsDeleted` filtresi kaldırılınca silinmiş ilişki testi kırmızı; `/eligible` sorgusundan `Status = Active` koşulu kaldırılınca liste testi kırmızı; ilişki yokken `supplierMasterId` doldurulunca sızıntı testi kırmızı; MDM LE kontrolü atlanınca (ara katman kaydı kaldırılınca ya da `NotReferenceable` geçirilince) C2 testleri kırmızı; `Unavailable` geçirilince 503 testi kırmızı; `[HasAnyPermission]` yerine yalnız `eligibility.read` konunca read-sahibi testi, yalnız `read` konunca dar-izin testi kırmızı; `WF-` üretici geri konunca MOD-0023 başlatma testi kırmızı; uzlaştırmada örnek kimliği karşılaştırması kaldırılınca başka-örnek testi kırmızı; servis token'ı claim kontrolü (`service_name` / audience / `tenant_id` eşitliği) gevşetilince DL-7 testleri kırmızı; 1.3.0'da 1.2.0'dan bir alan silinince sözleşme fark testi kırmızı.
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
- [x] DL-3 öncesi: BL-501 `Diten.BuildingBlocks.CentralAudit` main'de (K-4) — Revizyon 2'de doğrulandı (2026-10-26)
- [ ] DL-3 öncesi: §21 "Audited Events" yazılı (ilk yazma komutu dilimiyle)
- [ ] Revizyon 2: bu sürümün CT-APR-02 bağımsız incelemesi (`reviews.tsv`) + CT-APR-01 onay satırı (`approvals.tsv`, yüksek risk, paketi yazmamış `high: evet` onaylayıcı); bu olmadan DL-1 sözleşme dosyası ve değişen bölümlerin kodu başlamaz
- [ ] DL-1: `SupplierEligibility` §3.1'den birebir; 1.2.0 ↔ 1.3.0 fark testi sıfır kırıcı; tüketicilere (MVP-6 MOD-0147/0148, Treasury MOD-0119/0289) FROZEN bildirimi; §20 S-1 FROZEN'dan önce kapalı
- [ ] DL-4 öncesi: MOD-0023 onboarding şablonu yayımlı (şablon kodu + atama kuralı MOD-0023 sahibiyle, §20 S-3); Procurement rollerine `mdm.legal-entities.read` ya da Procurement → MDM servis kimliği kararı (§20 S-2); MOD-0141..0145 sahiplerine C2 bildirimi
- [ ] DL-7 öncesi: Auth sahibinin BL-652 Option 2 kaydı (yeni audience + Procurement'ı çağıracak servis istemcileri + kiracı izinleri, §20 S-5) ve Procurement → MDM servis kimliği; 1.4.0 eklemeli sözleşme

## 19. Implementation Notes
İlk scaffold edilecek modül (DCP-010 §8 sıra: 0140→0145→0141→0142→0143→0144). Servis scaffold bu pack + `@orchestrator /add-module` ile doğar (JWT + Mongo V3 GUID + TenantId izolasyonu + port 5065 + gateway route). SUPPLIER contract producer surface `x-producer-status: REVIEW` — freeze owner+consumer review sonrası; MVP-6 slice superset korunur.

**2026-10-21 — kiracıda tek tedarikçi, şirket bazında ilişki (WP-MVP2-SUPPLIER-TENANT-01).**
- Kaynak: `docs/roadmap/plans/mvp2-supplier-tenant-01/PACK-CHANGE-PROPOSAL.md` (hedef model §1, göç §2, sözleşme etkisi §3, dilimler §4, sahip kararları §5a); ölçüm `docs/records/audits/2026-10/mvp2-supplier-tenant-01/MEASUREMENT.md`. Emsal: MOD-0290 kalem ana kaydı (kiracı geneli ana veri, şirket bazında hareket).
- Sahip kararları (2026-10-21): K-1 ortak alanlar `procurement.suppliers.master.update` · K-2 tekillik kiracı + ülke + normalize vergi no · K-3 tarama sonucu tedarikçide, şirket istisnası ilişkide (`ScreeningOverride`) · K-4 istisna gerekmez, yeni yazma komutları BL-501 `Diten.BuildingBlocks.CentralAudit` ile denetlenir (MVP-1 main'e girince; DL-3 — eski DL-2 — o zamana kadar başlamaz; Revizyon 2: BL-501 main'de) · K-5 eski kodlar ilişkide takma ad, geçmiş belgeler yeniden yazılmaz · K-6 başka şirkette aynı vergi no → 409 + mevcut kod + "bu şirkete bağla", uyumluluk anahtarı yok · K-7 Blocked/OnHold ilişkiye yeni işlem reddi (Procurement içi beş nokta bu WP'de; 2026-10-23: kapsam yalnız bu kontrol); Blocked/OnHold şirkette görünür ve yönetilebilir kalır, yeni işleme yalnız Active girer (2026-10-23); `Suppliers:Model` geri alma anahtarı DL-2'de (eski DL-1) kalır (2026-10-23) · K-8 ilişkide `PaymentTermsCode`, banka bilgisi Finance'ta · K-9 karar ADR'ye yazılır · K-10 pack CT-APR-01'e göre (yüksek risk) paketi yazmamış onaylayıcı tarafından onaylanır — onaylayıcı Beste (`bestepullukcu-alt`, K2, `high: evet`; 2026-10-23).
- **ADR (K-9):** `docs/records/decisions/2026-10/mvp2-supplier-tenant-master-management-decision-01.md` — Natig (yönetim) kararı 2026-10-21; repodaki kaynak `docs/roadmap/plans/mvp6-development-plan-v10.0.md` C1 satırı (PR #177).
- **Mevcut veri:** tedarikçi modülü canlıda kullanılmadı, gerçek veri yok. Göç işi (dry-run / apply / verify / geri alma) yine yazılır, yalnız geliştirme/test verisi içindir; sayım sorgusu (öneri §2.3a) koşulmaz.
- **§21 "Audited Events"** ve `execution/approvals/approvals.tsv` / `reviews.tsv` satırları bu değişiklikte yazılmadı; ilk yazma komutu değişikliğiyle (DL-3, eski DL-2) eklenir (K-4; `module-pack-standard.md` Bölüm 10.1).
- **Dış konu — Inventory karantina:** `quarantine.openapi.yaml` 1.2.0 `DecisionDisposition.SupplierId` **Guid/uuid** (`QuarantineEntities.cs:177`), Procurement kodu ise `SUP-…` metni — bugünkü tip uyuşmazlığı, bu değişiklikten bağımsız. Sahibi Inventory; entegrasyonda ölçülüp düzeltilir. **Bu pack değiştirmez** (XMC-001).
- **Bildirim:** MVP-6 (MOD-0147/0148, DC-02 "aktif ilişki" tanımı, "tenant-scoped" seam belgeleri) ve Inventory tüketicilerine değişikliği sahip entegrasyon aşamasında iletir; onların paketleri bu pack'le değişmez.

**2026-10-26 — Revizyon 2: uygunluk sözleşmesi, MOD-0023 onayı, LE doğrulaması, servis token'ı (WP-MVP2-SUPPLIER-TENANT-01).**
- Karar veren: Beste (MVP-1 CT, sahip/onaylayıcı), 2026-10-26. Yedi karar başlıktaki "Revizyon 2" notunda; bu bölüm uygulama ayrıntısıdır.
- **Dilim yeniden numaralandırması:** eski DL-1 (model + göç) → DL-2; eski DL-2 (komutlar/sorgular) → DL-3; eski DL-3 (ekranlar) → DL-5; eski DL-4 (Procurement içi Blocked/OnHold) → DL-6. Yeni: DL-1 (1.3.0 contract-first), DL-4 (MOD-0023 onayı + C2), DL-7 (servis token'ı). Pack içindeki bütün dilim atıfları yeni numaralara çevrildi; 2026-10-21 notlarında eski numara parantezde verildi.
- **Sözleşme (DL-1):** §2 kuralları + §3.1 / §3.2 metni. `approval` için ayrı alan yok: §4.2'de onay sonucu `OnboardingStatus` (`Approved` / `Rejected`) içinde, ayrıntısı `Approval` nesnesinde; uygunluk izdüşümü yalnız durumu taşır (veri azaltma — `decidedBy` bir kişi kimliğidir).
- **Uygunluk kararları:** `validate` toplu denetimdir → ilişki yoksa satır `relationshipExists: false` (404 değil); tekil `eligibility` ucu bugünkü izolasyonu korur → 404. `eligibleForTransactions` = ilişki var ∧ silinmemiş ∧ `Active` (sahip tanımı; onboarding onayı hesaba girmez — §20 S-1).
- **İzin:** `procurement.suppliers.eligibility.read` yeni dar anahtar; `procurement.suppliers.read` sahipleri yeni uçları da alır (alt küme, anlam genişlemez — §14 gerekçe). "Veya" için Inventory `HasAnyPermissionAttribute` deseni Procurement'a kopyalanır (paylaşılan kod değil, servis içi kopya — MVP-1 deseni).
- **MOD-0023 (DL-4):** `services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Features/Supplier/Handlers/CommandHandlers/SubmitOnboardingCaseHandler.cs:54-63` ve `:82-83`'teki sahte `WF-` üretimi kalkar; yerine Inventory `GatewayWorkflowClient` + `ForwardCallerHeadersHandler` kopyası (yalnız başlat, by-objects, history çağrıları) ve okuma anı uzlaştırıcı. Karar geri çağırma ile değil, okuma anında sorularak gelir (Inventory'de garantili yol bu; olay tüketicisi isteğe bağlı ve RabbitMQ'ya bağlı — §20). Onboarding onayının ilişki `Status`'una etkisi yoktur (K-7 kapsamı değişmez).
- **C2 (DL-4):** Manufacturing `MdmLegalEntityReferenceValidator` + `LegalEntityValidationMiddleware` kopyası; tek fark `NotReferenceable` → 404 (Manufacturing 422). Servis geneli etki (MOD-0141..0145 uçları) sahip onayıyla kabul; başka modül dosyası değişmez. Canlı öncesi koşul: Procurement kullanıcılarının MDM'e sorabilmesi (§7, §20 S-2) — aksi halde her istek 503.
- **Servis token'ı (DL-7):** Platform `TrustedServiceTokenValidationExtensions` biçimi; Procurement'ta ayrı şema, yalnız iki uç; kiracı + LE açıkça gelir ve yine doğrulanır. Auth BL-652 Option 2 kaydı ve Procurement → MDM servis kimliği olmadan başlamaz.
- §21 "Audited Events", `approvals.tsv` / `reviews.tsv` satırları ve `approved_by` / `approved_on` bu revizyonda yazılmadı.

## 20. Follow-up Items
- Supplier performance/risk (0147) ve portal (0148) → MVP-6 (bu modül değil).
- Supplier bank/payment detayları → Finance/Treasury sınırı (EA-TBD). 2026-10-21: ödeme koşulu **kodu** ilişkiye alındı (K-8); banka bilgisi Finance/Treasury'de kalır; ödeme koşulu ana tablosu sonra.
- Dış ERP supplier senkron adapter (SourceSystem/ExternalRef; DEC-INV-19 deseni) → follow-up.
- Vergi no'suz kayıtların elle birleştirilmesi (göç bunları birleştirmez) → ayrı iş.
- Inventory karantina `supplierId` uuid / `SUP-` kod uyuşmazlığı → Inventory sahibi (XMC-001). Revizyon 2: 1.3.0 `GET /{supplierId}/eligibility` uuid (`supplierMasterId`) kabul eder; Inventory bu uçla uuid'yi çözebilir (karar Inventory sahibinde).
- Revizyon 2 — MOD-0023 tamamlanma olayı tüketicisi (`platform.workflow.instance.completed.v1`, Inventory `QuarantineWorkflowOutcomeConsumer` deseni) → RabbitMQ açıldığında ayrı iş; garantili yol okuma anı uzlaştırmasıdır.
- Revizyon 2 — Procurement içindeki diğer sahte `WF-` üreticileri (PO onayı, sözleşme aktivasyonu, talep gönderimi) → ilgili modül sahipleri (XMC-001); bu pack değiştirmez.
- Revizyon 2 — PKS-001 §4 namespace tablosuna `procurement.*` (sahibi `Diten.ProcurementService`) kaydı ve yeni anahtarların katalog/seed kaydı → AG-STEP-004B (`.antigravity/**` korunan yol).
- Revizyon 2 — `GET /eligible` ek filtreleri (ör. `purchasingEnabled`, ad araması) → ihtiyaç doğarsa sonraki eklemeli sürüm.
- **Açık sorular (Revizyon 2):**
  - **S-1:** `eligibleForTransactions` onboarding onayını (`onboardingStatus = Approved`) ya da `purchasingEnabled`'ı şart koşmalı mı? Sahip tanımı bugün yalnız ilişki + silinmemiş + `Active`; tüketici ek koşulu kendi tarafında `onboardingStatus` / `purchasingEnabled` alanlarıyla uygulayabilir. Değişirse alanın anlamı değişeceğinden 1.3.0 FROZEN'dan **önce** karar verilmeli.
  - **S-2:** C2 her Procurement isteğinde çağıranın token'ıyla MDM `lookup-validation` (`mdm.legal-entities.read`) çağırır. Procurement rollerine bu izin mi verilecek (Manufacturing'in bugünkü durumu), yoksa Procurement → MDM servis kimliği mi (BL-652 Option 2 deseni)? Karar DL-4 canlı öncesi; aksi halde izni olmayan kullanıcıda bütün Procurement uçları 503.
  - **S-3:** MOD-0023 onboarding şablonu: kod, onaylayıcı adayları (Inventory şirket onaylayıcı listesi tutar; Procurement'ta karşılığı yok) ve sahibi — MOD-0023 sahibiyle DL-4 öncesi.
  - **S-4:** Servis token'ı `GET /eligible` listesine de açılmalı mı (Treasury toplu senkron)? Bugünkü karar: yalnız `validate` ve tekil uygunluk; liste kapalı.
  - **S-5:** DL-7 audience adı (öneri `TRUSTED_SUPPLIER_ELIGIBILITY_CONSUMER`) ve kabul edilecek `service_name` listesi (MVP-6 `Diten.SupplyChain`; Treasury servis adı henüz yok — MOD-0119 pack'i `draft`) → Auth sahibi.
  - **S-6:** 2026-10-21 eklemelerinin (ilişki uçları: oku / bağla / durum / onay / ödeme koşulu; kiracı geneli vergi no araması) kesin yol, işlem ve şema adları pack'te birebir yazılı değil (yalnız §4.2 alanları ve izinler). Contract-first DL-1 bunları da yazacaksa adlar DL-1 incelemesinde sabitlenir; alternatif: DL-1 yalnız Revizyon 2 uygunluk yüzeyini + 409 `supplierId`'yi dondurur, ilişki uçları DL-3 ile 1.4.0'a eklenir. Sahip seçimi DL-1 öncesi.

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | S0 sözleşme 1.3.0 contract-first: supplier.openapi.yaml yalnız eklemeli yazılır ve FROZEN olur (ilişki uçları, kiracı geneli arama, uygunluk yüzeyi, SupplierEligibility); 1.2.0 değişmez | Ownership and Boundaries, Owned Objects, Gateway / API Routing Decision, Acceptance Criteria, Test Expectations |
| DL-2 | S1 model + göç: iki varlık, indeksler, göç işi (dry-run / apply / verify / geri alma) | Entity Fields, Validation Rules, Runtime Constraints, Repo Scope |
| DL-3 | S2 komutlar ve sorgular + merkezi denetim (BL-501): iki repository, ilişki üzerinden okuma, bağla-ya-da-oluştur, ortak alan ve ilişki komutları, validate uygunluk alanları, uygunluk ucu, aktif tedarikçi listesi, dar izin | Owned Objects, Runtime Constraints, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision |
| DL-4 | S2b onboarding onayı gerçek MOD-0023 iş akışıyla + başlık LE doğrulaması MDM üzerinden (C2, servis geneli) | Dependencies, Runtime Constraints, Validation Rules, Failure Path to Verify, Repo Scope |
| DL-5 | S3 ekranlar (7 dil): şirketin tedarikçileri, "mevcut tedarikçi bulundu → bağla", ilişki paneli, ortak alanlar yalnız yetkiliye | Frontend File Contract, Layout & Shell Contract, Acceptance Criteria |
| DL-6 | S4 tüketiciler: Procurement içi beş doğrulama noktasına yalnız Blocked / OnHold reddi eklenir | Dependencies, Runtime Constraints, Test Expectations |
| DL-7 | S5 validate ve uygunluk okuması için güvenilir servis token'ı (BL-652 Option 2'ye bağlı) | Dependencies, Runtime Constraints, Authorization Convention, Failure Path to Verify |

- DL-1 notu: yalnız `docs/analysis/contracts/supplier.openapi.yaml` yazılır (tek yazar MOD-0140; yalnız bu pack'in onayı gerekir). `SupplierEligibility` §3.1'den, `validate` satır eklemeleri §3.2'den birebir; 1.2.0 ↔ 1.3.0 fark testi sıfır kırıcı; `x-status: FROZEN` + tarih. Kod yok. DL-3 birleşene kadar yeni uçlar sunulmaz (feature flag / 501 yok); tüketiciler (MVP-6 MOD-0147/0148, Treasury MOD-0119/0289) sözleşmeye karşı mock ile yazar.
- DL-2 notu: model + göç işi `dry-run` / `apply` / `verify` / geri alma kipleriyle yazılır; **yalnız geliştirme/test verisi** — canlıda tedarikçi verisi yok.
- DL-3 notu: yeni yazma komutları BL-501 `Diten.BuildingBlocks.CentralAudit` ile denetlenir (K-4; BL-501 main'de, 2026-10-26). §21 "Audited Events" ve onay/inceleme satırları bu dilimle eklenir. Uygunluk uçları `[HasAnyPermission]` ile korunur (§14).
- DL-4 notu: sahte `WF-` kimliği kalkar; karar okuma anı uzlaştırmasıyla gelir. C2 ara katmanı Procurement'ın bütün kiracı uçlarını etkiler (sahip onayı 2026-10-26); MOD-0141..0145 sahiplerine bildirilir. Canlı öncesi §20 S-2 ve S-3 kapanır.
- DL-6 notu: yalnız Procurement içi tüketiciler (PO, fatura, RFx/teklif, sözleşme) ve yalnız "bloke tedarikçiye yeni işlem yok" kontrolü; başka davranış değişmez (XMC-001 onayı 2026-10-23). MVP-6, Treasury ve Inventory'ye sahip entegrasyonda bildirir.
- DL-7 notu: Auth sahibinin BL-652 Option 2 kaydı (yeni audience + istemci kaydı) ve Procurement → MDM servis kimliği olmadan başlamaz; servis token'ı şeması sözleşmeye ayrı eklemeli sürümle (1.4.0) girer.
- Her dilim ayrı commit, ayrı PR; push öncesi `origin/main` merge + ölçüm yeniden (main'e göre yeni kırmızı yok).
