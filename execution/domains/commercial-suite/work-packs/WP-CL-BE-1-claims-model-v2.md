# WORK PACKAGE — WP-CL-BE-1 · İddialar v2 veri modeli (çekirdek + ülke sürümü + ülke kapatma + kapsama matrisi) (CRM, backend)

> **CT (SoR).** İddialar v2 Faz 1. Kaynak: `SCMM-claims-v2-mockup-analysis-plan.md` (§3 veri modeli, K1–K12, kararlar D1–D5).
>
> **Amaç:** Çok ülkeli iddia yönetiminin **veri ve kural katmanı**: global çekirdek iddia ve yalnız yerel iddia, ülke sürümleri (dil başına metin, uyarlama, kitle daraltma), iddia × ülke hücresinin **gerekçeli kapatılması**, çekirdek yeni sürümü ile ülke sürümlerinin "gözden geçirilmeli" olması, kapsama matrisi okuması.
>
> **Kapsam dışı:** Onay akışı (workflow, CL-BE-4) ve kanıt (MOD-0031, CL-BE-5) **bu WP'de YOK**. Arayüz YOK (CL-FE-*).
>
> **Çalışma yeri:** worktree `C:\tmp\cl-be-1`, dal `wp/cl-be-1`. Commit bu dala, push YOK.

## Kanıt (CT kod okuması)
- **Mevcut Claim** (`CrmService.Domain/Entities/Claim.cs`):
  - ClaimCode (sürümler arası sabit), ClaimName, Description, **ClaimText**, Qualifiers, `ClaimApplicability{ProductRefs, MarketRefs, AudienceRefs (opak metin), EligibilityPolicyId?}`, EvidenceRefs (opak), ComponentRefs, **ClaimVersion (serbest metin)**, Status (`draft/approved/inactive/archived`), EffectiveFrom/To, ApprovedAt/By.
  - Onay gövdeyi dondurur. Silme yok.
- **CQRS:** `Application/Features/ContentComposition/Claims/{ClaimCommands, ClaimCommandHandlers, ClaimDtos, ClaimMapper, ClaimQueries, ClaimQueryHandlers, ClaimPermissions}.cs`.
  - Komutlar: Create, Update, **Approve (doğrudan)**, Archive.
  - İzinler: `crm.claim.read / manage / approve` (AuthService `DataSeeder.cs`'te seed'li).
