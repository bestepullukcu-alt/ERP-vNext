# WP-MVP2-SUPPLIER-TENANT-01 — ölçüm (P0, kod yok)

- Tarih: 2026-10-21 · taban: `origin/main` `a00538e17` · dal: `feature/mvp2/supplier-tenant-01`
- Yöntem: salt okuma (kaynak kodu, paketler, sözleşmeler); test tabanı ayrı worktree'de (`baseline-tests.txt`).
- Kapsam: MOD-0140 tedarikçi ana kaydı ve onu kullanan her yer. Başka modüle dokunulmadı (XMC-001).

## 0. Özet

1. **Bugün tedarikçi şirket başına bir kayıttır.** `Supplier` belgesi `TenantId` + `LegalEntityId` taşır; her sorgu ikisiyle
   süzülür (`SupplierRepository.cs:142-148` `TenantFilter()`); kod (`SUP-xxxxxxxx`) ve vergi no tekilliği de
   kiracı + şirket bazındadır (`SupplierIndexConfiguration.cs:27-43`). Aynı firma iki şirkette iki ayrı kayıt, iki ayrı koddur.
2. **Tedarikçinin kendi bilgisi ile şirketle ilişkisi aynı belgede.** Ayrı ilişki kaydı yok; ödeme koşulu, satın alma
   ilişkisi, banka, adres alanı hiç yok.
3. **Tedarikçi kimliğini yalnız Procurement (PO, fatura, RFx/teklif, sözleşme) ve Inventory karantina kararı taşır.**
   MVP-6 SupplyChainService'te (MOD-0148 portalı dahil) tedarikçi kodu yok; portal yalnız sözleşmede (supplier-performance
   1.0.0) ve paketlerde. Tedarikçi olayı yok; hiçbir belge ad / vergi no kopyası tutmuyor.
4. **Mevcut veri sayılamadı** (§4): kapta Mongo yok, 27017'ye erişilmedi (kural). Salt okunur sayım sorgusu öneride.
5. Yönetim kararının kaynağı (MVP-6 plan **v10.0**, soru 4) repoda yok; en son plan `mvp6-development-plan-v9.9.md`.

## 1. `Supplier` alanları — kime ait

Kaynak: `services/Diten.ProcurementService/src/Diten.ProcurementService.Domain/Entities/Supplier.cs` (+ `EntityBase.cs`).
**A** = tedarikçinin kendisi · **B** = şirketle ilişki · **C** = karar gerekir.

| alan | tip | satır | sınıf | not |
|---|---|---|---|---|
| `TenantId`, `LegalEntityId` | Guid? | EntityBase.cs:21, :28 | kapsam | `LegalEntityId` kaydı şirkete bağlayan tek alan |
| `SupplierId` | string (`SUP-` + 8 hex) | :91; üretim CreateSupplierHandler.cs:67-68 | C | kimlik gibi duruyor ama tekilliği kiracı+şirket; aynı firmanın her şirkette ayrı kodu var |
| `Name` | string | :93 | A | |
| `Country` | string? (küçük harf) | :98 | A | |
| `TaxId` | string? | :101 | A | tekillik kiracı+şirket (§2) |
| `Contacts[]` {Type, Email, Phone, Name} | | :103, :46-49 | A | tip: primary/billing/quality/logistics |
| `Documents[]` {Type, EvidenceRef} | | :109, :57-58 | A | sertifika vb. kanıt referansı |
| `Status` {Active, OnHold, Blocked, Inactive} | enum | :95 | **B** | yalnız oluşturmada Active yazılır (CreateSupplierHandler.cs:43); değiştiren uç yok |
| `OnboardingStatus` {Draft, InReview, Approved, Rejected} | enum | :106 | **B** | Approved/Rejected'a götüren kod yok |
| `Approval` {WorkflowInstanceId, DecidedBy, DecidedAt} | | :113, :79-81 | **B** | DecidedBy/At yalnız null yazılır (SubmitOnboardingCaseHandler.cs:61-62) |
| `KycOutcome`, `SanctionsOutcome` | enum | :107-108 | **C** | tarama sonucu firmaya ait bir olgu; `Override` bir şirket kararı |
| `OnboardingCase` | nesne? | :110 | B | hiçbir yerde yazılmıyor / okunmuyor (ölü alan) |
| `SourceSystem`, `ExternalRef` | string? | :116-117 | C | dış sistem eşlemesi; şirketin ERP'sine özgü olabilir |
| `IdempotencyKey`, `OnboardingIdempotencyKey` | string? | :121, :124 | teknik | |
| `Version`, `IsDeleted`, `DeletedAt`, `CreatedAt`, `UpdatedAt` | | EntityBase.cs:30-37 | teknik | soft delete var |

