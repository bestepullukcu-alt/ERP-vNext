---
id: MOD-0165-FU09
name: Campaign Scope Mirror + Scope-Aware Cycle Binding
parent: MOD-0165
parent_name: Campaign Management
siblings: MOD-0165-FU01 (frequency SoR) · MOD-0165-FU02 (campaign boundary) · MOD-0165-FU03 (frequency runtime) · MOD-0165-FU04 (campaign runtime) · MOD-0165-FU05 (campaign admin UI) · MOD-0165-FU06 (cycle period) · MOD-0165-FU07 (cycle period scope — AYNALANAN) · MOD-0165-FU08 (campaign cycle binding — GENİŞLETİLEN)
domain: commercial-suite
service: Diten.CrmService
frontend: frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: review
runtime_code_allowed: true
flip_approved_by: "control-tower (2026-08-28) — draft verified: no runtime touched, DCP-002 exit 0, supportsCampaignBinding stays false (MIRROR-NOT-SHARE, CyclePeriod untouched), read-only seams consumed (no new ICyclePeriodReader method). DECISIONS APPROVED: D-SCOPE-BU-COUNTRY=(a) strict discriminated fallback (user consciously accepted: a BU-scope campaign sees its BU + tenant cycles, NOT country cycles — cross-axis does not match); D-SCOPE-LEGACY-REF=(a) validate BU ref only when it CHANGES (don't break existing campaigns with legacy unpublished BU codes); D-MIRROR (mirror pure rules, consume read-only I/O seams, behavior-equivalence test §10.2 mandatory); D-SCOPE-MODEL/EDITABLE/PICKER/BIND-VALIDATION; all FU08 locks preserved (pin/B2/bind-active/D-OPENEND/D-RECHECK/guard read-only). F-SCOPE-SHARED = follow-up. Data prereq: COUNTRY_CODES + business-unit sets published (country limited to 6 per F-COUNTRY-SOT)."
runtime_code_scope: "Kapsam: Campaign aggregate'ine ayrımlı scope alanları (ScopeType + CountryScope + LegalEntityId; mevcut BusinessUnitId scope ref'ine TAŞINIR) + class-map + tek-alanlı index, CampaignScopeTypes + CampaignScopeRules (FU07 AYNASI, YENİ dosyalar), CampaignScopeWriteValidator (YENİ), CampaignCycleApplicability (YENİ — precedence-fallback kümesi), CampaignCycleBindingGuard'a scope-uygulanabilirlik adımı, Create/Update scope doğrulama + derive + bağlı-cycle re-validation, scope-filtreli cycle selector okuması, campaign scope-options endpoint'i, Compact form'da scope bölümü + cascade + scope-filtreli cycle picker, DataTable/Details scope gösterimi, 7 dil RESX, contract bayrağı + limitations revizyonu, boundary testleri. YASAK: CyclePeriod aggregate/rules/engine/repository/contract/UI YAZIMI, CyclePeriod'a Campaign referansı, supportsCampaignBinding'in çevrilmesi, CampaignTarget/snapshot/consent mutasyonu, MOD-0151 Territory yazımı, MDM yazımı, segment-targeting, backfill/migration script, Mongo hand-edit, ocelot.json yazımı, registry yazımı, RBAC seed/grant, paylaşılan scope-helper refactor'ı (F-SCOPE-SHARED)."
owner: module-pack-author
branch: feature/crm/mod-0165-fu09-campaign-scope-mirror
started: 2026-08-28
target: 2026-08-28
form_field_count: 25
predecessor: MOD-0165-FU08 (SHIPPED — 38 test, verifier 87/8 ≡ CRM baseline) + MOD-0165-FU07 (SHIPPED — aynalanan scope modeli)
revises: MOD-0165-FU08 D-SCOPE-MATCH (eşleştirme-yok → eşleştirme-var); FU08 contract limitations satırı #4 DEĞİŞTİRİLİR
dependencies:
  - MOD-0165-FU08 (ZORUNLU ÖNCÜL — CyclePeriodId pin + B2 + bind-active + D-OPENEND + D-RECHECK; HEPSİ KORUNUR)
  - MOD-0165-FU07 (AYNALANAN — CyclePeriodScopeTypes/ScopeRules/ResolveEngine deseni; dosyalarına DOKUNULMAZ)
  - MOD-0165-FU06 (ICyclePeriodReader — salt-okunur tüketim, imza değişmez)
  - MOD-0165-FU04/FU05 (Campaign aggregate + Compact UI — genişletilir)
  - MOD-0151-FU02A (Territory `TerritoryBusinessScope` + `TerritoryModel.CountryScope` — BU aday listesinin kaynağı, SALT OKUNUR)
  - MOD-0048 (reference data — `COUNTRY_CODES` + `business-unit` published values; fail-closed)
  - MOD-0220 / MDM Legal Entity (`/api/legal-entities/{id}/lookup-validation` — cross-service fail-closed)
  - MOD-0018 (RBAC — yalnız tüketim; yeni anahtar YOK, seed/grant YOK)
  - DEV-0001 (Golden Reference Compact — Campaign zaten Compact; 25 alan ile Compact kalır)
---

# MOD-0165-FU09 — Campaign Scope Mirror + Scope-Aware Cycle Binding

> **TASLAK / BOUNDARY + CONTRACT PACK (2026-08-28) — `status: ready-for-dev`, `runtime_code_allowed: true`.**
> Bu pack **kod yazma yetkisi vermez.**
>
> **Ne yapar:** Kampanyaya, dönemin FU07'de kazandığı **aynı ayrımlı adresi** verir ve FU08'in bıraktığı tek
> açık boşluğu kapatır: *"bu kampanya hangi adreste ve o adrese hangi dönemler uygulanabilir?"*
>
> **Neyi revize eder:** FU08 `D-SCOPE-MATCH = eşleştirme YOK` kararını **bilinçli bir boşluk** olarak ilan
> etmişti (FU08 §2.5, contract limitations #4, test T34). Bu FU o kararı **tersine çevirir**. FU08'in geri
> kalan her kilidi — `CyclePeriodId` pin'i, B2 (⊆, UTC-gün, INCLUSIVE), bind-active, D-OPENEND(a), D-RECHECK,
> guard'ın salt-okunurluğu, `supportsCampaignBinding: false` — **harfiyen korunur.** Bu FU onların **üstüne**
> bir kural ekler, hiçbirini gevşetmez.
>
> **Neden yeni aggregate yok:** Adres bir varlık değil, bir **niteliktir**. `CyclePeriod` FU07'de aynı kararı
> verdi ve scope'u kendi üzerine aldı; kampanya için ayrı bir `CampaignScope` aggregate'i açmak, tek bir satırın
> taşıyabileceği bir gerçeği ikinci bir okumaya böler.

---

## 0. Delivery Record (2026-08-28)

> **RUNTIME AUTHORIZATION (2026-08-28).** Kullanıcı pack'i `ready-for-dev` + `runtime_code_allowed: true` olarak
> yetkilendirdi ve §1.3'teki kararların tamamını onayladı — **D-SCOPE-BU-COUNTRY = (a)** (katı ayrımlı fallback,
> çapraz eksen yok) ve **D-SCOPE-LEGACY-REF = (a)** (BU referansı yalnız DEĞİŞTİĞİNDE doğrulanır). Uygulama pack'e
> harfiyen uyularak yapıldı; aşağıdaki üç sapma dışında hiçbir karar değişmedi.

