# WORK PACKAGE — WP-BRD-TENANT-CRM-SETS · CRM referans setlerinin her kiracı rolü için okunması ve doğrulanması (mobil + Web)

> **CT (SoR), 2026-10-02.**
> - **Talep:** mobil ekip, `BACKEND-MOD-0048-CRM-REFERENCE-SETS-REQUIREMENTS.md` (2026-10-01, `origin/main` `819cfe5c`): R1 / R2 / R3, kabul senaryoları 1-14. Android Phase 5.30 Customer Create/Edit `BLOCKED_BY_BACKEND`.
> - **Kullanıcı (2026-10-02):** ana daldan aç ve paketle; **kapsam CRM geneline genişletilsin** — Web'de Admin olmayan her rol aynı sorunu yaşıyor.
> - **Dal:** `fix/brd-tenant-crm-sets` (`origin/main` `819cfe5c`'den), worktree `C:\tmp\brd-crm-sets`. Commit bu dala, push YOK (CT doğrular, push eder; PR kullanıcı açar). `main`'e commit YOK.
> - **Modüller:** MOD-0048 (BRD, Platform) + CRM referans doğrulaması (tüm özellikler).

## Sorun (CT kod doğrulaması, `819cfe5c`)
1. **Kiracı okuma yolu dar:** `Diten.Platform.API/Controllers/TenantReferenceDataController.cs` — `GET api/lookups/reference-data/sets/{setCode}/published-values`, `[LoginOnly]`, izin listesi yalnız `legal-form`, `country`, `base-currency` (:34-35); okuma **referans kiracıda** (stopgap, `scope_key` null).
2. **Genel tüketici yolu Platform izni istiyor:** `api/v1/reference-data/sets/{setCode}/published-values` → `Platform.BusinessReferenceData.Consumer.Read` (seed: yalnız 97c5 Admin).
3. **CRM'in bütün referans okumaları kullanıcının token'ıyla bu yola gidiyor:** `Diten.CrmService.Infrastructure/ReferenceValidation/GatewayReferenceDataValidator.cs` (`IReferenceDataValidator`, `IReferenceMetadataReader`, `IReferenceDataCatalogReader`) — `?scope_key=<tenant>` + global set için tek retry, `X-Tenant-Id` + `Authorization` iletilir; 404 / başarısız → `SetMissing`.
   - **Etkilenenler (CT taraması, 40 dosya):** hesap (`account-type`, `account-status`, `account-category`), kişi (`contact-type`, `contact-status`, `country`, `city`, `district`, `professional-title`, `medical-specialty`, `department-type`, `gender`), hesap ilişkisi (`account-relationship-type` / `-status`), hesap-kişi (`contact-role`), kişi içe aktarma (`phone-country-code`, `preferred-language`), iddialar (`ClaimReferenceSets`: `COUNTRY_CODES`, `country-content-languages`, kapanış nedeni, uyarlama tipi …), kampanya / dönem / strateji şablonu / bölge kapsamı (`COUNTRY_CODES`, `business-unit`), kişi uygunluğu (`ContactAvailabilityReferenceSets`), dönem kapasitesi, segment özniteliği `reference-set` kaynağı.
   - **Sonuç:** Admin olmayan kullanıcı için zorunlu setli kayıtlar reddedilir; **opsiyonel setlerde değer doğrulanmadan kabul edilir** (`SetMissing` tolere); seçenek / kapsam uçları boş döner. Mobilde **ve Web'de**.
4. Mevcut `sets/{setCode}` yolunu genişletmek yanlış: o yol her seti referans kiracıda okur; CRM setleri kiracıya özel (izolasyon ihlali) ve `country` orada Global sayılıyor, CRM ise bugün setin kendi kapsamına göre okuyor (memory `country-vocabulary-divergence`).

## Karar (CT)
- **Yeni, ayrı uç:** `GET api/lookups/reference-data/consumable-sets/{setCode}/published-values`.
  - **Davranış = genel tüketici yolunun bugün CRM'e verdiği cevap, Platform izni olmadan:** setin **kendi kapsam tipine** (BRD set metadata `ScopeType`) göre okunur — `Tenant` → çağıranın kiracısı, `scope_key` = sunucunun çözdüğü kiracı; `Global` → global okuma (mevcut stopgap ile aynı referans kiracı kaynağı). Kapsam istemciden alınmaz; kod içinde "hangi set global" listesi YOK.
  - **İzin listesi Platform'da, yapılandırmada:** `BusinessReferenceData:ConsumableSets` (appsettings, varsayılan değerlerle) — CRM'in tükettiği setlerin tamamı. Katalog açılmaz: liste dışı → 404 `reference_set_not_tenant_accessible`.
  - **Kayma koruması:** CRM'in kodda kullandığı her set kodu Platform listesinde olmalı — kaynak tarayan bir test (repo içi; mevcut "kaynaktan sabit okuma" testleri deseni). Segment `reference-set` kaynağındaki setler: kayıtlı öznitelik kataloğundan çıkarılır; dinamik ise CRM fallback'i (aşağıda) güvenlik ağıdır ve raporlanır.
  - Mevcut `sets/{setCode}` ucu ve 3 Global set **aynen** kalır (Legal Entity sihirbazı).
- **CRM doğrulayıcısı:** bütün okumalar (`ValidateAsync`, `GetValueAttributesAsync`, katalog okuma) **önce `consumable-sets`**; 404 + `reference_set_not_tenant_accessible` gelirse bugünkü genel yola (scope_key + global retry) düşer, set kodu süreç içi önbellekte işaretlenir. CRM'de izin listesi kopyası yok.
- **Servisler arası kimlik** (seçenek b) bu pakette değil: altyapı yok (CT arama: 0 sonuç); ayrı mimari karar.
- **R3:** yanıt şeması, yayın / yönetişim akışı, Platform izinlerinin rollere dağılımı DEĞİŞMEZ.

## NE
1. **Platform — `consumable-sets` ucu:**
   - `[Authorize]` + `[LoginOnly]`; **kiracı bağlamı zorunlu** (kiracısız / platform düzeyi aktör → belgelenen 400 ya da 403 `tenant_context_required`); kiracı token / doğrulanmış bağlamdan, istemci `scope_key`'i yok sayılır.
   - Set kodu normalize (`Trim`, `OrdinalIgnoreCase`) → izin listesi → set metadata'sından kapsam → okuma (mevcut `GetBusinessReferenceDataPublishedValuesQuery` / tüketici servisi; paralel sistem yok).
   - Kiracıda yayında sürüm yok / set yok / emekli → 404 `reference_set_not_published` (500 asla).
   - Yalnız yayında, deprecated olmayan, bugün geçerli değerler; `SortOrder` sırası.
   - Gateway: mevcut `/api/lookups/{everything}` rotası (değişiklik gerekirse raporla).
2. **Platform yapılandırması:** `BusinessReferenceData:ConsumableSets` — CRM envanteri (yukarıdaki §Sorun 3 + ajanın tam taraması). Boş / eksik yapılandırma → kod içi varsayılan liste (aynı içerik); belgelenmiş.
3. **CRM — `GatewayReferenceDataValidator`:** ayar `ReferenceData:ConsumableSetsPathTemplate` (varsayılan `/api/lookups/reference-data/consumable-sets/{setCode}/published-values`); sıra consumable → (liste dışı) → mevcut yol; önbellek; `reference_set_not_published` → `SetMissing`.
4. **Kayma testi:** CRM kaynağındaki set kodu sabitleri (`*ReferenceSets`, `*ReferenceValidation`, `ClaimReferenceSets`, içe aktarma şemaları vb.) ⊆ Platform `ConsumableSets`. Yeni bir CRM seti listede yoksa test kırmızı.
5. **Belge:** mobil sözleşme (§Sözleşme) + MOD-0048 module pack'e not (yeni uç, yapılandırma, kayma testi).

## KORU / YAPMA
- `Platform.BusinessReferenceData.Consumer.Read` kiracı rollerine **verilmez**; RBAC / seed / veri yazılmaz.
- Mevcut `sets/{setCode}` ucu ve onun Global listesi / referans kiracı okuması DEĞİŞMEZ.
- CRM doğrulama **anlamı** değişmez: Admin için bugün dönen değerler, Admin olmayan için de aynı (karşılaştırma testi).
- HCM'in aynı desendeki istemcisi (`Diten.HcmService.Infrastructure/ReferenceValidation/GatewayReferenceValidationClient.cs`) bu pakette DEĞİŞMEZ → takip F-2.
- **DUR:** kiracı ara katmanı kiracıyı yalnız `X-Tenant-Id` başlığından (token doğrulaması olmadan) alıyorsa; set metadata'sından kapsam tipi güvenilir okunamıyorsa; segment `reference-set` kaynağı keyfi (kullanıcı tanımlı) set kodu kabul ediyorsa (izin listesi kavramı tartışılmalı) → raporla.

## Sözleşme (mobil)
```
GET api/lookups/reference-data/consumable-sets/{setCode}/published-values
Authorization: Bearer <kiracı kullanıcı token'ı>
X-Tenant-Id: <kiracı>
Mobilin ilk ihtiyacı: account-type, account-status, account-category, contact-type, contact-status
(CRM'in tükettiği diğer setler de aynı yoldan; tam liste Platform yapılandırmasında).
scope_key istemciden gönderilmez.
200 → Response<BusinessReferenceDataPublishedValuesModel>: data.setCode, data.versionNumber, data.publishedAt,
      data.items[{code, label, description, isActive, sortOrder, attributes}]
      items[].code = create/update'te gönderilecek değer (CRM büyük/küçük harf duyarsız); items[].label = gösterim.
401 oturum yok · 400 kiracı yok · 404 reference_set_not_tenant_accessible (liste dışı)
404 reference_set_not_published (bu kiracıda yayında değil) · 500 yok
```

## Takip (bu paket DIŞI)
- **F-2:** HCM aynı deseni kullanıyor (kullanıcı token'ı + genel tüketici yolu) → Admin olmayan HCM kullanıcıları için aynı sorun; ayrı paket (aynı uç + HCM listesi).
- **F-3:** servisler arası kimlik (izin listesi bakımını kaldırır) — mimari karar.

## Acceptance
- **E2:** Platform testleri 0 **yeni** kırmızı (taban ölç; memory ~173 ortam kaynaklı kırmızı — yalnız fark), CRM 0 kırmızı (taban ölç), Web 0 kırmızı, build 0 hata.
  - **Platform** (`MongoIntegrationHarness` + API): mobil §9 1-11 ve 14 — beş mobil seti Platform izni olmayan kullanıcı okur; **Tenant** kapsamlı set çağıranın kiracısından, **Global** kapsamlı set (`COUNTRY_CODES`) global kaynaktan; A kiracısı B'nin ve referans kiracının kiracı setlerini almaz (iki kiracıya farklı değer yayınla); liste dışı 404; mevcut `sets/{setCode}` + 3 Global set aynı (regresyon); oturumsuz 401; kiracısız aktör belgelenen yanıt; taslak / gönderilmiş / deprecated dönmez; yayında olmayan / emekli 404 `reference_set_not_published`; normal kullanıcı genel tüketici yolunda hâlâ 403; `ACCOUNT-TYPE` büyük harf.
  - **CRM:** doğrulayıcı consumable'ı önce çağırır; liste dışı → genel yol + önbellek (ikinci çağrıda tek istek); `reference_set_not_published` → `SetMissing`; **kayma testi** (CRM set sabitleri ⊆ Platform listesi); mevcut doğrulayıcı / özellik testleri yeşil.
  - **Uçtan uca (§9 12-13):** harness'ta mümkünse; değilse CT E4.
  - **Sabotaj:** (1) consumable ucunda Tenant seti referans kiracıda oku → izolasyon testi kırmızı; (2) Platform listesinden bir CRM seti çıkar → kayma testi kırmızı; (3) CRM fallback'i kaldır → liste dışı set doğrulama testi kırmızı.
- **E4 (CT, fleet):** 97c5'te **Admin olmayan** bir kullanıcıyla (kullanıcı giriş yapar): mobil beş seti oku; Web'den hesap + kişi oluştur / güncelle; bir iddia ülke sürümü ve bir kampanya kapsamı kaydet.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-BRD-TENANT-CRM-SETS · CRM referans setlerinin her kiracı rolü için okunması ve doğrulanması (mobil + Web)
Repository: C:\tmp\brd-crm-sets (worktree) · Branch: fix/brd-tenant-crm-sets (origin/main 819cfe5c'den) · commit bu dala, push YOK, main'e commit YOK · Platform (yeni consumable-sets ucu + yapılandırma) + CrmService (GatewayReferenceDataValidator + kayma testi)

Paket belgesi: execution/domains/platform-shared-services/work-packs/WP-BRD-TENANT-CRM-SETS-mobile-reference-sets.md — önce tamamını oku (Sorun / Karar / NE / KORU / Sözleşme / Acceptance). Ayrıca: execution/domains/platform-shared-services/module-packs/MOD-0048-lookups-reference-data.md · services/Diten.Platform/src/Diten.Platform.API/Controllers/{TenantReferenceDataController, BusinessReferenceDataController}.cs · Platform BusinessReferenceData Application (GetBusinessReferenceDataPublishedValuesQuery, IBusinessReferenceDataConsumerQueryService, set metadata ScopeType) · Platform kiracı ara katmanı / TenantScope · services/Diten.CrmService/src/Diten.CrmService.Infrastructure/ReferenceValidation/GatewayReferenceDataValidator.cs · CRM'de IReferenceDataValidator / IReferenceMetadataReader / IReferenceDataCatalogReader kullanan 40 dosya (set kodu envanteri) · gateway ocelot /api/lookups rotası · memory tenant-scoped-token-multi-tenant-login, country-vocabulary-divergence, gateway-404-empty-body-signature, platform-partial-index-ne-crash.

NE: (1) Platform GET api/lookups/reference-data/consumable-sets/{setCode}/published-values: [Authorize]+[LoginOnly], kiracı bağlamı zorunlu (kiracısız → belgelenen 400/403 tenant_context_required; platform-admin token'ı kiracı verisi okumaz), istemci scope_key yok sayılır; set kodu normalize → izin listesi (yapılandırma BusinessReferenceData:ConsumableSets + aynı içerikli kod varsayılanı) → liste dışı 404 reference_set_not_tenant_accessible → set metadata ScopeType: Tenant → çağıranın kiracısı (scope_key = sunucunun çözdüğü kiracı), Global → global okuma (mevcut stopgap kaynağı); kodda "global set" listesi YOK; mevcut published-values sorgusu/servisi; yayında değil/set yok/emekli → 404 reference_set_not_published (500 asla); mevcut sets/{setCode} ucu DEĞİŞMEZ. (2) İzin listesi = CRM'in tükettiği TÜM setler (tam tarama; WP §Sorun 3 başlangıç listesi). (3) CRM GatewayReferenceDataValidator: ayar ReferenceData:ConsumableSetsPathTemplate; ValidateAsync + GetValueAttributesAsync + katalog okuma önce consumable-sets, 404+reference_set_not_tenant_accessible → mevcut genel yol (scope_key + global retry) + set kodu önbelleği; reference_set_not_published → SetMissing; CRM'de liste kopyası YOK. (4) Kayma testi: CRM set kodu sabitleri ⊆ Platform ConsumableSets. (5) Mobil sözleşme + MOD-0048 module pack notu.
KORU/YAPMA: Platform.BusinessReferenceData.Consumer.Read kiracı rollerine VERİLMEZ, RBAC/seed/veri yazılmaz; mevcut uç + Global listesi değişmez; CRM doğrulama anlamı değişmez (Admin ile aynı değerler); HCM DOKUNMA (F-2); gateway değişikliği gerekirse raporla.
DOĞRULA (E2): Platform testleri (tabanı ölç; ~173 ortam kaynaklı kırmızı — yalnız FARK) → 0 yeni kırmızı; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo → 0 kırmızı (taban ölç); dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı; build 0 hata. Yeni testler WP Acceptance (mobil §9 1-11, 14 + Tenant/Global kapsam + CRM fallback/önbellek + kayma testi; 12-13 harness'ta mümkünse). Sabotaj: (1) Tenant seti referans kiracıda oku → izolasyon kırmızı; (2) listeden bir CRM seti çıkar → kayma kırmızı; (3) CRM fallback'i kaldır → liste dışı set testi kırmızı. Commit ("fix(brd): WP-BRD-TENANT-CRM-SETS — consumable reference sets route for every tenant role + CRM validator switch + drift guard" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: kiracı yalnız X-Tenant-Id başlığından (token doğrulamasız) alınıyorsa; set metadata'sından kapsam güvenilir okunamıyorsa; segment reference-set kaynağı keyfi set kodu kabul ediyorsa → DUR + raporla.
```
