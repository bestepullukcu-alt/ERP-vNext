---
id: MOD-0165-FU07
name: Cycle Period
parent: MOD-0165
parent_name: Campaign Management
siblings: MOD-0165-FU01 (frequency SoR) · MOD-0165-FU02 (campaign boundary) · MOD-0165-FU03 (frequency runtime) · MOD-0165-FU04 (campaign runtime) · MOD-0165-FU05 (campaign admin UI) · MOD-0165-FU06 (cycle period — SHIPPED, bu FU onu genişletir)
domain: commercial-suite
service: Diten.CrmService
frontend: frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: review
runtime_code_allowed: true
flip_approved_by: "control-tower (2026-08-28) — draft verified: no runtime touched, migration-cheapness proof confirmed (CyclePeriodOverlapRules.InScope narrows IN MEMORY, no Mongo backfill, key bijective), DCP-002 exit 0. D-SCOPE-SHAPE (discriminated) + D-BU-SOURCE (soft gate) + D-PRECEDENCE + D-OVERLAP-SCOPE endorsed. D-COUNTRY-SET = COUNTRY_CODES per user override (not `country`), with the mandatory §2.6 equivalence proof as a BUILD-GATE. supportsCampaignBinding stays false."
runtime_code_scope: "Aggregate scope alanları (ScopeType + CountryScope/LegalEntityId/BusinessUnitId + BusinessUnitSource), CyclePeriodScopeRules/OverlapRules/ResolveEngine, CyclePeriodScopeWriteValidator, ICyclePeriodLegalEntityValidator + MDM impl, ITerritoryBusinessUnitCatalog + Territory read adapter, ICyclePeriodLegalEntityCatalog + MDM lookup, genişleyen ICyclePeriodReader, scope-options endpoint, Slim→Compact UI, 7 dil RESX, GatewayReferenceDataValidator koşullu scope_key (S1). Kapsam: CyclePeriod aggregate'ine ayrımlı (discriminated) scope alanlarının EKLENMESİ, scope doğrulaması (country = MOD-0048 reference, legal-entity = MDM cross-service fail-closed, business-unit = Territory-türetilmiş aday listesi), 4 seviyeli precedence resolve, ICyclePeriodReader imza genişlemesi, Slim → Compact UI migrasyonu, 7 dil RESX genişlemesi. YASAK (FU06'dan devralınan ve DEĞİŞMEYEN): auto-close job, sürüm klonu, reschedule, çalışma-günü hesabı, Campaign/VFP/StrategyTemplate/MicroTarget yazımı, bulk-delete, hard delete, Mongo hand-edit, backfill script, ocelot.json yazımı, registry yazımı, RBAC seed/grant."
owner: module-pack-author
branch: feature/crm/mod-0165-fu07-cycle-period-scope-enrichment
started: 2026-08-28
target: TBD (kullanıcı onayı sonrası)
form_field_count: 11
predecessor: MOD-0165-FU06 (SHIPPED — authenticated smoke 56/0; 57 test metodu; verifier slim/proxy baseline)
dependencies:
  - MOD-0165-FU06 (ZORUNLU ÖNCÜL — SHIPPED; bu FU onun scope'unu genişletir, felsefesini KORUR)
  - MOD-0151-FU02A (Territory `TerritoryBusinessScope` + `TerritoryModel.CountryScope`/`EffectiveFrom`/`EffectiveTo` — SHIPPED; business-unit aday listesinin KAYNAĞI, salt-okunur)
  - MOD-0048 (reference data — `country` ve `business-unit` published-values; publish ön koşulu §2.6/P1'de değerlendirilir)
  - MOD-0220 / MDM Legal Entity (`GET /api/legal-entities/{id}/lookup-validation` — cross-service fail-closed doğrulama)
  - CAND-CAP-0008-FU03 (Working Calendar legal-entity scope — TRANSPORT PROFİLİ emsali; kod paylaşımı YOK)
  - MOD-0155-FU05 (MicroTarget — birincil tüketici; hâlâ YAPILMADI, bu FU satır üretmez)
  - MOD-0165-FU03/FU04/FU05 · MOD-0167-FU04 (kardeş runtime — DOKUNULMAZ)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant YOK)
  - DEV-0001 (Golden Reference Compact — yeni şablon; DEV-0000 Slim TERK EDİLİR)
---

# MOD-0165-FU07 — Cycle Period (Scope Enrichment)

## 0. Delivery Record (2026-08-28)

> **RUNTIME AUTHORIZATION (2026-08-28).** Kullanıcı pack'i `ready-for-dev` + `runtime_code_allowed: true` olarak
> yetkilendirdi ve **D-COUNTRY-SET'i `COUNTRY_CODES` lehine çevirdi** (pack `country` öneriyordu — §2.6). Uygulama
> pack'e harfiyen uyularak yapıldı; aşağıdaki sapmalar dışında hiçbir karar değişmedi.

**Teslim edilen yüzeyler.** Backend: `Domain/Entities/CyclePeriod.cs` (+`CyclePeriodScopeTypes`,
`CyclePeriodBusinessUnitSources`, `ScopeRef()`/`EffectiveScopeType()`/`EnsureScopeType()`/`HasConsistentScope()`) ·
`Rules/CyclePeriodScopeRules.cs` (YENİ) + genişleyen `CyclePeriodOverlapRules` / `CyclePeriodResolveEngine` ·
`Services/ICyclePeriodLegalEntityValidator.cs` + `Services/CyclePeriodScopeWriteValidator.cs` (YENİ) ·
`Read/ITerritoryBusinessUnitCatalog.cs` + `Read/ICyclePeriodLegalEntityCatalog.cs` (YENİ) · genişleyen
`ICyclePeriodReader` · `CyclePeriodReferenceSets.cs` (YENİ) · `Queries/GetCyclePeriodScopeOptionsQuery.cs` +
handler (YENİ) · Infrastructure `CyclePeriod/MdmCyclePeriodLegalEntityValidator.cs` ·
`MdmCyclePeriodLegalEntityCatalog.cs` · `TerritoryBusinessUnitCatalog.cs` (YENİ) · class-map `LegalEntityId`
string-Guid serializer · `CyclePeriodsController` +1 endpoint. Frontend: **Slim → Compact** —
`Create.cshtml` / `Edit.cshtml` / `Details.cshtml` / `_Form.cshtml` (YENİ), `_CreateEditOffcanvas.cshtml` +
`_DetailsQuickView.cshtml` **SİLİNDİ**, `form.js` (YENİ), 7 dil RESX **+31 anahtar** (parite doğrulandı).
Scripts: `verify-mod0165-fu07-country-equivalence.ps1` + `smoke-mod0165-fu07-cycle-period-scope-authenticated.ps1`
(YENİ); FU06 smoke script'i **değiştirilmedi**.

**Pack'ten sapmalar (üçü de daraltıcı veya düzeltici, genişletici değil):**

| # | Sapma | Gerekçe |
|---|---|---|
| **S1** | **`GatewayReferenceDataValidator` (paylaşılan CRM dosyası) `scope_key`'i artık KOŞULLU gönderiyor.** Pack P1'i "doğrula" diye yazmıştı; doğrulama, sorunun **kesin** olduğunu gösterdi: consumer servisi global bir sete scope key'i `scope_key_not_allowed_for_global` ile reddediyor (`BusinessReferenceDataConsumerQueryService.cs:541`), CRM ise key'i koşulsuz ekliyordu — yani `COUNTRY_CODES` CRM'den **hiç okunamazdı**. Düzeltme, servisin **kendi hata sinyaline** bakıp yalnız o durumda key'siz tekrar dener; başka hiçbir hata sessizce yeniden denenmez ve mevcut tenant-scoped tüketicilerin davranışı **değişmez** | Kullanıcı talimatı: *"global ise scope_key koşullu (global-set reddini önle)"* |
| **S2** | FU06'nın `Update_Active_Business_Unit_Is_409` testi, FU07 modelinde **aynı sözü** ifade edecek şekilde yeniden yazıldı: dönem artık `business-unit` scope'unda seed ediliyor ve **başka bir BU'ya** taşınmaya çalışılıyor (aynı seviye, farklı adres) → hâlâ **409 `dates_immutable`**. FU06 hâlinde istek, tenant scope'a bir BU referansı iliştirdiği için artık **400 `scope_ambiguous`** olurdu — yani testin *kastettiği* davranış değil, *ifade şekli* eskiydi | Beklenti korundu: aktif dönemin scope'u değiştirilemez |
| **S3** | Compact `_Form.cshtml`'de `ScopeType`, edit modunda `disabled` + gizli ikiz yerine **tek seçenekli enabled select** olarak render ediliyor | `disabled` bir kontrol POST edilmez; gizli ikiz aynı alanı iki kez bağlar ve verifier'ın "required marker ↔ ViewModel metadata" kontrolünü haklı olarak düşürür |

**Doğrulama (ham çıktı):**

| Kapı | Komut | Sonuç |
|---|---|---|
| DCP-002 kimlik | `verify_module_id.py --check-id MOD-0165-FU07 --name "Cycle Period" --parent MOD-0165` | `OK … proven against Blueprint/registry.` **exit 0** |
| DCP-002 repo geneli | `verify_module_id.py --check-all` | **exit 0** · `[HARD violations: 0]` (6 advisory legacy backlog + 1 filename warning — hepsi MOD-0021, FU07 dışı) |
| Backend build | `dotnet build …Diten.CrmService.Api.csproj` | **0 Hata** (2 uyarı — ikisi de FU07 öncesinden, Territory/Account dosyalarında) |
| Frontend build | `dotnet build frontend/Diten.Web/Diten.Web.csproj` | **0 Hata** |
| Test — CyclePeriod | `dotnet test --filter FullyQualifiedName~CyclePeriod` | **140 / 0** (FU06'nın 63'ü **davranış beklentileri değişmeden** + 77 yeni) |
| Test — tüm CRM | `dotnet test` | **1137 başarılı / 0 başarısız / 5 atlanan** |
| DataTable verifier | `verify_datatable_page.py --area CRM --module CyclePeriods --reference compact --api-profile proxy` | **PASS 87 / FAIL 8** — §17.1 delta tablosu aşağıda |
| CAND literal | `grep -rn "CAND-CAP" services frontend --include=*.cs --include=*.js --include=*.cshtml --include=*.resx` | **0** |
| P3 gateway probe | `GET /api/crm/cycle-periods/scope-options` | **401** (rota var, servise ulaşıyor) — *"route yok"* imzası olan **404 + `{}`** değil. `ocelot.json` **değişmedi** |

**Verifier delta — 8 FAIL'in tamamı hesaplı:**

| # | FAIL | Sınıf |
|---|---|---|
| 1 | `personalizationClient sends tenant header only for tenant users` | **FU07 DIŞI, ÖNCEDEN VAR.** Golden Reference Compact'ın kendisi de bu kontrolü düşürüyor (`--area DevEnablement --module GoldenReferenceCompact` → **PASS 93 / FAIL 1**, aynı kontrol). Paylaşılan `personalization-client.js` sorunu |
| 2–8 | select-all checkbox · `bulkOptions` · bulk selection · `/bulk` endpoint · bulk-delete trigger · `reloadWithToast` · clear-selection | **BEKLENEN N/A** (pack §17.1 kural 2 ve 3). Modülde **hiç delete yok** (kapatma = `close`), tablo **client-side** olduğu için paylaşılan `reloadWithToast` `dt.ajax.reload()` çağırır ve exception atardı. FU06'nın S1 kararı aynen korundu: yerel yardımcı **`reloadAndToast`** adını taşıyor ve kontrol **dürüstçe FAIL kalıyor** |

> Golden Compact referansının kendi taban çizgisi **93/1**; FU07 **87/8**. Fark tam olarak yukarıdaki 7 N/A'dır
> (93 − 7 + 1 yeni scope kontrolü ≈ 87). Hiçbir FAIL yeşile boyanmadı.

**Test suite'te bir kez görülen kırmızı — kök nedeni FU07 değil.** Bir çalıştırmada `1136/1` alındı:
`ContactLocationPiiHardeningTests.PiiMasking_Redacts_Email_And_Phone_But_Keeps_Guid_And_Country`. Test her
çalışmada `Guid.NewGuid()` üretiyor ve GUID'in ilk bloğu telefon-benzeri göründüğünde (`d7038490…`) redaktör onu
maskeliyor. `PiiMasking.cs` ve testin kendisi **git'te untracked** (`??`) — FU07 diff'inin parçası değiller ve
FU07 o koda dokunmuyor. Tek başına 5 kez çalıştırıldı: **5/5 PASS**; ardından tam suite tekrar: **1137/0**.
Bu bir **önceden var olan flaky test**tir ve ayrı bir iş olarak düzeltilmelidir (F-PII-FLAKE).

**KULLANICIDA KALAN (agent çalıştıramaz — parola gerektirir):**

1. **`./scripts/verify-mod0165-fu07-country-equivalence.ps1`** — §2.6 denklik BUILD-GATE'i. **GEÇERSE** Territory
   migrate olmadan da BU seçici eşleşmeye devam eder. **GEÇMEZSE FU07 bloke olmaz**: seçici, yayınlanmış
   `business-unit` vokabülerine düşer (D-BU-SOURCE yumuşak kapı) — BU-scope dönemler **hiçbir zaman bloke olmaz**;
   liste yalnızca artık bir saha planıyla daraltılmaz (F-COUNTRY-SOT). Ön koşul: **COUNTRY_CODES yayınlanmalı**.
2. **`./scripts/smoke-mod0165-fu06-cycle-period-authenticated.ps1`** — **18/18 zorunlu** (regresyon kanıtı).
3. **`./scripts/smoke-mod0165-fu07-cycle-period-scope-authenticated.ps1`** — 27 adım. **Fleet FU07 build'i ile
   yeniden başlatılmalı**; `.resx` değişiklikleri de tam restart ister.
4. **P2 / F-MDM-PERM** — `mdm.legal-entities.read` grant'i kullanıcıdadır. İzinsiz aktör için legal-entity create
   **503** verir; smoke adım 12 bunu **beklenen** olarak işaretler.

---

> **TASLAK / BOUNDARY + CONTRACT PACK (2026-08-28) — `status: draft`, `runtime_code_allowed: false`.**
> Bu pack **kod yazma yetkisi vermez.** Onayınıza sunduğu tek şey şudur: MOD-0165-FU06'nın **shipped**
> dönem master'ının scope'u `(tenant, business-unit)`'tan `(tenant, country, legal-entity, business-unit)`'a
> genişletilirken **kimlik anahtarının nasıl değişeceği**, **hangi kaynaktan doğrulanacağı** ve
> **hangi sınırların kesinlikle değişmeyeceği**.
>
> **Neden şimdi:** FU06 kapanış kaydı bu genişlemeyi *ayrı bir FU* olarak ertelemişti; FU06 `(tenant, BU)`
> ile SHIPPED oldu. Bugün üç ayrı yerde daha zengin bir scope zaten var:
> `TerritoryModel` (`CountryScope` + `BusinessScopes[]` + effective pencere, MOD-0151-FU02A SHIPPED),
> `WorkingCalendar` (`ScopeType` = country/tenant/organization-unit/legal-entity, CAND-CAP-0008 SHIPPED) ve
> MDM `LegalEntity` (`lookup-validation` yüzeyi). Dönem master'ı bu üçünün **en dar** scope modeline sahip
> tek nesnedir; genişletilmezse tenant, tek bir ülke veya tek bir tüzel kişi için ayrı takvim kuramaz ve
> bunu **sahte business-unit kodları üreterek** taklit etmeye zorlanır.
>
> **DCP-002 kimlik geçidi — PASS (2026-08-28):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0165-FU07 --name "Cycle Period" --parent MOD-0165`
> → `OK  MOD-0165-FU07: proven against Blueprint/registry.` (**exit 0**).
> **FU numarası gerekçesi (D-FU):** `MOD-0165` altında FU01/FU02/FU05/FU06 pack olarak, FU03/FU04 runtime
> kodu olarak kullanımdadır. İlk çakışmayan id **FU07**'dir. Registry satırı bu pack tarafından **EKLENMEZ**
> (registry yazımı pack yetkisi dışıdır) — F-REGISTRY, FU06'dan **hâlâ açık** devralınır
> (`execution/registries/module-id-registry.md` bugün yalnız `MOD-0165` parent satırını taşır, FU satırı yoktur).
>
> Otorite sırası: **Blueprint Excel** > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` >
> `.antigravity/rules/`.

---

## 1. Module Summary

FU06 şu soruyu cevaplıyordu: **"hangi dönem?"** — ve cevabı yalnız iki adreste arayabiliyordu: bir iş birimi,
ya da tenant'ın tamamı. FU07 aynı soruyu **dört adreste** cevaplanabilir kılar:

```text
FU06 :  ResolveActiveAsync(at, businessUnitId)                              → BU → tenant
FU07 :  ResolveActiveAsync(at, country, legalEntityId, businessUnitId)      → BU → legal-entity → country → tenant
```

Cevabın **doğası değişmez**: hâlâ `resolved | none | ambiguous`, hâlâ **salt-okunur**, hâlâ tahmin yok,
hâlâ birleştirme yok. Değişen tek şey **kaç seviyede arandığı** ve **her seviyenin nereden doğrulandığı**dır.

Hedef kullanıcı değişmez: saha planlama takvimini kuran tenant CRM yöneticisi. Yüzey **değişir**: 8 kullanıcı
alanı 11'e çıktığı için sayfa Golden **Slim**'den Golden **Compact**'a taşınır (§11.1 türetmesi).

### 1.1 FU06'dan ne DEĞİŞİR, ne DEĞİŞMEZ (bu pack'in en kritik tablosu)

| Konu | FU06 (SHIPPED) | FU07 (bu pack) |
|---|---|---|
| Scope seviyeleri | `tenant` · `business-unit` | `tenant` · `country` · `legal-entity` · `business-unit` |
| Scope şekli | `BusinessUnitId string?` (null = tenant) | **Ayrımlı (discriminated)** `ScopeType` + tek scope referansı (D-SCOPE-SHAPE) |
| BU'nun doğası | **Opak string** (master okunmaz) | **Reference-doğrulanmış kod** + Territory-türetilmiş aday listesi (D-BU-SOURCE) |
| Country | **YOK** | **Bağımsız scope seviyesi**, kimlik anahtarında (D-COUNTRY-ROLE) |
| Legal entity | **YOK** | **Bağımsız scope seviyesi**, MDM cross-service fail-closed (D-LEGAL-ENTITY) |
| Benzersizlik anahtarı | `(TenantId, BU-scope, Year, SequenceInYear)` | `(TenantId, ScopeType, ScopeRef, Year, SequenceInYear)` (D-MIGRATION — **eski satırlar için birebir denk**) |
| Çakışma yasağı | aktifler arası, aynı `(tenant, BU)` | aktifler arası, aynı `(ScopeType, ScopeRef)` — **seviyeler ARASI çakışma SERBEST** (§8.3) |
| Resolve | BU → tenant, birleştirme yok | BU → legal-entity → country → tenant, **birleştirme yok** (§8.4) |
| Golden reference | **Slim** (8 alan) | **Compact** (11 alan) — offcanvas/quick-view dosyaları **SİLİNİR** (§11.2) |
| Lifecycle | draft → active → closed, geri dönüş yok | **AYNI** |
| Sürümleme / reschedule / auto-close / çalışma-günü | **YOK** | **HÂLÂ YOK** — 12 kapalı bayrağın hiçbiri çevrilmez (§8.2) |
| `supportsCampaignBinding` | `false` | **`false` KALIR** — Campaign hâlâ pinler, yön değişmez (F-CAMPAIGN-BIND) |
| Seam'in yazma yetkisi | **YOK** | **HÂLÂ YOK** — `ICyclePeriodReader` salt-okunur kalır |

### 1.2 Ne DEĞİLDİR (FU06'dan devralınan kavram ayrımı — genişletilmiş)

| Kavram | Sahibi | Sorusu | Bu FU ile ilişkisi |
|---|---|---|---|
| **`CyclePeriod`** (bu FU) | MOD-0165 | *"Hangi **iş dönemi**, hangi **adreste**?"* | **BU FU** |
| **Working Calendar** | CAND-CAP-0008 (PSS) | *"Bu **gün** çalışma günü mü?"* | **AYRI KAVRAM.** FU07 `ScopeType` **desenini** ödünç alır, **kodunu değil**; `IWorkingCalendarProvider` **tüketilmez** (F-CALENDAR-DAYS açık kalır) |
| **`TerritoryModel`** | MOD-0151 (SHIPPED) | *"Saha planı hangi ülkeyi/iş birimini kapsıyor?"* | **Business-unit aday listesinin KAYNAĞI.** Salt-okunur tüketilir; Territory **hiç yazılmaz**, `Features/Territory/**` **protected** kalır (§2.3) |
| **MDM `LegalEntity`** | MOD-0220 / MDM | *"Bu tüzel kişi var mı, referanslanabilir mi?"* | **Cross-service fail-closed doğrulama.** LegalEntity kaydı **kopyalanmaz**, yalnız `Guid` referansı tutulur |
| **`Campaign`** | MOD-0165-FU04 | *"Hangi kampanya?"* | **DEĞİŞMEZ.** Campaign'e `CyclePeriodId` **eklenmez** (F-CAMPAIGN-BIND) |
| **`MicroTarget`** | MOD-0155-FU05 (yapılmadı) | *"Bu dönemde planı ne?"* | Birincil **tüketici**; bu FU satır **üretmez** |

> **Tek cümlelik sınır:** *FU06 dönemin **kimliğini** söylüyordu; FU07 o kimliğe bir **adres** ekler.
> Adresin ne anlama geldiğini (o ülkede kaç çalışma günü var, o iş biriminin hedefleri neler)
> söylemek hâlâ **başka modüllerin** işidir.*

### 1.3 D-Karar özeti (onayınıza sunulur — tam gerekçe: [Ek D](#ek-d--karar-gerekçeleri-tam))

| # | Karar | **Önerilen** | Kritiklik |
|---|---|---|---|
| **D-SCOPE-SHAPE** | Scope'un şekli | **A — Ayrımlı (discriminated) tek seviye:** `ScopeType` ∈ {`tenant`,`country`,`legal-entity`,`business-unit`} + o tipe ait **tek** referans. Kombinasyon (country **VE** LE **VE** BU aynı anda) **REDDEDİLİR** | **EN KRİTİK** |
| **D-MIGRATION** | Kimlik migrasyonu | **M1 — Toplamsal alanlar + okuma-anında türetme, backfill YOK, Mongo'ya dokunulmaz.** Eski satırlar için anahtar **birebir denktir** → yeni çakışma **matematiksel olarak imkânsız** (§8.7 ispatı) | **EN KRİTİK** |
| **D-PRECEDENCE** | Çözümleme sırası | **BU > legal-entity > country > tenant.** İlk **dolu** seviye kazanır; boş seviye atlanır; **`ambiguous` bir seviyede DURDURUR** (üst seviyeye düşmez) | Yüksek |
| **D-COUNTRY-ROLE** | Country'nin rolü | **Bağımsız scope seviyesi + kimlik anahtarında.** Bir alt-nitelik veya filtre değil | Yüksek |
| **D-COUNTRY-SET** | Country kaynağı | **MOD-0048 `COUNTRY_CODES` set kodu** (kullanıcı kararı 2026-08-28 — Territory de sonraki aşamada `COUNTRY_CODES`'a taşınacak). **ZORUNLU BUILD-GATE (denklik ispatı, §2.6):** `COUNTRY_CODES ⊇ country` (kod bazında) + her ikisi de ISO alpha-2 (`^[A-Z]{2}$`) + `TerritoryModel.CountryScope`'un TÜM değerleri `COUNTRY_CODES` içinde. İspat GEÇERSE join Territory migrate olmadan da çalışır (kodlar eşleşir); GEÇMEZSE Territory-türevi BU seçici Territory migrate olana dek boş → BU seçici **`business-unit` vokabülerine düşmeli** (D-BU-SOURCE soft-gate) ki BU-scope dönemler bloke olmasın. country/LE/tenant scope'ları etkilenmez. Ön koşul: `COUNTRY_CODES` 97c5 için **yayınlı** olmalı | **Kullanıcı kararı: COUNTRY_CODES (B)** |
| **D-LEGAL-ENTITY** | LE doğrulaması | **MDM cross-service fail-closed**, Working Calendar FU03 transport profili **birebir** (cache yok · 3 sn · 1 transient retry · 503 = persist yok · `CreateAsync` ÖNCESİ). Kod **paylaşılmaz**, CRM'de kendi validator'ı yazılır (`MdmSegmentProductReferenceValidator` emsali) | Yüksek |
| **D-BU-SOURCE** | BU'nun kaynağı | **B — Daraltılmış seçici + vokabüler kapısı:** aday liste Territory'den (country + dönem penceresi kesişimi) türetilir; **yazma** MOD-0048 `business-unit` published-values'a karşı fail-closed doğrulanır; aday listesi dışındaki geçerli bir kod **provenance damgasıyla** kabul edilir. (A = sert kapı, C = serbest string — ikisi de gerekçeleriyle reddedilir) | **Kullanıcı girdisinden kısmî SAPMA — onay gerekir** |
| **D-OVERLAP-SCOPE** | Çakışma yasağının kapsamı | **Yalnız aynı `(ScopeType, ScopeRef)` içinde.** Seviyeler **arası** çakışma **SERBEST ve ZORUNLU** — yasaklanırsa precedence'ın kendisi kullanılamaz hâle gelir | **EN KRİTİK** |
| **D-GOLDEN** | Golden reference | **Compact** (11 kullanıcı alanı > 8). `_CreateEditOffcanvas.cshtml` + `_DetailsQuickView.cshtml` **SİLİNİR** (§11.2). Hibrit **YASAK** | Yüksek |
| **D-SEAM-BREAK** | Seam imzası | **Kırıcı değişiklik KABUL EDİLİR.** `ICyclePeriodReader`'ın repo-içi **tek** tüketicisi FU06'nın kendi handler'ıdır (doğrulandı); harici tüketici **yoktur**. Aşırı yük (overload) **açılmaz** — iki imza iki gerçek olur | Orta |
| **D-VOCAB-SCOPE** | `ScopeType` vokabüleri | **A = in-domain fail-closed** (`CyclePeriodScopeTypes`), FU06'nın `CyclePeriodStatuses` emsali. MOD-0048 publish runtime ön koşulu **değildir** | Orta |
| **D-TERRITORY-STATUS** | Hangi Territory planı sayılır | **Yalnız `active`.** `draft` bir taahhüt değildir; süperseded plan bugünün dönemine kaynak olamaz | Orta |
| **D-CONTRACT** | Bayraklar | 12 kapalı bayrak **aynen kapalı kalır**; 5 yeni açık + 4 yeni kapalı bayrak eklenir (§8.2) | Orta |

---

## 2. Ownership and Boundaries

**In-scope:** `CyclePeriod` aggregate'ine ayrımlı scope alanlarının eklenmesi · scope vokabüleri
(`CyclePeriodScopeTypes`) · country reference doğrulaması · legal-entity MDM fail-closed doğrulaması ·
Territory-türetilmiş business-unit **aday listesi** (salt-okunur) · benzersizlik ve çakışma kurallarının
zengin scope'a genişletilmesi · 4 seviyeli precedence resolve · `ICyclePeriodReader` imza genişlemesi ·
contract bayraklarının genişletilmesi · Slim → **Compact** UI migrasyonu · 7 dil RESX genişlemesi ·
scope seçici için same-origin proxy uçları.

**Out-of-scope (YASAK — FU06'dan devralınır ve genişletilir):** `Campaign` / `CampaignTarget` mutation ·
MicroTarget satırı üretimi · `VisitFrequencyPolicy` yazımı · `TerritoryModel` / `TerritoryNode` /
`TerritoryBusinessScope` **yazımı** · MDM `LegalEntity` yazımı · `StrategyTemplate` apply/generate ·
çalışma günü / tatil hesabı · `CycleCalendar` / `CyclePlan` hiyerarşisi · auto-close scheduler / job ·
sürüm klonu · reschedule · hard delete · bulk-delete · **Mongo hand-edit** · **backfill / migration script** ·
RBAC seed / grant · MOD-0048 publish · `ocelot.json` yazımı · registry yazımı.

### 2.1 Kilitli sınırlar (FU06'dan devralınır — DEĞİŞTİRİLEMEZ)

| Sınır | Karar |
|---|---|
| CyclePeriod'un doğası | **Dönem master'ı.** Kampanya, hedef, frekans ve saha planı **SAHİPLENİLMEZ** |
| Working Calendar | **AYRI KAVRAM.** FU07 tatil/çalışma-günü **bilmez**; `ScopeType` deseni ödünç alınır, **kod değil** |
| MOD-0165-FU04 (Campaign) | **Hiç dokunulmaz**; `Campaign`'e `CyclePeriodId` **eklenmez** |
| MOD-0165-FU03 (Frequency) | Policy **YAZILMAZ**; resolver imzası **genişletilmez**; resolver bu FU'yu **çağırmaz** |
| MOD-0151 (Territory) | **Salt-okunur.** Territory aggregate'leri, handler'ları ve view'ları **değişmez** |
| MOD-0155 (MicroTarget) | Satır **üretilmez**; yalnız READ contract sunulur |
| MDM (LegalEntity) | **Salt-okunur doğrulama.** MDM'e yazma **yok**, LegalEntity alanı **kopyalanmaz** (yalnız `Guid`) |
| SoR | **MOD-0165.** Dönem MOD-0151'e, MOD-0155'e veya MOD-0048'e **taşınmaz** |
| Legacy CrmV2 | **adapt-not-copy.** FU06 §2.4 bulgusu geçerli: legacy'de `CyclePeriod` aggregate'i **yoktur** |
| Golden reference | **Compact** (§11.1 türetmesi) |
| RBAC | Anahtarlar **değişmez** (`crm.cycle-period.read/manage/activate`); seed/grant **YOK** (§14) |
| Registry / Gateway config | **YAZILMAZ**. Gateway'e **ihtiyaç yoktur** (§15) |

### 2.2 MOD-0155 sözleşme koruması (kırmızı çizgi — FU06'dan aynen)

- Bu FU **hiçbir** MicroTarget / PlannedVisit satırı üretmez ve MOD-0155 repository'lerine **erişmez**.
- `ICyclePeriodReader` **yalnız okur**; genişleyen imzasının **hiçbir** metodu kayıt oluşturmaz/günceller/siler.
- Seam **in-process**'tir; `HttpClient` **tutmaz**. (Legal-entity doğrulaması bir **yazma yolu** bileşenidir;
  seam'in içinde **değildir** — §8.6 ayrımı.)
- Tüketici dönemi **id ile** referanslar; scope alanlarını **kopyalamaz**.

### 2.3 MOD-0151 Territory koruması (yeni kırmızı çizgi)

- `Features/Territory/**`, `Domain/Entities/TerritoryModel.cs`, `TerritoryBusinessScope.cs`,
  `TerritoryNode.cs` ve Territory controller/view'ları **protected** (§6). Diff'te **yer almazlar**.
- BU aday listesi **dar bir okuma seam'i** üzerinden alınır (`ITerritoryBusinessUnitCatalog`, §8.5).
  CyclePeriod handler'ları `ITerritoryModelRepository`'yi **doğrudan enjekte etmez** — o arayüz
  `InsertAsync`/`UpdateAsync` taşır ve bir dönem handler'ının eline verilmesi yapısal bir risktir
  (yapısal test §17.2).
- Aday listesi **tavsiyedir, kapı değildir** (D-BU-SOURCE = B). Territory planı değiştiğinde **hiçbir mevcut
  dönem geçersizleşmez** — aksi hâlde kimlik, yabancı ve değişken bir aggregate'e bağlanırdı.

### 2.4 MDM koruması (yeni kırmızı çizgi)

- MDM'e yapılan **tek** çağrı `GET /api/legal-entities/{id}/lookup-validation`'dır ve **yalnız yazma yolunda**,
  **persist'ten önce** çalışır.
- Cevap **kopyalanmaz**: `CyclePeriod` üzerinde LegalEntity adı/kodu/ülkesi **saklanmaz**, yalnız `Guid`.
  (FU06'nın *"tüketici kopyalamaz, id ile referanslar"* kuralının aynası.)
- **Cache YOK.** Aynı id iki kez sorulursa iki çağrı yapılır — cache, artık var olmayan bir tüzel kişiye
  dönem açılmasına izin verirdi ki bu sınıfın **varlık nedeni** tam olarak budur.

### 2.5 FU06 çıktısının korunması (regresyon sınırı)

FU06 **SHIPPED**'dir (authenticated smoke 56/0). Bu FU'nun hiçbir değişikliği aşağıdakileri bozamaz:

1. `businessUnitId` **query parametresi** ve **payload alanı** adları/anlamları **korunur**.
2. Yalnız `(at, businessUnitId)` ile yapılan bir `resolve-active` çağrısı **FU06 ile birebir aynı sonucu**
   verir — country/legal-entity satırları **var olsa bile** (çünkü boş seviye **atlanır**, §8.4).
3. FU06'nın 12 kapalı bayrağı **kapalı** kalır; hiçbiri bu pack tarafından çevrilmez.
4. FU06'nın lifecycle, immutability, concurrency ve tenant izolasyonu kuralları **harfiyen** aynıdır.
5. FU06 smoke script'inin 18 adımı **aynen geçmeye devam eder** (§17.3 — FU06 script'i **silinmez**).

### 2.6 Kaynak vokabüler çelişkisi — **düzeltme ve onay talebi**

Görev girdisi *"Kaynak = reference-data `Country Codes` (COUNTRY_CODES) — Territory + Working Calendar ile
aynı kaynak"* diyor. **Kodda böyle tek bir kaynak yoktur.** 2026-08-28 itibarıyla repoda **üç** ayrı ülke
kaynağı vardır:

| # | Kaynak | Kim kullanıyor | Kanıt |
|---|---|---|---|
| 1 | MOD-0048 set kodu **`country`** (global scope, ISO alpha-2, 22 değer, seed'li) | **CRM'in tamamı**: Accounts, Contacts, **Territory** | `frontend/Diten.Web/Controllers/CRM/TerritoryManagementController.cs:42` (`CountrySetCode = "country"`), `AccountsController.cs:55`, `ContactsController.cs:39`, `services/.../Features/Contact/ContactReferenceValidation.cs:16`; seed: `services/Diten.Platform/src/Diten.Platform.API/Seed/business-reference-data/legal-entity-reference.json:22` |
| 2 | Platform lookup **`countries`** (`/api/lookups/countries`) | Working Calendar **backend** doğrulaması, Legal Entity sihirbazı, Tenants → Create | `services/Diten.Platform/.../Lookups/Services/PlatformLookupProvider.cs:248` — **kodda gömülü statik liste**, BRD değil |
| 3 | MOD-0048 set kodu **`COUNTRY_CODES`** | **Yalnız** Working Calendar *Overrides* frontend yüzeyi | `frontend/Diten.Web/Controllers/WorkingCalendarOverridesController.cs:34` |

Kaynak #3'ün kendi doküman notu bu çelişkiyi zaten yazıyor: *"Only this surface moved… a COUNTRY_CODES value
absent from that list saves as 400 `country_unknown`. The two lists must hold the same codes; this proxy does
not translate between them."*

**Sonuç (D-COUNTRY-SET):** FU07 için **#1 (`country`)** önerilir. Belirleyici gerekçe teknik değil, **mantıksal**:
FU07'nin business-unit aday listesi `TerritoryModel.CountryScope` üzerinde **eşitlik karşılaştırması** yapar.
`TerritoryModel.CountryScope` set **#1**'in vokabüleridir. `CyclePeriod.CountryScope` set **#3**'ten gelirse
join **hiçbir zaman hata vermez** — sadece **her zaman boş liste döner**. Bu, bir modülü öldüren en sessiz
hata türüdür.

> **Kullanıcı onayı gerekir.** `COUNTRY_CODES` ısrar edilirse pack `D-COUNTRY-SET = B` ile güncellenir ve
> ek olarak **denklik ispatı** zorunlu hâle gelir: `COUNTRY_CODES ⊇ country` (kod bazında), her iki set de
> ISO alpha-2, ve `TerritoryModel.CountryScope` değerlerinin tamamı `COUNTRY_CODES` içinde. Bu ispat
> yayınlanmadan (P1) FU07 kodlanamaz.

### 2.7 Legacy CrmV2 — yeni bir taşıma YOK

FU06 §2.4'ün bulgusu geçerlidir ve **genişletilmez**: legacy'de dönem aggregate'i yoktur, `Applicable`
bir dönem değildir. FU07 legacy'den **hiçbir** yeni kavram getirmez; `country`/`legal-entity` scope'ları
legacy'nin değil, **vNext'in** (Territory + Working Calendar + MDM) mevcut modellerinin genellemesidir.

---

## 3. Owned Objects

| Tür | Nesne | Durum |
|---|---|---|
| **Entity** | `CyclePeriod` — `ScopeType` · `CountryScope` · `LegalEntityId` · `BusinessUnitSource` alanları **eklenir**; `BusinessUnitId` **korunur** | DEĞİŞTİRİLİR |
| **Vokabüler** | `CyclePeriodScopeTypes` (in-domain: `tenant` \| `country` \| `legal-entity` \| `business-unit`) | YENİ |
| **Vokabüler** | `CyclePeriodStatuses` · `CyclePeriodResolutionOutcomes` | DEĞİŞMEZ (`CyclePeriodLimits`'e 2 sabit eklenir) |
| **Repository** | `ICyclePeriodRepository` — **imza değişmez**; scope daraltması hâlâ bellekte (§8.7) | DEĞİŞMEZ |
| **Commands** | `CreateCyclePeriodCommand` · `UpdateCyclePeriodCommand` — scope alanları eklenir. `Activate`/`Close` **değişmez** | DEĞİŞTİRİLİR |
| **Queries** | `GetCyclePeriodListQuery` · `GetCyclePeriodSelectorQuery` · `ResolveActiveCyclePeriodQuery` — scope parametreleri eklenir. `ById`/`Contract` **değişmez** | DEĞİŞTİRİLİR |
| **Queries** | `GetCyclePeriodScopeOptionsQuery` (country + legal-entity + Territory-türetilmiş BU adayları) | YENİ |
| **Rules** | `CyclePeriodOverlapRules` — `SameScope`/`InScope` `(ScopeType, ScopeRef)` üzerinden | DEĞİŞTİRİLİR |
| **Rules** | `CyclePeriodResolveEngine` — 2 seviyeden 4 seviyeye | DEĞİŞTİRİLİR |
| **Rules** | `CyclePeriodScopeRules` (saf fonksiyon — normalizasyon, tekil-referans invaryantı, legacy türetme) | YENİ |
| **Services** | `ICyclePeriodLegalEntityValidator` + MDM implementasyonu (Infrastructure) | YENİ |
| **Services** | `ITerritoryBusinessUnitCatalog` + Territory-okuyan implementasyon (**salt-okunur adaptör**) | YENİ |
| **Consumer seam** | `ICyclePeriodReader` — `ResolveActiveAsync` imzası genişler; **salt-okunur kalır** | DEĞİŞTİRİLİR |
| **API** | §8.1 — 10 endpoint (FU06'nın 9'u + `scope-options`) | DEĞİŞTİRİLİR |
| **Frontend route** | `/CRM/CyclePeriods` — **Compact** dosya seti | DEĞİŞTİRİLİR |
| **Permissions** | `crm.cycle-period.read` · `.manage` · `.activate` | **DEĞİŞMEZ** |

---

## 4. Entity Fields

### 4.1 `CyclePeriod` — eklenen ve değişen alanlar

> FU06'nın **tüm** alanları korunur (`CycleCode`, `CycleName`, `Year`, `SequenceInYear`, `StartDate`,
> `EndDate`, `Description`, `CycleStatus`, `ActivatedAt/By`, `ClosedAt/By`, `CreatedBy`, `UpdatedBy`,
> `Version`, `IsDeleted`, `DeletedAt`). Aşağıdaki tablo **yalnız farkı** gösterir.

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `ScopeType` | string | **Evet (server)** | `CyclePeriodScopeTypes` içinde: `tenant` \| `country` \| `legal-entity` \| `business-unit`. **Oluşturma sonrası IMMUTABLE** — scope kimliktir, kimlik değiştirilmez (D-SCOPE-SHAPE). Eski satırlarda **yoksa okuma anında türetilir** (§8.7) |
| `CountryScope` | string? | `ScopeType=country` ise **Evet**, aksi hâlde **null olmalı** | ISO alpha-2, **büyük harfe normalize**. MOD-0048 `country` published-values'a karşı **fail-closed** (D-COUNTRY-SET). Uzunluk 2 |
| `LegalEntityId` | Guid? | `ScopeType=legal-entity` ise **Evet**, aksi hâlde **null olmalı** | MDM `lookup-validation` ile **persist'ten önce** doğrulanır; `ACTIVE` + `Referenceable` **şart** (D-LEGAL-ENTITY) |
| `BusinessUnitId` | string? | `ScopeType=business-unit` ise **Evet**, aksi hâlde **null olmalı** | **FU06 alanı, adı korunur.** Artık opak değil: MOD-0048 `business-unit` published-values'a karşı fail-closed (D-BU-SOURCE). Max 60 |
| `BusinessUnitSource` | string? | Hayır (server) | **Provenance damgası**, kimlik **değil**: `territory` (aday listesinden seçildi) \| `manual` (geçerli ama plan-dışı kod). Resolve ve benzersizlik bu alana **hiç bakmaz** |
| `ScopeRef` | *(hesaplanan)* | — | **Alan değil, türetim**: `tenant → null` · `country → CountryScope` · `legal-entity → LegalEntityId.ToString("D")` · `business-unit → BusinessUnitId`. Benzersizlik ve çakışma anahtarının ikinci bileşeni |

**Değişmeyen davranışlar (aynen korunur):**

```csharp
public bool CoversInstant(DateTimeOffset at) => StartDate <= at && at <= EndDate;
public bool OverlapsWith(CyclePeriod other) => StartDate <= other.EndDate && other.StartDate <= EndDate;
```

**Yeni saf davranış:**

```csharp
// Tam olarak bir referans dolu olmalı (tenant hariç, orada hepsi null).
public bool HasConsistentScope();
// Anahtarın ikinci bileşeni. Normalize edilmiş; asla ham kullanıcı girdisi.
public string? ScopeRef();
```

### 4.2 Scope vokabüleri — **D-VOCAB-SCOPE = A (in-domain fail-closed)**

```csharp
public static class CyclePeriodScopeTypes
{
    public const string Tenant       = "tenant";
    public const string Country      = "country";
    public const string LegalEntity  = "legal-entity";
    public const string BusinessUnit = "business-unit";

    /// Precedence sırası: EN ÖZELDEN en genele. Resolve motoru bu diziyi SIRAYLA yürür.
    public static readonly IReadOnlyList<string> ByPrecedence =
        new[] { BusinessUnit, LegalEntity, Country, Tenant };

    public static readonly IReadOnlyList<string> All = ByPrecedence;
}
```

- Değerler **Working Calendar'ın `WorkingCalendarScopeType`'ı ile kasten aynı yazımdadır**
  (`organization-unit` hariç — CRM'de organizasyon birimi scope'u **yoktur**). Aynı yazım, iki modülü
  okuyan bir insanın **tek** zihinsel model öğrenmesini sağlar; **kod paylaşımı yoktur** (servisler ayrı).
- Listede olmayan değer → **400** (fail-closed). Bilinmeyen değer **asla** `tenant`'a düşürülmez.
- **Precedence sırası burada, TEK yerde tanımlıdır.** Resolve motoru bu diziyi yürür; ikinci bir yerde
  `if/else` zinciri yazılmaz (iki yerde tanımlı bir sıra, iki farklı sıradır).

### 4.3 Persistence kararı — **1 collection, şema değişikliği YOK**

- Collection **`cycle_periods`** olarak kalır. **Yeni collection yok, ikinci repo yok, gömülü tip yok.**
- **Class-map ZORUNLU:** yeni üyeler (`ScopeType`, `CountryScope`, `LegalEntityId`, `BusinessUnitSource`)
  `RegisterClassMaps` içindeki mevcut `CyclePeriod` haritasına **eklenmezse** sessizce **persist edilmez**;
  `LegalEntityId` bir `Guid?` olduğu için ayrıca **binary/string serialize uyuşmazlığı** riski taşır
  (bilinen CRM tuzağı — §19.2).
- **Yeni index YOK.** Gerekçe: FU06'da olduğu gibi scope daraltması **hiçbir zaman Mongo sorgusunda
  yapılmaz** — repository tenant'ın satırlarını döndürür, `CyclePeriodOverlapRules.InScope` **bellekte**
  daraltır (§8.7). Bu, migrasyonu risksiz kılan yapısal özelliktir.
- **DateTimeOffset tuzağı genişler:** BU aday türetmesi `TerritoryModel.EffectiveFrom` **ve** `EffectiveTo`
  ile kesişim hesaplar — **iki DateTimeOffset alanı**. Bu kesişim **bellekte** yapılır; Mongo'da bu iki alan
  **birlikte index'lenmez ve birlikte sort edilmez** (parallel-arrays 500, §19.2).

### 4.4 Index & benzersizlik kararı

| Kural | Nerede uygulanır | Neden |
|---|---|---|
| `CycleCode` unique (tenant, silinmemiş, closed dâhil) | **Handler** (`ListByCodeAsync`) | **FU06'dan DEĞİŞMEZ.** Kod tenant genelindedir, scope'a bağlı değildir — aynı kod iki farklı scope'ta kullanılamaz (kod = kalıcı tarihsel kimlik) |
| `(ScopeType, ScopeRef, Year, SequenceInYear)` unique (tenant, silinmemiş) | **Handler** (`ListByYearAsync` + `CyclePeriodOverlapRules`) | Mongo partial index filtresinde `$ne` **crash-loop** yapar; ayrıca kural **bellekte türetilen** `ScopeRef` üzerinden işler |
| Aktif çakışma yasağı (**aynı scope içinde**) | **Handler** (`activate` öncesi + `update` öncesi) | Küme kuralı; DB index ile ifade edilemez |
| Sorgu index'i | **FU06'daki index KORUNUR**: `{ TenantId: 1, IsDeleted: 1, CycleStatus: 1, Year: -1 }` | Yeni alanlar sorguya girmediği için yeni index gerekmez; DateTimeOffset içermez |

---

## 5. Repo Scope

**Backend — `services/Diten.CrmService/`**

```text
src/Diten.CrmService.Domain/Entities/CyclePeriod.cs                                           (DEĞİŞTİR — 4 alan + 2 saf metot + CyclePeriodScopeTypes)
src/Diten.CrmService.Application/Features/CyclePeriod/Commands/CreateCyclePeriodCommand.cs     (DEĞİŞTİR)
src/Diten.CrmService.Application/Features/CyclePeriod/Commands/UpdateCyclePeriodCommand.cs     (DEĞİŞTİR)
src/Diten.CrmService.Application/Features/CyclePeriod/Queries/GetCyclePeriodListQuery.cs       (DEĞİŞTİR)
src/Diten.CrmService.Application/Features/CyclePeriod/Queries/GetCyclePeriodSelectorQuery.cs   (DEĞİŞTİR)
src/Diten.CrmService.Application/Features/CyclePeriod/Queries/ResolveActiveCyclePeriodQuery.cs (DEĞİŞTİR)
src/Diten.CrmService.Application/Features/CyclePeriod/Queries/GetCyclePeriodScopeOptionsQuery.cs (YENİ)
src/Diten.CrmService.Application/Features/CyclePeriod/Handlers/CommandHandlers/*.cs            (DEĞİŞTİR — Create/Update; Activate/Close yalnız scope-aware overlap çağrısı)
src/Diten.CrmService.Application/Features/CyclePeriod/Handlers/QueryHandlers/*.cs              (DEĞİŞTİR + 1 YENİ)
src/Diten.CrmService.Application/Features/CyclePeriod/Rules/CyclePeriodOverlapRules.cs         (DEĞİŞTİR)
src/Diten.CrmService.Application/Features/CyclePeriod/Rules/CyclePeriodResolveEngine.cs        (DEĞİŞTİR)
src/Diten.CrmService.Application/Features/CyclePeriod/Rules/CyclePeriodScopeRules.cs           (YENİ — saf)
src/Diten.CrmService.Application/Features/CyclePeriod/Read/ICyclePeriodReader.cs               (DEĞİŞTİR — imza)
src/Diten.CrmService.Application/Features/CyclePeriod/Read/CyclePeriodReader.cs                (DEĞİŞTİR)
src/Diten.CrmService.Application/Features/CyclePeriod/Read/ITerritoryBusinessUnitCatalog.cs    (YENİ — dar okuma seam'i)
src/Diten.CrmService.Application/Features/CyclePeriod/Services/ICyclePeriodLegalEntityValidator.cs (YENİ)
src/Diten.CrmService.Application/Features/CyclePeriod/Validators/*.cs                          (DEĞİŞTİR)
src/Diten.CrmService.Application/Features/CyclePeriod/CyclePeriodModels.cs                     (DEĞİŞTİR — DTO'lara scope alanları)
src/Diten.CrmService.Application/Features/CyclePeriod/CyclePeriodMapper.cs                     (DEĞİŞTİR)
src/Diten.CrmService.Application/Features/CyclePeriod/CyclePeriodValidation.cs                 (DEĞİŞTİR)
src/Diten.CrmService.Application/Features/CyclePeriod/Contract/CyclePeriodFeatureFlags.cs      (DEĞİŞTİR — +5 açık, +4 kapalı bayrak)
src/Diten.CrmService.Infrastructure/CyclePeriod/MdmCyclePeriodLegalEntityValidator.cs          (YENİ)
src/Diten.CrmService.Infrastructure/CyclePeriod/TerritoryBusinessUnitCatalog.cs                (YENİ — salt-okunur adaptör)
src/Diten.CrmService.Persistence/Repositories/CyclePeriodRepository.cs                         (DEĞİŞTİR — okuma-anında ScopeType türetme)
src/Diten.CrmService.Persistence/... MongoClassMaps                                            (yalnız +4 üye kaydı)
src/Diten.CrmService.Persistence/DependencyInjection.cs                                        (yalnız +2 DI kaydı + HttpClient)
src/Diten.CrmService.Api/Controllers/CRM/CyclePeriodsController.cs                             (DEĞİŞTİR — +1 endpoint, +query param)
src/Diten.CrmService.Api/Models/CRM/CyclePeriodRequests.cs                                     (DEĞİŞTİR)
tests/Diten.CrmService.Application.Tests/CyclePeriod/CyclePeriodRuntimeTests.cs                (DEĞİŞTİR — mevcut 57 metot korunur)
tests/Diten.CrmService.Application.Tests/CyclePeriod/CyclePeriodScopeTests.cs                  (YENİ)
tests/Diten.CrmService.Application.Tests/CyclePeriod/CyclePeriodMigrationCompatTests.cs        (YENİ — §8.7 ispatı)
```

**Frontend — `frontend/Diten.Web/`** (Slim → Compact migrasyonu)

```text
Views/CRM/CyclePeriods/Index.cshtml                                                   (DEĞİŞTİR)
Views/CRM/CyclePeriods/_Filter.cshtml                                                 (DEĞİŞTİR — scope filtreleri)
Views/CRM/CyclePeriods/_DataTable.cshtml                                              (DEĞİŞTİR — Scope kolonu)
Views/CRM/CyclePeriods/_IndexL10n.cshtml                                              (DEĞİŞTİR)
Views/CRM/CyclePeriods/CyclePeriodsIndex.cs                                           (DEĞİŞMEZ — marker)
Views/CRM/CyclePeriods/Create.cshtml                                                  (YENİ — Compact)
Views/CRM/CyclePeriods/Edit.cshtml                                                    (YENİ — Compact)
Views/CRM/CyclePeriods/Details.cshtml                                                 (YENİ — Compact)
Views/CRM/CyclePeriods/_Form.cshtml                                                   (YENİ — Compact)
Views/CRM/CyclePeriods/_CreateEditOffcanvas.cshtml                                    (**SİL** — Compact'ta YASAK)
Views/CRM/CyclePeriods/_DetailsQuickView.cshtml                                       (**SİL** — Compact'ta YASAK)
wwwroot/assets/js/CRM/CyclePeriods/index.js                                           (DEĞİŞTİR — offcanvas kaldırılır)
wwwroot/assets/js/CRM/CyclePeriods/index.l10n.js                                      (DEĞİŞTİR)
wwwroot/assets/js/CRM/CyclePeriods/form.js                                            (YENİ — cascading scope seçici)
Resources/Views/CRM/CyclePeriods/CyclePeriodsIndex.{ar,en,es,fr,ru,tr,zh}.resx        (DEĞİŞTİR — 7 dil, parite)
Controllers/CRM/CyclePeriodsController.cs                                             (DEĞİŞTİR — Create/Edit/Details action + scope-options proxy)
```

**Scripts**

```text
scripts/smoke-mod0165-fu07-cycle-period-scope-authenticated.ps1                        (YENİ)
scripts/smoke-mod0165-fu06-cycle-period-authenticated.ps1                              (**DEĞİŞMEZ, SİLİNMEZ** — regresyon kanıtı §17.3)
```

---

## 6. Protected Paths

- `.antigravity/**` (global engineering system)
- `gateway/Diten.ApiGateway/**/ocelot.json` (**değişiklik GEREKMİYOR** — §15)
- `execution/registries/**` (registry yazımı pack yetkisi dışında — F-REGISTRY)
- **`services/Diten.CrmService/**/Features/Territory/**` · `Domain/Entities/TerritoryModel.cs` ·
  `TerritoryBusinessScope.cs` · `TerritoryNode.cs` · Territory controller/view'ları** (**MOD-0151 — YENİ, salt-okunur**)
- `services/Diten.CrmService/**/Features/Campaign/**` · `Domain/Entities/Campaign.cs` · `CampaignTarget` (**MOD-0165-FU04/FU05**)
- `services/Diten.CrmService/**/Features/VisitFrequencyPolicy/**` · `Domain/Entities/VisitFrequencyPolicy.cs` (**MOD-0165-FU03**)
- `services/Diten.CrmService/**/Features/StrategyTemplate/**` · `Domain/Entities/StrategyTemplate.cs` (**MOD-0167-FU04**)
- `services/Diten.CrmService/**/Features/Segmentation/**` · `Knowledge/**` · `ConsentPreference/**`
- Diğer servisler: `services/Diten.Platform/**`, `Diten.AuthService/**`, **`Diten.MdmService/**`**,
  `Diten.HcmService/**`, `Diten.EnterpriseStrategyService/**`, `Diten.DevEnablementService/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (**FROZEN**)
- `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` — **FU06'nın `<li>`'si zaten var; bu pack
  navigasyona DOKUNMAZ** (FU06'nın dar istisnası tekrarlanmaz)
- `frontend/Diten.Web/Controllers/Archive/**`, `Views/Archive/**` (**FROZEN**)
- MOD-0048 reference-data publish yüzeyleri (bu pack publish **yapmaz**)
- **Mongo veritabanı** — hiçbir script, hiçbir elle düzeltme, hiçbir backfill (D-MIGRATION = M1)

---

## 7. Dependencies

| Bağımlılık | Yön | Durum | Not |
|---|---|---|---|
| **MOD-0165-FU06** | **öncül** | **SHIPPED** | Bu FU onun üzerine yazar; §2.5 regresyon sınırı bağlayıcıdır |
| **MOD-0151-FU02A** Territory | **bu FU okur** | SHIPPED | `TerritoryModel.CountryScope` + `BusinessScopes[].ScopeCode` + `EffectiveFrom/To` + `Status`; Territory **yazılmaz** |
| **MOD-0048** `country` set | **fail-closed tüketim** | seed mevcut (**global** scope) | **P1 doğrulaması ŞART** — §2.6 + §19.3 (`scope_key` tuzağı) |
| **MOD-0048** `business-unit` set | **fail-closed tüketim** | MOD-0151 ile publish edildi | Territory ile **aynı** set kodu ve **aynı** doğrulama yolu |
| **MDM Legal Entity** | **cross-service fail-closed** | mevcut (`/api/legal-entities/{id}/lookup-validation`) | `mdm.legal-entities.read` izni **çağıranın** token'ında olmalı → F-MDM-PERM |
| **CAND-CAP-0008-FU03** Working Calendar | **emsal** | SHIPPED (PSS) | **Transport profili** ödünç alınır; kod paylaşımı ve provider tüketimi **YOK** |
| **MOD-0155-FU05** MicroTarget | **tüketici** | yapılmadı | Genişleyen seam'in ilk gerçek müşterisi; F-MICROTARGET |
| **MOD-0165-FU03/04/05 · MOD-0167-FU04** | komşu | SHIPPED | **Dokunulmaz** |
| **MOD-0018** RBAC | tüketim | mevcut | Anahtar **değişmez**; seed/grant **yok**; F-RBAC + F-MDM-PERM |
| **DEV-0001** Golden Compact | şablon | mevcut | §10/§11 birebir |
| **Gateway** (Ocelot) | **değişiklik GEREKMİYOR** | mevcut | `/api/crm/cycle-periods` + `{everything}` route çifti **zaten var** (§15) |

---

## 8. Runtime Constraints

- **Multi-tenancy:** `TenantId` server-side; payload'da **asla**; cross-tenant erişim **404**. **DEĞİŞMEZ.**
- **Soft lifecycle:** hard delete yok, bulk-delete yok, PATCH yok. **DEĞİŞMEZ.**
- **Concurrency:** `expectedVersion` + `ReplaceAsync`; uyuşmazlık **409**. **DEĞİŞMEZ.**
- **Transaction:** tek doküman yazımı; `StartTransaction` **çağrılmaz**. **DEĞİŞMEZ.**
- **Engine yok:** scheduler, job, auto-close **yok**. Zaman bir kaydı **değiştirmez**. **DEĞİŞMEZ.**
- **Frontend transport:** browser JS **yalnız** same-origin proxy; Gateway URL'i / bearer token **yok**
  (`--api-profile proxy`). **DEĞİŞMEZ.**
- **YENİ — dış bağımlılık disiplini:** MDM doğrulaması **yalnız yazma yolunda** ve **persist'ten önce**
  çalışır. Bağımlılık ulaşılamazsa **503, hiçbir şey yazılmadan**. Okuma yolları (list / byId / resolve /
  selector) MDM'e **hiç** çıkmaz — bir dış servis çökmesi dönem **okumayı** durduramaz.

### 8.1 API Contract

| # | Method + Path | Permission | Değişim | Davranış |
|---|---|---|---|---|
| 1 | `GET /api/crm/cycle-periods` | `.read` | **DEĞİŞTİ** | Filtreler: `year`, `cycleStatus`, `businessUnitId`, `cycleCode`, `coversDate`, `search` (**korunur**) **+** `scopeType`, `country`, `legalEntityId` |
| 2 | `GET /api/crm/cycle-periods/{id}` | `.read` | yanıt genişledi | Scope alanları eklenir; cross-tenant **404** |
| 3 | `POST /api/crm/cycle-periods` | `.manage` | **DEĞİŞTİ** | `scopeType` + eşleşen **tek** referans zorunlu; her zaman `draft` |
| 4 | `PUT /api/crm/cycle-periods/{id}` | `.manage` | **DEĞİŞTİ** | `draft`: scope **referansı** düzeltilebilir, `ScopeType` **edilemez** (409). `active`: yalnız `CycleName` + `Description`. `closed`: hiçbir şey (409) |
| 5 | `POST /api/crm/cycle-periods/{id}/activate` | `.activate` | davranış genişledi | Çakışma kontrolü artık **aynı `(ScopeType, ScopeRef)`** içinde |
| 6 | `POST /api/crm/cycle-periods/{id}/close` | `.activate` | **DEĞİŞMEZ** | draft/active → closed; terminal |
| 7 | `GET …/resolve-active?at=&country=&legalEntityId=&businessUnitId=` | `.read` | **DEĞİŞTİ** | 4 seviyeli precedence; `resolved`/`none`/`ambiguous` + **`resolvedScopeType`**. Kayıt **oluşturmaz** |
| 8 | `GET …/selector?year=&cycleStatus=&scopeType=&country=&legalEntityId=&businessUnitId=` | `.read` | **DEĞİŞTİ** | Hafif liste + scope alanları |
| 9 | `GET …/contract` | `.read` | **DEĞİŞTİ** | §8.2 bayrakları |
| 10 | `GET …/scope-options?country=&startDate=&endDate=` | `.read` | **YENİ** | Cascading seçici kaynağı: `countries[]` · `legalEntities[]` · `businessUnits[]` (**Territory-türetilmiş**) + her biri için `ready` bayrağı (§8.5) |

**Yok (kasten, FU06'dan aynen):** `DELETE`, `PATCH`, `POST /bulk-delete`, `POST /{id}/reopen`,
`POST /{id}/apply`, `POST /{id}/generate`, `GET /{id}/working-days`, `POST /{id}/reschedule`,
`POST /{id}/new-version`.

### 8.2 Contract flags

```jsonc
{
  // FU06'dan gelen 4 açık bayrak — DEĞİŞMEZ
  "supportsCyclePeriod": true,
  "supportsCyclePeriodLifecycle": true,
  "supportsActiveCycleResolution": true,
  "supportsBusinessUnitScopedCycles": true,

  // FU07 ile açılan 5 YENİ bayrak
  "supportsCountryScopedCycles": true,           // country = bağımsız scope seviyesi
  "supportsLegalEntityScopedCycles": true,       // MDM fail-closed doğrulanmış
  "supportsScopePrecedenceResolution": true,     // BU > legal-entity > country > tenant
  "supportsTerritorySourcedBusinessUnits": true, // aday liste Territory'den türetilir (TAVSİYE)
  "supportsScopeTypeMutation": false,            // ScopeType oluşturma sonrası IMMUTABLE

  // FU06'nın 12 kapalı bayrağı — HEPSİ AYNEN KAPALI KALIR
  "supportsCycleOverlap": false,                 // aktif çakışma yasağı: AYNI scope içinde
  "supportsCycleCalendarHierarchy": false,       // F-CYCLE-CALENDAR
  "supportsCyclePeriodVersioning": false,        // D-VER
  "supportsCycleReschedule": false,              // F-RESCHEDULE
  "supportsCycleAutoClose": false,               // scheduler/job YOK
  "supportsWorkingCalendarIntegration": false,   // F-CALENDAR-DAYS
  "supportsWorkingDayCount": false,
  "supportsMicroTargetGeneration": false,        // MOD-0155-FU05
  "supportsCampaignBinding": false,              // Campaign DEĞİŞMEZ — F-CAMPAIGN-BIND
  "supportsFrequencyPolicyWrite": false,         // MOD-0165-FU03 SoR
  "supportsFrequencyPolicyBackReference": false, // F-VFP-FK
  "supportsStrategyApply": false,                // MOD-0167-FU04
  "supportsHardDelete": false,
  "supportsBulkDelete": false,

  // FU07'nin AÇIKÇA reddettiği 4 yeni yetenek
  "supportsScopeMerge": false,                   // seviyeler asla birleştirilmez (§8.4)
  "supportsCrossScopeOverlapBan": false,         // seviyeler ARASI çakışma serbesttir (§8.3)
  "supportsOrganizationUnitScopedCycles": false, // WC'de var, CRM'de YOK (F-ORG-UNIT-SCOPE)
  "supportsScopeInheritance": false              // alt scope üst scope'un dönemini DEVRALMAZ; yalnız FALLBACK vardır
}
```

> **Bayrak kuralı (FU06'dan):** `false` bir **beyandır**, eksiklik itirafı değildir.
> `supportsScopeInheritance: false` özellikle önemlidir: *fallback* ile *inheritance* farklı şeylerdir —
> fallback "bu seviyede yoksa üsttekine bak" demektir, inheritance "üsttekini bu seviyenin kaydı say"
> demek olurdu. İkincisi **yoktur** ve `resolvedScopeType` her zaman **gerçekte hangi seviyeden** cevap
> geldiğini söyler.

### 8.3 Çakışma semantiği (D-OVERLAP-SCOPE) — kesin kural

**Scope** = `(TenantId, ScopeType, ScopeRef)`. Her `(ScopeType, ScopeRef)` çifti **kendi başına bir uzaydır**.

| Durum | Kural |
|---|---|
| İki `active` dönem, **aynı** `(ScopeType, ScopeRef)`, kesişen tarih | **YASAK → 409** (`cycle_period_overlap`) |
| İki `active` dönem, **farklı ScopeType** (ör. `country:TR` ve `business-unit:alpha`), kesişen tarih | **SERBEST — ve ZORUNLU** |
| İki `active` dönem, **aynı ScopeType farklı ScopeRef** (ör. `country:TR` ve `country:DE`) | **SERBEST** |
| `draft` ↔ herhangi | **SERBEST** (planlama alanı) |
| `closed` ↔ herhangi | **SERBEST** (geçmiş engellemez) |
| Bitişik dönemler, aynı scope | `EndDate` **dâhil** → `StartDate(n+1) == EndDate(n)` **çakışmadır → 409** |

> **Neden seviyeler arası çakışma SERBEST olmak ZORUNDA:** precedence'ın tanımı *"aynı günü kapsayan
> birden çok seviye varsa en özeli kazanır"*tır. Seviyeler arası çakışma yasaklanırsa precedence hiç
> devreye giremez — kural, kendi varlık sebebini yok ederdi. Bu, FU06'daki *"BU-özel ile tenant-geneli
> birleştirilmez, specificity seçer"* kuralının aynısıdır; sadece 2 yerine **4** seviye üzerinde çalışır.

Kontrol **iki** yerde çalışır (FU06'dan aynen): `activate` sırasında (asıl kapı) ve `active` bir kaydın
güncellenmesi sırasında (savunma katmanı).

### 8.4 `resolve-active` kararı (deterministik, engine değil) — 4 seviye

```text
girdi : at (zorunlu) · country? · legalEntityId? · businessUnitId?
adım 0: TenantId + IsDeleted=false + CycleStatus=active + StartDate <= at <= EndDate  → "kapsayanlar"
adım 1: CyclePeriodScopeTypes.ByPrecedence dizisini SIRAYLA yürü:
          business-unit  → businessUnitId null ise ATLA;  değilse kapsayanlar ∩ (business-unit, businessUnitId)
          legal-entity   → legalEntityId  null ise ATLA;  değilse kapsayanlar ∩ (legal-entity,  legalEntityId)
          country        → country        null ise ATLA;  değilse kapsayanlar ∩ (country,       country)
          tenant         → HER ZAMAN denenir;             kapsayanlar ∩ (tenant, null)
adım 2: ilk BOŞ OLMAYAN seviyede DUR:
          1 kayıt  → resolved  + resolvedScopeType = o seviye
          >1 kayıt → ambiguous + candidateIds + resolvedScopeType = o seviye   ← ÜST SEVİYEYE DÜŞMEZ
        hiçbir seviye dolmadıysa → none
```

**Değişmeyen ilkeler (FU06'dan aynen):**

- **`none` ≠ varsayılan.** Dönem yoksa uydurulmaz; en yakın dönem döndürülmez.
- **Birleştirme YOK.** Cevap her zaman **tam bir** seviyeden gelir (`supportsScopeMerge: false`).
- **Süresi geçmiş `active` dönem** pencere dışında kaldığı için doğal olarak seçilmez; hiçbir job durumunu
  değiştirmez.

**Yeni ve kritik iki ilke:**

- **Boş seviye ATLANIR, dolu seviye DURDURUR.** `businessUnitId` verilmemişse business-unit seviyesi hiç
  bakılmaz — bu, §2.5/#2 geriye-uyumunun mekanizmasıdır: FU06 çağrıları country/legal-entity seviyelerini
  **hiç görmez**.
- **`ambiguous` üst seviyeye DÜŞMEZ.** Bir seviyede iki aktif dönem bulunduysa o seviyede **veri bozuktur**;
  bir üst seviyeye düşmek, bozuk veriyi makul bir cevabın arkasına saklardı (FU06'nın *"ambiguous'ta seçim
  yok"* kuralının doğal sonucu).

### 8.5 Scope seçenek kaynakları (§8.1/#10) — üç kaynak, üç ayrı "hazır değil" hâli

| Kaynak | Nereden | Fail-closed davranışı |
|---|---|---|
| `countries[]` | MOD-0048 **`country`** published-values (D-COUNTRY-SET) | Set yayınlanmamışsa **boş liste + `countryReady: false`**. **Hardcoded fallback YASAK** (PSS-LOOKUPS-001). Form açılır ama kaydedilemez (400) |
| `legalEntities[]` | MDM `GET /api/legal-entities/lookup` (tenant-scoped ACTIVE) | Ulaşılamazsa **boş liste + `legalEntityReady: false`**. Seçenek listesi **doğrulamanın yerine geçmez** — persist öncesi per-id `lookup-validation` **yine de** çalışır (WC FU03'ün açık kuralı) |
| `businessUnits[]` | **Territory türetmesi** (§8.5.1) | Eşleşen plan yoksa **boş liste + `businessUnitReady: false`**; kullanıcı yine de geçerli bir `business-unit` kodu girebilir (`BusinessUnitSource = manual`) |

#### 8.5.1 Business-unit aday türetmesi (saf fonksiyon, salt-okunur)

```text
girdi : country? (seçili ülke) · startDate · endDate (dönemin penceresi)
adım 1: tenant'ın TerritoryModel satırlarından Status = active olanlar        (D-TERRITORY-STATUS)
adım 2: country doluysa  → CountryScope == country (case-insensitive) olanlar
        country boşsa    → hepsi
adım 3: effective pencere kesişimi (BELLEKTE — parallel-arrays tuzağı, §19.2):
        EffectiveFrom <= endDate && (EffectiveTo == null || startDate <= EffectiveTo)
adım 4: kalan modellerin BusinessScopes[] öğelerinden ScopeType == "business-unit" olanların
        ScopeCode değerlerini topla, tekilleştir, sırala
çıktı : aday kodlar + her biri için kaynak model kodları (kullanıcıya "neden bu liste?" cevabı)
```

- **Bu bir kapı değil, bir daraltmadır** (D-BU-SOURCE = B). Aday listesi **yazma kuralı değildir**;
  yazma kuralı MOD-0048 `business-unit` published-values doğrulamasıdır.
- **Territory ASLA yazılmaz.** Erişim `ITerritoryBusinessUnitCatalog` dar seam'i üzerindendir; CyclePeriod
  handler'ları `ITerritoryModelRepository`'yi **enjekte etmez** (yapısal test §17.2).

### 8.6 Tüketim seam'i — `ICyclePeriodReader` (read-only, imza genişler)

```csharp
namespace Diten.CrmService.Application.Features.CyclePeriod.Read;

public interface ICyclePeriodReader
{
    // FU06: ResolveActiveAsync(at, businessUnitId, ct)  →  FU07: 4 seviyeli scope
    Task<CyclePeriodResolution> ResolveActiveAsync(
        DateTimeOffset at,
        string? country,
        Guid? legalEntityId,
        string? businessUnitId,
        CancellationToken cancellationToken);

    Task<CyclePeriodSnapshot?> GetByIdAsync(Guid cyclePeriodId, CancellationToken cancellationToken);

    // Scope filtresi genişler; hiçbiri verilmezse yıl görünümü TÜM scope'ları listeler (listeleme ≠ çözümleme).
    Task<IReadOnlyList<CyclePeriodSnapshot>> ListByYearAsync(
        int year,
        string? scopeType,
        string? scopeRef,
        CancellationToken cancellationToken);
}

public sealed record CyclePeriodSnapshot(
    Guid CyclePeriodId, string CycleCode, string CycleName,
    int Year, int SequenceInYear,
    DateTimeOffset StartDate, DateTimeOffset EndDate,
    string CycleStatus,
    string ScopeType,          // YENİ
    string? ScopeRef,          // YENİ — normalize edilmiş
    string? CountryScope,      // YENİ — tipli erişim
    Guid? LegalEntityId,       // YENİ — tipli erişim
    string? BusinessUnitId);   // FU06'dan korunur

public sealed record CyclePeriodResolution(
    string Outcome,                       // resolved | none | ambiguous
    CyclePeriodSnapshot? Period,
    IReadOnlyList<Guid> CandidateIds,
    string? Reason,
    string? ResolvedScopeType);           // YENİ — CEVABIN HANGİ SEVİYEDEN geldiği
```

**Seam kuralları (FU06'dan aynen korunur + 2 yeni — testle sabitlenir §17.2):**

1. **Salt-okunur.** Hiçbir metot yazmaz; implementasyon `InsertAsync`/`ReplaceAsync` **çağırmaz**.
2. **In-process.** `HttpClient` **tutmaz**. → **YENİ:** legal-entity doğrulayıcısı bir **yazma yolu**
   bileşenidir ve seam'in **içinde değildir**; `CyclePeriodReader` onu **enjekte etmez**.
3. **Tenant bağlamı** çağıranın bağlamıdır; seam kendi başına tenant seçmez.
4. **Motor tek yerdedir.** Tüketici `active + pencere + precedence` mantığını **yeniden yazmaz**.
5. `Outcome` string'i contract ile aynı vokabülerdir; tüketici `none`'ı bir döneme **çevirmez**.
6. **YENİ:** `ResolvedScopeType` **bilgilendiricidir, bir izin değildir**. Tüketici ondan *"demek ki BU
   seviyesinde dönem yokmuş, ben bir tane açayım"* sonucunu **çıkaramaz** — yazma yetkisi hâlâ **yoktur**.
7. **YENİ:** `ListByYearAsync` bir **listelemedir, çözümleme değildir** — precedence **uygulamaz**,
   fallback **yapmaz**, scope filtresi verilmezse tüm seviyeleri **birlikte** döner.

### 8.7 Kimlik migrasyonu (D-MIGRATION = M1) — **ispat**

**Strateji: toplamsal alanlar + okuma-anında türetme. Backfill script YOK, Mongo'ya dokunulmaz.**

```text
Okuma anında (repository mapper), ScopeType boş/yok ise:
    BusinessUnitId == null  →  ScopeType = "tenant",        ScopeRef = null
    BusinessUnitId != null  →  ScopeType = "business-unit", ScopeRef = BusinessUnitId
Yazma anında ScopeType her zaman açıkça persist edilir (kayıt bir sonraki yazımında "doğal olarak" taşınır).
```

**Neden yeni bir çakışma matematiksel olarak imkânsız:**

1. FU06 anahtarı: `(TenantId, BusinessUnitId-scope, Year, SequenceInYear)` — BU-scope'un iki hâli var:
   `null` ve *bir kod*.
2. FU07 anahtarı: `(TenantId, ScopeType, ScopeRef, Year, SequenceInYear)`.
3. Türetme, FU06'nın iki hâlini FU07'nin `(tenant, null)` ve `(business-unit, kod)` çiftlerine **birebir ve
   örten** eşler. İki farklı FU06 scope'u asla aynı FU07 scope'una düşmez; aynı FU06 scope'u asla iki FU07
   scope'una bölünmez.
4. Yeni seviyeler (`country`, `legal-entity`) **ayrık** anahtar uzayı işgal eder: `ScopeType` farklı olduğu
   için hiçbir eski satırla aynı anahtara **düşemez**.
5. ⇒ **Hiçbir mevcut satır yeni bir çakışma kazanamaz veya var olan bir çakışmayı kaybedemez.**
   (AC #12 + `CyclePeriodMigrationCompatTests`)

**Neden bu strateji uygulanabilir (yapısal gerekçe):** FU06'nın repository'si scope'a göre **hiçbir Mongo
sorgusu yapmaz** — `ListAsync` / `ListByYearAsync` / `ListActiveAsync` tenant'ın satırlarını döndürür ve
scope daraltması `CyclePeriodOverlapRules.InScope` içinde **bellekte** olur. Dolayısıyla eksik bir
`ScopeType` alanı **hiçbir sorguyu ıskalatmaz**; bellekte türetilmesi yeterlidir. Bu, FU06'nın
partial-index-yasağı kararının **beklenmedik ve değerli** bir yan faydasıdır.

**Reddedilen alternatif (M2 — backfill script):** pack'in kendi out-of-scope listesi Mongo hand-edit ve
migration'ı yasaklıyor; ayrıca M1 ile **gereksiz**. Bir backfill, hiçbir davranış farkı üretmeden üretim
verisine dokunma riskini alırdı.

**Geriye-uyum sözleşmesi (testle sabitlenir):**

| Senaryo | Beklenen |
|---|---|
| `ScopeType` alanı olmayan eski satır okunur | `ScopeType` türetilir; liste/detay/selector **FU06 ile aynı** görünür |
| Eski satır düzenlenmeden `activate` edilir | Çakışma kontrolü **FU06 ile aynı** kümede çalışır |
| `resolve-active?at=X&businessUnitId=Y` (FU06 çağrısı) | **FU06 ile birebir aynı sonuç** — country/LE satırları var olsa bile |
| `resolve-active?at=X` (parametresiz, FU06 çağrısı) | Yalnız `tenant` seviyesi — **FU06 ile aynı** |
| Eski satıra `PUT` ile `scopeType` değiştirme denemesi | **409** `cycle_period_scope_immutable` |

---

## 9. Layout & Shell Contract

`shell: tenant` → **Razor layout AÇIKÇA yazılır**; `_ViewStart.cshtml` varsayılanına güvenilmez.

```cshtml
@{
    ViewData["Title"] = Localizer["PageTitle"];
    Layout = "_LayoutTenantShell";   // shell: tenant — AÇIKÇA
}
```

| Öğe | Değer |
|---|---|
| Layout | `_LayoutTenantShell` — **Index, Create, Edit, Details** dosyalarının **hepsinde** açıkça |
| View klasörü | `frontend/Diten.Web/Views/CRM/CyclePeriods/` (**değişmez**) |
| MVC route | `/CRM/CyclePeriods` · `/CRM/CyclePeriods/Create` · `/Edit/{id}` · `/Details/{id}` |
| Proxy route | `/CRM/CyclePeriods/api…` (same-origin; api-profile = `proxy`) |
| Canlı emsal | `Views/DevEnablement/GoldenReferenceCompact/` (Compact şablonu) + CRM Territory model formu (cascading reference seçici emsali) |
| `_Layout.cshtml` | **FROZEN** |

### 9.2 Navigation — **bu pack navigasyona DOKUNMAZ**

FU06 `_LayoutTenantShell.cshtml` içine `crm.cycle-period.read` guard'lı `<li>`'yi **zaten ekledi**.
Rota adı (`/CRM/CyclePeriods`) **değişmediği** için menü girdisi **olduğu gibi çalışır**.
`_LayoutTenantShell.cshtml` bu pack'te **protected**'tır (§6) — FU06'nın dar istisnası **tekrarlanmaz**.

---

## 10. Backend File Convention

**Golden Reference kanonik düzeni KORUNUR** (FU06'nın D-FILES kararı). Mevcut `Features/CyclePeriod/`
ağacı zaten bu düzendedir; FU07 **yeni bir düzen icat etmez**:

```text
Features/CyclePeriod/
├── Commands/                       (mevcut 4 — Create/Update DEĞİŞİR)
├── Queries/                        (mevcut 5 + GetCyclePeriodScopeOptionsQuery.cs YENİ)
├── Handlers/
│   ├── CommandHandlers/            (mevcut 4 — Create/Update DEĞİŞİR)
│   └── QueryHandlers/              (mevcut 5 + GetCyclePeriodScopeOptionsHandler.cs YENİ)
├── Validators/                     (mevcut 2 — ikisi de DEĞİŞİR)
├── Rules/
│   ├── CyclePeriodOverlapRules.cs       (DEĞİŞİR — scope tuple)
│   ├── CyclePeriodResolveEngine.cs      (DEĞİŞİR — 4 seviye)
│   └── CyclePeriodScopeRules.cs         (YENİ — saf: normalize + invaryant + legacy türetme)
├── Read/
│   ├── ICyclePeriodReader.cs            (DEĞİŞİR — imza)
│   ├── CyclePeriodReader.cs             (DEĞİŞİR)
│   └── ITerritoryBusinessUnitCatalog.cs (YENİ — dar okuma seam'i)
├── Services/
│   └── ICyclePeriodLegalEntityValidator.cs  (YENİ — Infrastructure'da implemente edilir)
├── Contract/                       (mevcut 2 — flags DEĞİŞİR)
├── CyclePeriodPermissions.cs       (DEĞİŞMEZ)
├── CyclePeriodMapper.cs            (DEĞİŞİR)
├── CyclePeriodValidation.cs        (DEĞİŞİR)
└── CyclePeriodModels.cs            (DEĞİŞİR — TEK dosyada tüm DTO'lar, kural korunur)
```

**Infrastructure (yeni klasör — CRM'de `Segmentation/` emsali):**

```text
src/Diten.CrmService.Infrastructure/CyclePeriod/
├── MdmCyclePeriodLegalEntityValidator.cs   (YENİ — HttpClient, Gateway üzerinden, cache YOK)
└── TerritoryBusinessUnitCatalog.cs         (YENİ — ITerritoryModelRepository'yi SARAR, salt-okunur)
```

**Naming (tartışmasız, FU06'dan):** Command `{Verb}{Module}Command` · Query `Get{Module}{Qualifier}Query` ·
Handler `{Verb}{Module}Handler` (**suffix YOK**) · Validator `{Verb}{Module}Validator` (**suffix YOK**).

**Response envelope:** tüm endpoint'ler `Response<T>` döner.

---

## 11. Frontend File Contract

### 11.1 Golden reference kararı — Slim → **Compact** (türetme GÖSTERİLİR)

| # | Create/Edit formundaki kullanıcı alanı | FU06 | FU07 |
|---|---|---|---|
| 1 | `CycleCode` | ✅ | ✅ |
| 2 | `CycleName` | ✅ | ✅ |
| 3 | `Year` | ✅ | ✅ |
| 4 | `SequenceInYear` | ✅ | ✅ |
| 5 | `StartDate` | ✅ | ✅ |
| 6 | `EndDate` | ✅ | ✅ |
| 7 | `Description` | ✅ | ✅ |
| 8 | `BusinessUnitId` | ✅ (serbest text) | ✅ (Territory-daraltılmış select) |
| 9 | **`ScopeType`** | — | **YENİ** (select — scope seviyesini seçer) |
| 10 | **`CountryScope`** | — | **YENİ** (select — MOD-0048 `country`) |
| 11 | **`LegalEntityId`** | — | **YENİ** (select — MDM lookup) |
| — | ~~`CycleStatus`~~ | form dışı | **form dışı KALIR** (lifecycle aksiyonu) |
| — | ~~`BusinessUnitSource`~~ | — | **form dışı** (server damgası) |

**Toplam = 11 > 8 → `golden_reference: compact`.**

> FU06 §11.1 bu riski **yazılı olarak öngörmüştü**: *"8, Slim sınırının tam üstüdür. İleride tek bir
> kullanıcı alanı eklenirse pack Compact'a düşer ve bu yeniden yetkilendirme gerektirir."* Bu FU o
> yeniden yetkilendirmedir.
>
> **Alternatif sayım reddedildi:** *"scope referansı tek bir alan sayılsın, çünkü aynı anda yalnız biri
> görünür"* argümanı **kabul edilmez**. Golden kuralı **form alanlarını** sayar, aynı anda görünenleri
> değil; ve üç ayrı kontrol, üç ayrı doğrulama kuralı ve üç ayrı seçenek kaynağı vardır. 11 ≠ 9
> tartışması sonucu **değiştirmez** (ikisi de > 8).

### 11.2 Dosya seti — kanonik **Compact**

```text
frontend/Diten.Web/Views/CRM/CyclePeriods/
├── Index.cshtml                    (Layout = "_LayoutTenantShell" AÇIKÇA)
├── Create.cshtml                   (YENİ — Compact-özel)
├── Edit.cshtml                     (YENİ — Compact-özel)
├── Details.cshtml                  (YENİ — Compact-özel)
├── _Form.cshtml                    (YENİ — Compact-özel; Create+Edit paylaşır)
├── _Filter.cshtml
├── _DataTable.cshtml               (data-dt-standard="v2" + skeleton)
├── _IndexL10n.cshtml
└── CyclePeriodsIndex.cs            (marker class — değişmez)
```

**SİLİNECEK (Compact'ta YASAK — FU06'dan devralınan Slim dosyaları):**

```text
Views/CRM/CyclePeriods/_CreateEditOffcanvas.cshtml     ← SİL
Views/CRM/CyclePeriods/_DetailsQuickView.cshtml        ← SİL
```

> **Hibrit YASAK.** MOD-0162-FU03'ün kaydı açıktır: hem offcanvas hem ayrı sayfa taşıyan bir sayfa
> **hiçbir** verifier referansını geçemez (compact 87/8, slim 80/10). Ya Slim ya Compact — ikisi birden değil.

```text
frontend/Diten.Web/wwwroot/assets/js/CRM/CyclePeriods/
├── index.js                        (DEĞİŞTİR — offcanvas kaldırılır; satır aksiyonları Edit/Details'e gider)
├── form.js                         (YENİ — cascading scope seçici)
└── index.l10n.js                   (DEĞİŞTİR — camelCase → PascalCase köprüsü KORUNUR)

frontend/Diten.Web/Resources/Views/CRM/CyclePeriods/
└── CyclePeriodsIndex.{ar,en,es,fr,ru,tr,zh}.resx     (DEĞİŞTİR — 7 dil, parite zorunlu)
```

### 11.3 UI davranış kararları

| Konu | Karar |
|---|---|
| **Cascading scope seçici** | `ScopeType` seçimi, altındaki **tek** referans kontrolünü açar; diğer ikisi **gizlenir ve temizlenir** (gizli ama dolu bir alan, sunucuda invaryant ihlaline yol açardı) |
| Sıra | `ScopeType` → (`country` ise) ülke → (`legal-entity` ise) tüzel kişi → (`business-unit` ise) **önce opsiyonel ülke filtresi, sonra** BU adayları |
| **BU aday listesi** | `country` + `startDate` + `endDate` değiştikçe yeniden yüklenir. Liste boşsa **açık mesaj**: *"Seçilen ülke ve tarih aralığında etkin bir saha planı yok"* — sessiz boş select **YASAK** |
| **Plan-dışı BU** | Kullanıcı aday listesinde olmayan **geçerli** bir kod girerse kayıt **kabul edilir** ve satırda *"plan dışı"* rozeti gösterilir (`BusinessUnitSource = manual`) |
| Reference hazır değilse | `countryReady=false` / `legalEntityReady=false` → select **boş** + uyarı bandı + kaydet **denenebilir ama 400 alır** (fail-closed; hardcoded liste **YASAK**) |
| `ScopeType` düzenlemede | **Edit'te disabled** (immutable). Değiştirmek isteyen kullanıcıya *"yeni bir dönem oluşturun"* denir |
| Lifecycle aksiyonları | `Activate` (yalnız `draft`) / `Close` (yalnız `draft`/`active`); `closed` satırda hiçbir mutasyon aksiyonu **render edilmez**. **FU06'dan aynen** |
| Bulk action bar | **Bulk-delete YOK** — FU06'dan aynen |
| Çakışma hatası | `409 cycle_period_overlap` → form-level hata + çakışan dönemin **kodu, tarih aralığı ve scope'u** (scope eklendi: kullanıcı hangi *uzayda* çakıştığını bilmeli) |
| `resolve-active` rozeti | Sayfa üstünde salt-okunur "Şu an geçerli dönem" + **`resolvedScopeType` rozeti**. `none` → *"aktif dönem yok"*, `ambiguous` → **uyarı** (sessizce ilk kaydı seçmek **YASAK**) |
| Liste kolonu | Tek bir **"Scope"** kolonu: `Tenant-wide` / `TR` / `Acme GmbH` / `alpha` biçiminde, `ScopeType` rozetiyle |
| Filtre | `Year` · `Status` · **`ScopeType`** · **`Country`** · **`BusinessUnit`** · `q` — `dt-inline-filter-host` sınıfı **zorunlu** |
| Tarih girişi | Gün hassasiyeti; saat/zaman dilimi gösterilmez. **FU06'dan aynen** |

---

## 12. Validation Rules

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| `ScopeType` | **Evet** | `CyclePeriodScopeTypes.All` içinde; oluşturma sonrası **immutable** | index yok | mevcut kayıtla karşılaştırma |
| `CountryScope` | `scopeType=country` ise Evet | trim + **UPPER**, `^[A-Z]{2}$` | — | `IReferenceDataValidator.ValidateAsync("country", value)` → **fail-closed** |
| `LegalEntityId` | `scopeType=legal-entity` ise Evet | geçerli `Guid`, boş Guid **reddedilir** | — | `ICyclePeriodLegalEntityValidator.ValidateAsync(id)` → `ACTIVE` + `Referenceable`; **persist'ten ÖNCE** |
| `BusinessUnitId` | `scopeType=business-unit` ise Evet | trim, max 60, boş-olmayan | — | `IReferenceDataValidator.ValidateAsync("business-unit", value)` → **fail-closed** |
| **scope invaryantı** | **Evet** | `ScopeType`'a ait referans dolu **VE** diğer üçü **null**; `tenant` için **hepsi null** | — | `CyclePeriodScopeRules.HasConsistentScope` |
| `CycleCode` | Evet | trim, max 40, `^[A-Za-z0-9._-]+$`, create sonrası immutable | — | `ListByCodeAsync` (tenant geneli, **scope'tan bağımsız**) |
| `CycleName` | Evet | trim, max 200 | — | — |
| `Year` | Evet | 2000 ≤ Year ≤ 2100 | — | — |
| `SequenceInYear` | Evet | 1 ≤ n ≤ 99 | — | `(ScopeType, ScopeRef, Year, Seq)` benzersizliği |
| `StartDate` / `EndDate` | Evet | UTC gün başına normalize; `EndDate > StartDate` | — | — |
| `Description` | Hayır | max 2000 | — | — |
| `CycleStatus` | Evet (server) | payload'dan **kabul edilmez** | — | — |
| — (küme) | — | `activate` sırasında **aynı `(ScopeType, ScopeRef)`** içinde `active` çakışma yok | — | `FindActiveOverlaps` |
| — (lifecycle) | — | geçiş `D-STATUS` matrisinde olmalı | — | mevcut `CycleStatus` |

**Doğrulama sırası (fail-closed, kesin):**

```text
1. Şekil doğrulaması (FluentValidation)         → 400
2. Scope invaryantı (tek referans)              → 400
3. Reference doğrulaması (country / BU)         → 400 (değer) | 400 (set yayınlanmamış, AYRI mesaj)
4. MDM legal-entity doğrulaması                 → 400 (referanslanamaz) | 503 (bağımlılık ulaşılamaz)
5. Benzersizlik (CycleCode, sequence)           → 409
6. Lifecycle / immutability kapıları            → 409
7. Çakışma (yalnız activate / active-update)    → 409
8. PERSIST
```

> **Kural:** 4. adım **kesinlikle** 8'den önce ve **yazma olmadan** çalışır. Bir `InsertAsync`/`ReplaceAsync`
> çağrıldıktan sonra doğrulama yapılması, Working Calendar FU03'te açıkça yasaklanmış bir hatadır ve burada
> da yasaktır (sıralama testi §17.2).

---

## 13. Failure Path to Verify

- **Scope invaryant ihlali** (`scopeType=country` ama `businessUnitId` de dolu)
  - Expected: **400** `cycle_period_scope_ambiguous` + kayıt oluşmaz
- **`scopeType=tenant` ama bir referans dolu**
  - Expected: **400** (sessizce temizlenmez — kullanıcı ne istediğini bilmiyor demektir)
- **Bilinmeyen `scopeType`** → **400**; asla `tenant`'a düşürülmez
- **Yayınlanmamış `country` seti** → **400** + *"reference set not published"*; **hardcoded fallback YOK**
- **Geçersiz ülke kodu** (sette yok / deprecated) → **400** `country_unknown`
- **MDM ulaşılamaz** (timeout / 5xx / auth reddi) → **503** + **hiçbir şey persist edilmez**
- **MDM 404 / `Referenceable=false` / `LifecycleState != ACTIVE`** → **400** `legal_entity_not_referenceable`
  (**503 değil** — bağımlılık konuştu)
- **Territory aday listesi boş** → **200** + boş liste + `businessUnitReady: false`; **kayıt yine de mümkün**
- **`ScopeType` değiştirme denemesi** (`PUT`) → **409** `cycle_period_scope_immutable`
- **Aynı scope'ta `(Year, Sequence)` tekrarı** → **409** `cycle_period_sequence_taken`
- **Farklı scope'ta aynı `(Year, Sequence)`** → **201** (ayrı uzaylar)
- **Aynı `CycleCode`, farklı scope** → **409** (kod tenant genelindedir)
- **Aynı scope'ta çakışan `activate`** → **409** `cycle_period_overlap`, kayıt **`draft` kalır**
- **Farklı seviyede çakışan `activate`** (`country:TR` × `business-unit:alpha`) → **200** (serbest)
- **`resolve-active` bir seviyede iki aktif kayıt** → **200** + `ambiguous` + `candidateIds` +
  `resolvedScopeType`; **üst seviyeye DÜŞMEZ**
- **Concurrency conflict** (`expectedVersion` eski) → **409**, sessiz overwrite **YOK**
- **Unauthorized actor** → **403**; menü girdisi görünmez
- **Cross-tenant id** → **404** (403 değil); liste **boş**
- **`mdm.legal-entities.read` izni olmayan aktör** legal-entity scope'lu dönem açar → **503** (validator 403'ü
  *"bilmiyorum"* sayar) + açık mesaj. **Bu senaryo dev ortamında BEKLENEN'dir** (§19.3/P2, F-MDM-PERM)
- **Upstream 204 proxy'den geçerken** (`activate` / `close`) → frontend proxy **500 vermez**
- **FU06 regresyonu:** FU06 smoke script'inin **18 adımının tamamı** hâlâ PASS

---

## 14. Authorization Convention

```text
Policy:     [Authorize]                                   // shell: tenant
Permission: [HasPermission("crm.cycle-period.{action}")]   // PKS-001
Actor type: tenant_user (platform_admin otomatik geçer)
```

| Anahtar | Kapsam | Değişim |
|---|---|---|
| `crm.cycle-period.read` | list · byId · selector · contract · resolve-active · **scope-options** | **YENİ uç eklendi**, anahtar aynı |
| `crm.cycle-period.manage` | create · update | **DEĞİŞMEZ** |
| `crm.cycle-period.activate` | activate · close | **DEĞİŞMEZ** |

**Kararlar:**

- **Yeni anahtar YOK.** Scope'un genişlemesi bir **yetki** genişlemesi değildir: dönem yazabilen aktör zaten
  tenant genelinde yazabiliyordu. Ülke/tüzel kişi bazında **yetki ayrımı** (ör. *"yalnız TR dönemleri"*)
  gerçek bir talep hâline gelirse **F-SCOPE-RBAC** ile tasarlanır — bugün spekülatif olurdu.
- **`scope-options` `.read` altındadır.** Ülke listesi, tüzel kişi listesi ve BU adayları PII içermez;
  ayrı bir anahtar ölü anahtar olurdu (FU06'nın `.resolve` gerekçesinin aynısı).
- **YENİ — türetilmiş izin bağımlılığı:** legal-entity doğrulaması **çağıranın token'ıyla** MDM'e gider ve
  `mdm.legal-entities.read` ister. Bu izin CRM rollerine **bu pack tarafından verilmez** (seed/grant yok).
  İzinsiz aktör için validator 403 alır ve bunu `Unavailable` sayar → **503**. Bu **bilinen ve belgelenmiş**
  bir boşluktur → **F-MDM-PERM**.
- **Seed/grant bu pack'te YOK.** `CyclePeriodPermissions.cs` **değişmez**.
- **DEV-ONLY fallback KORUNUR:** `ReadFallback = crm.territory.read`, `ManageFallback = crm.territory.model.manage`.
  Fallback **hiçbir guard'ı genişletmez** — tenant izolasyonu, lifecycle, çakışma, scope invaryantı ve
  fail-closed vokabüler aynen çalışır. F-RBAC açık kalır.

---

## 15. Gateway / API Routing Decision

**Karar: Gateway değişikliği GEREKMİYOR.**

- Kanıt (2026-08-28, `gateway/Diten.ApiGateway/ocelot.json:2266-2300`): FU06'nın F-GATEWAY takibiyle
  eklenen route çifti **mevcuttur**:
  - `/api/crm/cycle-periods` → `localhost:5061` (GET, POST, OPTIONS)
  - `/api/crm/cycle-periods/{everything}` → `localhost:5061` (GET, POST, PUT, OPTIONS)
- FU07'nin **tek yeni ucu** (`/scope-options`) `{everything}` wildcard'ının **altındadır** ve **GET**'tir →
  mevcut route yeter.
- FU07 `DELETE` veya `PATCH` **eklemediği** için mevcut method listesi de yeterlidir.
- **Doğrulama zorunlu (P3):** implementasyon sırasında `OPTIONS /api/crm/cycle-periods/scope-options`
  ile probe edilir. **404 + `{}` gövdesi** görülürse bu *"endpoint yok"* değil **"route yok"** imzasıdır ve
  F-GATEWAY yeniden açılır (bilinen teşhis deseni).
- **Bu pack `ocelot.json`'a YAZMAZ.** Gerekirse ayrı bir `integration-agent` task'ıdır.
- Browser JS **servis portuna gitmez**; yalnız same-origin proxy.

**MDM ve reference-data çağrıları:** CrmService → Gateway (`http://localhost:5000`) üzerinden gider
(`Gateway:BaseUrl`). `/api/legal-entities/{everything}` route'u **mevcuttur** (`ocelot.json:215-223`);
`/api/v1/reference-data/...` yolu CRM tarafından **zaten kullanılmaktadır** (`GatewayReferenceDataValidator`).
**Yeni route gerekmez.**

---

## 16. Acceptance Criteria

**Kimlik & yönetişim**

1. `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0165-FU07 --name "Cycle Period" --parent MOD-0165` → **exit 0**.
2. Pack `status: draft` ve `runtime_code_allowed: false` iken **hiçbir** runtime dosyası değişmemiştir
   (`git status` — yalnız bu pack dosyası).

**Scope modeli & invaryant**

3. `POST` — `scopeType` eksik veya bilinmeyen → **400**; `tenant`'a **düşürülmez**.
4. `scopeType` ile referans **uyuşmazlığı** (fazla veya eksik referans) → **400** `cycle_period_scope_ambiguous`.
5. `scopeType=tenant` → üç referansın **hepsi null** olmalı; biri doluysa **400**.
6. `PUT` ile `scopeType` değiştirme → **409** `cycle_period_scope_immutable`; `draft`'ta bile.
7. `CountryScope` küçük harfle gönderilse de **UPPER** persist edilir; `country` setinde yoksa **400**.
8. `BusinessUnitId`, `business-unit` setinde yoksa **400** — FU06'nın *"opak, master okunmaz"* davranışı
   **artık geçerli değildir** ve bu bilinçli bir sıkılaştırmadır.
9. `LegalEntityId` MDM'de `ACTIVE`+`Referenceable` değilse **400**; MDM ulaşılamazsa **503** ve
   **hiçbir kayıt oluşmaz** (koleksiyon sayımı ile doğrulanır).

**Benzersizlik & çakışma**

10. `(ScopeType, ScopeRef, Year, Sequence)` tekrarı → **409**; **farklı** scope'ta aynı `(Year, Sequence)` → **201**.
11. `CycleCode` tenant genelinde unique kalır — **farklı scope'ta bile 409** (`closed` kayıt dâhil).
12. **Migrasyon ispatı:** `ScopeType` alanı olmayan eski satırlarla dolu bir kümede, hiçbir satır yeni bir
    benzersizlik/çakışma ihlali **kazanmaz** ve var olan hiçbiri **kaybolmaz** (§8.7 —
    `CyclePeriodMigrationCompatTests`).
13. `activate` — **aynı** `(ScopeType, ScopeRef)` içinde kesişen `active` dönem varsa **409**, kayıt `draft` **kalır**.
14. `activate` — **farklı** `ScopeType` veya **farklı** `ScopeRef` ile kesişme → **200** (seviyeler arası serbest).
15. `StartDate(n+1) == EndDate(n)` aynı scope'ta → **409** (EndDate dâhil).

**Resolve (4 seviye)**

16. Yalnız `tenant` seviyesinde dönem varken `resolve-active?at=X&businessUnitId=Y` → `resolved` +
    `resolvedScopeType: "tenant"` (fallback çalışır).
17. Hem `business-unit:Y` hem `tenant` seviyesinde kapsayan dönem varken → **BU kazanır**;
    `resolvedScopeType: "business-unit"`; **birleştirme yok**.
18. `legalEntityId` verilmemişse legal-entity seviyesi **hiç sorgulanmaz** — o seviyede kapsayan dönem olsa
    **bile** cevabı etkilemez.
19. `country=TR` verilmiş, BU ve LE verilmemişse → country seviyesinde kapsayan dönem `resolved`; yoksa
    `tenant`'a düşer.
20. Bir seviyede iki aktif kapsayan dönem varsa → `ambiguous` + `candidateIds` + `resolvedScopeType`;
    **üst seviyeye DÜŞMEZ** ve hiçbir dönem seçilmez.
21. Hiçbir seviyede kapsayan dönem yoksa → `none`; **en yakın dönem döndürülmez**.
22. **FU06 geriye-uyum:** `resolve-active?at=X&businessUnitId=Y` çağrısı, country/legal-entity satırları
    veritabanında **var olduğu hâlde**, FU06 ile **birebir aynı** sonucu verir.

**Territory türetmesi & MDM**

23. `scope-options?country=TR&startDate=&endDate=` — dönen `businessUnits[]`, yalnız `Status=active`,
    `CountryScope=TR` ve effective penceresi **kesişen** TerritoryModel'lerin `BusinessScopes[].ScopeCode`
    değerlerinden **tekilleştirilmiş** kümedir.
24. Aday listesi **kapı değildir**: listede olmayan ama `business-unit` setinde **yayınlı** bir kod **201**
    ile kabul edilir ve `BusinessUnitSource = "manual"` damgalanır.
25. Territory'de eşleşen plan yokken `scope-options` → **200** + boş `businessUnits[]` +
    `businessUnitReady: false`; hardcoded liste **yok**.
26. CyclePeriod handler'larının **hiçbiri** `ITerritoryModelRepository`'yi enjekte etmez
    (yapısal/reflection testi); Territory'ye **hiçbir yazma çağrısı** yoktur.
27. MDM doğrulayıcısı **cache tutmaz** (aynı id iki kez → iki HTTP çağrısı) ve `CyclePeriodReader` tarafından
    **enjekte edilmez**.

**Sınır ihlali yokluğu (yapısal)**

28. `Domain/Entities/Campaign.cs`, `VisitFrequencyPolicy.cs`, `StrategyTemplate.cs`, `TerritoryModel.cs`,
    `TerritoryBusinessScope.cs` **diff'te yer almaz**.
29. `contract` endpoint'i §8.2 bayraklarını **birebir** döner; FU06'nın 12 kapalı bayrağının **hiçbiri**
    `true` olmamıştır; `supportsCampaignBinding` **`false`**.
30. `supports*: false` olan hiçbir yetenek için endpoint veya kod yoktur (`reschedule`, `new-version`,
    `working-days`, `apply`, `generate`, `bulk-delete`, `DELETE`, `PATCH` **yok**).

**Frontend (Compact)**

31. `Views/CRM/CyclePeriods/*.cshtml` dosyalarının **hepsinde** `Layout = "_LayoutTenantShell";` açıkça yazılıdır.
32. Compact dosya seti tamdır (`Create` + `Edit` + `Details` + `_Form` **var**); `_CreateEditOffcanvas.cshtml`
    ve `_DetailsQuickView.cshtml` **SİLİNMİŞTİR** (dosya sisteminde yok).
33. `py .antigravity/scripts/verify_datatable_page.py . --area CRM --module CyclePeriods --reference compact --api-profile proxy`
    çalıştırılır; sonuç **kaydedilir**; **beklenen N/A FAIL seti dışında** PASS (§17.1).
34. Browser JS hiçbir yerde `5000`/`5061` portunu, Gateway URL'ini veya `Bearer` token'ı kurmaz.
35. Cascading seçici: `ScopeType` değişince diğer referans alanları **gizlenir ve temizlenir**;
    gizli-ama-dolu bir alan sunucuya **gönderilmez**.
36. 7 dil RESX parite: `{ar,en,es,fr,ru,tr,zh}` aynı anahtar kümesine sahiptir; **yeni scope anahtarları
    7 dilde de vardır**; eksik anahtar yoktur.
37. `window.L10n` köprüsü çalışır (toast'larda `(undefined: …)` yok).
38. `closed` satırda `Activate`/`Close`/`Edit` aksiyonları **render edilmez**; `Details` render edilir.
39. `ambiguous` resolve sonucu UI'da **uyarı** olarak görünür; `resolvedScopeType` rozeti gösterilir.
40. BU aday listesi boşken **açık mesaj** görünür; sessiz boş select **yoktur**.

**Kalite kapıları**

41. `dotnet build` (CrmService + Diten.Web) **PASS**. (Gateway değişmediği için build'i **regresyon** amaçlıdır.)
42. FU06'nın **57 test metodunun tamamı** hâlâ PASS (imza değişimi nedeniyle derlemesi güncellenebilir,
    **davranış beklentileri değişemez**); yeni testler §17.2 hedefini karşılar.
43. `scripts/smoke-mod0165-fu06-cycle-period-authenticated.ps1` **18/18 PASS** (regresyon kanıtı).
44. `scripts/smoke-mod0165-fu07-cycle-period-scope-authenticated.ps1` **tüm adımlar PASS**
    (script çalıştırılmadan "PASS" **rapor edilmez**).

---

## 17. Test Expectations

### 17.1 Build & statik doğrulama

| Kontrol | Komut / beklenti |
|---|---|
| Backend build | `dotnet build services/Diten.CrmService/Diten.CrmService.sln` → **0 error** |
| Frontend build | `dotnet build frontend/Diten.Web/Diten.Web.csproj` → **0 error** (fleet kilidi varsa `-p:BaseOutputPath=.tmp-x/` veya `-t:CoreCompile`) |
| DataTable verifier | `py .antigravity/scripts/verify_datatable_page.py . --area CRM --module CyclePeriods --reference compact --api-profile proxy` |
| RESX parite | 7 dil anahtar kümesi eşit |
| Module id gate | `verify_module_id.py … --check-id MOD-0165-FU07` → **exit 0** |

> **Beklenen verifier delta (ÖNEMLİ — referans DEĞİŞTİ):** FU06 `--reference slim` ile ölçülmüştü ve
> **7 kontrol** beklenen N/A FAIL üretiyordu (bulk-delete seti + `reloadWithToast`). FU07 `--reference compact`
> ile ölçülür; bu **farklı bir kontrol kümesidir** ve FU06 sayıları **doğrudan karşılaştırılamaz**. Kurallar:
> 1. Compact baseline **implementasyon başında** bir kez çalıştırılıp **ham çıktısı** kaydedilir.
> 2. Bulk-delete kontrolleri **hâlâ** beklenen N/A FAIL'dir (modül archive/close-only, `supportsBulkDelete: false`).
> 3. `reloadWithToast` kontrolü **hâlâ dürüstçe FAIL kalır** — sayfa client-side'dır ve paylaşılan helper
>    `dt.ajax.reload()` çağırır. FU06'nın S1 kararı (*"yeşil bir kontrol olmayan bir davranışı iddia edemez"*)
>    **aynen geçerlidir**; yerel yardımcı `reloadAndToast` adını korur.
> 4. Verifier sayıları **her zaman yeniden çalıştırılarak** doğrulanır; hiçbir ajanın kendi bildirdiği sayı
>    kanıt sayılmaz.

### 17.2 Backend unit/integration testleri — hedef **≥ 45 YENİ test** (mevcut 57 korunur)

| Grup | Kapsam |
|---|---|
| **Scope invaryantı** | 4 `ScopeType` × doğru referans → geçerli · fazla referans → 400 · eksik referans → 400 · `tenant` + referans → 400 · bilinmeyen tip → 400 |
| **Scope immutability** | `draft`'ta `ScopeType` değişimi → 409 · `active`'te → 409 · `draft`'ta scope **referansı** düzeltmesi → 200 |
| **Normalizasyon** | ülke küçük harf → UPPER · BU kodu trim · `ScopeRef` türetimi 4 tip için doğru · Guid `"D"` formatı |
| **Benzersizlik** | aynı scope `(Year,Seq)` → 409 · farklı `ScopeType` aynı `(Year,Seq)` → serbest · farklı `ScopeRef` → serbest · `CycleCode` scope'tan **bağımsız** 409 |
| **Çakışma** | aynı scope `active` çakışma → 409 · **farklı seviye çakışma → SERBEST** · farklı `ScopeRef` → serbest · `draft`/`closed` engellemez · bitişik gün çakışır · `excludeId` kendini saymaz |
| **Resolve — precedence** | BU kazanır · LE kazanır · country kazanır · tenant'a düşer · **boş seviye atlanır** · **ambiguous üst seviyeye düşmez** · `resolvedScopeType` her vakada doğru · birleştirme **yok** |
| **Resolve — geriye-uyum** | FU06 çağrı şekli (`at` + `businessUnitId`) country/LE satırları varken **FU06 ile birebir aynı** · `at` tek başına → yalnız tenant |
| **Migrasyon (§8.7)** | `ScopeType` alanı olmayan satır → `tenant`/`business-unit` türetilir · türetilmiş satırla yeni satır **çakışmaz** · anahtar denkliği (birebir-örten eşleme) · yazma sonrası `ScopeType` persist edilir |
| **Country doğrulaması** | set yayınlanmamış → 400 (AYRI mesaj) · değer yok → 400 · deprecated → 400 · geçerli → 201 · **hardcoded fallback yok** (validator çağrılmadan geçilemez) |
| **Legal-entity doğrulaması** | `ACTIVE`+`Referenceable` → 201 · 404 → 400 · `Referenceable=false` → 400 · timeout/5xx → **503 + persist yok** · 403 → 503 · **cache yok** (iki çağrı) · **persist ÖNCESİ** çağrıldığı (sıralama testi) |
| **Territory türetmesi** | ülke filtresi · pencere kesişimi (tam/kısmi/hiç) · `EffectiveTo == null` açık uçlu · yalnız `active` model · tekilleştirme · `business-unit` olmayan scope tipleri **elenir** · boş sonuç → `businessUnitReady: false` |
| **Sınır (yapısal)** | Reader `HttpClient` **kullanmaz** · Reader **hiçbir** write metodu çağırmaz · Reader legal-entity validator'ı **enjekte etmez** · handler'lar `ITerritoryModelRepository` / `ICampaignRepository` / `IVisitFrequencyPolicyRepository` / `IStrategyTemplateRepository` **enjekte etmez** (compile-time + reflection) |
| **Contract** | 12 kapalı bayrak **kapalı** · 5 yeni açık bayrak doğru · 4 yeni kapalı bayrak doğru · `supportsCampaignBinding: false` |
| **Regresyon** | FU06'nın 57 test metodunun **davranış beklentileri** değişmemiştir |

### 17.3 Authenticated smoke (Gateway)

**İki script çalıştırılır:**

**(a) `scripts/smoke-mod0165-fu06-cycle-period-authenticated.ps1` — DEĞİŞTİRİLMEZ.**
18/18 PASS **zorunludur**. Bu, §2.5 regresyon sınırının tek gerçek kanıtıdır. Script'in bir adımı FU07
yüzünden düşerse **FU07 hatalıdır**, script değil.

**(b) `scripts/smoke-mod0165-fu07-cycle-period-scope-authenticated.ps1` — YENİ.**
Tenant-scoped token (`X-Tenant-Id` header'ı **zorunlu**; yoksa platform tenant'ı için token alınır ve
testler yanıltıcı olur):

1. Login (tenant-scoped) → 2. `contract` (16 kapalı + 5 açık bayrak) → 3. `scope-options` (üç `ready` bayrağı) →
4. `scopeType` eksik → 400 → 5. scope invaryant ihlali → 400 → 6. geçersiz ülke → 400 →
7. `country=TR` dönem create (draft) → 8. legal-entity dönem create (MDM doğrulaması) →
9. MDM izinsiz aktörle legal-entity create → 503 (**beklenen dev boşluğu**, F-MDM-PERM) →
10. Territory-türetilmiş BU adayı ile create + `businessUnitSource=territory` →
11. plan-dışı geçerli BU kodu ile create + `businessUnitSource=manual` →
12. aynı scope `(Year,Seq)` tekrarı → 409 → 13. farklı scope aynı `(Year,Seq)` → 201 →
14. `CycleCode` farklı scope'ta tekrar → 409 → 15. `scopeType` PUT → 409 →
16. aynı scope çakışan activate → 409 → 17. **farklı seviye** çakışan activate → 200 →
18. `resolve-active` BU seviyesi → `resolvedScopeType=business-unit` →
19. `resolve-active` LE verilmeden → LE seviyesi **atlanır** →
20. `resolve-active` country → `resolvedScopeType=country` → 21. hiçbir seviye yok → `none` →
22. bir seviyede iki aktif → `ambiguous` (üst seviyeye düşmez) →
23. **FU06 çağrı şekli** (`at`+`businessUnitId`) → FU06 ile aynı sonuç →
24. close 200 → 25. closed'a PUT/activate → 409 → 26. cross-tenant → 404 → 27. concurrency → 409 →
28. selector + list scope filtreleri.

> **PowerShell 5.1 tuzağı (FU06'dan):** `@(Where-Object).Count` — tek elemanlı sonuçta `.Count` yok sayılır;
> sayım her zaman `@()` ile sarılır.

### 17.4 Browser smoke

- `/CRM/CyclePeriods` yüklenir, DataTable v2 render eder, skeleton kaybolur, **Scope kolonu** görünür.
- **Create sayfası** (offcanvas **değil**) açılır; `ScopeType` seçimi cascading olarak doğru kontrolü açar.
- `country` seçilince BU aday listesi yeniden yüklenir; tarih değişince **tekrar** yüklenir.
- Aday liste boşken açık mesaj görünür (sessiz boş select **yok**).
- Reference seti yayınlanmamışken uyarı bandı görünür ve kaydetme 400 alır (**hardcoded liste yok**).
- **Edit sayfasında** `ScopeType` disabled.
- **Details sayfası** (quick view **değil**) scope'u ve `BusinessUnitSource` damgasını gösterir.
- Çakışma 409 → form-level hata + çakışan dönemin kodu, aralığı **ve scope'u** görünür.
- `resolve-active` rozeti `resolvedScopeType` ile birlikte görünür; `ambiguous` uyarı olarak.
- Filtre chip'leri (`dt-inline-filter-host`) yeni scope filtreleriyle çalışır.
- 7 dilden en az `tr` + `en` + `ar` (RTL) gözle doğrulanır.

---

## 18. Ready-for-dev Checklist

- [ ] **D-listesi (§1.3) kullanıcı tarafından onaylandı** — özellikle **D-SCOPE-SHAPE**, **D-MIGRATION**,
      **D-OVERLAP-SCOPE**, **D-PRECEDENCE**
- [ ] **§2.6 D-COUNTRY-SET onaylandı** — `country` mi `COUNTRY_CODES` mi? (`COUNTRY_CODES` seçilirse
      denklik ispatı P1'e eklenir)
- [ ] **§1.3 D-BU-SOURCE onaylandı** — B (daraltılmış seçici + vokabüler kapısı) mı, A (sert kapı) mı?
- [ ] **§19.3 P1** doğrulandı: `country` seti tenant için okunabiliyor (`scope_key` global-set tuzağı çözüldü)
- [ ] **§19.3 P2** kabul edildi: `mdm.legal-entities.read` boşluğu bilinen bir dev kısıtıdır (F-MDM-PERM)
- [ ] **§19.3 P3** planlandı: `scope-options` route probe'u
- [ ] Golden Reference **Compact** referans olarak okundu (`GoldenReferenceCompact` backend + frontend)
- [ ] Frontmatter tüm zorunlu alanlar dolu (`service`, `shell`, `golden_reference`, `entity_base`, `form_field_count`)
- [ ] Layout & Shell Contract'ta Razor `Layout = "_LayoutTenantShell"` açıkça yazılı (§9)
- [ ] Backend File Convention'da `Handlers/CommandHandlers/` + `Handlers/QueryHandlers/` ayrımı korunuyor (§10)
- [ ] Frontend File Contract'ta **Compact** dosya listesi tam; **Slim dosyalarının silineceği** yazılı (§11.2)
- [ ] Validation Rules her field için yazılı + **doğrulama sırası** (§12)
- [ ] Failure Path ≥ 4 senaryo — **20 senaryo var** (§13)
- [ ] Authorization Convention: anahtar değişmiyor + **türetilmiş MDM izin bağımlılığı** yazılı (§14)
- [ ] Gateway kararı açık: **gerekmiyor**, kanıtla (§15)
- [ ] Acceptance criteria test edilebilir (§16, **44 madde**)
- [ ] Test expectations build/verifier/RESX/**iki smoke**/migrasyon ispatını kapsıyor (§17)
- [ ] **FU06 regresyon sınırı (§2.5) kabul edildi** — FU06 smoke script'i silinmez, 18/18 PASS zorunlu
- [ ] `status` → `approved` / `ready-for-dev` ve `runtime_code_allowed` → `true` **kullanıcı tarafından** çevrildi
- [ ] Registry satırı için follow-up hâlâ açık (F-REGISTRY — FU06 **ve** FU07)

---

## 19. Implementation Notes

### 19.1 Sıralama önerisi

1. **P1/P2/P3 pre-flight** (§19.3) — kod yazmadan önce, üçü de **yazılı olarak** cevaplanır.
2. **Domain + Rules (saf)** — `CyclePeriodScopeTypes`, `CyclePeriodScopeRules`, genişleyen `OverlapRules` +
   `ResolveEngine`. **Testler burada yazılır** ve I/O yoktur.
3. **Migrasyon uyumu** — repository okuma-anında türetme + `CyclePeriodMigrationCompatTests`.
   **Bu adım 4'ten önce bitmeli**: kimlik ispatı olmadan yazılan bir handler, ispatı sonradan "uydurmaya" zorlar.
4. **Doğrulayıcılar** — country (mevcut `IReferenceDataValidator` üzerinden) + legal-entity (yeni MDM
   validator) + Territory katalog adaptörü.
5. **Handler'lar + endpoint'ler** — §12 doğrulama sırası **harfiyen**.
6. **Seam imzası** (`ICyclePeriodReader`) — repo-içi tek tüketici FU06'nın kendi handler'ıdır.
7. **Frontend Compact migrasyonu** — Slim dosyaların silinmesi **son adımdır**, ki ara bir commit'te sayfa
   hem offcanvas hem Create sayfası taşımasın (hibrit tuzağı).
8. **İki smoke** — FU06 script'i **önce** (regresyon), sonra FU07 script'i.

### 19.2 Bilinen tuzaklar (bu servis üzerinde daha önce yaşandı — tekrarlanmamalı)

| Tuzak | Önlem |
|---|---|
| **Mevcut** aggregate'e eklenen üyeler class-map'e yazılmazsa **sessizce persist edilmez**; `Guid?` alan ayrıca binary/string uyuşmazlığı üretir | 4 yeni üye class-map'e ilk commit'te eklenir; ilk integration testi bir `legal-entity` dönem yazıp **id ile** okur |
| `DateTimeOffset` BSON'da `[ticks, offset]` dizisidir → iki DTO alanını birlikte index/sort **500** | `StartDate`/`EndDate` **ve** `TerritoryModel.EffectiveFrom`/`EffectiveTo` **hiçbir zaman** birlikte Mongo'da index/sort edilmez; **Territory pencere kesişimi BELLEKTE** yapılır |
| `DateTimeOffset` "instant vs date" karşılaştırması yanlış reddeder | Tüm karşılaştırmalar **normalize edilmiş gün başı** değerler üzerinde |
| Mongo partial index filtresinde `$ne` → **crash-loop** | Benzersizlik **handler'da**; yeni index **kurulmaz** |
| **Global scope'lu reference set'e `scope_key` eklenirse** consumer `scope_key_not_allowed_for_global` döndürür → select **sessizce boş** | **P1 doğrulaması** (§19.3). `GatewayReferenceDataValidator` bugün `scope_key`'i **koşulsuz** ekliyor (`:54`, `:95`, `:168`); `country` seti seed'de **global**'dir |
| **Platform lookup cache anahtarında tenant segmenti yok** (cross-tenant sızıntı riski) | FU07 `/api/lookups/countries` yolunu **hiç kullanmaz**; MOD-0048 consumer yolu **çağıranın token'ıyla** okunur ve cache'lenmez (WC Overrides emsali). **Bu tuzak FU07'de yapısal olarak devre dışıdır** |
| Frontend proxy `ForwardAsync` upstream 204'ü **500'e** çevirir | `activate`/`close` proxy'lerindeki bodyless status guard **korunur**; yeni `scope-options` proxy'si de guard'lı yazılır |
| `index.l10n.js` camelCase→PascalCase dönüşümü atlanırsa `window.L10n` **undefined** | Loader deseni korunur; **yeni scope anahtarları** için de |
| İkinci DataTable `drawCallback`'i global selector kullanırsa ilk tablonun rozetlerini siler | Container-scoped selector zorunlu |
| `.resx` değişiklikleri fleet yeniden başlatılmadan görünmez | Smoke öncesi **fleet restart** |
| Hibrit sayfa (offcanvas + ayrı sayfa) **hiçbir** verifier referansını geçemez | Slim dosyaları **silinir**; ara commit'te hibrit bırakılmaz |
| Dış bağımlılık doğrulaması `InsertAsync`'ten **sonra** çalışırsa yarım kayıt kalır | §12 doğrulama sırası **testle sabitlenir** (sıralama testi) |
| MDM 403'ün **404 gibi** ele alınması, izin eksikliğini *"tüzel kişi yok"* diye gösterir | 403 → `Unavailable` → **503**; asla 400 değil (WC FU03'ün ayrımı) |

### 19.3 Pre-flight — kod yazmadan önce cevaplanması gereken üç soru

| # | Soru | Neden bloke edici | Nasıl cevaplanır |
|---|---|---|---|
| **P1** | Tenant bağlamında `country` seti **okunabiliyor mu**? | `GatewayReferenceDataValidator` `scope_key`'i koşulsuz ekler; `country` seti seed'de **`global`** scope'ludur ve consumer global bir sete scope key'i **reddedebilir** (`scope_key_not_allowed_for_global`). Reddederse **her ülke doğrulaması 400** verir ve modül açılmaz | Canlı tenant token'ıyla `GET /api/v1/reference-data/sets/country/published-values?scope_key={tenantId}` çağrılır; sonuç **ham hâliyle** pack'e eklenir. Reddederse D-COUNTRY-SET'in transport kolu düzeltilir (scope_key'siz okuma — WC Overrides emsali) |
| **P2** | Test edilecek tenant aktörü `mdm.legal-entities.read` iznine **sahip mi**? | Sahip değilse **her** legal-entity scope'lu create **503** verir; bu kodun değil **izin kataloğunun** eksiğidir ve smoke'ta yanıltıcı bir "başarısızlık" üretir | Aktörün izinleri kontrol edilir. Yoksa **F-MDM-PERM** açık kabul edilir ve §17.3 adım 9 **beklenen 503** olarak yazılır (bu pack izin **vermez**) |
| **P3** | `GET /api/crm/cycle-periods/scope-options` Gateway'den geçiyor mu? | `{everything}` wildcard'ının GET taşıdığı doğrulandı (§15) ama probe **yapılmadı** | `OPTIONS /api/crm/cycle-periods/scope-options` probe'u; **404 + `{}`** = route yok imzası → F-GATEWAY yeniden açılır |

**Ek doğrulama (implementasyon başında, bloke edici değil):**
`ITerritoryModelRepository.ListActiveAsync(tenantId, excludeId, ct)` imzasındaki `excludeId` parametresinin
anlamı okunur. FU07 adaptörü `Guid.Empty` geçmeyi planlar; `Guid.Empty` beklenmedik bir filtreleme yapıyorsa
adaptör `ListAsync` üzerinden **kendi** status filtresini uygular (D-TERRITORY-STATUS).

### 19.4 Master-plan bağlantısı

- MOD-0165 parent'ın registry satırı (*"Owns Campaign/CyclePeriod execution"*) bu FU ile **derinleşir**;
  `crm-campaign-core` lane'inin `execution` ve `results` kalemleri **hâlâ açıktır**.
- `cycleperiod-scope-enrichment-followup` kararının *"ayrı bir FU"* şartı bu pack ile **karşılanmıştır**.
- `F-CAMPAIGN-BIND` (Campaign ↔ CyclePeriod bağı) FU06'da olduğu gibi **açık** kalır ve bu pack tarafından
  **çevrilmez**; kullanıcı kararı gereği Campaign'in aynası **FU07 ship olduktan SONRA** değerlendirilir.
- Working Calendar'ın `organization-unit` scope'unun CRM karşılığı **kasten yoktur** (F-ORG-UNIT-SCOPE):
  CRM'de organizasyon birimi bir dönem sahibi değildir; ihtiyaç kanıtı çıkarsa ayrı bir FU'dur.

---

## 20. Follow-up Items

| # | İş | Domain | Neden |
|---|---|---|---|
| **F-REGISTRY** | `module-id-registry.md`'ye **MOD-0165-FU06 ve FU07** satırları | portfolio-delivery | DCP-002 izlenebilirliği — FU06'dan **hâlâ açık** devralındı (bugün yalnız parent satırı var) |
| **F-MDM-PERM** | CRM rollerine `mdm.legal-entities.read` verilmesi (veya CRM'e özel dar bir doğrulama izni) | platform-shared-services | §19.3/P2 — bu pack izin **vermez**; izinsiz aktör 503 alır |
| **F-COUNTRY-SOT** | Üç ülke kaynağının (`country` / platform `countries` / `COUNTRY_CODES`) **tek bir SoT**'a indirilmesi | platform-shared-services / MDM | §2.6 — üç kaynak, üç vokabüler; bu FU **birini seçer**, çelişkiyi çözmez |
| **F-SCOPE-RBAC** | Scope-bazlı yetki ayrımı (*"yalnız TR dönemleri"* / *"yalnız kendi BU'su"*) | platform-shared-services | §14 — bugün spekülatif; gerçek talep çıkarsa tasarlanır |
| **F-ORG-UNIT-SCOPE** | CRM'de `organization-unit` scope seviyesi gerekli mi? | commercial-suite | WC'de var, CRM'de **kasten yok** (`supportsOrganizationUnitScopedCycles: false`) |
| **F-TERRITORY-GATE** | BU aday listesinin **sert kapıya** dönüştürülmesi (D-BU-SOURCE = A) | commercial-suite | Territory kapsamı olgunlaştığında; bugün kimliği değişken bir aggregate'e bağlardı |
| **F-MICROTARGET** | **MOD-0155-FU05 MicroTarget** — genişleyen seam'in ilk gerçek tüketicisi | commercial-suite | FU06 + FU07'nin **varlık nedeni** |
| **F-CAMPAIGN-BIND** | `Campaign` ↔ `CyclePeriod` bağı (kullanıcı kararı: Campaign aynası **FU07'den sonra**) | commercial-suite | `supportsCampaignBinding: false` **KALIR** |
| **F-VFP-FK** | `VisitFrequencyPolicy.CyclePeriodId`'nin var-olan bir döneme işaret ettiğinin doğrulanması | commercial-suite | Bu FU VFP'ye **dokunmaz** |
| **F-CYCLE-CALENDAR** | `VisitFrequencyPolicy.CycleId`'nin master'ı — **gerçekten gerekli mi?** | commercial-suite | D-HIER: hâlâ çözümlenmemiş, hâlâ **açıkça ilan edilmiş** |
| **F-CALENDAR-DAYS** | *"Bu dönemde kaç çalışma günü var?"* — WC × CyclePeriod **MOD-0155'te** | commercial-suite / PSS | İki kavram bu FU'da da **birleştirilmez** |
| **F-RESCHEDULE** | Aktif dönemin tarihlerinin yönetişimli değiştirilmesi | commercial-suite | `supportsCycleReschedule: false` **KALIR** |
| **F-RBAC** | `crm.cycle-period.*` katalog kaydı + rol ataması; `activate`/`close` SoD ayrımı | platform-shared-services | Fallback hâlâ dev-only |
| **F-VOCAB-0048** | `cycle-period-status` **ve** `cycle-period-scope-type` setlerinin MOD-0048'e taşınıp taşınmayacağı | commercial-suite | D-VOCAB-SCOPE in-domain |
| **F-FILE-DRIFT** | Mevcut CRM feature'larının gruplanmış dosya düzeninin Golden Reference'a hizalanması | commercial-suite | FU06'dan devralınır |
| **F-NAV** | Nav'ın katalog-güdümlü hâle gelmesi (MOD-0285) | commercial-suite | FU06'dan devralınır; bu pack nav'a **dokunmaz** |

---

## Ek D — Karar Gerekçeleri (tam)

### D-SCOPE-SHAPE — **Ayrımlı (discriminated) tek seviye**

**Alternatif (reddedildi): kombinasyon modeli** — `CountryScope`, `LegalEntityId` ve `BusinessUnitId`'nin
**aynı anda** dolu olabilmesi.

Reddin üç gerekçesi:

1. **Precedence tanımsız hâle gelir.** `(country=TR, BU=alpha)` ile `(legal-entity=X)` arasında hangisi daha
   özeldir? Kombinasyon modelinde bu sorunun **matematiksel bir cevabı yoktur** — kısmî sıralı bir kafes
   (lattice) oluşur ve toplam sıralama gerektiren *"en özel kazanır"* kuralı uygulanamaz. Kullanıcının talep
   ettiği `BU > legal-entity > country > tenant` **zaten bir toplam sıralamadır** ve ancak ayrımlı modelde
   ifade edilebilir.
2. **Benzersizlik anahtarı null-kafesine düşer.** 3 nullable alan = 8 scope şekli; `(TR, null, alpha)` ile
   `(null, null, alpha)` "aynı" mı? Her cevap bir başka kural gerektirir ve kuralların hepsi handler'da,
   elle yazılır.
3. **Emsal ayrımlıdır.** Working Calendar (`WorkingCalendarScopeType`) tam olarak bu modeli kullanır ve
   SHIPPED'dir. İki modülün iki farklı scope felsefesi taşıması, iki farklı zihinsel model demektir.

**"Peki BU seçilirken kullanılan ülke nereye gidiyor?"** — Hiçbir yere. O ülke bir **yazma aracıdır**,
kimliğin parçası değil: kullanıcının `business-unit:alpha` dönemini kurarken hangi ülkenin planına baktığı,
dönemin **ne olduğunu** değiştirmez. Eğer *"Türkiye'deki alpha"* ile *"Almanya'daki alpha"* gerçekten farklı
iki şeyse, bu bir **MOD-0048 business-unit kodlama** sorunudur (iki farklı kod gerekir), bir CyclePeriod
sorunu değil. Bu sınırı bulanıklaştırmak, dönem master'ına bir **saha planı modeli** kaçırmak olurdu.

*Ara çözüm (A2, gerekirse):* `business-unit` scope'lu satırlara **kimlik-dışı** bir `CountryContext` damgası
eklenebilir (provenance; anahtarda **yok**, resolve'da **yok**). Bugün önerilmiyor — kullanılmayan bir alan,
bir sonraki okuyucu için bir yalan olur.

### D-MIGRATION — **M1: toplamsal + okuma-anında türetme**

Ayrıntılı ispat §8.7'dedir. Buradaki asıl gerekçe **risk asimetrisidir**: bir backfill script'i, hiçbir
davranış farkı üretmeden üretim verisine dokunma riskini alır. Okuma-anında türetme ise **hiçbir riski**
almaz ve aynı sonucu verir; üstelik FU06'nın *"scope daraltması bellekte"* mimarisi sayesinde **hiçbir
sorguyu ıskalatmaz**.

`ScopeType`'ın *"kayıt bir sonraki yazımında doğal olarak taşınması"* kasıtlıdır: hiçbir kayıt
**dokunulmadan** değişmez, ve her kayıt dokunulduğunda doğru şekle **kalıcı olarak** geçer. Bir gün tüm
satırlar `ScopeType` taşıdığında türetme kodu **sessizce ölür** — ama kaldırılması için bir sebep yoktur,
çünkü maliyeti bir `if`'tir.

### D-PRECEDENCE — **BU > legal-entity > country > tenant; boş atlanır, ambiguous durdurur**

Sıra kullanıcı kararıdır ve iş mantığı olarak da doğrudur: bir iş biriminin kendi takvimi, bağlı olduğu tüzel
kişinin takviminden **daha spesifik** bir taahhüttür; tüzel kişi de ülkeden, ülke de tenant'tan.

**"Boş seviye atlanır"** kuralı, geriye-uyumun **tek** mekanizmasıdır (§2.5/#2): FU06 çağrıları
`country`/`legalEntityId` göndermediği için o seviyeler **hiç bakılmaz** ve cevap birebir eski cevaptır.
Alternatif — *"country parametresi yoksa tüm country satırlarına bak"* — FU06 tüketicilerinin cevabını
**sessizce değiştirirdi** ve bu, bir sonraki FU'nun bir öncekini bozmasının klasik yoludur.

**"Ambiguous durdurur"** kuralı FU06'nın *"ambiguous'ta seçim yok"* kararının doğal sonucudur: bir seviyede
iki aktif dönem varsa orada **veri bozuktur**. Üst seviyeye düşmek, bozuk veriyi makul bir cevabın arkasına
saklardı — tam olarak `ambiguous` outcome'ının **var olma sebebinin** tersi.

### D-COUNTRY-ROLE — **Bağımsız scope seviyesi, kimlik anahtarında**

Kullanıcı kararıdır ve modelle tutarlıdır. Alternatif (*"country yalnız bir filtre/nitelik olsun"*)
reddedilir çünkü *"Türkiye için 2026/3. dönem"* cümlesi **bir dönemdir**, bir dönemin **niteliği** değildir.
Nitelik olsaydı iki farklı ülkenin aynı `(Year, Sequence)` çiftini kullanması bir çakışma sayılırdı — oysa bu
tam olarak **olması gereken** durumdur.

Working Calendar'da da `country` **bağımsız bir scope tipidir** (üstelik orada **platform** katmanına aittir).
CRM'de dönem takvimi **tenant'ın** iş kararı olduğu için `country` scope'u **tenant tarafından yazılabilir** —
bu, WC'den bilinçli ve gerekçeli bir farktır (WC'de ülke takvimi tüm tenant'ları etkiler, burada etkilemez).

### D-COUNTRY-SET — **`country` (öneri), `COUNTRY_CODES` değil**

Tam kanıt §2.6'dadır. Özet: FU07'nin BU türetmesi `TerritoryModel.CountryScope` ile **eşitlik
karşılaştırması** yapar; o alan `country` setinin vokabüleridir. Farklı bir set seçmek, hata vermeyen ama
**her zaman boş** bir liste üretir. Bir modülü öldüren en sessiz hata türü budur.

`COUNTRY_CODES`'un tek gerçek avantajı — yönetişimli, operatör tarafından yayınlanan bir set olması —
`country` seti için **de** geçerlidir: o da MOD-0048'de yaşayan, seed'li, yayınlanabilir bir settir
(`legal-entity-reference.json`). Aradaki fark yönetişim değil, **kim kullanıyor** sorusudur; ve CRM'in tamamı
`country` kullanıyor.

**Kullanıcı `COUNTRY_CODES` konusunda ısrar ederse** pack `D-COUNTRY-SET = B` ile güncellenir ve P1'e bir
**denklik ispatı** eklenir. Kod yazılmadan önce `COUNTRY_CODES ⊇ country` ve
`TerritoryModel.CountryScope ⊆ COUNTRY_CODES` **kanıtlanmalıdır**; kanıtlanamazsa BU türetmesi çalışmaz ve
D-BU-SOURCE yeniden açılır.

### D-LEGAL-ENTITY — **MDM cross-service fail-closed, WC FU03 profili birebir**

Working Calendar FU03 bu problemi **zaten çözdü** ve profili kodda duruyor
(`WorkingCalendarLegalEntityValidator`): cache yok · 3 sn toplam timeout · **bir** transient retry
(502/503/504, 75 ms) · `Authorization` + `X-Tenant-Id` + `X-Correlation-Id` forward · **her zaman Gateway
üzerinden** · 404 → *"referanslanamaz"* (400) · timeout/5xx/403/bozuk gövde → *"bilmiyorum"*
(**503, persist yok**).

CRM'de aynı profilin **ikinci** bir kopyası zaten var: `MdmSegmentProductReferenceValidator` (MOD-0167-FU02),
doküman notunda *"transport profile mirrors the Working Calendar legal-entity validator verbatim"* diyor.
FU07 **üçüncü** kopyayı yazar — ve bu **kasıtlıdır**: servisler ayrıdır, ortak bir kütüphane paylaşmak
`Diten.CrmService`'i `Diten.Platform`'a bağlardı. Kopya bir borçtur ve üç kopyanın da **aynı testleri
geçmesiyle** yönetilir.

**Kritik ayrım (WC'den aynen):** 403 bir *"yok"* cevabı **değildir**. İzin eksikliği yüzünden alınan 403'ü
400'e çevirmek, kullanıcıya *"böyle bir tüzel kişi yok"* demek olurdu — oysa vardır, sadece göremiyoruz.
Bu yüzden 403 → **503** (F-MDM-PERM'in görünür olmasının tek yolu).

### D-BU-SOURCE — **B: daraltılmış seçici + vokabüler kapısı**

| Seçenek | Ne yapar | Neden reddedildi / seçildi |
|---|---|---|
| **A — sert kapı** | Create/update, BU'yu **o anki** Territory aday listesine karşı doğrular; listede yoksa 400 | **Reddedildi.** Dönemin kimliği **değişken ve yabancı** bir aggregate'e bağlanır. Territory planı süperseded olduğunda, o BU için açılmış **mevcut** bir dönemin adını düzeltmek bile **imkânsız** hâle gelir (edit yeniden doğrular → 400). Kimlik, başkasının lifecycle'ına rehin verilmez |
| **B — daraltılmış seçici + vokabüler kapısı** ✅ | Aday liste Territory'den türetilir (**UI daraltması**); yazma MOD-0048 `business-unit` published-values'a karşı fail-closed doğrulanır; liste dışı geçerli kod `manual` damgasıyla kabul edilir | **Seçildi.** Kullanıcının *"BU artık opak string değil"* şartını **karşılar** (kod artık reference-doğrulanmıştır ve Territory ile **aynı** doğrulama yolunu kullanır), ama kimliği Territory'nin lifecycle'ına bağlamaz. Ayrıca bir dönem, saha planı **henüz kurulmadan** planlanabilir — gerçek hayatta dönemler planlardan **önce** gelir |
| **C — serbest string** | FU06 durumu | **Reddedildi.** Kullanıcı girdisiyle doğrudan çelişir ve `alpha` ile `Alpha`'nın iki ayrı scope olduğu bir dünyaya kapı açar |

**Provenance damgası (`BusinessUnitSource`) neden kimlik değil:** damga, kaydın **nasıl** oluştuğunu anlatır,
**ne olduğunu** değil. İki dönem, biri seçiciden biri elle girilmiş aynı BU kodunu taşıyorsa **aynı
scope'tadırlar** ve birbirleriyle çakışırlar. Damgayı anahtara koymak, aynı iş birimi için iki paralel takvim
yaratırdı — sessiz ve yıkıcı.

### D-OVERLAP-SCOPE — **Yalnız aynı `(ScopeType, ScopeRef)` içinde**

§8.3'te gerekçelendirildi: seviyeler arası çakışmayı yasaklamak, precedence'ın **kendi varlık sebebini** yok
eder. Bu kadar açık bir mantıksal zorunluluk olmasına rağmen ayrı bir bayrakla
(`supportsCrossScopeOverlapBan: false`) **açıkça ilan edilir**, çünkü bir tüketicinin *"herhâlde hiçbir iki
aktif dönem çakışmaz"* varsayımına kapılması kolaydır ve o varsayım MicroTarget'ta yanlış satırlar üretirdi
(sessiz varsayım yasağı).

### D-GOLDEN — **Compact**

§11.1 türetmesi: 11 > 8. FU06 bu riski **yazılı olarak** öngörmüştü. Hibrit (offcanvas + ayrı sayfa) seçeneği
MOD-0162-FU03'ün kanıtıyla reddedilir: hibrit sayfa **hiçbir** verifier referansını geçemez. Slim dosyalarının
**silinmesi** bu yüzden bir tercih değil, bir **zorunluluktur**.

### D-SEAM-BREAK — **Kırıcı imza değişikliği, overload YOK**

`ICyclePeriodReader`'ın repo genelindeki **tek** tüketicisi FU06'nın kendi `ResolveActiveCyclePeriodHandler`'ıdır
(2026-08-28 doğrulandı); MOD-0155 **henüz yazılmadı**. Dolayısıyla kırıcı değişikliğin **maliyeti sıfırdır** ve
bu, onu yapmak için **son fırsattır**.

**Overload açılmaması** kasıtlıdır: iki imza (biri BU-only, biri 4 seviyeli) iki farklı precedence davranışı
demektir ve tüketici hangisini çağırdığını unuttuğunda hata **sessizdir**. Tek imza, `null` geçilen
seviyelerin atlanmasıyla eski davranışı **zaten** verir (§8.4) — yani geriye-uyum imzada değil, **semantikte**
korunur; bu daha güçlü bir garantidir.

### D-VOCAB-SCOPE — **A (in-domain fail-closed)**

FU06'nın `CyclePeriodStatuses` kararının aynısı ve aynı gerekçeyle: dört değerlik **yapısal** bir liste
(her değer resolve motorunun davranışını değiştirir) için runtime'ı MOD-0048 publish operasyonuna bağımlı
kılmak orantısızdır. `WorkingCalendarScopeType` de **in-domain**'dir ve doküman notu bunu açıkça
gerekçelendirir: *"These are STRUCTURAL: each value changes what the provider does, so a tenant cannot extend
them freely."* Aynı cümle burada da geçerlidir. Taşıma kararı F-VOCAB-0048'de.

### D-TERRITORY-STATUS — **Yalnız `active` TerritoryModel nitelikli sayılır**

`TerritoryModel.Status` MOD-0048 `territory-model-status` setinin bir değeridir (7 değer). Aday türetmesi
yalnız `active` modelleri sayar: bir `draft` plan bir **taahhüt değildir** ve ondan türetilen bir BU listesi,
kullanıcıya var olmayan bir organizasyonu **varmış gibi** gösterirdi. `inactive` / süperseded modeller de
sayılmaz — geçmişte kalmış bir planın iş birimleri bugünün dönemine kaynak olamaz.

### D-CONTRACT — **12 kapalı bayrak aynen kapalı, 5 yeni açık, 4 yeni kapalı**

Bir scope genişletmesi bir **yetenek** genişletmesi değildir. FU06'nın kapalı bayraklarının hiçbiri scope
yüzünden açılmaz — özellikle `supportsCampaignBinding`, çünkü kullanıcı kararı gereği Campaign'in aynası
**FU07 ship olduktan SONRA** ayrıca değerlendirilecektir. Yeni kapalı bayraklar (`supportsScopeMerge`,
`supportsCrossScopeOverlapBan`, `supportsOrganizationUnitScopedCycles`, `supportsScopeInheritance`) **yeni
sessiz varsayım kapılarını** kapatır: her biri, bir tüketicinin makul ama yanlış bir şey varsayabileceği tam
noktayı işaret eder.

---

## Handoff

Module pack **`draft`** olarak hazır ve **kod yazma yetkisi vermiyor**.

Lütfen §1.3'teki D-kararlarını inceleyin. **Üç karar açık onay gerektiriyor:**

1. **D-SCOPE-SHAPE** — scope ayrımlı (tek seviye) mi, kombinasyon mu? (Öneri: **ayrımlı**; precedence ancak
   böyle tanımlanabilir.)
2. **D-COUNTRY-SET** — görev girdisi `COUNTRY_CODES` diyor; kod `country` diyor (§2.6, üç kaynaklı çelişki
   kanıtlarıyla). Öneri: **`country`**, çünkü BU türetmesi `TerritoryModel.CountryScope` ile join eder ve
   farklı vokabüler **sessizce boş liste** üretir.
3. **D-BU-SOURCE** — Territory aday listesi **sert kapı** mı (A), **daraltılmış seçici + vokabüler kapısı**
   mı (B)? Öneri: **B**, çünkü A dönem kimliğini Territory'nin lifecycle'ına rehin verir.

Ayrıca §19.3'teki **üç pre-flight sorusu (P1/P2/P3)** kod yazılmadan önce yazılı olarak cevaplanmalıdır;
özellikle **P1** (global scope'lu `country` setinin `scope_key` ile okunabilirliği) bir **bloke edicidir**.

Geliştirme için `status` **`approved`** veya **`ready-for-dev`** olmalı ve `runtime_code_allowed` **`true`**
çevrilmelidir; sonra `@orchestrator MOD-0165-FU07-cycle-period-scope-enrichment` çağrılır.

Hazırlık sırasında Golden Reference **Compact** şablon olarak alındı — sapma yok.
