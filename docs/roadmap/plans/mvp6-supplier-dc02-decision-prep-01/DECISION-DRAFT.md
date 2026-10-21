---
status: draft
kind: owner-decision-draft
gate: DC-02 (MOD-0140 Supplier consumption + Tenant/LE eligibility)
modules: [MOD-0147, MOD-0148]
consumed: MOD-0140
measured_at: origin/main 365f260a9 (2026-10-20)
---

DURUM: TASLAK — **tedarikçi modeli kararlaştırıldı (Natig, yönetim, 2026-10-21; §4.0)**; D1 bu karara göre yeniden
yazıldı (öneri). D1–D6 hâlâ öneridir; karar veren ve tarih yok.

# DC-02 — karar taslağı (öneri, onay değil)

> Bu belge **taslaktır**. Hiçbir kararı kapatmaz, kod, paket durumu ya da dağıtım yetkisi vermez. Ölçüm salt okunur kod
> okumasıdır; **çalışma testi yapılmadı** (lane kapalı). MOD-0140 Procurement domain'indedir: oradaki her değişiklik
> XMC-001 onayı ister.

## 1. DC-02 nedir

MOD-0147 (Supplier Performance & Risk) ve MOD-0148 (Supplier Portal) Tenant+LE kapsamlıdır. Her yazma işleminden önce
"bu tedarikçi bu kiracının bu Legal Entity'sinde var ve bu işlem için uygun mu?" sorusunun **yetkili** bir cevabı
gerekir. Kaynak MOD-0140 Supplier'dır. Bu kapı iki modülün ortak engelidir:

- MOD-0147 `POST /evaluations`, `POST /risks` → **BLOKE** (DC-02 + DC-04/DC-05).
- MOD-0148 `GET /portal/status`, `POST /portal/submissions` → **BLOKE** (DC-02 + DC-03).

Sahip ataması (`docs/records/decisions/2026-10/mvp6-supplier-dc01-dc05-owner-assignment-01.md`): DC-02 = MOD-0140
Supplier lane, **build commitment** — imzayla kapanmaz.

## 2. Okunan kaynaklar (olgu, `365f260a9`)

| # | Olgu | Kaynak |
|---|---|---|
| F1 | Sözleşme dondurulmuş: `supplier.openapi.yaml` v1.2.0, `x-status: FROZEN`, `x-owner: MOD-0140`, `servers: /api/suppliers`; Tenant+LE sunucuda çözülür, payload'da yok | `docs/analysis/contracts/supplier.openapi.yaml:12-22` |
| F2 | `POST /validate` (`validateSuppliers`): `supplierIds[]` (string) → her id için `known` + `status`; bilinmeyen `known:false` | `supplier.openapi.yaml:126-160` |
| F3 | Durum kümesi `Active`, `OnHold`, `Blocked`, `Inactive` — hangisinin neye izin verdiği sözleşmede yok | `supplier.openapi.yaml:197` |
| F4 | MOD-0140 **uygulanmış**: `Diten.ProcurementService` (5065), `SuppliersController` `[Route("api/suppliers")]`, `validate` ucu `[HasPermission("procurement.suppliers.read")]` | `services/Diten.ProcurementService/src/Diten.ProcurementService.Api/Controllers/SuppliersController.cs:20,111-113`; commit `93f030eb1` (2026-09-16) |
| F5 | Her tedarikçi sorgusu **Tenant + LegalEntity + IsDeleted=false** ile filtrelenir; yazılan kayda çağıranın LE'si basılır | `services/Diten.ProcurementService/src/Diten.ProcurementService.Persistence/Repositories/SupplierRepository.cs:72,84,141-148` |
| F6 | Gateway `/api/suppliers` ve `/api/suppliers/{everything}` → `localhost:5065` | `gateway/Diten.ApiGateway/ocelot.json:562-590` |
| F7 | Procurement LE'yi JWT `legal_entity_id` claim'inden, yoksa `X-Legal-Entity-Id` başlığından alır; ikisi farklıysa 400. Başlıktaki LE'nin kiracıya ait ve aktif olduğunu **doğrulamıyor** (MDM'e sormuyor). Dev bypass yalnız Development | `services/Diten.ProcurementService/src/Diten.ProcurementService.Infrastructure/Middleware/TenantResolutionMiddleware.cs:16-19,77-100,150-156` |
| F8 | SupplyChain'de MOD-0140'ı tüketen kod **yok** (`api/suppliers`, supplier reader/client araması: 0 sonuç) | `git grep` on `services/Diten.SupplyChainService/src` |
| F9 | Daha önceki matris: LE uygunluğu "MISSING", durum uygunluğu "PARTIAL"; aktör bağlama ve LE uygunluğu liste ucundan çıkarılamaz | `docs/roadmap/plans/mvp6-supplier-seam-readiness-01/CONSUMED-SEAM-MATRIX.md:18-21,74-76` |

