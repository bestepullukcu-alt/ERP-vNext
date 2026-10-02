# WORK PACKAGE — WP-BRD-TENANT-CRM-SETS · Kiracı kullanıcısı için CRM referans setleri (okuma + CRM doğrulaması)

> **CT (SoR), 2026-10-02.**
> - **Talep:** mobil ekip, `BACKEND-MOD-0048-CRM-REFERENCE-SETS-REQUIREMENTS.md` (2026-10-01, `origin/main` `819cfe5c`): R1 / R2 / R3, kabul senaryoları 1-14. Android Phase 5.30 Customer Create/Edit `BLOCKED_BY_BACKEND`.
> - **Kullanıcı (2026-10-02):** ana daldan aç ve paketle.
> - **Dal:** `fix/brd-tenant-crm-sets` (`origin/main` `819cfe5c`'den), worktree `C:\tmp\brd-crm-sets`. Commit bu dala, push YOK (CT doğrular, push eder; PR kullanıcı açar). `main`'e commit YOK.
> - **Modüller:** MOD-0048 (BRD, Platform) + CRM referans doğrulaması.

## Sorun (CT kod doğrulaması, `819cfe5c`)
1. **Okuma yolu kiracıya kapalı:** `Diten.Platform.API/Controllers/TenantReferenceDataController.cs` — `GET api/lookups/reference-data/sets/{setCode}/published-values`, `[LoginOnly]`, izin listesi yalnız `legal-form`, `country`, `base-currency` (:34-35); okuma **referans kiracıda** (`TenantScope.Begin(_tenantContext, referenceTenantId)`, `scope_key` null).
2. **Genel tüketici yolu Platform izni istiyor:** `api/v1/reference-data/sets/{setCode}/published-values` → `Platform.BusinessReferenceData.Consumer.Read` (seed: yalnız 97c5 Admin).
3. **CRM doğrulaması kullanıcının token'ıyla gidiyor:** `Diten.CrmService.Infrastructure/ReferenceValidation/GatewayReferenceDataValidator.cs` — yol `ReferenceData:PublishedValuesPathTemplate` (varsayılan genel tüketici yolu), `?scope_key=<tenant>` + global set için tek retry (:56-77), `X-Tenant-Id` + `Authorization` iletilir; 404 / başarısız → `SetMissing` → zorunlu alanda kayıt reddedilir. **Sonuç:** Admin olmayan kullanıcı hesap / kişi oluşturamaz — mobilde ve **Web'de**.
4. **Neden mevcut yola eklemek yanlış:** o yol referans kiracıda okur; CRM setleri kiracıya özel → başka kiracının değerleri döner (izolasyon ihlali). Ayrıca `country` bu yolda "Global / referans kiracı" sayılıyor, CRM ise `country`'yi çağıranın kiracısında doğruluyor (memory `country-vocabulary-divergence`): aynı yolu CRM doğrulamasına bağlamak `country` anlamını değiştirir.

## Karar (CT)
- **R1 — AYRI yol** (mobil belge §5 R1 "alternatif ayrı yol kabul"): `GET api/lookups/reference-data/tenant-sets/{setCode}/published-values`.
  - İzin listesi (tek kaynak, Platform): `account-type`, `account-status`, `account-category`, `contact-type`, `contact-status` (`OrdinalIgnoreCase`, normalize sonrası kontrol).
  - Okuma **çağıranın kiracısında**; `scope_key` = sunucunun çözdüğü kiracı (istemciden alınmaz, gönderilirse yok sayılır ya da 400 — belgele). Mevcut `GetBusinessReferenceDataPublishedValuesQuery` / tüketici servisi kullanılır; paralel sistem yok.
  - Mevcut `sets/{setCode}` yolu ve 3 Global set **aynen** kalır.
  - Mevcut `/api/lookups/{everything}` gateway rotası altında → gateway değişikliği yok (doğrula).
- **R2 — seçenek (a):** CRM `GatewayReferenceDataValidator` önce `tenant-sets` yolunu dener; **404 + `reference_set_not_tenant_accessible`** gelirse bugünkü genel tüketici yoluna (scope_key + global retry) düşer.
  - İzin listesi CRM'de **kopyalanmaz** (Platform tek kaynak). Erişilemez cevabı set kodu başına süreç içi önbelleğe alınır (her doğrulamada çift istek olmasın).
  - Servisler arası kimlik altyapısı yok (CT arama: 0 sonuç) → (b) bu pakette değil.
- **R3:** yanıt şeması (`BusinessReferenceDataPublishedValuesModel`, `Response<T>`), yayın / yönetişim akışı, Global izin listesi, Platform izinlerinin rollere dağılımı DEĞİŞMEZ.

## NE
1. **Platform — yeni uç** (`TenantReferenceDataController` içinde ya da yanında):
   - `[Authorize]` + `[LoginOnly]`; **kiracı bağlamı zorunlu**: kiracısız (platform düzeyi) aktör → 400 (mevcut kiracı ara katmanı davranışı) ya da 403 `tenant_context_required` — hangisi ise belgele. Platform-admin token'ı kiracı verisi okumamalı (kiracı yalnız token / doğrulanmış bağlamdan).
   - İzin listesi dışı → 404 `reference_set_not_tenant_accessible`.
   - Kiracıda yayında sürüm yok / set yok / emekli → **404 `reference_set_not_published`** (500 asla).
   - Yalnız yayında, deprecated olmayan, bugün geçerli değerler; `SortOrder` sırası (mevcut servis davranışı).
2. **CRM — `GatewayReferenceDataValidator`:**
   - yeni ayar `ReferenceData:TenantSetsPathTemplate` (varsayılan `/api/lookups/reference-data/tenant-sets/{setCode}/published-values`);
   - sıra: tenant-sets → (404 + `reference_set_not_tenant_accessible`) → mevcut yol; erişilemez set kodları önbellekte;
   - `ValidateAsync`, `GetValueAttributesAsync` ve katalog okuma (`IReferenceDataCatalogReader`) **aynı** sırayı kullanır;
   - `reference_set_not_published` → `SetMissing` (bugünkü 404 anlamı).
3. **Belge:** mobil sözleşme notu bu WP'nin §Sözleşme bölümünde (CT mobil ekibe iletir); MOD-0048 module pack'e kısa not (yeni uç + izin listesi).

## KORU / YAPMA
- `Platform.BusinessReferenceData.Consumer.Read` kiracı rollerine **verilmez**; RBAC'a hiçbir şey yazılmaz.
- Mevcut `sets/{setCode}` ucu, Global izin listesi, referans kiracı okuması DEĞİŞMEZ.
- CRM'de izin listesi kopyası yok. Diğer CRM doğrulamaları (iddia, kampanya, dönem, bölge, içe aktarma) davranış olarak aynı kalır (tenant-sets'e düşmeyen setler bugünkü yoldan).
- Veri / seed değişikliği yok.
- **DUR:** kiracı ara katmanı kiracıyı yalnız `X-Tenant-Id` başlığından (token doğrulaması olmadan) alıyorsa → izolasyon riski, raporla; tüketici servisi kiracı setini `scope_key` olmadan okuyamıyor ve sunucu tarafında güvenli veremiyorsa → raporla.