**Bugün olmayanlar:** adres, banka, ödeme koşulu, para birimi, alıcı (buyer), risk notu, durum geçmişi. MOD-0140 §20
(`execution/domains/procurement/module-packs/MOD-0140-supplier-onboarding.md:154`) banka/ödeme ayrıntısını
Finance/Treasury sınırına bırakmış (EA-TBD). KYC girdisi (`LegalName`, `RegistrationNo`, `BeneficialOwners`) saklanmıyor,
yalnız sonucu hesaplamada kullanılıyor (SubmitOnboardingCaseHandler.cs:46-52).

## 2. Kapsam ve tekillik

| konu | bugün | kaynak |
|---|---|---|
| Sorgu filtresi | `TenantId == ctx` **ve** `LegalEntityId == ctx` **ve** `!IsDeleted` — her metotta | SupplierRepository.cs:142-148 |
| LE nereden | JWT `legal_entity_id` ya da `X-Legal-Entity-Id`; çelişki 400, yoksa 400 | TenantResolutionMiddleware.cs:17-19, :77-107 |
| Kod tekilliği | unique (TenantId, LegalEntityId, SupplierId) — silinmişler dahil | SupplierIndexConfiguration.cs:27-29 |
| Vergi no tekilliği | partial unique (TenantId, LegalEntityId, TaxId), yalnız dolu + silinmemiş; el kontrolü `ExistsByTaxIdAsync` → 409 `DUPLICATE_SUPPLIER` | SupplierIndexConfiguration.cs:32-43; SupplierRepository.cs:128-139 ("MOD-0140 §12") |
| Paket kuralı | §12: "aktif supplier'da unique (Tenant+LE)"; §4 :42/:46/:53; §8 :79 "Tenant + Legal-Entity izolasyonu her sorguda" | MOD-0140 :98 |
| DCP kararı | AD-5 "Tenant + Legal-Entity scoping (DEC-INV-18)" | DCP-010-procurement-p2p.md:80 |
| Kural ile çelişki | `mongo-indexing.md:30-31` vergi no örneğini **kiracı bazında** veriyor: `{ TenantId, TaxNumber }` | — |

