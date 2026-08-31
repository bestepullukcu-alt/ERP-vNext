---
id: CAND-CAP-0008-FU03
name: Working Calendar Legal-Entity Scope Extension
parent: CAND-CAP-0008
runtime_slug: working-calendar
domain: platform-shared-services
service: Diten.Platform + frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: HybridEntity
status: ready-for-dev
runtime_code_allowed: true
runtime_code_scope: "AÇIK (ready-for-dev, flip 2026-08-27 kullanıcı kararı). Yetkilendirilen DAR kapsam: shipped WorkingCalendar aggregate/provider/repository/DTO sözleşmelerine additive `LegalEntityId` ve `legal-entity` vokabüleri; scope=legal-entity iken zorunlu, aksi halde yasak FK; persist öncesi MDM `lookup-validation` cross-service doğrulaması (cache yok, 3sn timeout, yalnız transient tek retry, fail-closed — MDM erişilemezse doğrulanmamış kayıt yazılmaz); provider scope contract'ına additive `LegalEntityId` + precedence zinciri (organization-unit > legal-entity > tenant > country) — motor ülke + SEÇİLEN TEK override çözer; tek-aktif ve duplicate-code guard anahtarına `LegalEntityId`; mevcut tenant Overrides Create/Edit/Details yüzeyine koşullu MDM lookup dropdown'ı; mevcut Gateway legal-entity rotasının read-only tüketimi. FU01 provider seam'i (metot sayısı + sonuç DTO'ları + country+tek-override çözümlemesi) KORUNUR. YASAK: çoklu override merge, MDM write, yeni RBAC anahtarı, FU02 auto-fetch kapsam genişlemesi, Gateway config yazımı, registry write, Mongo hand-edit."
owner: module-pack-author
branch: feature/pss/cand-cap-0008-fu03-working-calendar-legal-entity-scope
started: 2026-08-27
target: TBD (ayrı ready-for-dev + runtime flip kararı sonrası)
form_field_count: 10
dependencies:
  - CAND-CAP-0008 (FU01 — shipped aggregate, write guard, provider seam ve tenant Overrides yüzeyi; additive genişletilir)
  - CAND-CAP-0008-FU02 (draft auto-fetch; yalnız ülke katmanı, legal-entity override yazmaz)
  - MOD-0220 (MDM Legal Entity — tenant-scoped lookup + lookup-validation SoR)
  - MOD-0018 (mevcut working-calendar override izinleri; yeni anahtar yok)
  - MOD-0032 (mevcut /api/legal-entities ve /api/legal-entities/{everything} Gateway rotaları; config deltası yok)
  - DEV-0001 (Golden Reference Compact)
---

# CAND-CAP-0008-FU03 — Working Calendar Legal-Entity Scope Extension

> **READY-FOR-DEV — KOD YETKİSİ AÇIK (flip 2026-08-27 kullanıcı kararı).** `status: ready-for-dev` ve
> `runtime_code_allowed: true`. Kapsam yalnızca yukarıdaki `runtime_code_scope` ile sınırlıdır; oradaki YASAK
> maddeleri (çoklu override merge, MDM write, yeni RBAC anahtarı, FU02 kapsam genişlemesi, Gateway/registry write)
> flip sonrası da bağlayıcıdır.

> **Kimlik kapısı (D-F11 deseni):** FU03 yeni bir capability kimliği mint etmez. Kayıtlı parent candidate
> kapısına dayanır:
>
> `py .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0008 --name "Working Calendar & Public Holidays"`
>
> `CAND-CAP-0008-FU03` için registry/reconciliation satırı **açılmaz**. Parent ve FU dizeleri yalnız governance
> dokümanlarında yaşayabilir; `services/`, `frontend/`, `gateway/`, `tests/` altında runtime literal olamaz.

---

## 1. Module Summary

FU03, tenant Working Calendar Override kapsamına `legal-entity` seçeneğini ekler. Tenant tarafından yazılabilen
kapsam kümesi aşağıdaki gibi kilitlenir:

```text
TenantAuthorable = { tenant, organization-unit, legal-entity }
```

Aynı ülke/yıl için tenant, legal-entity ve organization-unit override satırları birlikte bulunabilir. Provider
tüketici bağlamına uyan satırlardan **yalnız en spesifik olanı** seçer ve mevcut ülke katmanıyla onu çözer:

```text
organization-unit > legal-entity > tenant > country
```