## 3. Çıkarımlar (olgu değil)

- **I1 — LE uygunluğu fiilen var, ama sözleşmede adı yok.** F5'e göre tedarikçi yalnız kaydedildiği Tenant+LE içinde
  bulunur. Doğru LE bağlamıyla çağrılan `validateSuppliers`, başka LE'nin tedarikçisi için `known:false` döner. F9'daki
  "MISSING", sözleşme metni için doğru, **davranış için fazla sert**. Bunun güvenilir olması F7'deki boşluğun
  kapanmasına bağlı.
- **I2 — F7 bir güven boşluğu.** LE yalnız başlıkla geliyorsa (JWT'de claim yoksa), aynı kiracıdaki bir çağıran başka bir
  LE'nin tedarikçilerini doğrulayabilir. Kiracılar arası sızıntı yok (Tenant JWT ile bağlı). SupplyChain kendi isteğinde
  LE'yi zaten MDM'le doğruluyor (R-2, `MdmLegalEntityScopeValidator`); Procurement'a aynı doğrulanmış LE'yi iletirse bu
  boşluk SupplyChain yolunda kapanır. Procurement'ın kendisinin düzeltilmesi MOD-0140 sahibinin işidir.
- **I3 — Durum anlamı eksik (F3).** Hangi durumun hangi işleme izin verdiği bir sahip kararıdır; kod bunu tahmin etmemeli.
- **I4 — Yetki.** `validate` için `procurement.suppliers.read` gerekir. SupplyChain kullanıcının token'ıyla çağırırsa
  değerlendirme yapan her kullanıcının bu anahtarı tutması gerekir; servis kimliğiyle çağırırsa (S2S) DC-03 ve servis
  istemcisi kaydı devreye girer.
- **I5 — Kimlik biçimi.** Sözleşmede `supplierId` string (örnek `SUP-2001`); MOD-0147/0148 paketleri bunu nasıl tutacak
  (string mi, Guid mi) paketlerde sabit olmalı.

## 4.0 Verilmiş karar — tedarikçi modeli (Natig, yönetim, 2026-10-21)

> Tedarikçinin **ana kaydı kiracı içinde tektir**; kiracının bütün tüzel kişilikleri (LE) aynı kaydı paylaşır. **Onay
> durumu, ödeme koşulları ve satın alma ilişkisi her LE için ayrı** tutulur. Aynı firma iki kez kaydedilmez; tedarikçi
> onayı şirket/tesis bazında kalır.

Bu kararın sonuçları (çıkarım, olgu değil):

- **K1 — Bugünkü kod kararla uyuşmuyor.** F5'e göre Procurement tedarikçiyi **Tenant + LE** anahtarıyla tutuyor; aynı firma
  iki LE'de iki ayrı kayıt olur. Kararın istediği yapı: kiracı düzeyinde bir ana kayıt + LE başına bir ilişki kaydı (onay,
  ödeme koşulları, satın alma ilişkisi). Bu MOD-0140'ın veri modeli değişikliğidir: **MOD-0140 sahibinin işi, XMC-001**.
  Mevcut LE başına kayıtların ana kayda birleştirilmesi (aynı firmanın tekrarları) bir göç kararı ister.