Ek gözlemler (bugünkü kusurlar, bu WP'nin işi değil): vergi no karşılaştırması yalnız `Trim`, normalizasyon yok;
"aktif" = silinmemiş (Status bakılmıyor); `DuplicateKey` yakalanmıyor (yarışta 409 yerine 500); sözleşme tekrarı
"taxId/name" diyor (supplier.openapi.yaml:283), kod yalnız TaxId bakıyor.

## 3. Tedarikçiyi okuyan / yazan her şey

### 3.1 MOD-0140'ın kendi uçları (`SuppliersController.cs`, rota `api/suppliers`)

| uç | izin | işleyici |
|---|---|---|
| GET `/` ?status&cursor | `procurement.suppliers.read` | GetSupplierListHandler (sayfa 50, bellekte) |
| GET `/{supplierId}` | read | GetSupplierByIdHandler (404 UNKNOWN_SUPPLIER) |
| POST `/` (Idempotency-Key) | create | CreateSupplierHandler (Status=Active, Onboarding=Draft) |
| PATCH `/{supplierId}` (If-Match) | update | UpdateSupplierHandler (409 eşzamanlılık / tekrar) |
| DELETE `/{supplierId}`, DELETE `/bulk` | delete, bulk-delete | soft delete |
| POST `/validate` | read | ValidateSuppliersHandler (başka şirketin kodu → `known:false`) |
| GET / POST `/{supplierId}/onboarding` | read / onboard | GetOnboardingCase / SubmitOnboardingCase (InReview'a kadar) |

Olay: **yok** (outbox / yayın / tüketim yok). Denetim: servis defterinde iz yok; Create/Update/Delete/BulkDelete
"bilinen borç" (`tests/architecture/audit-ledger/Diten.ProcurementService.md:16, :38-63`).

### 3.2 Tedarikçi kodunu taşıyan başka yerler

| yer | alan | şirket yanında mı | nasıl doğrulanıyor | şirkete bağlı kimliği mi tutuyor |
|---|---|---|---|---|
| PO (MOD-0141) `PurchaseOrder.cs:59` | `SupplierId` | evet | CreatePurchaseOrderHandler.cs:71-76 → `GetBySupplierIdsAsync`, yoksa 404 | **evet** (çağıranın şirketinde arar) |
| Fatura (MOD-0143) `Invoice.cs:88` | `SupplierId`; unique (Tenant, LE, SupplierId, InvoiceNumber) | evet | CaptureInvoiceHandler.cs:72-82; PO'nun tedarikçisiyle eşitliği bakılmıyor | **evet** |
| RFx / teklif (MOD-0145) `RfxEvent.cs:76`, `Bid.cs:31`, `RfxEvent.cs:50` | davetliler, teklif sahibi, kazanan | evet | CreateRfxEventHandler.cs:66-78, SubmitBidHandler.cs:80-85; kazanan tekliften kopya | **evet** |
| Sözleşme (MOD-0144) `Contract.cs:50` | `SupplierId` (değişmez) | evet | CreateContractHandler.cs:58-65 | **evet** |
| GRN (MOD-0142) | tedarikçi alanı yok (PO üzerinden) | evet | — | hayır |
| Inventory karantina `QuarantineEntities.cs:177` | `DecisionDisposition.SupplierId` **Guid?** | evet | doğrulama yok; `OptionalUuid` (QuarantineRequestReader.cs:215) | belirsiz — **tip uyumsuz**: Procurement kodu `SUP-…`, karantina uuid istiyor |
| MVP-6 SupplyChainService (MOD-0148 dahil) | **yok** | — | — | — (yalnız sözleşme ve paket) |
| MDM, Platform, Manufacturing, Planning | yok | — | — | — |
| Ağ geçidi `ocelot.json:562-599` | `/api/suppliers` → 5065 | — | — | yol bazlı |

Hiçbir tüketici `Supplier.Status`'a bakmıyor: Blocked / OnHold tedarikçiye PO, fatura, teklif, sözleşme açılabiliyor.

### 3.3 Ekranlar (`frontend/Diten.Web`)

| ekran | ne yapıyor | şirket nasıl seçiliyor |
|---|---|---|
| Tedarikçiler (`Views/Procurement/Suppliers/*`, 7 dil, 47 anahtar) | liste, oluştur, düzenle, detay, onboarding | **seçilmiyor**: sunucu vekili yalnız Bearer + `X-Tenant-Id` gönderir (SuppliersController.cs:427-443); LE JWT'den gelir |
| PO / Sözleşme / RFx formları | `GET /api/suppliers` ile seçici | aynı (JWT) |
| Fatura formu | serbest metin `SupplierId` | — |
| Karantina iade seçicisi (QuarantineController.cs:306-310) | `GET /api/suppliers`, `company:false` | LE başlığı gönderilmiyor |

Ekran kusurları (bu WP'nin işi değil): form `Status` gönderiyor ama arka uç yok sayıyor; onboarding belgesi
`EvidenceRef`'siz gidiyor (doğrulayıcı reddediyor, yalnız log); `Lookups` yanlış zarfla okunuyor.

### 3.4 Sözleşmeler

| sözleşme | sürüm / durum | tedarikçi kapsamı |
|---|---|---|
| `supplier.openapi.yaml` (sahip MOD-0140) | 1.2.0 · FROZEN; "ŞEKLİ DEĞİŞMEZ" (:9); MVP-6 için öne çekilmiş (:19) | ":12 Tenant + Legal-Entity scoped" |
| `requisition-po`, `contracting`, `sourcing`, `invoice-match` | 1.1.0 · FROZEN | `supplierId` opak metin; kapsam yazılmamış |
| `supplier-performance.openapi.yaml` (MOD-0147/0148) | 1.0.0 · FROZEN; tedarikçi tüketimi AWAITING-CT (DC-02) | Tenant + LE JWT'den; portal tedarikçiyi aktörden çözer |
| `quarantine.openapi.yaml` | 1.2.0 · FROZEN | `supplierId` **uuid** — MOD-0140'a bağlı değil |

MVP-6 belgeleri tedarikçi aramasını "tenant-scoped" diye tanımlıyor ve şirket uygunluğu için ayrı, olumlu bir sonuç
istiyor (DC-02: `docs/roadmap/plans/mvp6-supplier-concurrence-close-prep-01/OWNER-DECISION-PACK.md:36-37`,
"Tenant-scoped identity/status alone is not affirmative LE eligibility"; durum UNAPPROVED).

## 4. Mevcut veri — ÖLÇÜLEMEDİ

Kapta çalışan Mongo yok; geliştirme verisi 27017'de ve bu WP'nin kuralı gereği oraya bağlanılmadı. Sayım, öneri
belgesindeki salt okunur sorguyla veri sahibinin ortamında alınmalı (yalnız sayı döner, vergi no / ad döndürmez).

## 5. Test tabanı

`baseline-tests.txt`: Procurement 147/147, gateway 259/259, mimari 141/144 (3 kırmızı main'in kendisinin; hiçbiri
Procurement kodundan değil). Tedarikçi testleri: `Suppliers/SupplierBackendTests.cs` (tekrar vergi no 409, şirketler arası
okuma / yazma 404, soft delete, idempotency), `Tenancy/LegalEntityResolutionGuardTests.cs`, tüketicilerde "unknown supplier
→ 404" testleri. **Yok:** aynı vergi no'nun başka şirkette kabulü, silindikten sonra yeniden kullanım, indeks testi,
Blocked tedarikçinin reddi.