- **API:** `CrmService.Api/Controllers/CRM/ClaimsController.cs`, `api/crm/content-composition/claims[/{id}[/approve|/archive]]`.
- **Tüketici:** İçerik Seti `ContentSetClaim{ClaimId, ClaimVersion}` (`Domain/Entities/ContentSet.cs:98`). **Bu referans DEĞİŞMEZ.**
- **Referans doğrulama:** `Application/Common/ReferenceValidation/IReferenceDataValidator` + `IReferenceMetadataReader` (değer attribute'ları). Uygulama `Infrastructure/ReferenceValidation/GatewayReferenceDataValidator.cs` **global/tenant scope'u kendisi çözüyor** (global set'e scope_key göndermiyor).
- **Referans setleri** (CL-REF-1 ile gelir; testte mock'lanır):
  - `COUNTRY_CODES` (Global, **mevcut**: TR, BY, UZ, TM, GE, AZ).
  - `country-content-languages` (Global, attribute `Languages`, ör. UZ="uz,ru").
  - `claim-country-closure-reason` (tenant: no-license / regulation-disallows / business-decision).
  - `claim-adaptation-type` (tenant: verbatim / narrowed / softened).
- **MDM ürün:** StrategyTemplate zaten global ürünü MDM'den **fail-closed** okuyor (memory `mod0167-fu04-strategy-template`). Aynı okuyucu yeniden kullanılır.
- **Tuzaklar:**
  - Yeni aggregate `RegisterClassMaps`'e eklenmezse Guid FK'ler binary saklanır ve sorgular **sessizce boş döner** (memory `crm-new-aggregate-classmap-guid`).
  - Class-map AutoMap katıdır: bilinmeyen BSON alanı okumada patlar (memory `crm-classmap-rejects-unknown-elements`).
  - DateTimeOffset dizileri (memory `crm-datetimeoffset-array-pitfalls`).

## Kararlar (kullanıcı, bu WP'yi bağlar)
- **Ülke kısıtı YOK** (D1). `crm.claim.manage` sahibi tüm ülkeleri düzenler.
- **Ülke ekseni = `COUNTRY_CODES`** (bugün 6 ülke). Liste koda gömülmez, her okumada BRD'den gelir.
- **Kapatma:** hücre **tek seçimli neden** ile kapatılır (`claim-country-closure-reason`). Kapatma **geri alınabilir**, tarihçe tutulur.
- **Ruhsat bilgisi** (ruhsat no ve sahibi) YOK. Yalnız kapatma nedeni var (D3).
- **Onay MVP'de sıralı ve workflow ile olacak** (CL-BE-4). Bu WP'de onay yalnız **geçici doğrudan uç** ile yapılır. BE-4'te kaldırılacak.

## NE

### 1. Claim (mevcut entity genişler — geriye uyumlu)
Yeni alanların hepsi opsiyonel. Eski kayıtlar ve mevcut UI çalışmaya devam eder.
- **`Kind`:** `core` | `local` (varsayılan `core`). **`LocalCountryCode`:** yalnız `local` için zorunlu, `COUNTRY_CODES` içinde olmalı.
- **`ProductId`** (Guid, MDM global ürün) + **`ProductDisplay`** snapshot.
  - Yeni oluşturmada `Kind` verilmişse zorunlu. MDM'den fail-closed doğrulanır.
  - Eski `Applicability.ProductRefs` korunur.
- **`AudienceProfileIds`** (Guid listesi). Knowledge AudienceProfile id'leri; varlık doğrulanır. Eski `AudienceRefs` korunur.
- **`ResponsibleOrgUnitId`** (Guid?, bilgi amaçlı). Doğrulanmaz, org birimi seçicisi FE'de.
- **`TextLanguageCode`:** çekirdek için varsayılan `en`; yerel için ülkenin dillerinden biri.
- **Durumlara ekle:** `in-review`, `review-required`.
- **Yeni çekirdek sürüm** `POST claims/{id}/new-version`:
  - Yalnız `approved` olandan açılır.
  - Aynı `ClaimCode` ile yeni kayıt oluşur, `ClaimVersion` **ana sürüm +1** ("1.0" → "2.0"; eski serbest metin sürümlerde ayrıştırılamazsa "2.0").
  - Durum `draft`, `SupersedesClaimId` dolu.
  - Bir `ClaimCode` için aynı anda **en çok bir** draft / in-review.
- **Çekirdek onaylanınca yayılım** (mevcut doğrudan approve'da şimdilik; BE-4'te workflow sonucunda):
  - Eski onaylı çekirdek → `inactive`.
  - O `ClaimCode`'un **onaylı ülke sürümleri** → `review-required`. `ApprovedAt` korunur. Kullanılabilirlik kararı içerik tarafında verilecek.
- **Ülke kapatma** (Claim'e gömülü `CountryClosures`, append-only tarihçe):
  - Kayıt: `{CountryCode, ReasonCode, ClosedBy, ClosedAt, ReopenedBy?, ReopenedAt?, ReopenNote?}`.
  - `POST claims/{id}/country-closures` `{countryCode, reasonCode}`:
    - country `COUNTRY_CODES`'ta, reason setinde geçerli olmalı.
    - Arşivlenmemiş bir ülke sürümü varsa → **409 `country_has_version`**.
    - Zaten kapalıysa → 409.
    - `local` iddiada kendi ülkesi dışındaki ülke kapatılamaz → **400 `not_applicable`**.
  - `POST claims/{id}/country-closures/{countryCode}/reopen` `{note?}`.

### 2. ClaimCountryVersion (YENİ aggregate, koleksiyon `claim_country_versions`)
**Alanlar:**
- Kimlik: `ClaimCode`, `ClaimId` (bağlı çekirdek ya da yerel iddia kaydı), `BoundCoreVersion`, `CountryCode`, `Version` ("1.0", "1.1"…).
- Metin: `Texts[{LanguageCode, Text}]`, `Qualifiers[{LanguageCode, Text}]`.
- Uyarlama: `AdaptationTypeCode`, `AdaptationReason`.
- Kapsam: `AudienceProfileIds`, `ValidFrom` (zorunlu), `ValidTo`.
- Yaşam döngüsü: `Status` (`draft / in-review / approved / review-required / inactive / archived`), `ApprovedAt/By`, `SupersedesVersionId`, `ReviewRounds[]` (**boş kalır**; BE-4 dolduracak), audit alanları.

**Kurallar (backend, fail-closed; hata kodları sabit):**
- **Açma** `POST claims/{id}/country-versions` `{countryCode, texts, qualifiers, adaptationTypeCode, adaptationReason, audienceProfileIds, validFrom, validTo}`:
  - `core` iddia **onaylı değilse** → **409 `core_not_approved`**. `local` iddiada bu kontrol yok, ama country = `LocalCountryCode` olmalı.
  - Hücre **kapalıysa** → **409 `country_closed`** (önce reopen).
  - O ülke için arşivlenmemiş bir sürüm zaten varsa → 409 `country_version_exists` (yeni sürüm için aşağıdaki uç kullanılır).
  - `Texts[].LanguageCode` ⊆ `country-content-languages[country].Languages`. Set ya da attribute yoksa → **400 `reference_set_missing`**. Taslakta en az 1 dil yeterli.
  - `AdaptationTypeCode` sette geçerli olmalı. `verbatim` dışındakilerde `AdaptationReason` zorunlu.
  - `AudienceProfileIds` ⊆ bağlı çekirdeğin `AudienceProfileIds`'i (çekirdek boşsa serbest). Aksi → **400 `audience_not_narrowing`**.
  - `ValidTo` ≥ `ValidFrom`.
- **Güncelleme** yalnız `draft`'ta. Onaylı sürüm kilitlidir → 409.
- **Yeni ülke sürümü** `POST country-versions/{id}/new-version`:
  - Yalnız `approved` ya da `review-required`'dan açılır.
  - Ara sürüm +1 ("1.0" → "1.1"). `BoundCoreVersion` = son onaylı çekirdek sürümü. Durum draft.
  - Bir (ClaimCode, Country) için en çok bir draft / in-review.
- **Geçici doğrudan onay** `POST country-versions/{id}/approve` (`crm.claim.approve`):
  - Ülkenin **tüm dillerinde** metin olmalı.
  - Önceki onaylı sürüm → `inactive`.
  - **BE-4 bunu workflow ile değiştirecek.** Kodda `// TEMPORARY: replaced by WP-CL-BE-4 (workflow)` işareti bırakılır.
- **Arşivle** `POST country-versions/{id}/archive`.

### 3. Okuma modeli
- `GET claims/{id}/country-versions` · `GET country-versions/{id}`.
- **`GET claims/coverage?productId=&kind=&status=&search=`** — kapsama matrisi:
  - **Satır:** her `ClaimCode`'un güncel çekirdek ya da yerel kaydı (kod, ad, ürün, tür, kitle sayısı, çekirdek sürüm/durum).
  - **Sütun:** `COUNTRY_CODES` (BRD'den, sıralı).
  - **Hücre:** `{ state: approved | in-review | draft | review-required | closed | not-opened | not-applicable, versionId?, version?, boundCoreVersion?, closureReasonCode?, isExpiring }`.
  - `isExpiring` türetilmiş alandır, saklanmaz: `ValidTo` ≤ bugün + 60 gün. Eşik ayar ile değişebilir: `Crm:Claims:ExpiringWindowDays`.
  - `local` iddiada kendi ülkesi dışındaki hücreler `not-applicable`.
- Liste DTO'suna (`ClaimDto` / list item) **ek alanlar**: `kind`, `productId/Display`, `audienceProfileIds`, `countrySummary[{countryCode, state}]`. Mevcut alanlar aynen kalır.

### 4. Altyapı
- `ClaimCountryVersion` için **class-map kaydı** (Guid'ler standart subtype), repository (`TenantRepository` deseni), **tenant-first indeksler**:
  - `{TenantId, ClaimCode, CountryCode, Status}`
  - `{TenantId, ClaimId}`
- Yeni alanlar Claim class-map'ine eklenir. Eski dokümanlar (alan yok) sorunsuz okunur (test).
- Audit publisher (`IContentCompositionAuditPublisher`) yeni eylemler için olay yazar: `claim_country_version_created / updated / approved / archived`, `claim_country_closed / reopened`, `claim_new_version_created`, `claim_country_versions_review_required`. **Metin yok**, yalnız id, kod ve ülke (PII-safe).
- **İzinler:** mevcut `crm.claim.read / manage / approve`. Yeni anahtar YOK.
- `ClaimsController`'a yeni uçlar (thin, MediatR). Aynı route öneki. Gateway'de mevcut `/api/crm/…` wildcard'ı kapsıyor mu doğrula.

## KORU / YAPMA
- **Mevcut Claim uçları ve DTO alanları geriye uyumlu** (yalnız ekleme). Mevcut İddialar UI ve Diten.Web.Tests bozulmaz.
- **İçerik Seti `ContentSetClaim` DEĞİŞMEZ.** Ülke sürümüne referans içerik tarafının işidir (CL-BE-6).
- **Workflow ve MOD-0031 entegrasyonu YOK.** Kanıt sayısı ve "≥1 kanıt" kuralı YOK (BE-5). ReviewRounds boş kalır.
- **Ülke kısıtı YOK.** Ruhsat no ve sahibi alanı YOK.
- Ülke ve dil listeleri **koda gömülmez**. BRD yoksa fail-closed 400; sessiz varsayılan yok.
- Frontend, Platform, Auth DOKUNMA. Test dışında seed/fixture verisi YOK.
- **DUR:**
  - MDM ürün okuyucusu CRM'de yeniden kullanılabilir değilse → ürün doğrulamasını atlama. Dur ve raporla.
  - `IReferenceMetadataReader` global setin attribute'unu okuyamıyorsa → dur ve raporla.
  - Eski `ClaimVersion` değerleri ayrıştırılamazsa → "2.0" varsayımıyla devam et ve raporla.

## Acceptance
- **E2:**
  - `dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo` → **0 kırmızı** (taban 1935/0/5 + yeni testler).
  - `dotnet test frontend/Diten.Web.Tests -c Release --nologo` → **229/0** (UI bozulmadı).
- **Yeni testler:**
  - core onaysız → `core_not_approved`
  - kapalı hücre → `country_closed`
  - sürüm varken kapatma → `country_has_version`
  - reopen sonrası açılabilir
  - `local` iddia: yalnız kendi ülkesi, diğerleri not-applicable
  - dil ⊆ ülke dilleri; set yok → `reference_set_missing`
  - uyarlama nedeni kuralı
  - kitle daraltma → `audience_not_narrowing`
  - onaylı sürüm kilitli; yeni ülke sürümü 1.0 → 1.1 ve bağlı çekirdek güncel
  - geçici approve: tüm diller zorunlu, önceki onaylı inactive
  - çekirdek yeni sürüm 1.0 → 2.0; onayda ülke sürümleri review-required, eski çekirdek inactive
  - matris: 6 ülke sütunu BRD'den, hücre durumları, `isExpiring`
  - eski Claim dokümanı yeni alanlar olmadan okunuyor
  - Guid'ler class-map'le doğru sorgulanıyor (boş dönmüyor)
  - kiracı izolasyonu
  - audit olaylarında metin yok
- **Diff:** yalnız `services/Diten.CrmService/**`. İçerik Seti, Platform, Auth, Web ve gateway diff'i YOK. Gateway kapsamı yoksa yalnız rapor.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-specialist.md]
WP: WP-CL-BE-1 · İddialar v2 veri modeli (çekirdek + ülke sürümü + ülke kapatma + kapsama matrisi) (CRM, backend)
Repository: C:\tmp\cl-be-1 (worktree) · Branch: wp/cl-be-1 (taban feature/crm-claims-v2) · commit bu dala, push YOK

Amaç: Çok ülkeli iddia yönetiminin veri+kural katmanı — çekirdek/yerel iddia, ülke sürümleri (dil başına metin, uyarlama, kitle daraltma), iddia×ülke hücresinin gerekçeli (tek seçim) kapatılması/geri açılması, çekirdek yeni sürümünde ülke sürümlerinin review-required olması, kapsama matrisi. Workflow (BE-4), kanıt (BE-5), UI YOK.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CL-BE-1-claims-model-v2.md (Kararlar + NE §1–4 + hata kodları) · …/SCMM-claims-v2-mockup-analysis-plan.md §3 · services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/{Claim,ContentSet}.cs · Application/Features/ContentComposition/Claims/*.cs · Api/Controllers/CRM/ClaimsController.cs · Application/Common/ReferenceValidation/{IReferenceDataValidator,IReferenceDataCatalogReader}.cs · Infrastructure/ReferenceValidation/GatewayReferenceDataValidator.cs · Persistence/DependencyInjection.cs (RegisterClassMaps) · StrategyTemplate'in MDM ürün okuyucusu · memory: crm-new-aggregate-classmap-guid, crm-classmap-rejects-unknown-elements, crm-datetimeoffset-array-pitfalls, mod0167-fu04-strategy-template.

NE:
 1) Claim genişlet (geriye uyumlu, alanlar opsiyonel): Kind core|local (+LocalCountryCode), ProductId+ProductDisplay (MDM fail-closed), AudienceProfileIds, ResponsibleOrgUnitId, TextLanguageCode; durumlara in-review + review-required; POST claims/{id}/new-version (approved'dan, ana sürüm +1, tek draft/in-review); çekirdek onayında eski çekirdek inactive + o ClaimCode'un onaylı ülke sürümleri review-required; CountryClosures (append-only): POST claims/{id}/country-closures {countryCode∈COUNTRY_CODES, reasonCode∈claim-country-closure-reason} (sürüm varsa 409 country_has_version, local'de başka ülke 400 not_applicable) + .../{countryCode}/reopen.
 2) ClaimCountryVersion (yeni aggregate, claim_country_versions) + uçlar: POST claims/{id}/country-versions (core onaysız 409 core_not_approved; kapalı 409 country_closed; var 409 country_version_exists; diller ⊆ country-content-languages.Languages, set yoksa 400 reference_set_missing; adaptation kodu geçerli + verbatim dışı gerekçe zorunlu; kitle ⊆ çekirdek kitlesi yoksa 400 audience_not_narrowing; ValidTo≥ValidFrom) · PUT (yalnız draft) · POST country-versions/{id}/new-version (ara +1, BoundCoreVersion güncel) · POST country-versions/{id}/approve GEÇİCİ (tüm ülke dilleri zorunlu, önceki onaylı inactive; kodda "TEMPORARY: replaced by WP-CL-BE-4") · archive.
 3) Okuma: GET claims/{id}/country-versions, GET country-versions/{id}, GET claims/coverage (satır=ClaimCode güncel kaydı, sütun=COUNTRY_CODES BRD'den, hücre state/version/boundCoreVersion/closureReasonCode/isExpiring [ValidTo ≤ bugün+Crm:Claims:ExpiringWindowDays=60]); liste DTO'ya kind, product, audienceProfileIds, countrySummary ekle.
 4) Class-map kaydı (Guid standart), tenant-first indeksler, audit olayları (metinsiz), mevcut crm.claim.* izinleri, gateway kapsamını doğrula.
KORU/YAPMA: mevcut Claim uçları/DTO geriye uyumlu (yalnız ekleme), mevcut İddialar UI bozulmaz; ContentSetClaim DEĞİŞMEZ; workflow/MOD-0031/kanıt kuralı YOK (ReviewRounds boş); ülke kısıtı YOK; ruhsat no/sahibi YOK; ülke/dil listesi koda gömülmez (BRD yoksa fail-closed 400); Frontend/Platform/Auth DOKUNMA; seed YOK.
DOĞRULA (E2): cd C:\tmp\cl-be-1; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo → 0 kırmızı (taban 1935/0/5 + yeni); dotnet test frontend/Diten.Web.Tests -c Release --nologo → 229/0; yeni testler WP Acceptance listesindeki gibi; git diff yalnız services/Diten.CrmService/**. Commit ("feat(crm): WP-CL-BE-1 — claims v2 model (core/local, country versions, country closures, coverage matrix)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: MDM ürün okuyucusu yeniden kullanılamıyorsa ürün doğrulamasını ATLAMA, DUR+raporla; IReferenceMetadataReader global set attribute'unu okuyamıyorsa DUR+raporla; eski ClaimVersion ayrıştırılamazsa "2.0" varsay+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-28) → **ACCEPTED (E2)**
```
Commit: b3672c9c · Agent: PASS (CRM 1974/0/5, Web 229/0, 4 sabotaj kanıtı) · CT: ACCEPTED E2 · worktree C:\tmp\cl-be-1 → CRM 1974/0/5 · Web 229/0
```
- ✅ **Kapsam:** 19 dosya, +3284/−31, **yalnız `services/Diten.CrmService/**`**. ContentSetClaim, Platform, Auth, Web, gateway diff YOK.
- ✅ **Hata kodları:** sabitler `ClaimV2Support.cs`'te (core_not_approved, country_closed, country_version_exists, country_has_version, not_applicable, reference_set_missing, audience_not_narrowing).
- ✅ **BRD:** set kodları `COUNTRY_CODES` / `country-content-languages` / `claim-country-closure-reason` / `claim-adaptation-type` koda gömülü DEĞİL (yalnız set adları sabit). Set yoksa fail-closed 400.
- ✅ **Class-map (Guid tuzağı):**
  - `ClaimCountryVersion`: ClaimId, SupersedesVersionId, AudienceProfileIds, ReviewRound.WorkflowInstanceId → stringGuid.
  - `Claim`: ProductId, AudienceProfileIds, ResponsibleOrgUnitId, SupersedesClaimId → stringGuid.
  - İndeksler tenant-first.
- ✅ **Geçici onay** `TEMPORARY: replaced by WP-CL-BE-4` işaretli (controller + komut + handler).
- ✅ **Gateway:** mevcut `/api/crm/content-composition/{everything}` kapsıyor, değişiklik yok.
- ➕ **Agent kararları (kabul):**
  - Kapatmalar ClaimCode düzeyinde: yeni çekirdek sürüme kopyalanıyor.
  - Ülke sürümü uçları `claims/country-versions/{id}` altında.
  - Geçici onayda bağlı çekirdek onaylı değilse `core_not_approved`.
  - Liste `countrySummary`'si BRD'ye gitmiyor (tam görünüm matriste).
- ⚠ **Canlı:** CL-REF-1 birleşip Platform restart olmadan ülke sürümü / kapatma / matris uçları `reference_set_missing` döner. Bu beklenen davranış.
- ⚠ **BE-4 notu:** Çekirdek onayındaki yayılım şimdilik doğrudan onay ucunda; BE-4'te workflow sonucuna taşınacak.