Bu çalışma yeni bir merge motoru veya ikinci aggregate açmaz. FU01'in `country + tek override` mimarisi korunur.

---

## 2. Ownership and Boundaries

### 2.1 In-scope

- Shipped `WorkingCalendar` aggregate'ine nullable `LegalEntityId` eklenmesi.
- `WorkingCalendarScopeType` in-domain kümesine `legal-entity` eklenmesi; set dışı değerlerin fail-closed reddi.
- Create/update/DTO/contract/repository/provider sözleşmelerinin additive genişlemesi.
- Provider seçim zincirinin `organization-unit > legal-entity > tenant` olarak uygulanması.
- Tenant Overrides Create/Edit/Details yüzeyinde legal-entity seçimi ve tenant-scoped MDM lookup proxy'si.
- LegalEntityId'nin MDM `lookup-validation` endpoint'iyle gerçek, aktif, aynı-tenant FK olarak doğrulanması.
- Scope-key tabanlı duplicate-code ve tek-active guard/index genişletmesi.
- Yedi dil tenant L10n (`en`, `tr`, `fr`, `es`, `ru`, `zh`, `ar`).

### 2.2 Out-of-scope

- MDM Legal Entity aggregate, repository, lifecycle veya izinlerinin değiştirilmesi.
- OrganizationUnit ile LegalEntity ilişkisinin bu capability tarafından sahiplenilmesi veya yeniden doğrulanması.
- Birden fazla override satırının alan/gün bazında merge edilmesi.
- Legal-entity override'larının FU02 ile otomatik üretilmesi veya dış sağlayıcıdan doldurulması.
- Yeni permission/role/seed/grant, Gateway route/config yazımı, registry satırı ve Mongo hand-edit.
- Persist edilmiş kaydın `ScopeType`, `CountryCode`, `CalendarYear`, `OrganizationUnitId` veya `LegalEntityId`
  eksenleri arasında taşınması. FU01'in scope immutability kuralı korunur.

### 2.3 Boundary güncellemesi

FU01'de `OrganizationUnitId` doğrulaması aynı servis içinde ve in-process idi. FU03, aynı shipped aggregate'in
**ilk cross-service HTTP FK bağımlılığıdır**. Bağımlılık yalnız read-only doğrulama/lookup içindir; MDM verisi
kopyalanmaz, legal entity SoR'u değişmez. HTTP çağrısı Application handler içine gömülmez; mevcut
`ILegalEntityReferenceValidator` / `MdmLegalEntityReferenceValidator` deseni yeniden kullanılır veya
WorkingCalendar'a özel dar bir Application seam'i bu altyapı client'ına delege eder.

> **⚠ DÜZELTME 2026-08-29 (BL-316).** Bu paragraf daha önce `TenantPropagationHandler` desenini de yeniden
> kullanmayı söylüyordu. O sınıf **SİLİNDİ** ve bir daha yazılmamalıdır: `IHttpClientFactory` handler zincirini
> KENDİ kapsamında önbelleğe alır, dolayısıyla istek kapsamlı `ITenantContext`'i enjekte eden bir
> `DelegatingHandler` hiçbir isteğe ait olmayan bir örnek tutar, `IsResolved == false` döner ve `X-Tenant-Id`
> başlığını **sessizce eklemez** (ölçüldü 2026-08-28, BL-311). `X-Tenant-Id` **çağıran sınıfın kendisi**
> tarafından, kendi istek kapsamından yazılır; hangi kiracının tele çıkacağına dair tek cevap
> `TenantOnTheWire`'dır.

FU01 provider yüzeyi genişler ama kırılmaz: metot sayısı, sonuç DTO'ları ve `country + tek override` çözümleme
motoru aynıdır. FU02 yalnız country takvimlerine uygulanır; legal-entity override'ları manuel kalır.

---

## 3. Owned Objects