## Sözleşme (mobil)
```
GET api/lookups/reference-data/tenant-sets/{setCode}/published-values
Authorization: Bearer <kiracı kullanıcı token'ı>
X-Tenant-Id: <kiracı>
setCode ∈ { account-type, account-status, account-category, contact-type, contact-status }
scope_key istemciden gönderilmez.
200 → Response<BusinessReferenceDataPublishedValuesModel>: data.setCode, data.versionNumber, data.publishedAt,
      data.items[{code, label, description, isActive, sortOrder, attributes}]
      items[].code = create/update'te gönderilecek değer (CRM büyük/küçük harf duyarsız); items[].label = gösterim.
401 oturum yok · 400 kiracı yok · 404 reference_set_not_tenant_accessible (liste dışı)
404 reference_set_not_published (bu kiracıda yayında değil) · 500 yok
Global setler (legal-form, country, base-currency) bugünkü sets/{setCode} yolunda, değişmeden.
```

## Takip (bu paket DIŞI — CT notu)
- **F-1:** CRM'in diğer referans doğrulamaları (iddia `country-content-languages`, kişi opsiyonel setleri, kampanya / dönem / bölge kapsamı, içe aktarma) Admin olmayan kullanıcı için hâlâ genel tüketici yoluna (Platform izni) bağlı. Opsiyonel setlerde `SetMissing` tolere edildiği için değer **doğrulanmadan** kabul edilebiliyor. Çözüm: izin listesini ihtiyaçla genişletmek ya da servisler arası kimlik — ayrı karar.

## Acceptance
- **E2:** Platform testleri 0 yeni kırmızı (taban: ajan ölçer; memory: ~173 ortam kaynaklı kırmızı taban — farkı raporla), CRM testleri 0 kırmızı (taban ajan ölçer), Web testleri 0 kırmızı, build 0 hata.
  - **Platform testleri** (`MongoIntegrationHarness` + API): mobil §9 senaryo 1-11 ve 14 — dört set + `account-category` 200 (Platform izni olmayan kullanıcı); A kiracısı B'nin ve referans kiracının değerlerini almaz (iki kiracıya farklı değer yayınla); liste dışı 404; Global setler aynı (regresyon); oturumsuz 401; kiracısız aktör belgelenen yanıt; taslak / gönderilmiş sürüm ve deprecated değer dönmez; yayında olmayan / emekli set 404 `reference_set_not_published`; normal kullanıcı genel tüketici yolunda hâlâ 403; `ACCOUNT-TYPE` büyük harf.
  - **CRM testleri:** doğrulayıcı tenant-sets'i önce çağırır; 404 + `reference_set_not_tenant_accessible` → genel yola düşer ve set kodu önbelleğe alınır (ikinci doğrulamada tek istek); `reference_set_not_published` → `SetMissing`; mevcut doğrulayıcı testleri yeşil.
  - **Uçtan uca (§9 12-13):** harness'ta mümkünse; değilse CT E4.
  - **Sabotaj:** (1) yeni uçta okumayı referans kiracıya çevir → izolasyon testi kırmızı; (2) CRM'de fallback'i kaldır → genel set doğrulama testi kırmızı.