**Teslim edilen yüzeyler.** Backend: `Domain/Entities/Campaign.cs` (+`ScopeType`/`CountryScope`/`LegalEntityId`,
`BusinessUnitId` scope ref'ine taşındı, +`ScopeRef()`/`EffectiveScopeType()`/`HasConsistentScope()`,
+`CampaignScopeTypes`, +`CampaignScopeLimits`, +10 reason code) ·
`Features/Campaign/Rules/CampaignScopeRules.cs` (**YENİ** — FU07 aynası) ·
`Features/Campaign/Rules/CampaignCycleApplicability.cs` (**YENİ** — precedence-fallback) ·
`Features/Campaign/Services/CampaignScopeWriteValidator.cs` (**YENİ**) ·
`Features/Campaign/CampaignScopeReferenceSets.cs` (**YENİ**) ·
`CampaignCycleBindingGuard.cs` (+scope-uygulanabilirlik adımı, containment'tan ÖNCE) ·
`Commands/CampaignCommands.cs` + `Handlers/CampaignCommandHandlers.cs` (scope validate → derive → guard → yaz) ·
`Queries/CampaignScopeQueries.cs` + `Handlers/QueryHandlers/CampaignScopeQueryHandlers.cs` (**YENİ**, 2 READ) ·
`CampaignDtos.cs` / `CampaignMapper.cs` · `Contract/CampaignContract.cs` (+`SupportsScopeAwareCycleBinding: true`,
+10 reason code, FU08 limitations satırı **REVİZE** + 5 yeni satır) · `Persistence/DependencyInjection.cs`
(class-map `LegalEntityId`, tek-alanlı `ix_campaigns_tenant_scope`, DI) · API request + controller (+2 READ endpoint).
Frontend: `Controllers/CRM/CampaignsController.cs` (+2 salt-okunur passthrough) ·
`Models/CRM/CampaignScopeOptionsViewModel.cs` (**YENİ dosya** — §11.4 tuzağı) · `CampaignViewModels.cs` ·
`_Form.cshtml` (Scope bölümü + country-first cascade + scope-filtreli picker + uygulanamaz uyarısı) ·
`Details.cshtml` (aynı sırada Scope bölümü) · `_DataTable.cshtml` (BusinessUnitId kolonu → Scope) ·
`_IndexL10n.cshtml` · `index.js` · `form.js` · 7 dil RESX **+24 anahtar** (172×7, parite doğrulandı).
Tests: `CampaignScopeMirrorTests.cs` (**YENİ**, 47 test — 12'si davranış-eşitliği theory'si) ·
`CampaignScopeTestDoubles.cs` (**YENİ**, paylaşılan salt-okunur double'lar).

**Pack'ten sapmalar (üçü de düzeltici veya şeffaflaştırıcı, genişletici değil):**

| # | Sapma | Gerekçe |
|---|---|---|
| **S1** | **§17.3 iki test değişikliği öngörmüştü; DÖRT oldu.** Öngörülenler: `T34_Scope_Is_Not_Matched` → `T34_Scope_Is_Matched` (tersine çevrildi, silinmedi) ve `T35` bayrak kümesine 8. bayrak. Öngörülmeyen ikisi: (a) FU08 `T33` contract testi *"does NOT validate scope"* satırının **varlığını** assert ediyordu — FU09 o satırı süperseded ettiği için assertion yeni iddiaya çevrildi (+ eski iddianın **yokluğu** da assert edildi); (b) FU04 `T36_T37` endpoint **sayısını** (12) assert ediyordu, FU09 iki READ endpoint ekledi | İkisi de FU09'un gerçekten değiştirdiği bir iddiaydı; pack bunları öngörmemişti. `T36_T37` sayı yerine **ada göre** kümeye çevrildi (FU08'de `T35`'e uygulanan aynı sertleştirme), böylece bir sonraki endpoint bilinçli beyan gerektirir. **Hiçbir FU04/FU08 davranış iddiası gevşetilmedi**; FU06/FU07 testleri **hiç değişmedi** |
| **S2** | `CampaignScopeWriteValidator` MDM doğrulaması için **`ICyclePeriodLegalEntityValidator`'ı tüketir**, kendi kopyasını yazmaz. Aynı şekilde scope-options handler'ı `ICyclePeriodLegalEntityCatalog` + `ITerritoryBusinessUnitCatalog` seam'lerini tüketir | D-MIRROR/M4'ün pack'te yazılı gerekçesi: **anlam** aynalanır, **giden bağımlılık penceresi** aynalanmaz. İkinci bir kopya iki HTTP istemcisi, iki timeout politikası ve MDM yavaşladığı gün iki farklı davranış demekti. Seam'ler salt-okunur ve CyclePeriod **çekirdeğine** dokunulmadı |
| **S3** | `GetApplicableCyclePeriodsHandler` `ListByYearAsync`'i **üç yıl** için çağırır (bu yıl, önceki, sonraki) | Seam yıl parametresi ister; kampanyanın tarihleri henüz yazılmamış olabilir ve bir dönem yıl sınırını aşabilir. Yeni seam metodu eklememek (pack §8.4) için seçilen bedel; en fazla 2 adres × 3 yıl = 6 okuma |

**Doğrulama** (ham çıktılar teslim raporunda): verifier proxy profili **87 PASS / 8 FAIL** — 95 kontrolün
ad+sonuç diff'i CRM kardeşi Segments ile **boş** (FU08 baseline'ı ile birebir aynı, yeni FAIL yok) ·
`verify_module_id --check-all` **HARD violations: 0** · `--check-id MOD-0165-FU09` **exit 0** ·
derleme **0 hata** (CrmService + Diten.Web) · test **1260/1265 üç koşuda da 0 fail** (5 önceden var olan skip) ·
FU09 testleri **47/47** · CAND literal **0** · CyclePeriod'un 18 boundary bayrağı **değişmedi**
(`SupportsCampaignBinding: false` dâhil) ve hiçbir CyclePeriod dosyasının mtime'ı FU09 penceresine girmedi.

**FU09 dışı bulgu (FU08'den devam):** `ContactLocationPiiHardeningTests.PiiMasking_...` **önceden var olan flaky**
test (~7 koşuda 1). FU09 ile ilgisi yok; ayrı iş olarak açık.

**Açık kalan:** F-SCOPE-SHARED · F-REGISTRY · F-COUNTRY-SOT · F-SCOPE-RBAC · F-TARGET-SCOPE · F-SCOPE-FILTER ·
F-BU-COUNTRY-FALLBACK · F-MDM-PERM · F-TERRITORY-GATE · F-CYCLE-CONTRACT-NOTE · F-FILE-DRIFT · F-CLEANUP (§20).
**Veri ön koşulu:** `COUNTRY_CODES` + `business-unit` setleri tenant için **yayınlı** olmalı; aksi hâlde ilgili
scope seviyesi fail-closed 400 verir (tenant seviyesi etkilenmez). Authenticated smoke (§17.2 S1–S10)
**kullanıcı tarafından** çalıştırılır ve fleet'in FU09 build'i ile yeniden başlatılmasını gerektirir.

---

## 0.0 Kimlik Geçidi ve Ön Bulgular

### 0.1 DCP-002 — PASS (2026-08-28)

```text
$ py .antigravity/scripts/verify_module_id.py . --check-id MOD-0165-FU09 --name "Campaign Scope Mirror" --parent MOD-0165
OK  MOD-0165-FU09: proven against Blueprint/registry.
REAL_EXIT=0
```

`grep -rn "MOD-0165-FU09" execution/` → **0 sonuç**; FU09 boştu. Parent `MOD-0165` Blueprint'te (W-4 Marketing),
kanonik adı **"Campaign Management"**'tır ve değişmez.

> **Geçidin kapsamı — FU08'de kanıtlanan sınır burada da geçerlidir.** Geçit **kimliği** doğrular
> (parent'ın varlığı, FU id'sinin boşluğu, registry çakışması), **FU açıklayıcı adını doğrulamaz** — FU08
> pack'inde `--name "Totally Bogus Capability"` ile de `exit 0` alındığı kontrol koşusuyla gösterilmişti.
> Bu yüzden burada da "geçit adı onayladı" **denmiyor**: geçit id'yi onayladı.

**Registry satırı bu pack tarafından EKLENMEZ** (registry yazımı pack yetkisi dışı) — §20 / F-REGISTRY.

### 0.2 Kod okumasından çıkan bulgular

| # | Bulgu | Sonuç |
|---|---|---|
| **B1** | `CyclePeriodScopeRules` (FU07) **saf**tır: repository yok, saat yok, I/O yok. Normalizasyon + tek-referans invaryantı + `DeriveScopeType` hepsi burada | Aynalanacak yüzey **küçük ve saf** — mirror maliyeti düşük, davranış kopyası birebir doğrulanabilir |
| **B2** | `CyclePeriodResolveEngine` precedence'ı **`ByPrecedence` dizisinden** okur ve **adlandırılmayan seviyeyi ATLAR** (`scopeRef is null → continue`) | D-SCOPE-PICKER'ın uygulanabilirlik kümesi bundan **birebir türetilir** — ve bu, §1.4'teki en keskin sonucu doğurur |
| **B3** | `ITerritoryBusinessUnitCatalog` / `ICyclePeriodLegalEntityCatalog` / `ICyclePeriodLegalEntityValidator` **salt-okunur seam'lerdir**; hiçbirinde write metodu yok | Bunlar **yeniden yazılmaz, tüketilir** (D-MIRROR / M4). FU08 zaten `ICyclePeriodReader`'ı böyle tüketiyor |
| **B4** | `Campaign.BusinessUnitId` bugün *"opaque code, validated as a non-empty string only"* (FU04) | FU09 bunu **yönetişimli vokabülere** bağlar → bu bir **DARALTMA**dır ve mevcut kayıtlar için gerçek bir risk taşır → **D-SCOPE-LEGACY-REF (karar bekliyor)** |
| **B5** | Campaign liste sorgusu `BusinessUnitId`'ye göre **filtrelemiyor** (filtreler: type/status/brand/product/subject); frontend chip'leri de öyle | Scope modeli liste filtrelerini **kırmaz**; filtre eklemek ayrı iştir (F-SCOPE-FILTER) |
| **B6** | `Campaign` üzerinde **optimistic concurrency token'ı yok** (FU04 kararı) | Scope editable olsa da bu FU yeni bir eşzamanlılık yüzeyi açmaz; mevcut davranış korunur |

### 0.3 Devralınan ve DOĞRULANMAYAN kısıt — dürüst not

Görev girdisi *"aynı 6-kod F-COUNTRY-SOT limiti"* diyor. Bu, **kullanıcının beyan ettiği çalışma-zamanı
olgusudur**: `COUNTRY_CODES` seti bugün az sayıda (bildirilen: 6) yayınlanmış kod taşımaktadır.

**Bu pack o sayıyı doğrulamamıştır** — yayınlanmış reference-data değerlerini okumak login gerektirir ve
parola girmek bu ajanın yapabileceği bir şey değildir. Kısıt, FU07'nin `D-COUNTRY-SET` kararının ve
`F-COUNTRY-SOT` follow-up'ının **mirası** olarak aynen devralınır; sonuçları §2.5'te yazılıdır.
Sayının kendisi operatör tarafından `scripts/verify-mod0165-fu07-country-equivalence.ps1` ile teyit edilir.

---

## 1. Module Summary

`Campaign`, FU09 ile **tam olarak bir adres** kazanır: `ScopeType` (tenant / country / legal-entity /
business-unit) ve o seviyenin gerektirdiği **tek** referans. Bu, `CyclePeriod`'un FU07'de kazandığı modelin
**aynısıdır** — aynı seviye isimleri, aynı ayrımlı invaryant, aynı precedence sırası.

Adres kazanınca FU08'in bağı **anlamlı biçimde kısıtlanabilir** hâle gelir: bir kampanya artık yalnızca
**kendi adresine uygulanabilir** dönemlere bağlanabilir.

### 1.1 Ne DEĞİLDİR

| Kavram | Sahibi | Bu FU ile ilişkisi |
|---|---|---|
| **`Campaign.ScopeType` + tek ref** (bu FU) | MOD-0165-FU04 (Campaign) | **BU FU** — ayna model + uygulanabilirlik kuralı |
| **`CyclePeriod` scope'u** | MOD-0165-FU07 | **AYNALANIR, PAYLAŞILMAZ.** Dosyalarına dokunulmaz; `supportsCampaignBinding` **false kalır** |
| **`Campaign.CyclePeriodId` + B2** | MOD-0165-FU08 | **KORUNUR.** Bu FU üstüne scope-uygulanabilirliği ekler; B2 aynen çalışmaya devam eder |
| **`CampaignTarget` / snapshot / consent** | MOD-0165-FU04 · MOD-0164 | **DOKUNULMAZ.** Hedefler scope'a göre filtrelenmez (F-TARGET-SCOPE) |
| **Territory planı** | MOD-0151 | **SALT OKUNUR** — BU aday listesini daraltır, yazılmaz, kapı değildir |
| **MDM legal entity** | MOD-0220 | **SALT OKUNUR**, cross-service, fail-closed (400 ≠ 503) |
| **Scope-bazlı YETKİ** (*"yalnız TR kampanyaları"*) | — | **KAPSAM DIŞI.** Scope bir **veri adresi**dir, bir RBAC sınırı değil (F-SCOPE-RBAC, FU07'den devralındı) |

> **Tek cümlelik sınır:** *Scope, kampanyanın **nerede** olduğunu söyler; kimin onu görebileceğini
> **söylemez**.*

### 1.2 FU08'den devralınan ve DEĞİŞMEYEN her şey

| FU08 kilidi | FU09'daki durumu |
|---|---|
| `CyclePeriodId` nullable pin, tek yön | **Aynen** |
| B2: `[campaign] ⊆ [period]`, INCLUSIVE, UTC kanonik gün | **Aynen** — `.Date` indirgemesi dâhil |
| bind-active (yalnız binding değişince) | **Aynen** |
| close-a-dayanıklılık (kapanan dönem bağını korur) | **Aynen** |
| D-OPENEND(a): açık uçlu kampanya bindlenemez | **Aynen** |
| D-RECHECK asimetrisi | **Aynen** — ve FU09 kuralı **aynı desene** eklenir (§12.2) |
| Guard salt-okunur, HTTP self-call yok | **Aynen** — yeni scope adımı da salt-okunur |
| Projeksiyon read-time, asla kalıcı değil | **Aynen** |
| `CyclePeriod.supportsCampaignBinding: false` | **Aynen false** — CyclePeriod hâlâ kampanyayı bilmiyor |
| 3 reason code | **Korunur**, üstüne FU09'unkiler eklenir |

### 1.3 D-Karar özeti

| # | Karar | Durum |
|---|---|---|
| **D-SCOPE-MODEL** | Campaign, FU07'nin ayrımlı scope'unu birebir alır; düz `BusinessUnitId` scope ref'ine taşınır | **KİLİTLİ** (kullanıcı) |
| **D-MIRROR** | Kuralları **aynala**, CyclePeriod dosyalarına dokunma; paylaşımlı refactor follow-up | **KİLİTLİ** (kullanıcı) · uygulama şekli §10.2'de |
| **D-SCOPE-DERIVE** | ScopeType boşsa: BU dolu→business-unit, boş→tenant. Backfill YOK | **KİLİTLİ** (kullanıcı) |
| **D-SCOPE-EDITABLE** | Campaign scope'u kimlik değil → editable; scope değişince bağlı cycle re-validate, uygulanamazsa 400 | **KİLİTLİ** (kullanıcı) |
| **D-SCOPE-PICKER** | Precedence-fallback: tam scope + onu kapsayan daha geniş scope'lar. Yalnız-tam-eşleşme DEĞİL | **KİLİTLİ** (kullanıcı) · **kritik netleştirme §1.4** |
| **D-SCOPE-BIND-VALIDATION** | Bind anında seçilen cycle uygulanabilir kümede olmalı; değilse 400 fail-closed | **KİLİTLİ** (kullanıcı) |
| **D-COUNTRY-SET** | `COUNTRY_CODES` + `business-unit`, country-first cascade, Territory-narrowed BU | **KİLİTLİ** (FU07'den miras) |
| **D-SCOPE-LEGACY-REF** | Scope referansı **yalnız DEĞİŞTİĞİNDE** vokabülere karşı doğrulanır | **KARAR BEKLİYOR** ⚠️ |
| **D-SCOPE-BU-COUNTRY** | BU-scope kampanya, country-scope dönem **görmez** (ayrımlı fallback) | **ONAY BEKLİYOR** ⚠️ (§1.4) |
| **D-SCOPE-DISPLAY** | DataTable'da `BusinessUnitId` kolonu **scope** kolonuna dönüşür | **ÖNERİ** |
| **D-FILES** | Campaign'in gruplanmış düzeni korunur (FU08'den miras) | **ÖNERİ** |
| **D-RBAC** | Yeni permission anahtarı YOK | **ÖNERİ** |

### 1.4 D-SCOPE-PICKER'ın kritik sonucu — **onay bekliyor**

Kullanıcı kararı: *"kampanyanın TAM scope'undaki dönemler + onu KAPSAYAN daha geniş scope dönemleri
(fallback zinciri, resolve-active mantığı)"*.

`CyclePeriodResolveEngine` (B2) fallback'i **çağıranın ADLANDIRDIĞI seviyeler** üzerinden yürütür:
adlandırılmayan seviye **atlanır**. Kampanyanın scope'u **ayrımlıdır** — tam olarak **bir** seviye adlandırır.
Bu ikisi birleşince uygulanabilirlik kümesi şudur:

| Kampanyanın scope'u | Uygulanabilir dönem scope'ları | Uygulanamaz (görünmez) |
|---|---|---|
| `tenant` | `tenant` | country · legal-entity · business-unit — **hepsi** |
| `country:TR` | `country:TR` · `tenant` | `country:DE` · her legal-entity · her business-unit |
| `legal-entity:X` | `legal-entity:X` · `tenant` | `legal-entity:Y` · her country · her business-unit |
| `business-unit:alpha` | `business-unit:alpha` · `tenant` | `business-unit:beta` · **her country** · her legal-entity |

Kullanıcının açıkça istediği iki sonuç **sağlanıyor**: tenant-geneli dönemler her kampanyaya fallback olarak
görünüyor; `business-unit:beta` dönemi `business-unit:alpha` kampanyasına **hiç** görünmüyor.

> ⚠️ **Ama dikkat çekilmesi gereken üçüncü sonuç:** yukarıdaki tabloya göre **`business-unit:alpha`
> kampanyası `country:TR` dönemini de GÖRMEZ.** Sebep mekaniktir: kampanya ayrımlı bir adres taşır ve
> `business-unit:alpha` derken **hiçbir ülke adlandırmaz** — dolayısıyla "TR beni kapsıyor mu?" sorusunu
> soracak veri kampanyada **yoktur**.

Bunu değiştirmenin üç yolu var ve üçü de bedelli:

| # | Yol | Değerlendirme |
|---|---|---|
| **(a)** | **Katı ayrımlı fallback** — tablo aynen. BU kampanya yalnız BU + tenant görür | ✅ **ÖNERİLEN.** `resolve-active`'in **birebir** aynası; ek okuma yok, ek hata modu yok. Bir okuyucu iki modül için **tek** zihinsel model öğrenir |
| (b) | `BusinessUnitCountryContext` alanını kullanarak BU'nun ülkesini türet ve country dönemlerini de göster | ❌ FU07 o alanı **"documentation, never identity"** olarak ilan etti ve uniqueness/overlap/resolver'ın onu **görmezden geldiğini** yazdı. Bir kuralı ona dayandırmak, dokümantasyonu kimliğe terfi ettirir — FU07'nin açıkça reddettiği şey |
| (c) | Pick anında Territory'den BU'nun ülkesini çöz ve country dönemlerini ekle | ❌ Salt-okunur bir picker'a **cross-module bağımlılık** ve yeni bir "Territory erişilemez" hata modu ekler. Ayrıca aynı BU birden çok ülkeye ait olabilir → küme belirsizleşir |

**Öneri: (a).** Karşı-karar verilirse D-SCOPE-PICKER tablosu ve §16'daki AC'ler yeniden yazılmalıdır;
bugünkü hâliyle bırakmak, contract'ın vaat ettiğiyle runtime'ın yaptığını ayırır.

---

## 2. Ownership and Boundaries

**In-scope:** `Campaign` aggregate'ine `ScopeType` + `CountryScope` + `LegalEntityId` alanları (mevcut
`BusinessUnitId` **taşınır**, yeni alan değil) · `CampaignScopeTypes` + `CampaignScopeRules` (FU07 aynası) ·
`CampaignScopeWriteValidator` · `CampaignCycleApplicability` (precedence-fallback kümesi) ·
`CampaignCycleBindingGuard`'a scope-uygulanabilirlik adımı · Create/Update scope doğrulama + derive +
bağlı-cycle re-validation · scope-filtreli cycle selector okuması · campaign scope-options endpoint'i ·
Compact form'da scope bölümü + country-first BU cascade · DataTable/Details scope gösterimi · 7 dil RESX ·
contract bayrağı + FU08 limitations satırının **revizyonu** · boundary testleri.

**Out-of-scope (YASAK):**

| Yasak | Neden |
|---|---|
| `CyclePeriod` aggregate / rules / engine / repository / contract / UI **yazımı** | D-MIRROR: ayna, paylaşım değil |
| `CyclePeriod.supportsCampaignBinding`'in çevrilmesi | Yön hâlâ tek: dönem kampanyayı bilmiyor (§2.3) |
| Paylaşılan scope-helper refactor'ı (ortak `Features/Shared/Scope/`) | **F-SCOPE-SHARED** — kullanıcı talimatıyla follow-up ilan edildi |
| `CampaignTarget` / snapshot / consent mutasyonu; hedeflerin scope'a göre filtrelenmesi | F-TARGET-SCOPE; hedef seçimi ayrı bir sorudur |
| MOD-0151 Territory **yazımı**; BU aday listesinin **sert kapıya** dönüştürülmesi | D-BU-SOURCE soft-gate mirası (F-TERRITORY-GATE) |
| MDM legal entity **yazımı** | Salt okuma + fail-closed doğrulama |
| Scope-bazlı **RBAC** | F-SCOPE-RBAC (FU07'den devralındı) |
| `organization-unit` scope seviyesi | CRM'de kasten yok (FU07 `supportsOrganizationUnitScopedCycles: false`) |
| Backfill / migration script · Mongo hand-edit | D-SCOPE-DERIVE: okuma-anı türetme (§4.3) |
| `ocelot.json` yazımı · registry yazımı · RBAC seed/grant | Pack yetkisi dışı |
| FU08 kilitlerinin gevşetilmesi (B2, bind-active, D-OPENEND, D-RECHECK) | §1.2 |

### 2.1 Protected paths

```text
services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/CyclePeriod.cs            [OKUNUR, YAZILMAZ]
services/Diten.CrmService/src/Diten.CrmService.Domain/Repositories/ICyclePeriodRepository.cs
services/Diten.CrmService/src/Diten.CrmService.Application/Features/CyclePeriod/**
    ├── Rules/**              — AYNALANIR, değiştirilmez
    ├── Services/**           — ICyclePeriodLegalEntityValidator TÜKETİLİR, değiştirilmez
    ├── Contract/**           — supportsCampaignBinding false KALIR
    └── Read/**               — ICyclePeriodReader + katalog seam'leri TÜKETİLİR, imzaları DEĞİŞMEZ
services/Diten.CrmService/src/Diten.CrmService.Persistence/Repositories/CyclePeriodRepository.cs
services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/CyclePeriodsController.cs
frontend/Diten.Web/{Controllers/CRM/CyclePeriodsController.cs, Views/CRM/CyclePeriods/**,
                    wwwroot/assets/js/CRM/CyclePeriods/**, Resources/Views/CRM/CyclePeriods/**}
services/Diten.CrmService/src/Diten.CrmService.Application/Features/Campaign/Snapshot/**
services/Diten.CrmService/src/Diten.CrmService.Application/Features/Campaign/Handlers/CampaignTargetCommandHandlers.cs
services/Diten.CrmService/src/Diten.CrmService.Application/Features/{ConsentPreference,VisitFrequencyPolicy,Segment}/**
gateway/**/ocelot.json                       [yeni route gerekmiyor — §15]
execution/registries/module-id-registry.md   [F-REGISTRY]
```

> **FU08 emsali korunur:** FU08, `ICyclePeriodReader`'a **yalnız** salt-okunur `GetByIdsAsync`'i ekleyerek
> tek bir istisna kullandı. **FU09 o seam'e hiç dokunmaz** — mevcut `ResolveActiveAsync` / `GetByIdAsync` /
> `ListByYearAsync` / `GetByIdsAsync` yeterlidir (§8.4).

### 2.2 Yön asimetrisi — FU08'den aynen devralınır

```text
Campaign ──ScopeType+ScopeRef──▶ (kendi adresi)
Campaign ──CyclePeriodId───────▶ CyclePeriod          (PIN — FU08)
Campaign ──"uygulanabilir mi?"─▶ CyclePeriod.scope    (OKUMA — FU09)
Campaign ◀──────────────────────  CyclePeriod          (YOK — hiç açılmaz)
```

`CyclePeriod` hâlâ kampanyayı **bilmez**: üzerinde `CampaignId` yok, kampanya listesi yok, cascade yok.
`supportsCampaignBinding: false` bu yüzden **doğru kalır** ve FU09 onu çevirmez. Yeni eklenen tek şey,
Campaign tarafında yapılan bir **okumadır**.

### 2.3 D-MIRROR — ayna neyi kapsar, neyi kapsamaz

| Aynalanır (Campaign'in kendi kopyası) | Tüketilir (mevcut salt-okunur seam) | Dokunulmaz |
|---|---|---|
| `CampaignScopeTypes` (4 seviye + `ByPrecedence`) | `ICyclePeriodReader` (FU06/FU07/FU08) | `CyclePeriodScopeTypes` |
| `CampaignScopeRules` (normalize · tek-ref invaryantı · `DeriveScopeType` · `Describe`) | `ITerritoryBusinessUnitCatalog` | `CyclePeriodScopeRules` |
| `CampaignScopeWriteValidator` (vokabüler + MDM sırası) | `ICyclePeriodLegalEntityCatalog` | `CyclePeriodScopeWriteValidator` |
| `CampaignCycleApplicability` (precedence-fallback kümesi) | `ICyclePeriodLegalEntityValidator` | `CyclePeriodResolveEngine` |
| `CampaignScopeReferenceSets` (aynı iki set kodu) | `IReferenceDataValidator` / `IReferenceDataCatalogReader` | `CyclePeriodReferenceSets` |

**Neden saf kurallar aynalanıyor ama I/O seam'leri tüketiliyor:** aynalanan şey **anlamdır** (bir adres neye
benzer, hangi sırayla daralır) ve iki modülün bu anlamı ayrı ayrı sahiplenmesi doğrudur — FU07'nin scope'u
**kimliktir**, FU09'un scope'u **değildir** (§D-SCOPE-EDITABLE), bu yüzden kuralları zamanla **ayrışacaktır**.
Buna karşılık `ITerritoryBusinessUnitCatalog` bir **anlam** değil, MOD-0151'e açılmış dar bir **pencere**dir;
ikinci bir kopyası aynı Territory sorgusunu iki yerde bakım gerektiren iki koda dönüştürür ve iki farklı
"Territory erişilemez" davranışı doğurur. FU08 aynı gerekçeyle `ICyclePeriodReader`'ı zaten tüketiyor.

Değerlendirilen ve reddedilen alternatifler:

| # | Alternatif | Neden reddedildi |
|---|---|---|
| M1 | Territory/MDM adapter'larını da kopyala | Aynı HTTP/Mongo kodunun ikinci nüshası; iki ayrı outage davranışı |
| M2 | Seam'leri ortak bir `Features/Shared/Scope/` altına **taşı** | CyclePeriod dosyalarını **değiştirir** → D-MIRROR ihlali. Doğru iş, ama **bu FU'nun işi değil** → **F-SCOPE-SHARED** |
| M3 | Campaign tarafında ince adapter arayüzleri tanımla | Davranış eklemeyen bir dolaylılık katmanı; okuyucuya iki isim öğretir |
| **M4** | Saf kuralları aynala, salt-okunur seam'leri **doğrudan tüket** | ✅ **SEÇİLEN.** FU08 emsaliyle tutarlı; yeni bağlantı sınıfı yok |

### 2.4 Legacy CrmV2

FU06 §2.4 / FU07 §2.7 bulguları geçerlidir ve **genişletilmez**: legacy'de ne dönem aggregate'i ne de
kampanya scope'u vardır. FU09 legacy'den **hiçbir** kavram getirmez.

### 2.5 Devralınan vokabüler kısıtı ve sonuçları (F-COUNTRY-SOT)

FU07 `D-COUNTRY-SET`'i **`COUNTRY_CODES`** olarak kilitledi; FU09 aynı seti kullanır, çünkü kampanyanın
ülkesi dönemin ülkesiyle **aynı alfabeden** okunmazsa iki taraf hiçbir zaman eşleşmez ve uygulanabilirlik
kuralı sessizce **hep boş** döner — bir modülü öldüren en sessiz hata türü (FU07 §2.6'nın tespiti).

Kullanıcının bildirdiği çalışma-zamanı olgusu: `COUNTRY_CODES` bugün **az sayıda (6) yayınlanmış kod**
taşımaktadır. **Bu pack o sayıyı doğrulamamıştır** (§0.3). Sonuçları:

| Sonuç | Etki |
|---|---|
| Bir tenant'ın gerçekte çalıştığı ülke `COUNTRY_CODES`'ta yoksa | O ülke için **country-scope kampanya açılamaz** — fail-closed 400, hardcoded fallback **yasak** |
| BU cascade'inin ülke filtresi de aynı setten beslenir | Listelenmeyen ülkedeki BU'lar Territory üzerinden **daraltılamaz** → liste `business-unit` vokabülerine düşer (D-BU-SOURCE soft gate) — **BU-scope kampanya bloke olmaz** |
| `tenant` ve `legal-entity` scope'ları | **Etkilenmez** |

Yani limit, **country-scope'u kısıtlar; diğer üç seviyeyi ve FU08'in tüm davranışını kısıtlamaz.**
Operatör setleri genişletince ek kod gerekmez. Çözümü F-COUNTRY-SOT'tur.

---

## 3. Owned Objects

| Nesne | Tür | Sahiplik |
|---|---|---|
| `Campaign.ScopeType` / `.CountryScope` / `.LegalEntityId` | Alan | **YENİ — bu FU** |
| `Campaign.BusinessUnitId` | Alan | **MEVCUT — anlamı daralır** (opak string → scope ref, yönetişimli) |
| `CampaignScopeTypes` · `CampaignScopeRules` | Saf sınıf | **YENİ — bu FU** (FU07 aynası) |
| `CampaignScopeWriteValidator` | Application servisi | **YENİ — bu FU** |
| `CampaignCycleApplicability` | Saf sınıf | **YENİ — bu FU** (precedence-fallback) |
| `CampaignScopeReferenceSets` | Sabit | **YENİ — bu FU** (aynı iki set kodu) |
| `CampaignCycleBindingGuard` | Application servisi | **MEVCUT (FU08) — +1 adım** |
| `GetCampaignScopeOptionsQuery` + handler | CQRS | **YENİ — bu FU** |
| 8 yeni reason/error code | Sabit | **YENİ — bu FU** (§12.4) |
| `CampaignFeatureFlags.SupportsScopeAwareCycleBinding` | Contract bayrağı | **YENİ — bu FU** |
| `CyclePeriod` ve tüm FU07 scope altyapısı | Aggregate + kurallar | **MOD-0165-FU07** — salt okunur |

---

## 4. Entity Fields

### 4.1 `Campaign` — eklenen ve anlamı değişen alanlar

| Alan | Tip | Zorunlu | Kısıt | Açıklama |
|---|---|---|---|---|
| `ScopeType` | `string` | Hayır (türetilir) | `CampaignScopeTypes` — tenant / country / legal-entity / business-unit | Kampanyanın **seviyesi**. Boşsa okuma anında türetilir (§4.3). **Editable** (D-SCOPE-EDITABLE) |
| `CountryScope` | `string?` | `ScopeType=country` iken evet | ISO alpha-2, upper-case, `COUNTRY_CODES` içinde | Diğer her seviyede `null` |
| `LegalEntityId` | `Guid?` | `ScopeType=legal-entity` iken evet | MDM'de referanslanabilir + ACTIVE (fail-closed) | Diğer her seviyede `null` |
| `BusinessUnitId` | `string?` | `ScopeType=business-unit` iken evet | **YENİ:** yayınlanmış `business-unit` vokabülerinde (D-SCOPE-LEGACY-REF koşuluyla) | **MEVCUT ALAN.** FU04'te opak bir bağlam koduydu; artık scope ref'idir |
| `CyclePeriodId` | `Guid?` | Hayır | FU08 kuralları **+** FU09 uygulanabilirlik kuralı | **DEĞİŞMEZ** (FU08) |

**Bilinçli olarak EKLENMEYEN alanlar:**

| Alan | Neden yok |
|---|---|
| `BusinessUnitSource` (FU07'de var) | FU07'de provenance damgasıdır çünkü dönem **kimliği** scope'tur. Campaign scope'u kimlik değil ve editable — bir damganın anlamı ilk düzenlemede bayatlar |
| `BusinessUnitCountryContext` (FU07'de var) | Aynı gerekçe **artı** §1.4/(b): kural dayanağı olmayacaksa saklamanın değeri yok |
| `ScopeRef` (kalıcı) | FU07'de olduğu gibi **türetilmiş** bir okumadır (`ScopeRef()`), kalıcı alan değil |

### 4.2 `ScopeRef()` / `EffectiveScopeType()` — FU07 deseninin aynası

```text
ScopeRef()            country → CountryScope (UPPER) | legal-entity → LegalEntityId "D" |
                      business-unit → BusinessUnitId (trim) | tenant → null

EffectiveScopeType()  ScopeType biliniyorsa onu normalize et;
                      bilinmiyorsa: BusinessUnitId dolu → business-unit, boş → tenant
```

`tenant`'ın `ScopeRef`'i `null`dur ve bu **"scope yok"** demek değildir — **kendi başına bir scope**tur
(FU07'nin cümlesi aynen geçerlidir).

### 4.3 D-SCOPE-DERIVE — migration YOK, gerekçe

FU04/FU05/FU08 ile yazılmış her kampanyada `ScopeType` **yoktur**. Bu satırlar **taşınmaz**:

- `BusinessUnitId` doluysa → `business-unit` (o kampanyanın FU04'te zaten sahip olduğu tek bağlam);
- boşsa → `tenant`.

Türetme **okuma anındadır ve hiçbir şey yazmaz** (FU07 `EnsureScopeType` deseni). Değer, satır **kendi
sebebiyle** bir sonraki kez yazıldığında kalıcılaşır. Dolayısıyla backfill script'i **yok**, migration
**yok**, Mongo hand-edit **yok**.

> **Türetmenin sınırı — FU07'den aynen:** `CountryScope` veya `LegalEntityId` dolu olup `ScopeType` boşsa
> türetme **yapılmaz**, yazma **reddedilir**. O seviyeler eskiden yoktu, dolayısıyla hiçbir eski kayıt onları
> kastediyor olamaz; tahmin etmek niyet uydurmak olurdu.

### 4.4 Index kararı — ve FU08'de kayda geçen tuzak

Tek-alanlı, tenant ile birlikte: `(TenantId, ScopeType, BusinessUnitId)` **kabul edilebilir** çünkü üçü de
skalerdir. **Hiçbir `DateTimeOffset` alanı bileşik index'e girmez** — CRM'de `DateTimeOffset` BSON'da
`[ticks, offset]` **dizisi** olarak saklanır ve iki dizi alanı aynı index'te *"cannot index parallel arrays"*
hatası verir. FU08'in `ix_campaigns_tenant_cycle_period` index'i tek alanlı bırakılmıştı; FU09 da aynı
disiplini korur.

---

## 5. Repo Scope

### 5.1 Backend

```text
src/Diten.CrmService.Domain/Entities/Campaign.cs                        [DEĞİŞİR] +3 alan, +ScopeRef()/EffectiveScopeType(),
                                                                                  +CampaignScopeTypes, +8 error code
src/Diten.CrmService.Application/Features/Campaign/
├── Rules/CampaignScopeRules.cs                                         [YENİ] FU07 aynası (saf)
├── Rules/CampaignCycleApplicability.cs                                 [YENİ] precedence-fallback kümesi (saf)
├── Services/CampaignScopeWriteValidator.cs                             [YENİ] vokabüler + MDM sırası
├── CampaignScopeReferenceSets.cs                                       [YENİ] COUNTRY_CODES + business-unit
├── CampaignCycleBindingGuard.cs                                        [DEĞİŞİR] +scope-uygulanabilirlik adımı
├── CampaignValidation.cs                                               [DEĞİŞİR] +saf scope kuralları
├── CampaignDtos.cs / CampaignMapper.cs                                 [DEĞİŞİR] +scope alanları + scope-options DTO
├── Commands/CampaignCommands.cs                                        [DEĞİŞİR] +4 scope parametresi
├── Handlers/CampaignCommandHandlers.cs                                 [DEĞİŞİR] scope validate + derive + re-validate
├── Handlers/CampaignQueryHandlers.cs                                   [DEĞİŞİR] scope projeksiyonu
├── Queries/GetCampaignScopeOptionsQuery.cs                             [YENİ]
├── Handlers/QueryHandlers/GetCampaignScopeOptionsHandler.cs            [YENİ]  (D-FILES istisnası — §10.1)
├── Queries/GetApplicableCyclePeriodsQuery.cs                           [YENİ]
├── Handlers/QueryHandlers/GetApplicableCyclePeriodsHandler.cs          [YENİ]
└── Contract/CampaignContract.cs                                        [DEĞİŞİR] +1 bayrak, limitations REVİZYONU
src/Diten.CrmService.Persistence/DependencyInjection.cs                 [DEĞİŞİR] class-map + index + DI
src/Diten.CrmService.Api/Models/CRM/CampaignRequests.cs                 [DEĞİŞİR] +4 scope alanı
src/Diten.CrmService.Api/Controllers/CRM/CampaignsController.cs         [DEĞİŞİR] +2 read endpoint
tests/.../CampaignScopeMirrorTests.cs                                   [YENİ]
tests/.../CampaignCycleBindingTests.cs                                  [DEĞİŞİR] T34 REVİZE (§17.3)
```

### 5.2 Frontend

```text
Controllers/CRM/CampaignsController.cs                                  [DEĞİŞİR] +2 salt-okunur passthrough
Models/CRM/CampaignViewModels.cs                                        [DEĞİŞİR] +scope alanları
Models/CRM/CampaignScopeOptionsViewModel.cs                             [YENİ]  ⚠ ayrı dosya — §11.4
Views/CRM/Campaigns/_Form.cshtml                                        [DEĞİŞİR] scope bölümü + cascade
Views/CRM/Campaigns/Details.cshtml                                      [DEĞİŞİR] scope gösterimi
Views/CRM/Campaigns/_DataTable.cshtml                                   [DEĞİŞİR] BusinessUnitId kolonu → Scope
Views/CRM/Campaigns/_IndexL10n.cshtml                                   [DEĞİŞİR] +anahtarlar
wwwroot/assets/js/CRM/Campaigns/index.js                                [DEĞİŞİR] scope kolonu render
wwwroot/assets/js/CRM/Campaigns/form.js                                 [DEĞİŞİR] scope cascade + scope-filtreli picker
Resources/Views/CRM/Campaigns/CampaignIndex.{ar,en,es,fr,ru,tr,zh}.resx [DEĞİŞİR] 7 dil
```

---

## 6. Protected Paths

§2.1'de tam liste verilmiştir.

---

## 7. Dependencies

| Bağımlılık | Rol | Durum | Not |
|---|---|---|---|
| **MOD-0165-FU08** | genişletilen | SHIPPED | Tüm kilitleri **korunur** (§1.2); D-SCOPE-MATCH **revize edilir** |
| **MOD-0165-FU07** | **aynalanan** | SHIPPED | Dosyalarına dokunulmaz; salt-okunur seam'leri tüketilir |
| **MOD-0165-FU06** | tüketilen | SHIPPED | `ICyclePeriodReader` imzası **değişmez** |
| **MOD-0151-FU02A** Territory | okunan | SHIPPED | `ITerritoryBusinessUnitCatalog` — narrowing, kapı değil |
| **MOD-0048** | okunan | mevcut | `COUNTRY_CODES` + `business-unit`; publish **ön koşul** (fail-closed) |
| **MOD-0220 / MDM** | okunan | mevcut | legal-entity fail-closed (400 ≠ 503); `mdm.legal-entities.read` gerekir (F-MDM-PERM) |
| **Gateway** | — | route **mevcut** | Yeni ocelot route'u gerekmez (§15) |
| **DEV-0001** | şablon | mevcut | 25 alan → Compact kalır |

---

## 8. Runtime Constraints

### 8.1 Doğrulama sırası (write path) — FU07 sırasının aynası

```text
1. saf: normalize + tek-referans invaryantı  (CampaignScopeRules)          → 400
2. saf: FU08 kuralları (kod/ad/tip/tarih/referans formatı)                  → 400/409
3. I/O: country  → COUNTRY_CODES yayınlanmış değerler                       → 400 (set yayınsız ≠ değer bilinmiyor)
4. I/O: business-unit → business-unit yayınlanmış değerler                   → 400   [D-SCOPE-LEGACY-REF koşuluyla]
5. I/O: legal-entity  → MDM fail-closed                                      → 400 | 503
6. I/O: FU08 cycle guard  (bulunamadı / active değil / açık uçlu / B2)       → 400
7. I/O: FU09 cycle uygulanabilirlik (scope kümesi)                           → 400
8. YAZ
```

**1–7 arasındaki her ret, hiçbir şey yazılmadan gerçekleşir.** Bir bağımlılık kesintisi asla yarı-yazılmış
bir kampanya bırakamaz.

### 8.2 Erişilemezlik davranışı

| Durum | Cevap | Gerekçe |
|---|---|---|
| `COUNTRY_CODES` / `business-unit` seti **yayınlanmamış** | **400**, ayrı hata kodu | Operatörün çözeceği bir şey; yazarın yeniden yazmasıyla düzelmez → farklı kod |
| Değer sette **yok** | **400**, ayrı hata kodu | Yazarın düzelteceği şey |
| MDM **"referanslanamaz" dedi** | **400** | Bağımlılık konuştu; girdi geçersiz |
| MDM **erişilemez** (timeout/5xx/403/bozuk gövde) | **503**, hiçbir şey yazılmaz | **Bilmiyoruz.** 403'ü "böyle bir tüzel kişi yok"a çevirmek, bakma izni olmadığımızda yazara varlık hakkında yalan söylemektir |
| Cycle uygulanabilirlik okuması (in-process Mongo) | **500** (mevcut davranış) | Cross-service değil; sahte bir 503/retry katmanı eklenmez (FU08 §8.2 kararı) |

### 8.3 Scope, RBAC değildir

Scope bir **veri adresidir**. Bugün hiçbir yerde *"yalnız TR kampanyalarını görebilirsin"* anlamına gelmez ve
bu FU öyle bir anlam **üretmez**. Aksi belgelenmezse bir okuyucu scope'u güvenlik sınırı sanabilir — bu yüzden
contract limitations'a açıkça yazılır (§13.2) ve **F-SCOPE-RBAC** olarak kaydedilir.

### 8.4 `ICyclePeriodReader` yeterlidir — yeni seam metodu YOK

Uygulanabilirlik kümesi, mevcut seam üzerinden okunabilir:

- Picker: `GetByIdsAsync` **değil**, tenant'ın dönemlerini scope'a göre daraltan bir okuma gerekir. Bunun
  için `ListByYearAsync(year, scopeType, scopeRef, ct)` **zaten** scope filtresi alıyor ve her seviye için
  bir kez çağrılabilir (uygulanabilir seviye sayısı **en fazla 2**: kendi seviyesi + tenant).
- Bind doğrulaması: `GetByIdAsync` (FU08 zaten kullanıyor) dönemin `ScopeType`/`ScopeRef`'ini
  `CyclePeriodSnapshot` içinde **zaten döndürüyor** → ek okuma **yok**.

> Yani FU09, FU06/FU07 seam'ine **tek bir metot bile eklemez**. FU08'in eklediği `GetByIdsAsync` de olduğu
> gibi kalır.

### 8.5 Projeksiyon kuralı korunur

Dönemin kodu/adı/penceresi/scope'u kampanyaya **asla yazılmaz** (FU08 D-PROJECTION). FU09 kampanyanın **kendi**
scope'unu yazar — bu bir kopya değil, kampanyanın kendi verisidir.

---

## 9. Layout & Shell Contract

| Öğe | Değer |
|---|---|
| `shell` | `tenant` |
| Razor layout | **`Layout = "_LayoutTenantShell";`** — `Index` / `Create` / `Edit` / `Details` |
| View klasörü | `frontend/Diten.Web/Views/CRM/Campaigns/` |
| Golden reference | **Compact** (`DEV-0001`) — mevcut, değişmez |
| Nav | **Yeni nav girdisi YOK** |

Bu FU **hiçbir yeni sayfa açmaz**. `_CreateEditOffcanvas.cshtml` ve `_DetailsQuickView.cshtml` Compact'ta
**yasaktır** ve açılmaz. AC-UI-0'da test edilir.

---

## 10. Backend File Convention

### 10.1 D-FILES — gruplanmış düzen korunur, iki bilinçli istisna

`Features/Campaign/` FU04'ten beri **gruplanmış** düzendedir ve FU08 bunu koruyup **F-FILE-DRIFT**'i
yeniden kaydetmişti. FU09 aynı kararı sürdürür.

**İki istisna, ikisi de FU07 aynasının gereği:**

| Dosya | Neden alt klasör |
|---|---|
| `Rules/CampaignScopeRules.cs` · `Rules/CampaignCycleApplicability.cs` | FU07'nin aynalanan yüzeyi `Rules/` altındadır; aynayı düz köke koymak, iki modülü karşılaştıran okuyucuya sahte bir fark gösterir |
| `Services/CampaignScopeWriteValidator.cs` | Aynı gerekçe (`CyclePeriodScopeWriteValidator` `Services/` altında) |

Yeni **query handler**'lar (`Handlers/QueryHandlers/`) Golden kanonik düzendedir; mevcut
`Handlers/CampaignQueryHandlers.cs` gruplanmış hâlde **kalır**. Bu bilinçli bir yarım-adımdır ve
F-FILE-DRIFT'e **eklenir** — tam hizalama ayrı iştir.

### 10.2 Aynanın davranış-eşitliği nasıl kanıtlanır

Ayna, "benzer görünen ikinci bir kod" olarak kabul edilemez. §17'de **aynı girdi kümesini iki kurala da
veren** karşılaştırmalı testler zorunludur: `CampaignScopeRules.Normalize(...)` ile
`CyclePeriodScopeRules.Normalize(...)` aynı 12 girdi için **aynı kabul/ret** kararını vermelidir. İkisi
ayrışmaya başladığı gün test kırılır ve ayrışma **bilinçli** olarak kayda geçer.

---

## 11. Frontend File Contract

### 11.1 Golden karar — Compact KALIR

| Sayım | Değer |
|---|---|
| FU08 sonrası mevcut kullanıcı alanı | **21** |
| FU09'un eklediği | **+4** — `ScopeType`, `CountryScope`, `LegalEntityId`, BU country filtresi (`buFilterCountry`) |
| `BusinessUnitId` | **zaten sayılıydı** — yeni alan değil, scope bölümüne **taşınır** |
| **Toplam** | **25** → `> 8` → **Compact** |

### 11.2 Scope bölümü — FU07 `_Form.cshtml` aynası

`ScopeType` seçicisi + seviyeye göre görünen tek referans bloğu. `business-unit` seçilince
**country-first cascade**: önce `buFilterCountry`, sonra Territory-daraltılmış BU listesi.

**FU07'den ayrılan tek nokta ve gerekçesi:** FU07'de `ScopeType` **immutable**dır (kimlik) ve edit'te
tek seçenek olarak render edilir (`ScopeTypeImmutableHint`). **Campaign'de scope kimlik değildir**, bu yüzden
edit'te de **tüm seviyeler seçilebilir** — ama seçici, bağlı bir cycle varken kullanıcıyı uyarır
(§11.3).

### 11.3 Scope değişimi + bağlı cycle — UI sözleşmesi

Kullanıcı scope'u değiştirdiğinde form:

1. cycle picker'ı **yeni scope'a göre yeniden yükler**;
2. mevcut seçim yeni kümede **yoksa** onu listede **`uygulanamaz` rozetiyle korur** (FU08 AC-UI-3'ün
   aynı gerekçesi: sessiz unbind yasak) ve görünür bir uyarı gösterir: *"bu dönem yeni scope'a
   uygulanamaz — kaydetmeden önce dönemi kaldırın veya scope'u geri alın"*;
3. kullanıcı yine de kaydederse sunucu **400** verir (D-SCOPE-BIND-VALIDATION) — UI kuralı sunucu
   kuralının yerine **geçmez**.

### 11.4 ⚠️ Verifier tuzağı — FU08'de yaşandı, tekrarlanmamalı

`CampaignScopeOptionsViewModel` **kendi dosyasında** olmalıdır. DataTable contract verifier'ı bir form
alanının tipini dosyadaki **son** aynı-isimli property'den çözer; FU08'de projeksiyon VM'inin non-nullable
`EndDate`'i formun nullable `EndDate`'ini gölgeleyip *"Optional numeric/date fields use nullable ViewModel
types"* kontrolünü **var olmayan bir kusurla** düşürmüştü. Scope options VM'i tarih taşımasa da aynı
disiplin uygulanır: **form-bağlı olmayan VM'ler ayrı dosyada.**

### 11.5 `_Form.cshtml` ↔ `Details.cshtml` bölüm paritesi

Campaign Compact yüzeyi verifier'ın bölüm-haritası kontrolünü geçmektedir (sıra: Summary → References →
ExternalReferences → ConsentContext). Scope **yeni bir bölüm** ister.

**Karar:** yeni `<section>` **her iki dosyada da aynı konuma** eklenir — **Summary'den hemen sonra**
(sıra: Summary → **Scope** → References → ExternalReferences → ConsentContext). Gerekçe: scope, kampanyanın
kimliğine en yakın bilgidir ve cycle picker'ı Summary'dedir; ikisinin komşu olması kullanıcının
uygulanabilirlik uyarısını görmesini sağlar.

> Bölüm sırası **iki dosyada birebir aynı** olmazsa verifier kontrolü kırılır — FU08'de bu kontrol
> yeşildi ve öyle kalmalıdır (AC-V-1).

---

## 12. Validation Rules

### 12.1 Scope alan düzeyi

| Kural | Hata kodu | HTTP |
|---|---|---|
| `ScopeType` bilinmeyen değer | `campaign_scope_type_unknown` | 400 |
| `ScopeType` yok **ve** country/legal-entity dolu (türetilemez) | `campaign_scope_type_unknown` | 400 |
| Seviyenin gerektirdiği referans yok | `campaign_scope_reference_required` | 400 |
| Birden fazla referans verilmiş (ayrımlı ihlali) | `campaign_scope_ambiguous` | 400 |
| `CountryScope` ISO alpha-2 değil | `campaign_country_invalid` | 400 |
| `COUNTRY_CODES` seti yayınlanmamış | `campaign_country_set_unpublished` | 400 |
| `CountryScope` sette yok | `campaign_country_unknown` | 400 |
| `business-unit` seti yayınlanmamış | `campaign_business_unit_set_unpublished` | 400 |
| `BusinessUnitId` sette yok | `campaign_business_unit_unknown` | 400 |
| `LegalEntityId` referanslanamaz / ACTIVE değil | `campaign_legal_entity_not_referenceable` | 400 |
| MDM erişilemez | `campaign_legal_entity_validation_unavailable` | **503** |
| **Bağlı cycle kampanyanın uygulanabilir kümesinde değil** | **`campaign_cycle_period_scope_mismatch`** | **400** |

### 12.2 D-RECHECK deseninin ÜÇÜNCÜ uygulaması (bu pack'in en kritik tablosu)

FU08 iki tetikleyici tanımlamıştı: **bind-active** yalnız binding değişince, **B2** bağlı olan her yazımda.
FU09 üçüncüsünü ekler ve **aynı desene** uyar.

| Kontrol | Ne zaman çalışır | Neden |
|---|---|---|
| **bind-active** (FU08) | binding **değişince** ve null değilse | Kapanan dönem bağını korur |
| **B2 containment** (FU08) | sonuçta **bağlı olan her** yazımda | Bind sonrası tarih kaydırmayı engeller |
| **scope-uygulanabilirlik** (FU09) | sonuçta **bağlı olan her** yazımda | **Scope EDITABLE.** Yalnız binding değiştiğinde kontrol edilseydi, scope'u değiştirip bağı bırakmak kuralı atlatırdı |
| **scope referans vokabüleri** (FU09) | referans **değişince** (D-SCOPE-LEGACY-REF) | §12.3 |

**Senaryo tablosu:**

| # | Senaryo | Sonuç |
|---|---|---|
| 1 | Scope `tenant`, bağsız | Serbest |
| 2 | Scope `business-unit:alpha`, `business-unit:alpha` döneme bind | **OK** |
| 3 | Scope `business-unit:alpha`, `tenant` döneme bind | **OK** (fallback) |
| 4 | Scope `business-unit:alpha`, `business-unit:beta` döneme bind | **400** `campaign_cycle_period_scope_mismatch` |
| 5 | Scope `business-unit:alpha`, `country:TR` döneme bind | **400** (§1.4/(a) — ayrımlı fallback) |
| 6 | Scope `tenant`, `business-unit:alpha` döneme bind | **400** — tenant kampanya bir BU takvimine bağlanamaz |
| 7 | `business-unit:alpha` + `alpha` döneme bağlı → scope `business-unit:beta`'ya değişiyor | **400** — önce unbind gerekir |
| 8 | `business-unit:alpha` + **`tenant`** döneme bağlı → scope `country:TR`'ye değişiyor | **OK** — tenant her seviyeden uygulanabilir |
| 9 | Bağlı dönem **closed** (FU08 korur) + scope değişiyor, dönem yeni scope'a uygulanamaz | **400** — close-dayanıklılığı scope kuralını **muaf tutmaz** |
| 10 | Bağlı dönem closed + scope değişiyor, dönem **hâlâ** uygulanabilir (ör. tenant) | **OK** — FU08 davranışı korunur |
| 11 | Scope değişiyor + aynı yazımda **unbind** | **OK** — bağ yoksa kural yok |
| 12 | Scope değişmiyor, sadece açıklama değişiyor, bağlı dönem uygulanabilir | **OK** |
| 13 | Eski kampanya (ScopeType yok, BusinessUnitId=`legacy-x`), yalnız açıklama düzenleniyor | **OK** — türetme + D-SCOPE-LEGACY-REF (§12.3) |

### 12.3 ⚠️ D-SCOPE-LEGACY-REF — **karar bekliyor**

`Campaign.BusinessUnitId` bugün FU04'e göre *"opaque code, validated as a non-empty string only"*. FU09 onu
yönetişimli `business-unit` vokabülerine bağlıyor. Bu bir **daraltmadır** ve mevcut veride şu risk vardır:

> Yayınlanmış `business-unit` setinde **olmayan** bir kod taşıyan mevcut bir kampanya, FU09'dan sonra
> **hiç düzenlenemez** hâle gelir — kullanıcı yalnızca açıklamayı değiştirmek istese bile scope doğrulaması
> yazımı reddeder.

| # | Seçenek | Değerlendirme |
|---|---|---|
| **(a)** | **Referans yalnız DEĞİŞTİĞİNDE doğrulanır**; dokunulmayan legacy kod olduğu gibi geçer | ✅ **ÖNERİLEN.** D-RECHECK'in aynı mantığı (§12.2). Yazar kodu düzeltmeye **karar verdiği anda** kural devreye girer; düzeltmediği sürece kampanyası rehin kalmaz. FU07'de bu sorun **yoktu** çünkü orada scope immutable'dır ve yalnız create'te doğrulanır |
| (b) | Her yazımda doğrula | ❌ Yayınlanmamış/eski kodlu kampanyaları kilitler. Sessiz bir veri kilidi, açık bir hata mesajından beterdir |
| (c) | Legacy kodları otomatik `tenant`'a düşür | ❌ **Veri kaybı.** Kampanyanın taşıdığı bağlamı, kimse istemeden siler |
| (d) | Backfill ile setleri hizala | ❌ Bu FU'da migration **yasak**; ayrıca hangi kodun hangi yayınlanmış değere karşılık geldiği **bilinmiyor** |

**Öneri: (a).** Karşı-karar verilirse §16'daki AC-LEGACY-1/2 ve §12.2/13 satırı yeniden yazılmalıdır ve
kullanıcının önce bir veri temizliği planlaması gerekir.

### 12.4 Reason / error code'lar

FU08'in üç kodu **korunur** (`campaign_outside_cycle_window`, `campaign_cycle_period_not_active`,
`campaign_cycle_period_not_found`). FU09 §12.1'deki **12** kodu ekler; hepsi mevcut snake_case
konvansiyonuna uyar ve `AllReasonCodes` listesine girer (*"nothing in this feature is silent"*).

### 12.5 Failure Path to Verify

| Yol | Beklenen |
|---|---|
| Duplicate | **N/A** — scope benzersiz değildir; aynı adreste çok kampanya olabilir |
| Missing | Seviye referansı yok → 400, hiçbir şey yazılmaz |
| Cross-tenant | Dönem seam'i tenant-scoped → başka tenant'ın dönemi `null` → 400, varlık **sızdırılmaz** (FU08 T22 emsali) |
| Unauthorized | Mevcut `crm.campaign.*`; yeni anahtar yok. MDM izni yoksa **503** (400 değil) |
| Concurrency | Campaign'de token yok (FU04); bu FU yeni yüzey açmaz |
| Half-applied | **İmkânsız** — §8.1 sırası: her doğrulama yazımdan önce |
| Dependency down | MDM → 503 + hiçbir şey yazılmaz; reference-data yayınsız → 400 (ayrı kod) |

---

## 13. Contract Surface

### 13.1 Bayrak

```jsonc
{
  "supportsCampaignManagement": true,
  "supportsCampaignTargetManagement": true,
  "supportsStaticTargetSnapshot": true,
  "supportsConsentEvaluationIntegration": true,
  "supportsTargetExclusionReason": true,
  "supportsTargetSourceProvenance": true,
  "supportsCyclePeriodBinding": true,            // FU08
  "supportsScopeAwareCycleBinding": true         // ← FU09
}
```

FU04'ün kuralı korunur: **kapalı hiçbir yetenek `false` olarak yayımlanmaz.** Yeni bayrak `true`dur çünkü
yetenek gerçekten açılmaktadır.

> **FU08 testi güncellenmelidir:** `T35_Forbidden_Contract_Flags_Are_Absent` bayrak kümesini **ada göre**
> assert ediyor (FU08'de sayıdan ada çevrilmişti, tam da bu sebeple). FU09 sekizinci adı oraya **bilinçli
> olarak** ekler — §17.3.

### 13.2 `limitations` — biri REVİZE, dördü YENİ

**REVİZE (FU08'in yazdığı satır silinir):**

```diff
- "FU08: the binding does NOT validate scope — a campaign's BusinessUnitId is not matched against the
-  period's ScopeType/ScopeRef, so a campaign may be bound to a period filed at a different address;
-  campaign scope is a separate follow-up and is not silently implied here"
+ "FU09: the binding IS scope-aware — a campaign carries a discriminated scope (tenant / country /
+  legal-entity / business-unit) and may only bind a period APPLICABLE to it: its own address, or the
+  tenant-wide fallback. A period at a different address of the same level is never offered and never accepted"
```

**YENİ:**

1. *"FU09: campaign scope is DATA, not authorization — it says where a campaign lives, never who may see it;
   no read is filtered by scope and no permission is derived from it"*
2. *"FU09: campaign scope is EDITABLE (unlike a cycle period's, which is identity), and changing it
   re-validates the bound period — a period that is no longer applicable refuses the write rather than being
   silently unbound"*
3. *"FU09: applicability follows the resolve-active precedence and a campaign names exactly ONE level, so a
   business-unit-scoped campaign sees business-unit and tenant periods only — a country period is not offered
   to it, because the campaign names no country"*
4. *"FU09: the scope model MIRRORS MOD-0165 FU07 rather than sharing its code; CyclePeriod is untouched and
   its own supportsCampaignBinding flag stays false. Consolidating the two rule sets is a follow-up"*

### 13.3 CyclePeriod contract'ına DOKUNULMAZ

`supportsCampaignBinding: false` **olduğu gibi kalır** (§2.2). FU08'in F-CYCLE-CONTRACT-NOTE follow-up'ı
açık kalır.

---

## 14. Authorization Convention

| Konu | Karar |
|---|---|
| Yeni permission anahtarı | **YOK.** Scope belirlemek bir **kampanya düzenleme** işidir |
| Yazma yolu | Mevcut `crm.campaign.*` guard'ları — değişmez |
| Scope-options okuma | Kampanya **read** kapısı; alt bağımlılıklar kendi guard'larını uygular |
| MDM legal-entity doğrulaması | `mdm.legal-entities.read` gerekir; yoksa **503** (F-MDM-PERM, FU07'den miras) |
| Scope-bazlı yetki | **YOK** — F-SCOPE-RBAC (§8.3) |
| RBAC seed / grant | **YASAK** |

---

## 15. Gateway / API Routing Decision

| Soru | Cevap |
|---|---|
| Yeni Ocelot route'u gerekli mi? | **HAYIR.** Yeni endpoint'ler `/api/crm/campaigns/...` altındadır; FU04'ün route'u zaten wildcard'lıdır |
| Yeni backend endpoint | **2 adet, ikisi de READ:** `GET /api/crm/campaigns/scope-options` · `GET /api/crm/campaigns/applicable-cycle-periods` |
| `integration-agent` görevi | **HAYIR** |
| Frontend | Campaigns proxy'sine **2 salt-okunur passthrough** |

**Neden `applicable-cycle-periods` ayrı bir endpoint:** FU08'in `api/cycle-periods` passthrough'u FU06
selector'ına *"aktif dönemleri ver"* diyordu. Uygulanabilirlik kampanyanın scope'una bağlıdır ve
**precedence mantığı sunucuda yaşamalıdır** — tarayıcıya "hangi dönem uygulanabilir" kararını verdirmek,
kuralı iki yere yazmak olurdu (§10.2 disiplininin aynısı). FU08 passthrough'u **korunur** ama form artık
yeni endpoint'i kullanır; eski passthrough'un tek tüketicisi kalmazsa kaldırılması F-CLEANUP'a yazılır.

---

## 16. Acceptance Criteria

### Scope modeli

| # | Kriter |
|---|---|
| **AC-S-1** | `Campaign` üzerinde `ScopeType` / `CountryScope` / `LegalEntityId` alanları vardır; `BusinessUnitId` **yeniden kullanılır**, ikinci bir BU alanı **eklenmemiştir** |
| **AC-S-2** | Ayrımlı invaryant: iki referans birlikte verilirse **400** `campaign_scope_ambiguous`; sessizce **temizlenmez** |
| **AC-S-3** | Her seviye kendi referansını **zorunlu** kılar; eksikse 400 `campaign_scope_reference_required` |
| **AC-S-4** | `tenant` scope hiçbir referans kabul etmez |
| **AC-S-5** | Country ISO alpha-2 ve **upper-case** normalize edilir; `tr` ile `TR` **aynı** adrestir |
| **AC-S-6** | Yayınlanmamış set ile bilinmeyen değer **ayrı** hata kodları döndürür |
| **AC-S-7** | MDM erişilemezken legal-entity scope **503** verir ve **hiçbir şey yazılmaz** (400 **değil**) |
| **AC-S-8** | Backfill script'i **yoktur**; `ScopeType`'sız mevcut satırlar okunur ve türetilir |
| **AC-S-9** | Türetme: BU dolu→`business-unit`, boş→`tenant`; country/legal-entity dolu + ScopeType yok → **400** |
| **AC-S-10** | Türetme **hiçbir şey yazmaz** (okuma sonrası satır Mongo'da değişmemiştir) |

### Uygulanabilirlik ve bağ

| # | Kriter |
|---|---|
| **AC-A-1** | §12.2 senaryo tablosunun **13 satırının tamamı** birebir doğrulanır |
| **AC-A-2** | `business-unit:beta` dönemi, `business-unit:alpha` kampanyasının picker'ında **hiç görünmez** |
| **AC-A-3** | `tenant` dönemler **her** scope'taki kampanyaya fallback olarak görünür |
| **AC-A-4** | `tenant` kampanya yalnız `tenant` dönem görür |
| **AC-A-5** | Picker'ı atlayıp doğrudan API ile uygulanamaz bir dönem bind etmek **400** verir (fail-closed) |
| **AC-A-6** | Scope değişimi bağlı dönemi uygulanamaz kılıyorsa **400**; kampanya **sessizce unbind edilmez** |
| **AC-A-7** | Aynı yazımda scope değişimi **+ unbind** → **OK** |
| **AC-A-8** | Kapanmış döneme bağlı kampanya scope kuralından **muaf değildir** (senaryo 9) |

### FU08 regresyonu (hiçbiri gevşemez)

| # | Kriter |
|---|---|
| **AC-R-1** | B2 aynen çalışır: iki uç eşit → geçer; bir gün dışarı → 400 |
| **AC-R-2** | AC-B2-4 (dönem son günü 00:00Z, kampanya bitişi 18:00Z) **hâlâ geçer** — kanonik gün korunur |
| **AC-R-3** | bind-active hâlâ **yalnız binding değişince** çalışır |
| **AC-R-4** | Kapanan dönem bağını korur ve kampanya (scope uygulanabilir kaldığı sürece) düzenlenebilir kalır |
| **AC-R-5** | D-OPENEND: açık uçlu + bağlı → 400 |
| **AC-R-6** | Guard hâlâ **yalnız** salt-okunur seam'leri tutar; `HttpClient` / `ICyclePeriodRepository` **yok** |
| **AC-R-7** | `CyclePeriod` üzerinde **hâlâ** kampanya referansı yok; `supportsCampaignBinding` **hâlâ false**; FU07 dosyaları **değişmemiş** |
| **AC-R-8** | FU08'in 38 testinden **T34 dışında hiçbiri değişmez** (§17.3) |

### Ayna

| # | Kriter |
|---|---|
| **AC-M-1** | `CampaignScopeRules.Normalize` ile `CyclePeriodScopeRules.Normalize` **aynı 12 girdi** için aynı kabul/ret verir (§10.2) |
| **AC-M-2** | `CampaignScopeTypes.ByPrecedence` ile `CyclePeriodScopeTypes.ByPrecedence` **aynı sırayı** taşır |
| **AC-M-3** | Campaign kodu `CyclePeriodScopeRules` / `CyclePeriodResolveEngine` **çağırmaz** (ayna, paylaşım değil) |
| **AC-M-4** | Campaign kodu `ICyclePeriodRepository` **kullanmaz**; yalnız salt-okunur seam'ler |

### UI

| # | Kriter |
|---|---|
| **AC-UI-0** | Dört sayfa `_LayoutTenantShell`; offcanvas/quickview **açılmamış** |
| **AC-UI-1** | Scope bölümü `_Form` ve `Details`'te **aynı sırada** (Summary → Scope → References → ExternalReferences → ConsentContext); verifier bölüm-haritası kontrolü **yeşil** |
| **AC-UI-2** | `ScopeType` değişince yalnız ilgili referans bloğu görünür |
| **AC-UI-3** | BU seçimi **country-first cascade**; ülke seçilmeden BU listesi açılmaz |
| **AC-UI-4** | Üç boş-liste durumu (**set yayınsız** / **bağımlılık erişilemez** / **plan yok**) UI'da **ayırt edilir**; hardcoded fallback **yok** |
| **AC-UI-5** | Cycle picker kampanyanın scope'una göre filtrelenir; scope değişince **yeniden yüklenir** |
| **AC-UI-6** | Mevcut seçim yeni scope'ta uygulanamazsa listede `uygulanamaz` rozetiyle **korunur** + uyarı gösterilir; **sessiz unbind yok** (§11.3) |
| **AC-UI-7** | DataTable'da scope kolonu; `tenant` satır anlamlı gösterilir (boş değil) |
| **AC-UI-8** | Details'te scope seviyesi + referansı görünür |
| **AC-L10N-1** | Yeni anahtarlar **7 dilde**; XML dengeli, parite tam |
| **AC-L10N-2** | Değerler gerçekten çevrilmiş (tr dosyasında İngilizce değer yok) |

### Doğrulama

| # | Kriter |
|---|---|
| **AC-V-1** | `verify_datatable_page --area CRM --module Campaigns --reference compact --api-profile proxy` **CRM baseline'ından gerilemez** (FU08: 87/8, FAIL kümesi Segments ile aynı) |
| **AC-V-2** | `dotnet build` **0 hata** (CrmService + Diten.Web) |
| **AC-V-3** | Test süiti yeşil; **FU06/FU07 testlerinin hiçbiri değişmez** |
| **AC-V-4** | CAND literal **0** |
| **AC-V-5** | `verify_module_id --check-all` **HARD violations: 0** |

---

## 17. Test Expectations

Yeni dosya: `tests/.../CampaignScopeMirrorTests.cs`.

### 17.1 Kapsam matrisi

| Grup | Test |
|---|---|
| **Ayrımlı invaryant** | Her seviye tek referans · iki referans → 400 · tenant + referans → 400 · eksik referans → 400 |
| **Normalizasyon** | `tr`→`TR` · BU trim · aynı adres iki yazımda aynı `ScopeRef` |
| **Türetme** | BU dolu→business-unit · boş→tenant · country+ScopeType yok → 400 · türetme **yazmaz** |
| **Vokabüler** | Set yayınsız ≠ değer yok (ayrı kod) · hardcoded fallback yok |
| **MDM** | referanslanamaz→400 · erişilemez→503 + yazma yok · 403→503 (400 değil) |
| **Uygulanabilirlik** | §12.2'nin **13 senaryosu**, her biri ayrı test |
| **Picker** | BU kampanya: BU+tenant görür, country **görmez** · beta dönem alpha kampanyaya görünmez · tenant kampanya yalnız tenant görür |
| **Scope-editable** | Scope değişimi + uygulanamaz bağ → 400 · + unbind → OK · tenant bağ her seviyede hayatta kalır |
| **Legacy** | Yayınlanmamış BU kodlu kampanya yalnız açıklama düzenlenebilir (D-SCOPE-LEGACY-REF (a)) · referans değişince doğrulanır |
| **FU08 regresyonu** | B2 sınırları · AC-B2-4 kanonik gün · bind-active tetikleyicisi · close-dayanıklılığı · D-OPENEND |
| **Ayna eşitliği** | `CampaignScopeRules` ≡ `CyclePeriodScopeRules` (12 girdi) · `ByPrecedence` aynı sıra |
| **Yön** | `CyclePeriod`'da Campaign property'si **0** · Campaign'de `ICyclePeriodRepository` **0** · guard'da `HttpClient` **0** |
| **Contract** | Yeni bayrak true · kapalı yetenek `false` yayımlanmıyor · 12 yeni kod listede · revize + 4 yeni limitations satırı |

### 17.2 Frontend / manuel

| # | Adım |
|---|---|
| S1 | Fleet FU09 build'iyle yeniden başlatılır (RESX + yeni JS) |
| S2 | `COUNTRY_CODES` ve `business-unit` setlerinin yayınlı olduğu teyit edilir (yoksa fail-closed davranış gözlenir) |
| S3 | `business-unit:alpha` scope'lu kampanya oluşturulur; picker'da yalnız alpha + tenant dönemler görünür |
| S4 | `business-unit:beta` dönemi listede **yoktur** |
| S5 | Tenant dönem seçilip kaydedilir → OK |
| S6 | Scope `business-unit:beta`'ya çevrilir → tenant bağ **hayatta kalır** (senaryo 8) |
| S7 | `alpha` döneme bağlanıp scope `beta`'ya çevrilir → uyarı + kaydetmede 400 (senaryo 7) |
| S8 | Ülke seçilmeden BU listesi açılmaz (cascade) |
| S9 | Legal-entity scope: MDM izni yoksa **503** gözlenir (400 değil) |
| S10 | Targeting/snapshot sekmesi **etkilenmemiştir** (regresyon) |

> Authenticated smoke script'i uygulama sırasında yazılır ve **kullanıcı tarafından** çalıştırılır.

### 17.3 Bilerek değiştirilecek MEVCUT testler (şeffaflık)

| Test | Değişiklik | Gerekçe |
|---|---|---|
| `CampaignCycleBindingTests.T34_Scope_Is_Not_Matched` | **Tersine çevrilir** → `T34_Scope_Is_Matched` | FU08 bilinçli boşluğu test ediyordu; FU09 o kararı revize ediyor. Testi **silmek yerine** ters yönde assert etmek, kararın değiştiğini kayda geçirir |
| `CampaignTargetingRuntimeTests.T35_Forbidden_Contract_Flags_Are_Absent` | Bayrak **ad kümesine** `SupportsScopeAwareCycleBinding` eklenir | FU08 bu testi sayıdan ada çevirmişti; her yeni bayrak **bilinçli beyan** gerektirir |
| FU08'in diğer **36** testi | **DEĞİŞMEZ** | AC-R-8 |
| FU06/FU07 testleri | **DEĞİŞMEZ** | AC-V-3 |

---

## 18. Localization

Yeni anahtarlar **7 dilde** (`CampaignIndex.{ar,en,es,fr,ru,tr,zh}.resx`), XML dengeli, parite tam:

| Anahtar | Kullanım |
|---|---|
| `ScopeSection` | Form + Details bölüm başlığı (**iki dosyada birebir aynı** — §11.5) |
| `ScopeType` · `ScopeType_tenant` · `ScopeType_country` · `ScopeType_legal-entity` · `ScopeType_business-unit` | Seviye seçici |
| `CountryScope` · `LegalEntity` · `BusinessUnit` · `BusinessUnitCountryFilter` | Referans alanları |
| `ScopeHelp` | *"Kampanyanın adresi. Bağlanabileceği dönemler bu adrese göre belirlenir."* |
| `ScopeEditableHint` | *"Scope değiştirilebilir; bağlı dönem yeni scope'a uygulanamıyorsa önce kaldırılmalıdır."* |
| `CyclePeriodNotApplicable` | Picker'daki `uygulanamaz` rozeti (§11.3/2) |
| `CyclePeriodScopeMismatch` | 400 hatasının kullanıcı metni |
| `ScopeReferenceRequired` · `ScopeAmbiguous` · `CountryUnknown` · `CountrySetUnpublished` · `BusinessUnitUnknown` · `BusinessUnitSetUnpublished` · `LegalEntityNotReferenceable` · `LegalEntityUnavailable` | Hata metinleri |
| `NoTerritoryPlanMatches` · `ReferenceSetUnpublished` · `DependencyUnavailable` | **Üç ayrı** boş-liste açıklaması (AC-UI-4) |

Anahtarlar `_IndexL10n.cshtml` köprüsüne eklenir. **Parite testi zorunludur.**

---

## 19. Ready-for-dev Checklist

| # | Madde | Durum |
|---|---|---|
| 1 | DCP-002 exit 0 + kapsam notu | ✅ §0.1 |
| 2 | FU09 çakışması yok | ✅ §0.1 |
| 3 | Golden reference (Compact, 25 alan) | ✅ §11.1 |
| 4 | Layout açıkça yazıldı | ✅ §9 |
| 5 | Backend dosya konvansiyonu + iki istisna | ✅ §10.1 |
| 6 | Frontend dosya seti | ✅ §5.2, §11 |
| 7 | Validation Rules + D-RECHECK üçüncü uygulaması | ✅ §12 |
| 8 | Failure Path | ✅ §12.5 |
| 9 | Authorization | ✅ §14 |
| 10 | Gateway kararı | ✅ §15 |
| 11 | Acceptance Criteria | ✅ §16 |
| 12 | Test Expectations + değiştirilecek testlerin şeffaf listesi | ✅ §17 |
| 13 | Protected paths | ✅ §2.1 |
| 14 | Migration gerekmediği kanıtlandı | ✅ §4.3 |
| 15 | FU08 kilitlerinin korunduğu gösterildi | ✅ §1.2, AC-R-1..8 |
| 16 | **D-SCOPE-LEGACY-REF kararı** | ⛔ **BEKLİYOR** — §12.3 |
| 17 | **D-SCOPE-BU-COUNTRY onayı** (BU kampanya country dönem görmez) | ⛔ **BEKLİYOR** — §1.4 |
| 18 | D-MIRROR uygulama şekli (M4), D-SCOPE-DISPLAY, D-FILES onayı | ⛔ **BEKLİYOR** — §1.3 |
| 19 | `COUNTRY_CODES` + `business-unit` **yayınlı** (veri ön koşulu) | ⛔ **OPERATÖR** — §0.3, §2.5 |
| 20 | `status: ready-for-dev` + `runtime_code_allowed: true` | ⛔ **BEKLİYOR** |

> **Pack, 16–20 kapanmadan `ready-for-dev` sayılmaz.**

---

## 20. Follow-up Items

| # | İş | Domain | Neden |
|---|---|---|---|
| **F-SCOPE-SHARED** | `CampaignScopeRules` ile `CyclePeriodScopeRules`'un ortak bir `Features/Shared/Scope/`'a indirgenmesi | commercial-suite | **Kullanıcı talimatıyla ilan edildi.** D-MIRROR bugün kopyayı seçiyor; birleştirme CyclePeriod dosyalarına dokunmayı gerektirir |
| **F-REGISTRY** | Registry'ye MOD-0165-FU06/FU07/FU08/**FU09** satırları | portfolio-delivery | FU06'dan beri açık |
| **F-COUNTRY-SOT** | Üç ülke kaynağının tek SoT'a indirilmesi (**6-kod limiti buradan geliyor**) | PSS / MDM | §0.3 — FU09 birini seçer, çelişkiyi çözmez |
| **F-SCOPE-RBAC** | Scope-bazlı yetki (*"yalnız TR kampanyaları"*) | PSS | §8.3 — scope bugün **veri**, yetki değil |
| **F-TARGET-SCOPE** | `CampaignTarget`'ların kampanya scope'una göre kısıtlanması | commercial-suite | Bu FU hedeflere dokunmaz |
| **F-SCOPE-FILTER** | Campaign listesinde scope'a göre filtre chip'i | commercial-suite | FU09 kolon ekler, filtre eklemez (B5) |
| **F-BU-COUNTRY-FALLBACK** | BU-scope kampanyanın country dönemlerini de görmesi | commercial-suite | §1.4 — bugün **kasten** yok; talep çıkarsa Territory türetmesiyle tasarlanır |
| **F-MDM-PERM** | CRM rollerine `mdm.legal-entities.read` | PSS | FU07'den miras; izinsiz aktör 503 alır |
| **F-TERRITORY-GATE** | BU aday listesinin sert kapıya dönüşmesi | commercial-suite | D-BU-SOURCE soft-gate mirası |
| **F-CYCLE-CONTRACT-NOTE** | FU06 contract'ının `supportsCampaignBinding: false` satırına yön netleştirmesi | commercial-suite | FU08'den açık |
| **F-FILE-DRIFT** | `Features/Campaign/`'in Golden kanonik düzene tam hizalanması | commercial-suite | §10.1 — FU09 yarım adım attı |
| **F-CLEANUP** | FU08'in `api/cycle-periods` passthrough'u tüketicisiz kalırsa kaldırılması | commercial-suite | §15 |
| **F-VFP-FK** · **F-MICROTARGET** · **F-ORG-UNIT-SCOPE** | FU06/FU07'den devralındı | commercial-suite | Değişmedi |

---

## Ek A — Bu pack'in reddettiği altı kolay yol

| # | Kolay yol | Neden reddedildi |
|---|---|---|
| A1 | `CyclePeriodScopeRules`'u doğrudan çağır (ayna yerine paylaşım) | Campaign scope'u **kimlik değil**, CyclePeriod'unki **kimlik**. Bugün aynı görünen iki kural yarın ayrışır; paylaşılan kod ayrışmayı **yasaklar** ve biri yanlış davranmaya başlar |
| A2 | `BusinessUnitCountryContext`'i kural dayanağı yap | FU07 onu *"documentation, never identity"* ilan etti. Dokümantasyonu kimliğe terfi ettirmek, FU07'nin uniqueness/overlap garantilerini sessizce değiştirir |
| A3 | Scope'u da immutable yap (FU07 gibi kopyala) | Kampanyanın adresi kimliği değildir; yanlış BU ile açılmış bir kampanyayı kapatıp yeniden açmaya zorlamak, hedef geçmişini gereksizce çatallar |
| A4 | Scope uyumsuzluğunda bağı **sessizce kaldır** | Sessiz unbind, kullanıcının kurduğu bir ilişkiyi haber vermeden siler. FU08 AC-UI-3'ün tam tersi |
| A5 | Uygulanabilirlik kararını **tarayıcıda** ver | Kural iki yere yazılır; doğrudan API çağrısı onu atlar. FU07'nin `platform_surface_is_country_only` emsali |
| A6 | Legacy BU kodlarını backfill ile hizala | Hangi kodun hangi yayınlanmış değere karşılık geldiği **bilinmiyor**; tahminle yazılan bir migration geri alınamaz |

## Ek B — İlan edilmiş boşluklar (sessiz değil)

| # | Boşluk | Nerede ilan edildi |
|---|---|---|
| B1 | BU-scope kampanya country dönem görmez | §1.4 · limitations #3 · AC-A-2 · F-BU-COUNTRY-FALLBACK |
| B2 | Scope yetki değildir; okuma scope'a göre filtrelenmez | §8.3 · limitations #1 · F-SCOPE-RBAC |
| B3 | Hedefler scope'a göre kısıtlanmaz | §2 yasak listesi · F-TARGET-SCOPE |
| B4 | `COUNTRY_CODES` 6-kod limiti devralınır, bu pack **doğrulamaz** | §0.3 · F-COUNTRY-SOT |
| B5 | İki scope kural seti kopyadır, birleştirilmemiştir | §2.3 · limitations #4 · F-SCOPE-SHARED |

---

**Otorite sırası:** Blueprint Excel > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` >
`.antigravity/rules/`.