| Katman | Delta |
|---|---|
| Aggregate | `WorkingCalendar.LegalEntityId : Guid?` additive |
| Vokabüler | `WorkingCalendarScopeType.LegalEntity = "legal-entity"`; `TenantAuthorable` üçlü küme |
| Contract | `WorkingCalendarScope(CountryCode, OrganizationUnitId?, LegalEntityId?)` additive |
| Repository | tenant override sorgusu ve scope-key guard'larına `LegalEntityId?` |
| Provider | en-spesifik mevcut override seçimi; merge yok |
| DTO/request | `LegalEntityId?` additive; `TenantId` hâlâ payload'da yok |
| Cross-service seam | MDM lookup-validation tüketimi; yeni SoR yok |
| Frontend proxy | `GET /WorkingCalendar/Overrides/api/legal-entities` |
| UI | mevcut `_Form`, Create, Edit, Details ve `form.js` içinde koşullu legal-entity alanı |
| Permission | yeni anahtar **yok**; mevcut `platform.working-calendar.override.read|manage` anahtarları |

---

## 4. Entity Fields

| Field | Tip | Required | Kural | DB/index etkisi |
|---|---|---|---|---|
| `LegalEntityId` | `Guid?` | Koşullu | `ScopeType=legal-entity` ise dolu; diğer tüm scope'larda `null` | Scope-key index/guard'a dahil |
| `OrganizationUnitId` | `Guid?` | Koşullu | `ScopeType=organization-unit` ise dolu; diğer scope'larda `null` | Mevcut sparse index korunur |
| `ScopeType` | string | Evet | `country|tenant|organization-unit|legal-entity`; Overrides yalnız TenantAuthorable | Vokabüler additive |

Kanonik scope-key:

```text
organization-unit => OrganizationUnitId
legal-entity       => LegalEntityId
tenant/country     => null
```

`OrganizationUnitId` ve `LegalEntityId` aynı satırda birlikte dolu olamaz. `LegalEntityId`, serbest Guid veya
display metni değildir; create/update persist öncesi MDM doğrulaması zorunludur.

---

## 5. Repo Scope

Runtime flip verilirse yalnız aşağıdaki mevcut yüzeylerde dar değişiklik yapılabilir:

- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/WorkingCalendar/**`
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IWorkingCalendarRepository.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/WorkingCalendar/**`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkingCalendarRepository.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Mdm/**` ve dar DI/options kayıtları
- `services/Diten.Platform/src/Diten.Platform.API/Models/WorkingCalendarRequests.cs`
- `frontend/Diten.Web/Controllers/WorkingCalendarOverridesController.cs`
- `frontend/Diten.Web/Views/WorkingCalendar/Overrides/{_Form,Create,Edit,Details}.cshtml`
- `frontend/Diten.Web/wwwroot/assets/js/WorkingCalendarOverrides/{form,details}.js`
- `frontend/Diten.Web/Resources/Views/WorkingCalendar/Overrides/**` (7 dil)
- İlgili WorkingCalendar backend/frontend testleri

`Index.cshtml`, `_DataTable.cshtml` ve `_Filter.cshtml` bu FU'nun UI scope'unda değildir.

---

## 6. Protected Paths

- `.antigravity/**`
- `execution/registries/**` ve reconciliation ledger'ları
- `gateway/Diten.ApiGateway/**` (mevcut route yalnız tüketilir)
- `services/Diten.MdmService/**` (provider/SoR; değişmez)
- `services/Diten.AuthService/**` ve RBAC seed/grant yüzeyleri
- Diğer domain servisleri ve Archive/FROZEN frontend yolları
- FU02 import aggregate/job/UI dosyaları
- Çalışma takvimi tüketicilerinin kendi kodu

---

## 7. Dependencies

| Bağımlılık | Tüketim | Fail davranışı |
|---|---|---|
| FU01 WorkingCalendar | Aggregate/provider/write guard additive genişleme | Mevcut ülke/org/tenant davranışı regresyona uğrayamaz |
| MDM `GET /api/legal-entities/lookup` | Tenant-scoped aktif dropdown listesi | Boş/erişilemez → dropdown boş + kayıt yapılamaz; fallback yok |
| MDM `GET /api/legal-entities/{id}/lookup-validation` | Persist öncesi gerçek FK doğrulama | 404/401/403/5xx/timeout/bozuk envelope → doğrulanmamış kayıt yok |
| `TenantOnTheWire` + çağıran sınıf | `X-Tenant-Id` çağıran sınıfın kendi istek kapsamından yazılır (DelegatingHandler ile DEĞİL — BL-316); çağıran bearer/correlation bağlamı | Tenant eksik/mismatch → MDM fail-closed |
| Mevcut Gateway route | `/api/legal-entities` + catch-all child route | Route yoksa validation dependency unavailable |
| FU02 | Yalnız country auto-fetch boundary | Legal-entity satırına fetch/apply yasak |

---

## 8. Runtime Constraints

- `TenantId` JWT/tenant context'ten gelir; request body/query ile seçilemez.
- Cross-service doğrulama Gateway `http://localhost:5000` üzerinden yapılır; servis portu 5059'a doğrudan çağrı yoktur.
- Header aktarımı: `Authorization`, `X-Tenant-Id`, `X-Correlation-Id`. MDM middleware JWT/header mismatch'i 400,
  tenant yokluğunu 400; repository ise `TenantId + IsDeleted=false` ile filtreler.
- Sadece `LifecycleState=ACTIVE` ve `Referenceable=true`, response id'si talep id'siyle aynı kayıt kabul edilir.
- Doğrulama v1'de cache'lenmez. Timeout **3 saniye**, transient network/502/503/504 için **en fazla bir retry**
  (kısa jitter, toplam süre 3 saniyeyi aşmaz); 4xx ve semantik invalid cevap retry edilmez.
- Dependency unavailable, invalid JSON veya envelope/id/status uyuşmazlığı create/update'i kontrollü **503** veya
  **400** ile durdurur; hiçbir durumda `LegalEntityId` doğrulanmadan persist edilmez.
- Provider read-only kalır, exception tüketiciye fırlatmaz; mevcut unresolved sonuç semantiği korunur.

---

## 9. Layout & Shell Contract

Tenant yüzeyi `Views/WorkingCalendar/Overrides/` altında ve `_LayoutTenantShell` ile yaşar. Golden Reference Compact
korunur. Kullanıcı form alan sayısı 9'dan 10'a çıkar (`LegalEntityId` yeni koşullu alan), dolayısıyla ayrı
Create/Edit/Details + ortak `_Form` deseni değişmez.

---

## 10. Backend File Convention

Yeni command/query ailesi açılmaz; shipped WorkingCalendar dosya organizasyonu korunur. Gerekli additive alanlar
mevcut command/query/model/validator/handler dosyalarına eklenir. Cross-service HTTP doğrudan handler içinde
oluşturulmaz: Application interface, Infrastructure typed client ve DI kaydı kullanılır. Handler adlarına
`CommandHandler`/`QueryHandler` suffix'i eklenmez; `Response<T>` envelope ve cancellation propagation korunur.

---

## 11. Frontend File Contract

- `_Form.cshtml`: `LegalEntityId` için `#legalEntityField` koşullu alanı ve `select2 form-select` select'i.
- `form.js`: contract + countries + scope types + legal entities yüklenir; edit'te existing değerler uygulanır;
  ardından Select2 initialize edilir. `change.select2` native `change` re-dispatch eder.
- Scope toggle iki alanı yönetir:
  - `organization-unit` → yalnız `#organizationUnitField`
  - `legal-entity` → yalnız `#legalEntityField`
  - `tenant` → ikisi de kapalı
- İnaktif alan gizlenir, disabled yapılır ve değeri temizlenir. Select2 state'i `.val(null).trigger('change.select2')`
  ile temizlenir; payload yalnız seçilen scope'un FK'sını taşır.
- Persist edilmiş Edit kaydında FU01 scope immutability korunur: ScopeType değiştirilemez; doğru koşullu alan
  gösterilir. DOM manipülasyonu ile scope/FK değişimi backend tarafından 409/400 ile reddedilir.
- Lookup proxy response'u MDM envelope'undan normalize edilmez/uydurulmaz; DTO shape:
  `{ legalEntityId, code, legalName, displayName, lifecycleState, referenceable }`. Select2 `id=legalEntityId`,
  metin `code — (displayName ?? legalName)` olur.
- Hardcoded legal-entity fallback listesi ve tarayıcıdan Gateway/MDM servis portuna doğrudan çağrı yoktur.
- Details seçili legal entity için en az `code — name` gösterir; Guid çıplak ana etiket olarak gösterilmez.
- Yeni kullanıcı metinleri yedi RESX dilinde, XML dengeli eklenir.

---

## 12. Validation Rules

| Alan/kural | Beklenen sonuç |
|---|---|
| `ScopeType=legal-entity`, `LegalEntityId=null` | 400 `legal_entity_scope_requires_legal_entity` |
| `ScopeType!=legal-entity`, `LegalEntityId!=null` | 400 `legal_entity_forbidden_for_scope` |
| `ScopeType!=organization-unit`, `OrganizationUnitId!=null` | 400 mevcut org-scope hata ailesi |
| İki FK birlikte dolu | 400; persist yok |
| LegalEntity Guid invalid/empty | 400 |
| MDM 404, inactive, non-referenceable, id mismatch | 400 `legal_entity_not_referenceable`; persist yok |
| MDM unavailable/timeout/5xx/invalid envelope | 503 `legal_entity_validation_unavailable`; persist yok |
| ScopeType update ile değiştiriliyor | 409; FU01 immutability korunur |
| Duplicate code | `(TenantId, CountryCode, CalendarYear, ScopeKey, CalendarCode)` içinde 409 |
| İkinci active | `(TenantId, CountryCode, CalendarYear, ScopeKey)` içinde 409 |

---

## 13. Failure Path to Verify

- Başka tenant'ın legal entity id'si hem dropdown'da görünmez hem lookup-validation'da 404 olur; kayıt oluşmaz.
- MDM kapalıyken seçilmiş Guid doğrudan POST edilirse write fail-closed olur; DB'de yeni/değişmiş satır yoktur.
- ScopeType Select2 ile `organization-unit → legal-entity → tenant` değiştirildiğinde yalnız ilgili alan görünür;
  önceki FK hem DOM hem payload'dan silinir.
- Aynı tenant/ülke/yılda tenant ve legal-entity override birlikte bulunabilir; duplicate guard birbirini yanlış
  duplicate saymaz. Aynı legal entity scope-key içindeki duplicate ise 409'dur.
- Org ve legal-entity override birlikteyken provider org bağlamında org satırını; yalnız legal-entity bağlamında
  legal-entity satırını; ikisi yoksa tenant satırını seçer.
- Draft/archived override seçim zincirine katılmaz; daha genel aktif satıra düşülür.

---

## 14. Authorization Convention

Yeni RBAC anahtarı yoktur. Legal-entity bir Working Calendar Override kapsamıdır; mevcut override
read/create/update/activate/archive permission ailesi kullanılır. MDM lookup ve validation çağrıları MDM'in
mevcut `mdm.legal-entities.read` kapısına ve çağıran tenant bağlamına tabidir. Frontend proxy cookie'deki bearer'ı
server-side Gateway'e aktarır; browser token okumaz.

---

## 15. Gateway / API Routing Decision

Gateway değişikliği gerekmez. Mevcut çift:

```text
/api/legal-entities
/api/legal-entities/{everything}
```

şu çağrıları taşır:

- `GET /api/legal-entities/lookup`
- `GET /api/legal-entities/{legalEntityId:guid}/lookup-validation`

Yeni same-origin MVC proxy endpoint'i: `GET /WorkingCalendar/Overrides/api/legal-entities` → Gateway
`GET /api/legal-entities/lookup`. Proxy query ile TenantId kabul etmez; tenant yalnız server-side request
context/header üzerinden aktarılır.

### Endpoint / contract deltası

| Yüzey | Önce | FU03 sonrası |
|---|---|---|
| Create/Update body | `OrganizationUnitId?` | additive `LegalEntityId?` |
| WorkingCalendar DTO | org/tenant/country | additive `LegalEntityId?`, `ScopeType=legal-entity` |
| Contract scope types | `tenant, organization-unit` | `tenant, organization-unit, legal-entity` |
| Provider scope | `(CountryCode, OrganizationUnitId?)` | `(CountryCode, OrganizationUnitId?, LegalEntityId?)` |
| Repository tenant read | country/year/org? | country/year/org?/legalEntity? |
| Guard scope-key | org id veya null | org id veya legal entity id veya null |
| MVC proxy | yok | `GET /WorkingCalendar/Overrides/api/legal-entities` |
| Gateway config | mevcut | **değişmez** |

---

## 16. Acceptance Criteria

- [ ] **AC-CHAIN-1:** Aynı tenant/ülke/yılda aktif org, legal-entity ve tenant override varken
  `WorkingCalendarScope(country, orgId, legalEntityId)` yalnız org override'ını seçer.
- [ ] **AC-CHAIN-2:** Org override yokken aynı bağlam yalnız legal-entity override'ını seçer; o da yoksa tenant
  override'ına, o da yoksa country katmanına düşer.
- [ ] **AC-CHAIN-3:** Seçimden sonra motor yalnız `country + seçilen tek override` ile çalışır; iki override'ın
  `WeekendDays` veya `Days` dizileri merge edilmez.
- [ ] **AC-CHAIN-4:** Draft/archived ve başka tenant'a ait satırlar zincire katılmaz.
- [ ] **AC-FK-1:** Create/update `legal-entity` scope'u MDM
  `/api/legal-entities/{id}/lookup-validation` ile aynı tenant + ACTIVE + referenceable olarak doğrulanmadan yazılmaz.
- [ ] **AC-FK-2:** Başka tenant, inactive, archived, deleted, non-referenceable veya id-mismatch cevap 400/404
  kontrollü hata üretir ve DB değişmez.
- [ ] **AC-FK-3:** MDM/Gateway timeout, 5xx, ağ veya JSON/envelope hatasında 503/400 fail-closed cevap vardır;
  doğrulanmamış Guid saklanmaz.
- [ ] **AC-FK-4:** Çağrı Gateway üzerinden bearer + `X-Tenant-Id` + correlation taşır; MDM servis portuna doğrudan
  HTTP yoktur ve cache ile tenantlar arası sonuç paylaşılmaz.
- [ ] **AC-SCOPE-1:** `LegalEntityId` yalnız `legal-entity` scope'unda zorunlu; diğer scope'larda yasaktır;
  `OrganizationUnitId` ile birlikte dolamaz.
- [ ] **AC-SCOPE-2:** Duplicate-code ve single-active guard scope-key'e LegalEntityId'yi dahil eder; tenant,
  org ve legal-entity satırları birlikte bulunabilir.
- [ ] **AC-UI-1:** ScopeType Select2'de `legal-entity` görünür; seçildiğinde yalnız `#legalEntityField`, org seçilince
  yalnız `#organizationUnitField`, tenant seçilince hiçbiri görünür değildir.
- [ ] **AC-UI-2:** Select2 `change.select2` native change re-dispatch eder; canlı scope toggle her geçişte çalışır.
- [ ] **AC-UI-3:** MDM lookup yalnız aktif, aynı-tenant legal entity DTO'larını döndürür; hardcoded fallback yoktur;
  browser Gateway veya servis portunu doğrudan çağırmaz.
- [ ] **AC-UI-4:** Create/Edit/Details Compact kalır; form alan sayısı 10'dur; Index/_DataTable/_Filter değişmez.
- [ ] **AC-UI-5:** Persist edilmiş editte ScopeType ve scope FK ekseni değiştirilemez; mevcut legal entity doğru
  yüklenir. İnaktif koşullu alanın eski FK'sı payload'a sızmaz.
- [ ] **AC-L10N:** Yeni etiket, seçenek, empty/loading/error ve validation metinleri 7 dilde bulunur; RESX XML dengelidir.
- [ ] **AC-RBAC:** Yeni permission/seed/grant yoktur; mevcut override permission'ları korunur.
- [ ] **AC-FU02:** Auto-fetch yalnız country target kabul etmeye devam eder; legal-entity override otomatik yazılmaz.
- [ ] **AC-ID:** Parent candidate gate exit 0; parent/FU governance literal'leri runtime yollarında 0 hit; FU registry satırı yok.

---

## 17. Test Expectations

- Domain/validator: legal-entity required/forbidden, iki-FK yasağı, vocabulary fail-closed.
- Repository: tenant izolasyonu; legal-entity scope-key duplicate ve single-active; org/legal/tenant birlikte varlık.
- Cross-service client: 200 valid, id mismatch, inactive, false referenceable, 404/401/403, 5xx, timeout, invalid JSON;
  header forwarding ve persist olmaması.
- Provider: dört basamaklı precedence matrisi ve tek-override assertion'ı.
- Frontend: lookup unwrap/shape, Select2 init sırası, native change re-dispatch, iki koşullu alan toggle/clear/payload.
- Canlı smoke: iki tenant ile lookup izolasyonu; Create legal-entity save; Edit immutable; Details label; provider zinciri.
- Quality: `dotnet build ... -t:CoreCompile` 0 hata; `node --check form.js`; 7 RESX XML parse; Compact verifier
  baseline'ı bilinçli olarak yalnız bu FU'nun izin verdiği form alan deltası dışında regresyonsuz.

> Bu pack yazımında build/test çalıştırılmaz; yukarıdaki koşumlar yalnız runtime flip sonrası implementation gate'idir.

---

## 18. Ready-for-dev Checklist

- [ ] Kullanıcı `FU03`, D-LE01…D-LE05 ve boundary güncellemesini onayladı.
- [ ] `status` ve `runtime_code_allowed` için ayrı flip kararı verildi.
- [ ] MDM lookup + lookup-validation canlı contract shape'i yeniden doğrulandı.
- [ ] Gateway route çifti mevcut ve tenant header forwarding smoke edildi.
- [ ] Scope immutability kararı UI/backend planında korunuyor.
- [ ] Provider precedence test matrisi ve no-merge assertion'ı hazır.
- [ ] FU02 country-only guard regresyon testi planlandı.
- [ ] Registry/Gateway/MDM protected path diff'lerinin boş kalacağı teyit edildi.

---

## 19. Implementation Notes — D Kararları

| ID | Karar | Gerekçe |
|---|---|---|
| **D-LE01** | Follow-up numarası **FU03** | FU02 auto-fetch için mevcut taslaktır; yeniden numaralandırmak otorite ve link kırar. FU03 parent'ın additive scope çocuğudur. |
| **D-LE02** | Tenant scope, caller bearer + `X-Tenant-Id` + correlation'ın Gateway üzerinden aktarılması ve MDM middleware/repository filtresiyle garanti edilir; tenant query/body parametresi yoktur | MDM `TenantResolutionMiddleware` mismatch/missing tenant'ı reddeder; repository `TenantId + IsDeleted=false` filtreler. İstemci tenant seçemez. |
| **D-LE03** | Write FK validation cache'siz; 3 sn toplam timeout; yalnız transient 502/503/504/ağ için en fazla bir kısa-jitter retry; 4xx/semantic invalid retry yok | Yanlış tenant/lifecycle sonucu cache'lenmez; kısa transient toleransı sağlanır, write belirsiz süre bloke olmaz. Retry read-only GET olduğundan güvenlidir. |
| **D-LE04** | Dropdown `GET /api/legal-entities/lookup` shape'ini kullanır: `legalEntityId, code, legalName, displayName, lifecycleState, referenceable`; Select2 value=id, text=`code — displayName/legalName` | Bu shape runtime'da mevcut ve tenant-scoped/referenceable'dır; ad-hoc `{id,name}` veya hardcoded fallback icat edilmez. |
| **D-LE05** | Persist edilmiş editte scope ekseni immutable kalır. Create/unsaved toggle'da inaktif FK anında clear+disable edilir; backend her durumda karşı FK'yı yasaklar | FU01'in immutability boundary'sini bozmaz, eski FK sızıntısını iki katmanda engeller. Scope migration gerekirse ayrı follow-up gerekir. |

### Provider sözleşmesi

```csharp
public sealed record WorkingCalendarScope(
    string CountryCode,
    Guid? OrganizationUnitId = null,
    Guid? LegalEntityId = null);
```

Seçim pseudo-code'u:

```text
activeRows = tenant overrides matching country/year
selected = active org row matching OrganizationUnitId
        ?? active legal-entity row matching LegalEntityId
        ?? active tenant row
resolve(countryActive, selected) // tek override; multi-merge YOK
```

---

## 20. Follow-up Items

- **F03-SCOPE-MIGRATION:** Persist edilmiş override'ın tenant/org/legal-entity scope'ları arasında taşınması istenirse
  ayrı pack; bu FU'da immutability korunur.
- **F03-CACHE:** Ölçüm sonrası tenant+legalEntityId+version/lifecycle güvenli invalidation sözleşmesi kurulursa pozitif
  validation cache ayrıca değerlendirilebilir; v1'de cache yoktur.
- **F03-ORG-LE-RELATION:** OrganizationUnit'ın seçilen LegalEntity altında olduğunun ayrıca doğrulanması gelecekte
  tüketici ihtiyacıyla açılabilir; bu FU precedence için yalnız iki bağımsız bağlam id'sini kullanır.
- **F03-REG:** FU registry/ledger satırı ancak kullanıcı D-F11 politikasını ayrıca değiştirirse açılır.

---

## Handoff

Bu dosya yalnız planlama sözleşmesidir. Runtime implementasyonu başlatmak için kullanıcı önce pack'i onaylamalı,
ardından **ayrı bir kararla** `status: ready-for-dev` (veya approved) + `runtime_code_allowed: true` flip'i vermelidir.
O zamana kadar `@orchestrator` runtime kodu yazamaz.