- **K2 — Bugünkü LE başına kayıt, fiilen "LE ilişkisi" gibi davranıyor.** Bir LE'de kayıtlı olmak, o LE'de ilişki var
  demektir. Bu yüzden "tedarikçi bu LE için uygun mu?" sorusu bugün de doğru LE bağlamıyla cevaplanabilir; değişen,
  cevabın **ana kayıt kimliği** ile verilmesidir.
- **K3 — Kimlik (I5'i keskinleştirir).** MOD-0147/0148, tedarikçiyi **kiracı düzeyindeki ana kayıt kimliğiyle** tutmalıdır;
  LE başına kayıt kimliği tutulursa, Procurement birleştirmesinden sonra puan kartları ve portal kayıtları yetim kalır.
  Bugünkü LE başına kimlikten ana kimliğe bir eşleme (Procurement göçünün çıktısı) gerekir.
- **K4 — Durum iki düzeyde olabilir (D2'yi etkiler).** LE ilişkisinin onay durumu (`Active`/`OnHold`/`Blocked`/`Inactive`)
  LE başınadır; ama bazı engeller kiracı düzeyinde olabilir (ör. yaptırım listesi → bütün LE'lerde blok). Hangi durumun
  hangi düzeyde tutulduğu D2'de sahibin kararıdır.

## 4. Sahip kararları (her biri için önerilen seçenek)

| # | Karar | Seçenekler | Öneri |
|---|---|---|---|
| D1 | Uygunluk kaynağı (**2026-10-21 tedarikçi modeline göre yeniden yazıldı**) | (a) mevcut `POST /api/suppliers/validate`'i doğrulanmış LE ile çağırmak (bugünkü LE başına kayıt = LE ilişkisi, K2); (b) MOD-0140 sözleşme halefi (v1.3.0): `validate`, her `supplierId` için **ana kayıt kimliğini** (`known`) ve çağıranın doğrulanmış LE'sindeki **ilişki durumunu** ayrı alanlarla döner; ana kayıt düzeyindeki engel (K4) ayrı alan | **(b) hedef, (a) köprü.** Kalıcı kaynak (b): karar ana kayıt + LE ilişkisi ayrımını istiyor; v1.2.0 `FROZEN` sözleşme bu ayrımı taşımıyor, halef **MOD-0140 sahibinin** işi. İlk dilim (C8) beklemeden (a) ile başlayabilir — **koşul:** MOD-0147/0148 kimliği ana kayıt kimliği olarak saklar (K3) ve (a) yanıtını yalnız "bu LE'de ilişki var mı + durumu" olarak okur; (b) gelince okuyucu aynı arayüzle (`ISupplierEligibilityReader`) değiştirilir |
| D2 | Durum → izin matrisi | Örn. değerlendirme/risk: `Active`, `OnHold` izinli; `Blocked`, `Inactive` → 422; portal erişimi/gönderim: yalnız `Active`. **2026-10-21 eki:** durum LE ilişkisinde tutulur; kiracı düzeyinde bir engel (K4) varsa her LE'de 422 | Öneri bu; **karar MOD-0147/0148 sahibi + MOD-0140 sahibi** (hangi durumun hangi düzeyde olduğu dahil) |
| D3 | Çağrı kimliği | (a) kullanıcının token'ı (yetki: `procurement.suppliers.read` rol şablonlarına); (b) S2S servis kimliği | **(a)** ilk dilim için — DC-03'e bağımlılık yaratmaz; (b) MOD-0148 portal aktörü için DC-03'le birlikte |
| D4 | LE iletimi | SupplyChain, MDM'le doğruladığı LE'yi `X-Legal-Entity-Id` ile iletir; Procurement'ta claim/başlık doğrulaması ayrı iş | Öneri bu; Procurement tarafı (F7) **MOD-0140 sahibine ayrı bildirim**, XMC-001 |
| D5 | Hata davranışı (fail-closed) | Procurement ulaşılamaz / zaman aşımı / `known:false` → yazma yok | 503 (ulaşılamaz) ve 422 (bilinmeyen/uygunsuz), sıfır yazma; tam kod listesi paket sürümünde |
| D6 | İnşa sahibi ve zamanı | DC-02 build seam'i kim, ne zaman | **TBD** — owner ataması "SupplyChain/MDM supplier lane (proje sahibi / CT)" diyor; takvim yok |

## 5. Önerilen ilk adım (öneri — onay değil)

D1–D5 onaylanırsa, MOD-0147 ve MOD-0148 paketlerine **aynı** "Supplier eligibility seam" bölümü eklenir (yeni paket
sürümü → yeni CT-APR-01 onayı), ardından tek bir SupplyChain-içi okuyucu (`ISupplierEligibilityReader`) dar bir dilimle
geliştirilir. Bu, DC-02'yi kapatır; MOD-0147'nin puanlaması (DC-04) ve risk sınıflaması (DC-05) ile MOD-0148'in portal
aktörü (DC-03) hâlâ açık kalır — yani ilk dilim tek başına kullanıcıya görünen bir özellik üretmez.

## 5a. D2–D6 için ek ölçüm ve seçenekler (2026-10-21, `origin/main` `9d44e8a1f`, salt okunur)

Yollar `services/Diten.ProcurementService/src/` altındadır.

**Yeni olgular (D2'yi değiştirir):**

| # | Olgu | Kaynak |
|---|---|---|
| F10 | `SupplierStatus` = Active / OnHold / Blocked / Inactive; **tek yazım create'te `Active`**. OnHold/Blocked/Inactive'e geçiren komut, handler ya da uç **yok** | `Domain/Entities/Supplier.cs:6-12,95`; `…/CreateSupplierHandler.cs:43`; `Api/Controllers/SuppliersController.cs:33-145` |
| F11 | Hiçbir işlem durumu okumuyor: PO, sözleşme, fatura, RFx ve teklif yalnız "kayıt var mı" diye bakıyor | `…/CreatePurchaseOrderHandler.cs:70-76`, `…/CreateContractHandler.cs:60-64`, `…/CaptureInvoiceHandler.cs:77-81`, `…/CreateRfxEventHandler.cs:66-77`, `…/SubmitBidHandler.cs:79-84` |
| F12 | Onay ayrı alanda: `OnboardingStatus` (Draft/InReview/Approved/Rejected), KYC, yaptırım, `Approval`. **Approved ya da Rejected'a geçiren kod yok**; onay gönderimi sahte bir `WF-xxxx` iş akışı kimliği üretiyor; onay ile `SupplierStatus` bağlı değil (onaylanmamış tedarikçi de `Active`) | `Supplier.cs:15-21,76-82,106-113`; `…/SubmitOnboardingCaseHandler.cs:46-68,83` |
| F13 | `validate` yanıtı yalnız `known` + `status` döner; **`OnboardingStatus` dönmez** | `…/Supplier/SupplierModels.cs:56-64`; `…/ValidateSuppliersHandler.cs:22-35` |
| F14 | `procurement.suppliers.read` Auth kaynağında hiç geçmiyor; `procurement-suppliers` `AdminModules`'te yok. Anahtar yalnız manifestle kataloğa gelir; kataloğa girerse varsayılan **Viewer** `read` kuralıyla alır, **Admin almaz** | `DefaultRolePermissionTemplate.cs:66,175-216`; `…/SupplierOnboardingManifestProvider.cs:15,30` |
| F15 | Procurement'ın **servis token şeması yok** (tek JwtBearer, audience `diten-erp`); Auth'un servis token audience politikasında Procurement yok | `Api/Program.cs:38-60`; `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/ServiceIdentityTokenAudiencePolicy.cs:5-17` |
| F16 | Kiracının varlığı kontrol edilmiyor (her GUID kabul); kiracı/LE eksik ya da claim–başlık çelişkisi → 400 problem+json | `Infrastructure/Middleware/TenantResolutionMiddleware.cs:50-58,68-74,80-88,101-107,109` |
| F17 | Tedarikçide ödeme koşulu / LE ilişkisi alanı yok; bütün belge ve indeksler Tenant+LE başına | `Supplier.cs:88-125`; `Persistence/SupplierIndexConfiguration.cs:21-39` |

**Çıkarım:** Bugün "uygun mu?" sorusunun Procurement'ta **anlamlı bir cevabı yok** — durum hep `Active`, onay hiç
verilmiyor. D2 matrisi yazılabilir, ama Procurement durum geçişlerini ve onayı (LE başına, §4.0) uygulamadan **etkisi
olmaz**. Bu, DC-02'nin MOD-0140 tarafındaki asıl açığıdır.

| # | Karar | Seçenekler | Öneri |
|---|---|---|---|
| D2 | Uygunluk ölçütü | (a) yalnız `SupplierStatus` (F10–F11: bugün hep Active); (b) `OnboardingStatus = Approved` **ve** `SupplierStatus ∉ {Blocked, Inactive}` — LE ilişkisinde (§4.0) | **(b)** — kararın "onay LE başına" ilkesine uyan tek seçenek. Ön koşul: MOD-0140 onay ve durum geçişlerini uygular (F10, F12) ve `validate` onayı da döner (F13; sözleşme halefi, D1-b) — **MOD-0140 sahibi, XMC-001** |
| D3 | Çağrı kimliği | (a) kullanıcının token'ı — her değerlendiricinin `procurement.suppliers.read` tutması gerekir (F14: Admin'de yok); (b) servis kimliği — Procurement'ta yeni bir servis-token şeması + Auth'ta yeni audience (F15) | **(b)**, BL-652 Seçenek 2 ile aynı desen (Auth `service-tokens/issue`). (a) BL-652'de yaşanan "kullanıcıya başka modülün iznini ver" sorununu tekrarlar |
| D4 | LE iletimi | SupplyChain, MDM'le doğruladığı LE'yi `X-Legal-Entity-Id` ile iletir | Öneri bu. F16: Procurement kiracının varlığını da kontrol etmiyor — SupplyChain kiracıyı token'dan, LE'yi MDM doğrulamasından verir; Procurement'ın kendi doğrulaması ayrı iş (C2, MOD-0140) |
| D5 | Hata eşlemesi (fail-closed) | Procurement 400 (eksik/çelişen kiracı-LE) · 401/403 · zaman aşımı/ulaşılamaz · `known:false` · uygun değil | 400/401/403/ulaşılamaz → **503**, sıfır yazma (bizim yapılandırma ya da kimlik sorunumuz, kullanıcının değil); `known:false` ve uygun değil → **422**; bunlar paket sürümünde sabitlenir |
| D6 | İnşa sahibi ve zamanı | — | **Yönetim (Natig)**: MOD-0140 tarafı (onay/durum geçişleri, sözleşme halefi, servis-token şeması) Procurement ekibinde; SupplyChain okuyucusu SCE'de. Zaman: MOD-0147 TPRM'den sonra başladığı için, MOD-0140 işi o zamana kadar bitmeli |

## 6. Ölçülmeyenler

- Çalışma testi: Procurement + gateway + SupplyChain ayağa kaldırılıp `validate` çağrılmadı.
- `verify_module_id.py --discover` bu iş için koşulmadı (kimlikler mevcut; yeni kimlik önerilmiyor).
- ~~Procurement'ın rol şablonunda `procurement.suppliers.read`'in hangi rollere verildiği ölçülmedi (I4).~~ Ölçüldü 2026-10-21 (F14).
- Anahtarın canlı kataloğa fiilen girip girmediği (manifest itmesi çalışma anında) ölçülmedi.