- **E4 (CT, fleet):** 97c5'te Admin **olmayan** bir kullanıcıyla (kullanıcı giriş yapar) dört seti oku + Web'den hesap oluştur / güncelle.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-BRD-TENANT-CRM-SETS · Kiracı kullanıcısı için CRM referans setleri (okuma + CRM doğrulaması)
Repository: C:\tmp\brd-crm-sets (worktree) · Branch: fix/brd-tenant-crm-sets (origin/main 819cfe5c'den) · commit bu dala, push YOK, main'e commit YOK · Platform (TenantReferenceDataController) + CrmService (GatewayReferenceDataValidator)

Paket belgesi: execution/domains/platform-shared-services/work-packs/WP-BRD-TENANT-CRM-SETS-mobile-reference-sets.md — önce tamamını oku (Sorun / Karar / NE / KORU / Sözleşme / Acceptance). Ayrıca: execution/domains/platform-shared-services/module-packs/MOD-0048-lookups-reference-data.md · services/Diten.Platform/src/Diten.Platform.API/Controllers/{TenantReferenceDataController, BusinessReferenceDataController}.cs · Platform BusinessReferenceData Application (GetBusinessReferenceDataPublishedValuesQuery, IBusinessReferenceDataConsumerQueryService) · Platform kiracı ara katmanı / TenantScope · services/Diten.CrmService/src/Diten.CrmService.Infrastructure/ReferenceValidation/GatewayReferenceDataValidator.cs · CRM AccountReferenceValidation / ContactReferenceValidation · gateway ocelot /api/lookups rotası · memory tenant-scoped-token-multi-tenant-login, country-vocabulary-divergence, gateway-404-empty-body-signature, platform-partial-index-ne-crash.

NE: (1) Platform yeni uç GET api/lookups/reference-data/tenant-sets/{setCode}/published-values: [Authorize]+[LoginOnly], kiracı bağlamı zorunlu (kiracısız → belgelenen 400/403; platform-admin token'ı kiracı verisi okumaz); izin listesi {account-type, account-status, account-category, contact-type, contact-status} OrdinalIgnoreCase, liste dışı → 404 reference_set_not_tenant_accessible; okuma ÇAĞIRANIN kiracısında, scope_key = sunucunun çözdüğü kiracı (istemciden alınmaz), mevcut published-values sorgusu/servisi; yayında değil/set yok/emekli → 404 reference_set_not_published (500 asla); mevcut sets/{setCode} ucu + Global liste + referans kiracı okuması DEĞİŞMEZ. (2) CRM GatewayReferenceDataValidator: ayar ReferenceData:TenantSetsPathTemplate (varsayılan /api/lookups/reference-data/tenant-sets/{setCode}/published-values); önce tenant-sets, 404+reference_set_not_tenant_accessible → mevcut genel yol (scope_key + global retry); erişilemez set kodu süreç içi önbellek; ValidateAsync + GetValueAttributesAsync + katalog okuma aynı sıra; reference_set_not_published → SetMissing; izin listesi CRM'de KOPYALANMAZ. (3) MOD-0048 module pack'e kısa not.
KORU/YAPMA: Platform.BusinessReferenceData.Consumer.Read kiracı rollerine VERİLMEZ, RBAC/seed/veri yazılmaz; mevcut uç/Global liste değişmez; diğer CRM doğrulamaları aynı davranır; gateway değişikliği gerekirse raporla.
DOĞRULA (E2): Platform testleri (tabanı önce ölç; ortam kaynaklı kırmızı tabanı ~173 — yalnız FARK raporlanır) → 0 yeni kırmızı; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo → 0 kırmızı (taban ölç); dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı; build 0 hata. Yeni testler WP Acceptance (mobil §9 1-11, 14 + CRM fallback/önbellek; 12-13 harness'ta mümkünse). Sabotaj: (1) yeni uçta okumayı referans kiracıya çevir → izolasyon testi kırmızı; (2) CRM fallback'i kaldır → genel set doğrulama testi kırmızı. Commit ("fix(brd): WP-BRD-TENANT-CRM-SETS — tenant-scoped CRM reference sets read route + CRM validator fallback" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: kiracı ara katmanı kiracıyı yalnız X-Tenant-Id başlığından (token doğrulamasız) alıyorsa ya da tüketici servisi kiracı setini sunucu tarafında güvenli okuyamıyorsa → DUR + raporla.
```
