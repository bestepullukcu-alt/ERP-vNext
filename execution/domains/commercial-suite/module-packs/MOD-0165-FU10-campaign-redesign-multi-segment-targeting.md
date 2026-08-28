---
id: MOD-0165-FU10
name: Campaign Redesign — Multi-Segment Targeting
parent: MOD-0165
parent_name: Campaign Management
siblings: MOD-0165-FU04 (campaign runtime) · MOD-0165-FU05 (campaign admin UI) · MOD-0165-FU06/FU07 (cycle period + scope) · MOD-0165-FU08 (cycle binding) · MOD-0165-FU09 (campaign scope mirror) — bu FU FU08+FU09'un ÜSTÜNE kurulur
domain: commercial-suite
service: Diten.CrmService
frontend: frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: review
runtime_code_allowed: true
flip_approved_by: "control-tower (2026-08-28) — R2 verified: draft, DCP-002 exit 0, supportsCampaignBinding stays false (CyclePeriod untouched), CampaignTarget/snapshot/consent backend+UI preserved (mode-gated, not deleted), CycleCapacity build in another session left Campaign untouched. DECISIONS APPROVED: D-TARGETING-MODE (required segment|manual toggle, mirrors Segment static/dynamic, manual UI KEPT mode-gated = no §11.2 regression); D-TARGETING-MODE-WRITES=(b) reject new writes to the passive mode with 400 (rule at the write path, not UI-only — FU07 lesson; existing passive data stays dormant, nothing deleted); D-SEGMENT-VERSION=(a) pin specific SegmentId + surface superseded; D-DEPRECATE-CASCADE=yes (filter chips+columns+query-params removed for deprecated fields); D-SEGMENT-MAX=50; D-SEGMENT-SEAM=ICampaignSegmentCatalog read-only in consumer folder (Segmentation untouched); D-SEGMENT-VALIDATION validate-on-change; pre-FU10 rows derive to manual; ≥1 segment on every write (D-RECHECK 5th); dormant≠invisible (AC-UI-10). Accepted regressions: deprecated 10 fields unreadable from API (data stays in Mongo); auto-code generate-on-POST no preview (Account pattern). All FU08+FU09 locks preserved."
revision: "R2 (2026-08-28) — D-TARGETING-MODE. R1'in §11.2 'yetenek gerilemesi' kararı İPTAL: manuel targeting UI'sı KALDIRILMAZ, mod-kapılı tutulur. Campaign'e zorunlu TargetingMode (segment | manual) eklenir; Segment'in static/dynamic deseni aynalanır."
runtime_code_scope: "Kapsam: Campaign aggregate'ine TargetingMode + TargetedSegments + deprecate işaretleri, CampaignCodeSequence + generator (AccountCodeSequence deseni), ICampaignSegmentCatalog salt-okunur seam + Persistence adapter, CampaignSegmentValidator (validate-on-change), mod-kapılı yazma kuralları, Create/Update handler entegrasyonu, DTO/VM/command sadeleştirmesi, Compact form yeniden düzeni (mod toggle + kaldırmalar + reorder + çoklu-segment picker + date-only), Details'te mod-kapılı manuel targeting kartı, liste filtresi/kolon temizliği, targeting detail sayfası + row action, Campaigns proxy'sine salt-okunur segment passthrough, 7 dil RESX, contract bayrağı + limitations, boundary testleri. YASAK: Segment aggregate/rules/handlers/UI YAZIMI, CyclePeriod her şeyi, supportsCampaignBinding'in çevrilmesi, CampaignTarget/snapshot/consent BACKEND'inin veya UI'sının SİLİNMESİ, segmentten üyelik çözümleme (snapshot resolution), MicroTarget/StrategyTemplate açılması, veri migrasyonu/backfill, pasif mod verisinin TEMİZLENMESİ, Mongo hand-edit, ocelot.json yazımı, registry yazımı, RBAC seed/grant."
owner: module-pack-author
branch: feature/crm/mod-0165-fu10-campaign-redesign-multi-segment-targeting
started: 2026-08-28
target: 2026-08-28
form_field_count: 18
predecessor: MOD-0165-FU09 (SHIPPED — 47 test, verifier 87/8) + MOD-0165-FU08 (SHIPPED)
dependencies:
  - MOD-0165-FU09 (ZORUNLU ÖNCÜL — ayrımlı scope + scope-filtreli cycle picker; KORUNUR)
  - MOD-0165-FU08 (ZORUNLU ÖNCÜL — CyclePeriodId pin + B2 + bind-active + D-OPENEND + D-RECHECK; KORUNUR)
  - MOD-0167-FU02 (Segment aggregate — SALT OKUNUR referans + static/dynamic deseninin KAYNAĞI; DOKUNULMAZ)
  - MOD-0165-FU04 (Campaign + CampaignTarget + snapshot — backend VE UI korunur, mod-kapılı hâle gelir)
  - MOD-0149 (Account — `AccountCodeSequence` deseninin kaynağı; DOKUNULMAZ)
  - MOD-0164 (consent — snapshot provenance; DOKUNULMAZ)
  - MOD-0155-FU05 / MOD-0167-FU04 (MicroTarget / StrategyTemplate — "ne promote" oraya gider; bu FU AÇMAZ)
  - MOD-0018 (RBAC — yalnız tüketim; yeni anahtar YOK)
  - DEV-0001 (Golden Reference Compact — 18 alan ile Compact KALIR)
---

# MOD-0165-FU10 — Campaign Redesign: Targeting Mode + Multi-Segment Targeting

> **TASLAK / BOUNDARY + CONTRACT PACK — R2 (2026-08-28), `status: ready-for-dev`, `runtime_code_allowed: true`.**
> Bu pack **kod yazma yetkisi vermez.**
>
> **R1'den ne değişti:** R1, manuel targeting UI'sını **kaldırıyordu** ve bunu bir *"ilan edilmiş yetenek
> gerilemesi"* olarak kayda geçiriyordu. **O karar iptal edildi.** Manuel targeting **kalır** ve kampanya
> artık **hangi yolla hedeflendiğini kendisi söyler**: `TargetingMode` = `segment` | `manual`.
> Böylece FU10 bir yetenek **eklemesi** olur, bir değiş-tokuş değil.
>
> **Kampanyanın yeni tanımı:** *KİM* (aktif moda göre: segmentler **veya** manuel hedefler) + *NE ZAMAN*
> (cycle period) + *NEREDE* (scope). **"Ne promote edilecek" hâlâ kampanyada değildir** — segment başına
> değişir, MicroTarget/StrategyTemplate'e aittir.
>
> **FU08 ve FU09'un hiçbir kilidi gevşemez.**

---

## 0. Delivery Record (2026-08-28)

> **RUNTIME AUTHORIZATION (2026-08-28).** Kullanıcı R2'yi `ready-for-dev` + `runtime_code_allowed: true` olarak
> yetkilendirdi ve tek açık kararı kapattı: **D-TARGETING-MODE-WRITES = (b)** — pasif moda YENİ yazım 400, mevcut
> veri dormant. Uygulama pack'e harfiyen uyularak yapıldı; aşağıdaki sapmalar dışında hiçbir karar değişmedi.

**Teslim edilen yüzeyler.** Backend: `Campaign.cs` (+`TargetingMode` +`TargetedSegments` +`CampaignTargetedSegment`
+`CampaignTargetingModes` +`CampaignLimits` +`EffectiveTargetingMode()` +`IsSegmentTargeted()`, 10 alan
**deprecate işaretli**, +8 reason code) · `CampaignCodeSequence` + repo + `CampaignCodeGenerator` (**YENİ**, Account
deseni birebir) · `Read/ICampaignSegmentCatalog.cs` (**YENİ**, salt-okunur) + `Persistence/CampaignSegmentCatalog.cs`
(**YENİ**) · `Services/CampaignSegmentValidator.cs` (**YENİ**) · `CampaignTargetCommandHandlers.cs`
(`LoadMutableCampaignAsync` içine **TEK** mod kapısı) · komut/DTO/mapper/query sadeleştirmesi + segment projeksiyonu ·
`CampaignContract.cs` (+`SupportsSegmentTargeting`, +vokabüler `targetingModes` + `maxTargetedSegments`, +8 reason
code, +8 limitations) · `DependencyInjection.cs` (class-map, 2 index, 4 DI) · API request + controller.
Frontend: proxy (+salt-okunur `api/segments`, +`{id}/Targeting`, +`PickedDayToUtc`/`StoredDayToUtc`) · view model'ler
+ **2 YENİ ayrı dosya** (`CampaignTargetedSegmentViewModel`, `CampaignTargetingPageViewModel` — VM-gölge tuzağı) ·
`_Form.cshtml` **yeniden yazıldı** (5 bölüm: Summary → Scope → Cycle → Targeting → Consent, date-only, mod toggle) ·
`Details.cshtml` **yeniden yazıldı** (aynı 5 bölüm + **mod-kapılı** manuel targeting kartı) · `Targeting.cshtml`
(**YENİ**, salt okunur) · `_DataTable` / `_Filter` / `_IndexL10n` / `index.js` / `form.js` / `details.js` ·
7 dil RESX (**+23 anahtar, −14 anahtar**, 181×7, parite doğrulandı).
Tests: `CampaignTargetingModeTests.cs` (**YENİ**, 36 test).

**Manuel targeting UI'sı SİLİNMEDİ** (R2'nin özü): `_TargetsDataTable` · `_TargetCreateEditOffcanvas` ·
`_SnapshotPanel` · `_ConsentProvenance` dosyalarının **hiçbirine dokunulmadı**; Details onları `manual` modunda
render eder.

**Pack'ten sapmalar:**

| # | Sapma | Gerekçe |
|---|---|---|
| **S1** | `_Form.cshtml`'de `TargetedSegmentIds` etiketindeki **`*` işareti kaldırıldı** | Verifier *"Required label markers match ViewModel required metadata"* kontrolü düştü ve **haklıydı**: alan yalnız `segment` modunda zorunlu. `[Required]` eklemek manuel-mod kampanyalarını kırardı; koşulsuz bir `*` ise kuralın söylediğinden **fazlasını** iddia ediyordu. İşaret kaldırıldı, koşul yardım metnine yazıldı, `required` attribute'ünü `form.js` mod'a göre kuruyor, sunucu `campaign_segment_required` ile zaten zorluyor |
| **S2** | Mod kapısı üç handler'a değil, **paylaşılan `LoadMutableCampaignAsync`'e** kondu | Manuel create / manuel update / snapshot üçü de zaten oradan geçiyor — kural üç yere yazılsaydı üç kural olurdu. **Archive bilinçli olarak kapsam dışı**: o metottan geçmiyor ve geçmemeli, çünkü mevcut bir satırı emekliye ayırmak *yeni veri yazmak* değildir |
| **S3** | FU04 `T40_ExternalReferences_Stored_And_Duplicates_Reported` testi **silindi** | External reference artık command/request/DTO'da yok; testin çalıştıracağı yazma yolu kalmadı. Zayıflatmak yerine silindi ve yerine **neden silindiğini + alanın ve guard'ın hâlâ durduğunu** anlatan bir yorum bırakıldı |

**§17.3'ün istediği TAM liste — 9-alan-kaldırmanın fixture etkileri:**

| Dosya | Değişiklik | Davranış iddiası değişti mi? |
|---|---|---|
| `CampaignTargetingRuntimeTests.CampaignCmd` helper | `brandId` + `externalReferences` parametreleri kaldırıldı; komut 21→11 argüman | **Hayır** |
| `CampaignTargetingRuntimeTests.T01` | `brandId: BrandId` argümanı düştü; `Assert.Equal(BrandId, row.BrandId)` → `Assert.Null(row.BrandId)` | **Hayır** — lifecycle iddiaları aynı |
| `CampaignTargetingRuntimeTests.T02` | `noTenant` handler'ı 2 yeni bağımlılık alıyor | **Hayır** |
| `CampaignTargetingRuntimeTests.T35` | Bayrak ad kümesine **9.** bayrak (`SupportsSegmentTargeting`) | **Hayır** — kural aynı, küme büyüdü |
| `CampaignTargetingRuntimeTests.T40` | **SİLİNDİ** (S3) | Evet — kaldırılan yeteneğin testiydi |
| `CampaignTargetingRuntimeTests` fixture | +`FakeSegmentCatalog` +`CodeGenerator`; `Get/ListCampaign` +1 bağımlılık | **Hayır** |
| `CampaignCycleBindingTests` fixture | +`Targeting` +`CodeGenerator`; `List/Get` +1 bağımlılık | **Hayır** — 38 testin tamamı aynı |
| `CampaignScopeMirrorTests` fixture | Aynı | **Hayır** — 47 testin tamamı aynı |
| `CampaignScopeTestDoubles` | +`FakeSegmentCatalog` +`FakeCampaignCodeSequence` | — (yeni double) |
| FU06 / FU07 testleri | **DEĞİŞMEDİ** | — |

**Doğrulama** (ham çıktılar teslim raporunda): verifier proxy profili **87 PASS / 8 FAIL** — 95 kontrolün ad+sonuç
diff'i CRM kardeşi Segments ile **boş** (FU09 baseline'ı korundu, yeni FAIL yok) · `verify_module_id --check-all`
**HARD violations: 0** · `--check-id MOD-0165-FU10` **exit 0** · derleme **0 hata** · test **1336/1341 üç koşuda da
0 fail** (5 önceden var olan skip) · FU10 testleri **36/36** · CAND literal **0** ·
`SupportsCampaignBinding: false` **değişmedi** ve CyclePeriod/Segment dosyalarının hiçbirinin mtime'ı FU10
penceresine (≥18:00) girmedi.

**Bölüm paritesi:** `_Form` ve `Details` beş başlığı **birebir aynı sırada** taşıyor
(`SummarySection → ScopeSection → CyclePeriod → TargetingSection → ConsentContextSection`).

**Açık kalan:** F-SEGMENT-RESOLUTION · F-WHAT-TO-PROMOTE · F-TARGETING-MODE-HYBRID · F-DORMANT-CLEANUP ·
F-REGISTRY · F-DEPRECATED-FIELD-REMOVAL · F-SEGMENT-READER · F-SEGMENT-VERSION-MOVE ·
F-CAMPAIGN-SEGMENT-REPORT + FU08/FU09'dan devralınanlar (§20). Authenticated smoke (§17.2 S1–S13)
**kullanıcı tarafından** çalıştırılır; fleet'in FU10 build'i ile yeniden başlatılmasını gerektirir.

---

## 0.0 Kimlik Geçidi ve Ön Bulgular

### 0.1 DCP-002 — PASS (2026-08-28)

```text
$ py .antigravity/scripts/verify_module_id.py . --check-id MOD-0165-FU10 --name "Campaign Redesign" --parent MOD-0165
OK  MOD-0165-FU10: proven against Blueprint/registry.
REAL_EXIT=0
```

`grep -rn "MOD-0165-FU10" execution/` → yalnız bu pack. Görev girdisindeki *"MOD-0155-FU06 = Cycle Capacity
zaten alındı"* uyarısı **doğrulandı** (`MOD-0155-FU06-cycle-capacity.md` mevcut); bu FU o id'ye dokunmaz.

> **Geçidin kapsamı (FU08'de kanıtlandı):** geçit **kimliği** doğrular, FU açıklayıcı **adını doğrulamaz**.

**Registry satırı bu pack tarafından EKLENMEZ** — §20 / F-REGISTRY.

### 0.2 Kod okumasından çıkan bulgular

| # | Bulgu | Sonuç |
|---|---|---|
| **B1** | `ISegmentRepository` **yazma metotları taşır** (`InsertAsync`, `ReplaceAsync`); Segment'in salt-okunur seam'i **YOK** | Campaign onu **alamaz** → yeni salt-okunur seam (**D-SEGMENT-SEAM**, §2.3) |
| **B2** | `AccountCodeGenerator` kodu **POST anında, alan boşsa** üretir; form açılışında önizleme **yapmaz** | Terk edilen form sıra numarası **yakmaz** → **D-AUTOCODE** (önizleme reddedildi) |
| **B3** | Date-only için iki ayrı, birbirinin yerine geçmeyen yardımcı var: `PickedDayToUtc` (submit) / `StoredDayToUtc` (edit-populate) + runtime `ToDay` | Desen hazır; FU08 B2 zaten `.Date` kullanıyor |
| **B4** | `brandId`/`productId`/`subjectId` **liste filtre chip'lerinde, liste sorgusu parametrelerinde ve DataTable kolonlarında** da var | DTO'dan kaldırmak liste yüzeyini de etkiler → **D-DEPRECATE-CASCADE** (§4.5) |
| **B5** | `Campaign.BrandId/ProductId/OwnerUserId`'yi **hiçbir iş kuralı okumuyor** | Kaldırma düşük riskli |
| **B6** | `CampaignTargetTypes` **`segment` içeriyor** | `CampaignTarget(segment)` ≠ `Campaign.TargetedSegments` (§1.2) |
| **B7** | Segment **sürümlü**: `SegmentVersion`, `VersionLineageId`, `Superseded`, `SupersededBySegmentId` | **D-SEGMENT-VERSION = (a)** belirli sürüm pinlenir (§12.3) |
| **B8** | `GET /api/crm/segments?segmentStatus=active` mevcut; ocelot route'u var | Picker için **yeni backend endpoint'i / Gateway route'u gerekmez** |
| **B9** | **Segment'te pasif modun verisi DORMANT DEĞİL, YASAK.** `static` + kriter → 400 (*"A static segment carries no criteria"*); `dynamic` + manuel satır → 400 `TypeForbidsManualMembership` (*"so the label never lies about where a member came from"*) | Kullanıcı kararı FU10 için **dormant** diyor → bilinçli bir **ayrışma**; sınırı §12.2'de kesin çizilmelidir |

---

## 1. Module Summary

FU10'dan sonra bir kampanya **dört** şey söyler ve beşincisini söylemeyi reddeder:

| Soru | Alan | Geldiği FU |
|---|---|---|
| **NASIL hedefleniyor?** | `TargetingMode` = `segment` \| `manual` | **FU10** |
| **KİM?** | aktif moda göre: `TargetedSegments` **veya** `CampaignTarget` satırları | **FU10** (segment) · FU04 (manual) |
| **NE ZAMAN?** | `CyclePeriodId` + B2 | FU08 |
| **NEREDE?** | `ScopeType` + tek scope referansı | FU09 |
| ~~NE PROMOTE?~~ | — | **KAPSAM DIŞI** — MicroTarget/StrategyTemplate |

### 1.1 Ne DEĞİLDİR

| Kavram | Sahibi | Bu FU ile ilişkisi |
|---|---|---|
| **`Campaign.TargetingMode`** (bu FU) | MOD-0165 | **BU FU** — hedeflemenin **hangi yolla** yapıldığı |
| **`Campaign.TargetedSegments`** (bu FU) | MOD-0165 | **BU FU** — `segment` modunda hedefleme **niyeti** |
| **`Segment`** | MOD-0167-FU02 | **SALT OKUNUR referans.** Aggregate, kuralları, UI'sı, sürümleme mantığı **hiç değişmez** |
| **`CampaignTarget` + snapshot + consent** | MOD-0165-FU04 | **BACKEND VE UI KORUNUR.** `manual` modun çalışma yüzeyidir. Segmentten satır üretmek **SONRAKİ FU** |
| **"Ne promote edilecek"** | MOD-0155-FU05 / MOD-0167-FU04 | **KAPSAM DIŞI** — targeting sayfasında **placeholder** |
| **`CyclePeriod`** | MOD-0165-FU06/FU07 | **DOKUNULMAZ.** `supportsCampaignBinding` **false kalır** |

> **Tek cümlelik sınır:** *Kampanya **nasıl** ve **kime** yöneldiğini söyler; o segmentte **kimlerin**
> olduğunu Segment, onlara **ne** anlatılacağını başka bir aggregate söyler.*

### 1.2 `TargetedSegments` ile `CampaignTarget(segment)` neden aynı şey değil

```text
Campaign.TargetedSegments   →  NİYET.  "Bu kampanya A ve B segmentlerine yöneliktir."
                               Yazar seçer. Üyelik çözümlenmez. Consent sorulmaz.

CampaignTarget(type=segment) →  SONUÇ. "Bu satır, şu snapshot koşusunda, şu provenance ve
                               şu consent verdict'i ile kampanyaya girdi."
```

İkisini tek alana indirmek, *"kime yöneliyoruz"* ile *"kim gerçekten girdi"* farkını siler — FU04'ün
`SelectionReason`/`ReasonCodes` zorunluluğuyla koruduğu tam olarak budur.

### 1.3 D-Karar özeti

| # | Karar | Durum |
|---|---|---|
| **D-TARGETING-MODE** | Zorunlu `TargetingMode` = `segment` \| `manual`; Segment'in static/dynamic deseni aynalanır | **KİLİTLİ (R2)** |
| **D-TARGETING-MODE-DERIVE** | Pre-FU10 satırlar okuma anında **`manual`** türetilir; backfill YOK | **KİLİTLİ (R2)** · gerekçe §4.4 |
| **D-TARGETING-MODE-DORMANT** | Mod değişince pasif modun verisi **temizlenmez** (veri kaybı yok) | **KİLİTLİ (R2)** |
| **D-TARGETING-MODE-WRITES** | Pasif moda **YENİ** veri yazılabilir mi? | **KARAR BEKLİYOR** ⚠️ — §12.2 |
| **D-SEGMENT-LINK** | Çoklu segment referansı; yön Campaign → Segment, salt okunur | **KİLİTLİ** |
| **D-SEGMENT-VALIDATION** | Segment VAR + ACTIVE; **validate-on-change**; `segment` modunda **≥1** | **KİLİTLİ (R2)** |
| **D-SEGMENT-MIXED** | Karışık subject-type serbest | **KİLİTLİ** |
| **D-SEGMENT-VERSION** | Belirli `SegmentId` (sürüm) pinlenir; superseded **yüzeye çıkar** | **KİLİTLİ (R2 onayı)** |
| **D-DEPRECATE-FIELDS** | 8 reference + `OwnerUserId` + `ExternalReferences` → deprecate-nullable, migrasyon yok | **KİLİTLİ** |
| **D-DEPRECATE-CASCADE** | Liste filtreleri + DataTable kolonları da temizlenir | **KİLİTLİ (R2 onayı)** |
| **D-AUTOCODE** | `CampaignCodeSequence` (Account deseni birebir), editable, edit'te readonly | **KİLİTLİ** · şekli §12.5 |
| **D-DATEONLY** | Form `type="date"`, backend UTC-anchor | **KİLİTLİ** |
| **D-SCOPE-ABOVE** | Sıra: Summary → Scope → Cycle → **Targeting** → Consent | **KİLİTLİ** |
| **D-TARGETING-PAGE** | Row action → salt-okunur sayfa; **aktif modun** hedeflerini gösterir | **KİLİTLİ (R2)** |
| **D-SEGMENT-MAX** | `MaxTargetedSegments = 50`, contract'ta yayımlanır | **KİLİTLİ (R2 onayı)** |
| **D-SEGMENT-SEAM** | Salt-okunur `ICampaignSegmentCatalog`, **tüketici tarafında** tanımlı | **ÖNERİ** — §2.3 |
| **D-FILES** | Gruplanmış düzen korunur | **ÖNERİ** |

### 1.4 R1 → R2 farkı (kayıt için)

| Konu | R1 | **R2 (geçerli)** |
|---|---|---|
| Manuel targeting UI | **Silinir** (`_TargetsDataTable` / `_TargetCreateEditOffcanvas` / `_SnapshotPanel` / `_ConsentProvenance`) | **KORUNUR**, `manual` modunda görünür |
| Yetenek gerilemesi | İlan edilmiş bir gerileme vardı | **YOK** — FU10 saf ekleme |
| `TargetingMode` | yoktu | **ZORUNLU alan** |
| `TargetedSegments` boş küme | serbest (D-SEGMENT-EMPTY) | `segment` modunda **≥1**; `manual` modunda ilgisiz |
| form_field_count | 17 | **18** |
| AC-UI-9 | "UI yok ama bayrak true" | **"manuel mod çalışıyor + segment mod eklendi + toggle gizle/göster"** |

---

## 2. Ownership and Boundaries

**In-scope:** `TargetingMode` + `TargetedSegments` + deprecate işaretleri · `CampaignCodeSequence` + generator ·
`ICampaignSegmentCatalog` + adapter · `CampaignSegmentValidator` · mod-kapılı yazma kuralları ·
Create/Update entegrasyonu · DTO/VM/command sadeleştirmesi · Compact form yeniden düzeni + **mod toggle** ·
Details'te **mod-kapılı** manuel targeting kartı · liste filtresi/kolon temizliği · targeting detail sayfası +
row action · salt-okunur segment passthrough · 7 dil RESX · contract bayrağı + limitations · boundary testleri.

**Out-of-scope (YASAK):**

| Yasak | Neden |
|---|---|
| `Features/Segmentation/**` · `Segment*.cs` · Segment UI **yazımı** | Yön tek, referans salt okunur |
| Segmentten **üyelik çözümleme** / snapshot üretimi | **Sonraki FU** (§2.4) |
| `CampaignTarget` / snapshot / consent **backend'inin veya UI'sının SİLİNMESİ** | **R2:** mod-kapılı korunur |
| **Pasif mod verisinin temizlenmesi** (mode-switch'te silme) | D-TARGETING-MODE-DORMANT — veri kaybı yok |
| MicroTarget / StrategyTemplate açılması | "Ne promote" oraya ait |
| `CyclePeriod` her şeyi · `supportsCampaignBinding`'in çevrilmesi | FU08/FU09'dan miras |
| FU08/FU09 kilitlerinin gevşetilmesi | §2.2 |
| Veri migrasyonu / backfill / Mongo hand-edit | D-DEPRECATE-FIELDS + D-TARGETING-MODE-DERIVE |
| `ocelot.json` · registry · RBAC seed/grant | Pack yetkisi dışı |

### 2.1 Protected paths

```text
services/.../Domain/Entities/Segment*.cs                                  [OKUNUR, YAZILMAZ]
services/.../Domain/Repositories/ISegmentRepository.cs                    [KULLANILMAZ — B1]
services/.../Application/Features/Segmentation/**                         [DOKUNULMAZ]
services/.../Api/Controllers/CRM/Segments*.cs                             [DOKUNULMAZ]
frontend/.../{Controllers/CRM/SegmentsController.cs, Views/CRM/Segments/**,
              wwwroot/assets/js/CRM/Segments/**, Resources/Views/CRM/Segments/**}  [DOKUNULMAZ]
services/.../Application/Features/CyclePeriod/**                          [DOKUNULMAZ]
services/.../Application/Features/Campaign/Snapshot/**                    [DOKUNULMAZ]
services/.../Application/Features/Campaign/Handlers/CampaignTargetCommandHandlers.cs
    └── TEK İSTİSNA: D-TARGETING-MODE-WRITES kararına göre mod kapısı EKLENEBİLİR (§12.2). Başka hiçbir
        satırı değişmez; snapshot/consent akışı aynen kalır.
services/.../Application/Features/ConsentPreference/**                    [DOKUNULMAZ]
services/.../Application/Features/Account/AccountCode*.cs                 [OKUNUR — desen kaynağı]
frontend/.../Views/CRM/Campaigns/_TargetsDataTable.cshtml                 [KORUNUR — R2; yalnız mod kapısı sarmalar]
frontend/.../Views/CRM/Campaigns/_TargetCreateEditOffcanvas.cshtml        [KORUNUR — R2]
frontend/.../Views/CRM/Campaigns/_SnapshotPanel.cshtml                    [KORUNUR — R2]
frontend/.../Views/CRM/Campaigns/_ConsentProvenance.cshtml                [KORUNUR — R2]
gateway/**/ocelot.json · execution/registries/module-id-registry.md       [DOKUNULMAZ]
```

### 2.2 FU08 + FU09 kilitleri — hepsi aynen korunur

| Kilit | FU10'daki durumu |
|---|---|
| `CyclePeriodId` nullable pin, tek yön | **Aynen** |
| B2: `[campaign] ⊆ [period]`, INCLUSIVE, UTC kanonik gün | **Aynen** — date-only onu kolaylaştırır, değiştirmez (§8.3) |
| bind-active (yalnız binding değişince) | **Aynen** |
| close-a-dayanıklılık | **Aynen** |
| D-OPENEND(a) | **Aynen** |
| D-RECHECK asimetrisi | **Aynen** — segment ve mod kuralları **aynı desene** eklenir (§12.1) |
| Ayrımlı scope + scope-filtreli cycle picker | **Aynen** — yalnız HTML sırası değişir |
| Guard salt-okunur, HTTP self-call yok | **Aynen** |
| `CyclePeriod.supportsCampaignBinding: false` | **Aynen false** |

### 2.3 D-SEGMENT-SEAM

`ISegmentRepository` yazma metotları taşır (B1). Onu bir Campaign handler'ına vermek, başka bir modülün
aggregate'ine yazma yolunu bir tuş uzağa koyar — FU07'nin `ITerritoryBusinessUnitCatalog` gerekçesinin aynısı.

| # | Seçenek | Değerlendirme |
|---|---|---|
| (a) | `ISegmentRepository`'yi doğrudan enjekte et | ❌ Yazma yolu açar |
| (b) | `Features/Segmentation/Read/ISegmentReader.cs` | ❌ Klasör **protected**; Segment'in tüketim seam'ini tasarlamak MOD-0167'nin işi |
| **(c)** | **`Features/Campaign/Read/ICampaignSegmentCatalog.cs`** | ✅ **SEÇİLEN.** Repo emsali tam bu: `ITerritoryBusinessUnitCatalog` **CyclePeriod'un** klasöründe durur |

```text
ICampaignSegmentCatalog
  ├─ GetByIdsAsync(IReadOnlyCollection<Guid>, ct) → IReadOnlyList<CampaignSegmentRef>   (TEK round trip)
  └─ ListSelectableAsync(ct)                      → IReadOnlyList<CampaignSegmentRef>   (ACTIVE)

CampaignSegmentRef(SegmentId, SegmentCode, SegmentName, SubjectType, SegmentStatus,
                   Superseded, VersionLineageId, SegmentVersion)
```

Hiçbir metodu yazmaz, `HttpClient` tutmaz. Segment **id ile** referanslanır; kod/ad/subject-type **kopyalanmaz**
(FU08 D-PROJECTION).

### 2.4 Bilinçli olarak YAPILMAYAN şey (açık ilan)

> Bu FU, `TargetedSegments`'ten **hiçbir `CampaignTarget` satırı üretmez.** Segment üyeliği **çözümlenmez**,
> consent **sorulmaz**, snapshot **koşturulmaz**.

`segment` modundaki bir kampanya *"kime yöneldiğini"* bilir, *"kimin içinde olduğunu"* **bilmez**. Bu bir
sıralama kararıdır. **F-SEGMENT-RESOLUTION** o adımı açar ve FU04'ün **hazır duran** provenance + consent
altyapısını kullanır — R2'de o altyapı **hem backend hem UI olarak yerinde durduğu için** hazırlık maliyeti
sıfırdır.

### 2.5 Legacy CrmV2

FU06–FU09 bulguları geçerlidir. Legacy'nin `SubjectList`/`UCLN` kavramları **"ne promote"** sorusuna aittir;
bu FU onları açmaz ve kaldırılan alanların yerine legacy kavramı **konmaz**.

---

## 3. Owned Objects

| Nesne | Tür | Sahiplik |
|---|---|---|
| `Campaign.TargetingMode` | Alan (`string`) | **YENİ — R2** |
| `CampaignTargetingModes` | Vokabüler sabit sınıfı | **YENİ — R2** |
| `Campaign.TargetedSegments` | Alan (`List<CampaignTargetedSegment>`) | **YENİ** |
| `CampaignTargetedSegment` | Value object (`SegmentId` + `LinkedAt`) | **YENİ** |
| `CampaignCodeSequence` + repo + `CampaignCodeGenerator` | Aggregate + seam + servis | **YENİ** (Account deseni) |
| `ICampaignSegmentCatalog` + `CampaignSegmentRef` | Salt-okunur seam | **YENİ** |
| `CampaignSegmentValidator` | Application servisi | **YENİ** |
| 8 reference alanı · `OwnerUserId` · `ExternalReferences` | Alan | **MEVCUT — DEPRECATE** (§4.3) |
| `CampaignTarget` + snapshot + consent (backend **ve** UI) | Aggregate + akış + yüzey | **MOD-0165-FU04 — KORUNUR, mod-kapılı** |
| `Segment` | Aggregate | **MOD-0167-FU02 — salt okunur** |

---

## 4. Entity Fields

### 4.1 Eklenen

| Alan | Tip | Zorunlu | Kısıt | Açıklama |
|---|---|---|---|---|
| `TargetingMode` | `string` | **Evet** (türetilir) | `CampaignTargetingModes` — `segment` \| `manual` | Kampanyanın **nasıl** hedeflendiği. Boşsa okuma anında `manual` türetilir (§4.4). **Değiştirilebilir**; mod değişimi pasif modun verisini **silmez** |
| `TargetedSegments` | `List<CampaignTargetedSegment>` | `segment` modunda **≥1** | Aynı `SegmentId` iki kez olamaz; ≤ `MaxTargetedSegments` (50) | Hedeflenen segmentler. Yalnız **id** + bağlanma zamanı |
| `CampaignTargetedSegment.SegmentId` | `Guid` | Evet | Boş GUID reddedilir | MOD-0167 referansı, **belirli sürüm** (D-SEGMENT-VERSION) |
| `CampaignTargetedSegment.LinkedAt` | `DateTimeOffset` | Evet | Sunucu damgası | Provenance; iş kuralı değil |

### 4.2 Değişen

| Alan | Değişiklik |
|---|---|
| `CampaignCode` | Create'te boş bırakılabilir → sunucu üretir (§12.5). Edit'te **immutable** |
| `StartDate` / `EndDate` | Tip **değişmez**; yalnız form `type="date"` + iki uçta UTC-gün çapası (§8.3) |

### 4.3 Deprecate edilen (alan kalır, yazılmaz, okunmaz)

`BrandId` · `ProductId` · `SubjectId` · `TopicId` · `ConceptChainTemplateId` · `EngagementJourneyId` ·
`DefaultKnowledgePathId` · `DefaultKnowledgeContentId` · `OwnerUserId` · `ExternalReferences`
→ **deprecate-nullable**, entity'de kalır, command/DTO/VM/form'dan çıkar. **Migrasyon YOK.**

> ⚠️ **İlan edilen sonuç:** DTO'dan çıktıkları için bu değerler **hiçbir endpoint tarafından döndürülmez** —
> görünmez değil, **okunamaz** olurlar. Veri kaybolmaz ve halef FU okuyabilir. Yumuşak alternatif Ek A/A3'te
> reddedilmiş olarak kayıtlıdır.

### 4.4 D-TARGETING-MODE-DERIVE — pre-FU10 satırlar `manual` olur

Mevcut hiçbir kampanyada `TargetingMode` yoktur. Bu satırlar **taşınmaz**; okuma anında **`manual`** türetilir
(`EffectiveTargetingMode()`), FU09'un `EffectiveScopeType()` deseninin aynısı: **hiçbir şey yazmaz**, backfill
script'i **yoktur**, değer satır kendi sebebiyle bir sonraki kez yazıldığında kalıcılaşır.

| # | Seçenek | Değerlendirme |
|---|---|---|
| **(a)** | **Hepsi `manual`** | ✅ **SEÇİLEN.** FU10 öncesi **tek** hedefleme yolu manuel `CampaignTarget`'tı; hedefi olmayan bir kampanya da "manuel yolla, henüz hedefi yok" durumundaydı. Tek kural, belirsizlik yok |
| (b) | Hedefi varsa `manual`, yoksa `segment` | ❌ **Boş kampanyaları anında GEÇERSİZ kılar**: `segment` modu ≥1 segment ister, dolayısıyla o kampanyalar bir daha kaydedilemez. Ayrıca kimsenin vermediği bir niyeti uydurur |
| (c) | Nullable "seçilmemiş" üçüncü durum | ❌ İki durumlu bir alana üçüncü durum ekler; her tüketici üç dal yazmak zorunda kalır |

> **(b)'yi eleyen argüman tek başına yeterlidir:** türetme, mevcut bir kaydı **geçersiz hâle getiremez**.

### 4.5 D-DEPRECATE-CASCADE — liste yüzeyi

| Yüzey | FU10 sonrası |
|---|---|
| `filterBrandId` / `filterProductId` / `filterSubjectId` chip'leri | **Kaldırılır** |
| Liste sorgusu `brandId` / `productId` / `subjectId` parametreleri | **Kaldırılır** |
| DataTable `brandId` / `productId` kolonları | **Kaldırılır** |
| **YENİ kolon** | `TargetingMode` (rozet) — kampanyanın nasıl hedeflendiği listede görünür |
| `index.js` `emptyFilters()` / `saveViewColumnIndexes` / `totalColumnCount` / `baseOrder` | **Yeniden hesaplanır** |

> **Save-View uyarısı:** kolon sayısı değişir. `applyColOrder` uzunluk uyuşmazlığında **sessizce atlar**, yani
> kaydedilmiş görünümler **bozulmaz**, kolon düzeni varsayılana döner. AC-V-3'te doğrulanır.

### 4.6 `ExternalReferences` kaldırılınca

Alan ve mevcut değerler kalır. `FindExternalMappingConflictAsync` / `ValidateExternalReferences` **kod olarak
kalır** ama hiç tetiklenmez — silinmezler, çünkü çalışan bir guard'ı silmek geri alınması pahalı bir iştir.
`ix_campaigns_tenant_external_ref` index'i **kalır** (kullanılmayan index zararsızdır; düşürmek Mongo
hand-edit'i olurdu ve yasaktır).

### 4.7 Index kararı

`(TenantId, TargetingMode)` — liste filtresi/kolonu için.
`(TenantId, TargetedSegments.SegmentId)` — *"bu segmenti hangi kampanyalar hedefliyor?"* için.

> ⚠️ **Tuzak (FU08/FU09'da iki kez kayda geçti):** `TargetedSegments` zaten bir **dizidir**; bir bileşik index
> en fazla **bir** dizi alanı taşır. `LinkedAt` (bir `DateTimeOffset`, yani BSON'da yine dizi) bu index'e
> **girmez** — *"cannot index parallel arrays"*.

---

## 5. Repo Scope

### 5.1 Backend

```text
Domain/Entities/Campaign.cs                                   [DEĞİŞİR] +TargetingMode +CampaignTargetingModes
                                                                        +TargetedSegments +CampaignTargetedSegment
                                                                        +EffectiveTargetingMode() +CampaignLimits
                                                                        deprecate yorumları, +6 reason code
Domain/Entities/CampaignCodeSequence.cs                       [YENİ]
Domain/Repositories/ICampaignCodeSequenceRepository.cs        [YENİ]
Persistence/Repositories/CampaignCodeSequenceRepository.cs    [YENİ]  (Account birebir)
Application/Features/Campaign/
├── CampaignCodeGenerator.cs (+ICampaignCodeGenerator)        [YENİ]  (Account birebir)
├── Read/ICampaignSegmentCatalog.cs                           [YENİ]
├── Services/CampaignSegmentValidator.cs                      [YENİ]  mod-kapılı + validate-on-change
├── CampaignValidation.cs                                     [DEĞİŞİR] +saf mod/segment kuralları
├── Commands/CampaignCommands.cs                              [DEĞİŞİR] -9 alan, +TargetingMode +SegmentIds
├── Handlers/CampaignCommandHandlers.cs                       [DEĞİŞİR] auto-code + mod + segment doğrulama
├── Handlers/CampaignTargetCommandHandlers.cs                 [DEĞİŞİR?] yalnız D-TARGETING-MODE-WRITES=(b) ise mod kapısı
├── CampaignDtos.cs / CampaignMapper.cs                       [DEĞİŞİR] -9 alan, +mod +segment projeksiyonu
├── Handlers/CampaignQueryHandlers.cs                         [DEĞİŞİR] segment batch projeksiyonu; brand/product filtreleri kalkar
├── Queries/CampaignQueries.cs                                [DEĞİŞİR] filtre parametreleri temizlenir, +targetingMode filtresi
└── Contract/CampaignContract.cs                              [DEĞİŞİR] +1 bayrak, +vokabüler, limitations
Persistence/CampaignSegmentCatalog.cs                         [YENİ]  adapter
Persistence/DependencyInjection.cs                            [DEĞİŞİR] class-map + 2 index + DI
Api/Models/CRM/CampaignRequests.cs · Controllers/CRM/CampaignsController.cs  [DEĞİŞİR]
tests/.../CampaignTargetingModeTests.cs                       [YENİ]
tests/.../CampaignCycleBindingTests.cs · CampaignScopeMirrorTests.cs
        · CampaignTargetingRuntimeTests.cs                    [DEĞİŞİR] §17.3
```

### 5.2 Frontend

```text
Controllers/CRM/CampaignsController.cs        [DEĞİŞİR] +segment passthrough, +Targeting action, date-only anchor
Models/CRM/CampaignViewModels.cs              [DEĞİŞİR] -9 alan, +TargetingMode +SegmentIds
Models/CRM/CampaignTargetingViewModel.cs      [YENİ]  ⚠ ayrı dosya (VM-gölge tuzağı)
Views/CRM/Campaigns/_Form.cshtml              [DEĞİŞİR] yeniden düzen + mod toggle + segment picker
Views/CRM/Campaigns/Details.cshtml            [DEĞİŞİR] bölüm paritesi + targeting kartı MOD-KAPILI
Views/CRM/Campaigns/Targeting.cshtml          [YENİ]  salt-okunur, aktif moda göre
Views/CRM/Campaigns/_DataTable.cshtml         [DEĞİŞİR] kolon temizliği + TargetingMode kolonu
Views/CRM/Campaigns/_Filter.cshtml            [DEĞİŞİR] chip temizliği + mod chip'i
Views/CRM/Campaigns/_IndexL10n.cshtml         [DEĞİŞİR] anahtar temizliği + yeni anahtarlar
Views/CRM/Campaigns/_TargetsDataTable.cshtml          [KORUNUR — mod kapısı sarmalar]
Views/CRM/Campaigns/_TargetCreateEditOffcanvas.cshtml [KORUNUR]
Views/CRM/Campaigns/_SnapshotPanel.cshtml             [KORUNUR]
Views/CRM/Campaigns/_ConsentProvenance.cshtml         [KORUNUR]
wwwroot/assets/js/CRM/Campaigns/index.js      [DEĞİŞİR] kolon/filtre indeksleri + Targeting row action
wwwroot/assets/js/CRM/Campaigns/form.js       [DEĞİŞİR] applyTargetingModeVisibility + segment picker + date-only
wwwroot/assets/js/CRM/Campaigns/details.js    [DEĞİŞİR] targeting bloğu MOD-KAPILI (silinmez)
Resources/Views/CRM/Campaigns/CampaignIndex.*.resx  [DEĞİŞİR] 7 dil
```

---

## 6. Protected Paths

§2.1'de tam liste verilmiştir.

---

## 7. Dependencies

| Bağımlılık | Rol | Durum | Not |
|---|---|---|---|
| **MOD-0165-FU09** | genişletilen | SHIPPED | Scope + filtreli picker **korunur** |
| **MOD-0165-FU08** | genişletilen | SHIPPED | Tüm kilitler **korunur** |
| **MOD-0167-FU02** Segment | **okunan + desen kaynağı** | SHIPPED | Salt okunur; static/dynamic deseni aynalanır (§12.2) |
| **MOD-0165-FU04** CampaignTarget/snapshot | **korunan + mod-kapılı** | SHIPPED | Backend **ve** UI kalır |
| **MOD-0149** Account | desen kaynağı | SHIPPED | `AccountCodeSequence` birebir |
| **Gateway** | — | route **mevcut** | Yeni ocelot route'u gerekmez |
| **DEV-0001** | şablon | mevcut | 18 alan → Compact |

---

## 8. Runtime Constraints

### 8.1 Salt-okunurluk

Bu FU hiçbir `Segment` satırını yazmaz. Segment okuması **in-process**tir; HTTP self-call yasaktır.
Okuma tenant-scoped'tur; başka tenant'ın segmenti `null` → 400, varlık **sızdırılmaz**.

### 8.2 Erişilemezlik davranışı

| Durum | Cevap |
|---|---|
| Segment yok / başka tenant'ta | **400** `campaign_segment_not_found` |
| Segment `draft`/`archived`, **küme değişiyor** | **400** `campaign_segment_not_active` |
| Aynı segment iki kez | **400** `campaign_segment_duplicate` |
| Tavan (50) aşıldı | **400** `campaign_segment_limit_exceeded` |
| `segment` modu + sıfır segment | **400** `campaign_segment_required` |
| Bilinmeyen `TargetingMode` | **400** `campaign_targeting_mode_unknown` |
| Mongo erişilemez | **500** (mevcut davranış) — sahte 503 katmanı **eklenmez** (FU08/FU09 kararı) |

### 8.3 Date-only — B2 ile ilişkisi

Form `type="date"`; `PickedDayToUtc` submit'te, `StoredDayToUtc` edit-populate'te (B3). **FU08 B2 değişmez** —
zaten `UtcDateTime.Date` üzerinde karşılaştırıyor. Date-only o kuralı **kolaylaştırır**; AC-B2-4 testi
**kaldırılmaz** çünkü API doğrudan çağrılabilir ve saatli bir instant hâlâ gelebilir.

### 8.4 Projeksiyon kuralı

Segment kodu/adı/subject-type'ı kampanyaya **asla yazılmaz**; okuma anında projekte edilir (FU08
D-PROJECTION). Kampanya yalnız `SegmentId` + `LinkedAt` saklar.

---

## 9. Layout & Shell Contract

| Öğe | Değer |
|---|---|
| `shell` | `tenant` |
| Razor layout | **`Layout = "_LayoutTenantShell";`** — `Index` / `Create` / `Edit` / `Details` / **`Targeting`** |
| View klasörü | `frontend/Diten.Web/Views/CRM/Campaigns/` |
| Golden reference | **Compact** (`DEV-0001`) |
| Nav | Yeni nav girdisi **YOK** — Targeting bir **row action** hedefi |

---

## 10. Backend File Convention

**D-FILES:** `Features/Campaign/` gruplanmış düzeni **korunur** (FU08/FU09'dan miras; F-FILE-DRIFT açık).
FU09'un açtığı `Rules/` + `Services/` istisnaları sürer; FU10 aynı mantıkla `Read/` altına
`ICampaignSegmentCatalog.cs` koyar.

`CampaignCodeSequence` / generator, `Features/Account`'taki emsalin **birebir** karşılığıdır: aynı dosya
adları, aynı sınıf yapısı, aynı `MaxRetries` disiplini.

---

## 11. Frontend File Contract

### 11.1 Golden karar — Compact KALIR (türetme)

| Adım | Sayı |
|---|---|
| FU09 sonrası mevcut | **25** |
| **Kaldırılan** — `OwnerUserId` + 8 reference alanı | **−9** |
| **Eklenen** — `SegmentIds` (çoklu seçim, tek alan) + **`TargetingMode`** | **+2** |
| **Toplam** | **18** |

`18 > 8` → **Compact**. `_CreateEditOffcanvas.cshtml` / `_DetailsQuickView.cshtml` **yasaktır**.
(`ExternalReferences` tekrar eden grup olarak 25'e dâhil değildi; kaldırılması sayıyı değiştirmez ama formu
belirgin kısaltır.)

### 11.2 D-TARGETING-MODE — Segment'in static/dynamic deseninin aynası

R1'in *"manuel UI kaldırılır"* kararı **iptal edildi**. Yerine, Segment'in `SegmentType` toggle'ının aynısı:

```text
Segment  (MOD-0167, mevcut)              Campaign (MOD-0165, FU10)
─────────────────────────────            ─────────────────────────────
SegmentType = static                     TargetingMode = manual
   → criteriaSection GİZLİ                  → segment picker GİZLİ
   → manualMembershipSection GÖRÜNÜR         → manuel targeting kartı GÖRÜNÜR

SegmentType = dynamic                    TargetingMode = segment
   → criteriaSection GÖRÜNÜR                 → segment picker GÖRÜNÜR
   → manualMembershipSection GİZLİ           → manuel targeting kartı GİZLİ
```

`form.js` `applySegmentTypeVisibility` deseni birebir aynalanır (`applyTargetingModeVisibility`) ve aynı
gerekçeyi taşır — Segment'in kendi yorumundan:

> *"Hiding beats disabling here: an author never has to wonder why a section they can see does nothing."*

**Yüzey iki sayfaya yayılır ve bu doğrudur:** *mod* kampanyanın bir niteliğidir → **form**da seçilir; manuel
hedefler kampanya var olduktan sonra yazılan **çocuk kayıtlardır** → **Details**'te yönetilir. Details'teki
manuel targeting kartı `TargetingMode == manual` iken görünür.

> **Bölüm paritesi korunur:** manuel targeting kartı Details'te **`<section>` DEĞİL, `div.card`**tır — FU08
> bunu bilerek böyle bırakmıştı ki verifier'ın bölüm haritasına girmesin. R2 bu yapıyı **değiştirmez**.

### 11.3 Form sırası (D-SCOPE-ABOVE)

```text
1. Summary      CampaignCode(auto) · CampaignName · CampaignType · CampaignStatus · ObjectiveType
                StartDate(date) · EndDate(date) · Description
2. Scope        ScopeType → country | legal-entity | business-unit(country-first cascade)     [FU09]
3. Cycle period scope'a göre FİLTRELENMİŞ picker + pencere gösterimi                          [FU08+FU09]
4. Targeting    TargetingMode (segment | manual)
                └─ segment modunda: çoklu segment seçici (ACTIVE, ≥1)
                └─ manual modunda:  "hedefler kampanya kaydedildikten sonra Details'ten yönetilir" notu
5. Consent      DefaultConsentChannel · DefaultConsentPurpose
```

Scope, cycle'ın **üstünde** olmalıdır çünkü FU09'un picker'ı scope'a bağlıdır. Bu **yalnız HTML sırasıdır** —
FU09'un mantığı, olay bağlantıları ve sunucu kuralı değişmez.

`Details.cshtml` **aynı beş bölümü aynı sırada** göstermelidir (verifier bölüm-haritası kontrolü, AC-V-1).

### 11.4 Çoklu-segment picker

- Kaynak: `/CRM/Campaigns/api/segments?segmentStatus=active` (proxy; **yeni backend endpoint'i yok**).
- Çoklu select2; **karışık subject-type serbest** — ama her seçilenin subject-type'ı **rozetle** gösterilir,
  çünkü karışık küme bilinçli bir tercihtir ve yazar onu görmelidir.
- **Mevcut seçim round-trip'te korunur:** artık `active` olmayan (arşiv/superseded) bağlı segment listeye
  **enjekte edilir** ve rozetlenir.
  > FU08 AC-UI-3 ve FU09'un aynı tuzağı: seçenek listede yoksa form onu **sessizce siler**.
- Select2 seçenekler doldurulduktan **sonra** initialize edilir; `change` yeniden yayımlanır.

### 11.5 Targeting detail sayfası (D-TARGETING-PAGE)

`GET /CRM/Campaigns/{campaignId:guid}/Targeting` — **salt okunur**, `_LayoutTenantShell`.

| Bölüm | İçerik |
|---|---|
| Başlık | Kod + ad + **TargetingMode rozeti** + scope + cycle özeti |
| **Aktif modun hedefleri** | `segment` → hedeflenen segmentler (kod/ad/subject-type/statü + superseded rozeti, segment detayına link)<br>`manual` → manuel `CampaignTarget` satırları (salt okunur özet; yazma Details'te) |
| **Pasif modun verisi** | Varsa **"dormant"** olarak, sayı + tek satır açıklama ile gösterilir; **düzenlenemez** (§12.2 gereği: veri var, kullanılmıyor — gizlemek onu unutturur) |
| **Ne promote edilecek** | **PLACEHOLDER** — *"Segment başına içerik/ürün planı henüz açılmadı."* Boş tablo veya sahte veri **gösterilmez** |
| Aksiyon | **YOK** |

Row action: DataTable ⋮ → **Targeting**, `.js-campaign-targeting` ile event delegation
(`.js-quick-view` deseninin kardeşi).

---

## 12. Validation Rules

### 12.1 D-RECHECK deseninin DÖRDÜNCÜ ve BEŞİNCİ uygulaması

| Kontrol | Ne zaman | Neden |
|---|---|---|
| bind-active (FU08) | binding **değişince** | Kapanan dönem bağını korur |
| B2 containment (FU08) | bağlı olan **her** yazımda | Bind sonrası tarih kaydırmayı engeller |
| scope-uygulanabilirlik (FU09) | bağlı olan **her** yazımda | Scope editable |
| **segment VAR + ACTIVE (FU10)** | segment kümesi **değişince** (yalnız eklenenler) | Sonradan arşivlenen segment kampanyayı **rehin almamalı** |
| **`segment` modu ≥1 segment (FU10)** | mod `segment` **iken her** yazımda | Aksi hâlde mod seçilip küme boşaltılarak kural atlatılırdı |
| segment tekrarı / tavan (FU10) | **her** yazımda | Yapısal invaryant, I/O gerektirmez |

**Senaryo tablosu:**

| # | Senaryo | Sonuç |
|---|---|---|
| 1 | `manual` mod, segment yok | **OK** |
| 2 | `segment` mod, iki ACTIVE segment | **OK** |
| 3 | `segment` mod, sıfır segment | **400** `campaign_segment_required` |
| 4 | `segment` mod, `draft`/`archived` segment eklenir | **400** `campaign_segment_not_active` |
| 5 | Olmayan / başka tenant'ın segmenti | **400** `campaign_segment_not_found` |
| 6 | Aynı segment iki kez | **400** `campaign_segment_duplicate` |
| 7 | 51 segment | **400** `campaign_segment_limit_exceeded` |
| 8 | Karışık subject-type | **OK** |
| 9 | Bağlı segment sonradan arşivlenir; yazar **yalnız açıklamayı** değiştirir | **OK** — küme değişmedi |
| 10 | Aynı durumda yazar **başka bir segment ekler** | Yalnız **eklenen** doğrulanır → OK; arşivlenmiş olan **korunur** |
| 11 | Arşivlenmiş segment **kaldırılır** | **OK** — kaldırma her zaman serbest |
| 12 | `manual` → `segment` mod değişimi, ≥1 segment verilmiş | **OK**; manuel hedefler **silinmez** (dormant) |
| 13 | `manual` → `segment`, segment verilmemiş | **400** `campaign_segment_required` — mod değişimi **yarım kalmaz** |
| 14 | `segment` → `manual` mod değişimi | **OK**; `TargetedSegments` **silinmez** (dormant) |
| 15 | Bilinmeyen mod (`"auto"`) | **400** `campaign_targeting_mode_unknown` |
| 16 | Pre-FU10 kayıt, mod alanı yok, yalnız açıklama düzenleniyor | **OK** — `manual` türetilir (§4.4) |

> **9/10. satırlar** FU09 `D-SCOPE-LEGACY-REF`'in ve FU08 close-dayanıklılığının aynı mantığıdır.
> **13. satır**, mod değişiminin **atomik** olmasını sağlar: yeni mod geçerli değilse yazım hiç olmaz.

### 12.2 ⚠️ D-TARGETING-MODE-WRITES — **karar bekliyor** (bu revizyonun tek açık noktası)

Kullanıcı kararı: *"Yalnız AKTİF modun verisi validate edilir/kullanılır. Pasif modun verisi dormant kalır
(mode-switch'te temizlenmez — veri kaybı yok)."* Bu, **mevcut** veri için nettir. Belirsiz olan şey
**YENİ yazımlardır**: mod `segment` iken biri `POST /api/crm/campaigns/{id}/targets` çağırırsa ne olur?

**Aynaladığımız desen bu noktada FARKLI davranıyor (B9):** Segment, pasif modun verisini **reddediyor** —
`dynamic` bir segmente manuel satır eklemek 400 `TypeForbidsManualMembership` veriyor ve gerekçesi
kodda yazılı: *"so the label never lies about where a member came from."*

| # | Seçenek | Değerlendirme |
|---|---|---|
| **(b)** | **Pasif moda YENİ yazım 400 ile reddedilir; MEVCUT veri dormant kalır** | ✅ **ÖNERİLEN.** "Yalnız aktif modun verisi kullanılır" cümlesini bir **kurala** çevirir. Aynaladığımız desenin yazma semantiği birebir korunur; ayrışma yalnız **zaten var olan** veride kalır (Segment'te bu durum oluşamaz, çünkü tipi create'te seçilir). Kullanıcının "veri kaybı yok" şartı **tam olarak** sağlanır: hiçbir şey silinmez, sadece yenisi eklenmez |
| (a) | Pasif moda yazım serbest, yalnız UI gizler | ❌ FU07'nin dersi: *"kontrolü kaldırmak kuralı bir UI geleneğine çevirmez."* API doğrudan çağrılabilir; `segment` modundaki bir kampanya sessizce manuel hedef biriktirebilir ve hiçbir yüzey bunu göstermez |
| (c) | Mod değişiminde pasif veriyi **sil** | ❌ Kullanıcı kararına aykırı (veri kaybı) ve geri alınamaz |

**Öneri: (b).** Seçilirse `CampaignTargetCommandHandlers`'a **tek bir mod kapısı** eklenir (§2.1'deki tek
istisna); snapshot/consent akışının başka hiçbir satırı değişmez. (a) seçilirse §12.1'in 12/14. satırları ve
AC-M-4 yeniden yazılmalı, ayrıca contract limitations'a *"mod yalnız UI'yı yönlendirir"* satırı eklenmelidir.

### 12.3 D-SEGMENT-VERSION = (a) — belirli sürüm pinlenir

Kampanya **belirli bir `SegmentId`'yi**, yani belirli bir **sürümü** pinler. Yeni sürüm çıkması kampanyanın
neye hedeflendiğini **değiştirmez**; `Superseded` durumu picker'da ve targeting sayfasında **rozetle
gösterilir**, taşıma yazarın bilinçli kararıdır (F-SEGMENT-VERSION-MOVE).

Gerekçe: *"CyclePeriod pin deseni"* — FU08 belirli bir dönemi pinler, sonradan kapanırsa bağı korur ve durumu
gösterir. Lineage pinlemek, kampanyanın kime yöneldiğini **kimse dokunmadan** değiştirirdi.

### 12.4 `TargetingMode` vokabüleri

```text
CampaignTargetingModes: "segment" | "manual"      (in-domain, fail-closed)
Normalize(null/boş) → türetme (§4.4), asla sessizce "segment"
```

Bilinmeyen değer **reddedilir** (400), sessizce bir moda düşürülmez — Segment `SegmentTypes` ve FU09
`CampaignScopeTypes` disiplininin aynısı.

### 12.5 D-AUTOCODE

`AccountCodeGenerator` (B2) kodu **POST anında, alan boşsa** üretir.

| # | Seçenek | Değerlendirme |
|---|---|---|
| **(a)** | Boş bırakılırsa sunucu üretir; alan görünür + düzenlenebilir + *"boş bırakırsanız otomatik atanır"* ipucu | ✅ **SEÇİLEN.** Account deseni birebir; terk edilen form numara **yakmaz** |
| (b) | Form açılışında önizleme üret | ❌ Her açılış bir numara yakar; "birebir Account deseni" de değil |

`CampaignCode` VM'de **opsiyonel** olur (`[Required]`, `*` işareti ve `required` attribute'ü kalkar — verifier'ın
*"Required label markers match ViewModel required metadata"* kontrolü bunu gerektirir), edit'te **readonly**.
Format `CMP-{YYYY}-{sıra:000000}`, çakışmada 5 deneme, sessiz fallback yok.

### 12.6 Reason code'lar

FU08'in 3 + FU09'un 10 kodu **korunur**. FU10 ekler: `campaign_targeting_mode_unknown` ·
`campaign_segment_required` · `campaign_segment_not_found` · `campaign_segment_not_active` ·
`campaign_segment_duplicate` · `campaign_segment_limit_exceeded` · `campaign_code_generation_failed`
(+ D-TARGETING-MODE-WRITES=(b) ise `campaign_targeting_mode_forbids_manual_target`).

### 12.7 Failure Path to Verify

| Yol | Beklenen |
|---|---|
| Duplicate | Aynı segment iki kez → 400 |
| Missing | Olmayan segment → 400, hiçbir şey yazılmaz |
| Cross-tenant | `null` → 400, varlık **sızdırılmaz** |
| Unauthorized | Mevcut `crm.campaign.*`; yeni anahtar yok |
| Concurrency | Campaign'de token yok (FU04); yeni yüzey açılmaz |
| Half-applied | **İmkânsız** — tüm doğrulamalar yazımdan önce; mod değişimi atomik (senaryo 13) |
| Auto-code çakışması | 5 deneme → **500** `campaign_code_generation_failed`, sessiz fallback yok |

---

## 13. Contract Surface

### 13.1 Bayrak ve vokabüler

```jsonc
{
  "supportsCampaignManagement": true,
  "supportsCampaignTargetManagement": true,        // manual mod — backend VE UI çalışıyor
  "supportsStaticTargetSnapshot": true,            // idem
  "supportsConsentEvaluationIntegration": true,
  "supportsTargetExclusionReason": true,
  "supportsTargetSourceProvenance": true,
  "supportsCyclePeriodBinding": true,              // FU08
  "supportsScopeAwareCycleBinding": true,          // FU09
  "supportsSegmentTargeting": true                 // ← FU10
}
```

**R2'de bu bayrakların hepsi dürüsttür:** `manual` mod hem API hem UI olarak çalışır, `segment` mod eklenir.
R1'deki *"bayrak true ama UI yok"* gerilimi **ortadan kalkmıştır**.

Vokabülere `targetingModes: ["segment", "manual"]` ve limitlere `maxTargetedSegments: 50` eklenir — UI kendi
listesini/limitini uydurmasın.

### 13.2 `limitations` — eklenen satırlar

1. *"FU10: a campaign declares HOW it is targeted — `targetingMode` is either `segment` (targeted segments) or
   `manual` (hand-authored CampaignTarget rows). Only the ACTIVE mode's data is validated and used; the other
   mode's existing data is kept, never cleared by a mode switch"*
2. *"FU10: in `segment` mode a campaign declares WHO it targets through TargetedSegments — segment membership is
   never resolved here, no CampaignTarget row is produced and no consent is evaluated; turning targeted segments
   into an audience is a separate follow-up"*
3. *"FU10: a targeted segment is pinned by SEGMENT VERSION, not by lineage — a newer version does not change what
   an existing campaign targets; the superseded state is surfaced so an author can move it deliberately"*
4. *"FU10: segments are validated when the targeted set CHANGES, so a campaign whose segment was archived later
   stays editable; removing such a segment is always allowed"*
5. *"FU10: campaigns written before this release carry no targeting mode and are read as `manual` — the only way
   targeting existed at the time; nothing is migrated and no stored row is rewritten"*
6. *"FU10: Brand / Product / Subject / Topic / ConceptChainTemplate / EngagementJourney / KnowledgePath /
   KnowledgeContent / OwnerUserId / ExternalReferences are no longer authored or returned. The stored values are
   untouched — there is no migration — and 'what to promote' belongs to a per-segment model that is not opened here"*
7. *"FU10: CampaignCode is generated server-side when left empty (CMP-{YYYY}-{sequence}) and remains
   author-editable on create; it is immutable afterwards"*
8. *(D-TARGETING-MODE-WRITES=(b) seçilirse)* *"FU10: a manual target cannot be added while the campaign is in
   `segment` mode — the mode is a rule, not a UI convention; existing manual rows are preserved and become active
   again if the mode is switched back"*

### 13.3 CyclePeriod ve Segment contract'larına DOKUNULMAZ

`CyclePeriod.supportsCampaignBinding: false` **kalır**. Segment contract'ı **hiç değişmez** — MOD-0167 bu
kampanyanın kendisine referans verdiğini bilmez ve bilmemelidir.

---

## 14. Authorization Convention

| Konu | Karar |
|---|---|
| Yeni permission anahtarı | **YOK** |
| Yazma yolu | Mevcut `crm.campaign.*`; manuel hedefler mevcut `crm.campaign.target.*` |
| Segment okuma (picker + projeksiyon) | Kampanya **read** kapısı; alt bağımlılık kendi guard'ını uygular. Yetki yoksa picker **boş** → hedefleme yapılamaz (fail-closed) |
| Targeting sayfası | Kampanya **read** kapısı; salt okunur |
| RBAC seed / grant | **YASAK** |

---

## 15. Gateway / API Routing Decision

| Soru | Cevap |
|---|---|
| Yeni Ocelot route'u | **HAYIR** |
| Yeni backend endpoint | **HAYIR** (picker mevcut segment listesini kullanır) |
| Frontend | **2 ekleme:** salt-okunur `GET /CRM/Campaigns/api/segments` + `GET /CRM/Campaigns/{id}/Targeting` |
| Mevcut target/snapshot proxy action'ları | **KORUNUR** — `manual` mod onları kullanır (R1'deki F-PROXY-CLEANUP **iptal**) |

---

## 16. Acceptance Criteria

### Targeting mode

| # | Kriter |
|---|---|
| **AC-M-1** | `TargetingMode` zorunlu; bilinmeyen değer → 400, sessizce bir moda **düşürülmez** |
| **AC-M-2** | Pre-FU10 satırlar okuma anında **`manual`** okunur; türetme **hiçbir şey yazmaz**; backfill **yoktur** |
| **AC-M-3** | Mod değişimi pasif modun verisini **SİLMEZ** — `manual`→`segment` sonrası manuel hedefler, `segment`→`manual` sonrası `TargetedSegments` **durur** |
| **AC-M-4** | Pasif moda **yeni** yazım davranışı D-TARGETING-MODE-WRITES kararına göre test edilir ((b) → 400) |
| **AC-M-5** | `segment` modunda **≥1** segment; sıfır → 400 `campaign_segment_required` |
| **AC-M-6** | `manual` modunda `TargetedSegments` **doğrulanmaz** (dormant) ve yazımı engellemez |
| **AC-M-7** | Mod değişimi **atomik**: yeni mod geçersizse yazım hiç olmaz (senaryo 13) |

### Segment hedefleme

| # | Kriter |
|---|---|
| **AC-S-1** | `TargetedSegments` yalnız `SegmentId` + `LinkedAt` saklar; kod/ad/subject-type **kopyalanmaz** |
| **AC-S-2** | §12.1'in **16 senaryosunun tamamı** doğrulanır |
| **AC-S-3** | Karışık subject-type kabul edilir |
| **AC-S-4** | Aynı segment iki kez → 400; sessizce tekilleştirilmez |
| **AC-S-5** | Tavan (50) contract'ta **yayımlanır** ve aşımı 400 |
| **AC-S-6** | Cross-tenant segment → 400, varlık sızdırılmaz |
| **AC-S-7** | Sonradan arşivlenen segment: küme değişmedikçe kampanya **düzenlenebilir kalır** |
| **AC-S-8** | Yeni segment eklenirse **yalnız eklenen** doğrulanır |
| **AC-S-9** | Bu FU **hiçbir `CampaignTarget` satırı üretmez** (snapshot dışı write sayacı = 0) |
| **AC-S-10** | Campaign kodu `ISegmentRepository`'yi **hiç** referanslamaz |
| **AC-S-11** | Segment aggregate/handler/UI dosyaları **değişmemiştir** (grep + mtime) |
| **AC-S-12** | Superseded segment bağlı kalır ve **rozetlenir**; yeni sürüm kampanyayı **değiştirmez** |

### Sadeleştirme · auto-code · date-only · sıra

| # | Kriter |
|---|---|
| **AC-R-1** | 9 alan command/DTO/VM/form'da **yoktur**; entity'de **durur** + deprecate yorumu |
| **AC-R-2** | Backfill/migration **yoktur**; mevcut değerler Mongo'da **değişmemiştir** |
| **AC-R-3** | Liste filtreleri/kolonları temizlenmiş, `TargetingMode` kolonu eklenmiştir |
| **AC-R-4** | `ExternalReferences` guard'ı ve index'i **silinmemiştir** |
| **AC-R-5** | Form **18** kullanıcı alanı → **Compact** |
| **AC-C-1** | Boş kod → `CMP-{YYYY}-{000000}`; dolu kod → yazarınki korunur |
| **AC-C-2** | Çakışmada 5 deneme → **500** + açık hata; sessiz fallback yok |
| **AC-C-3** | Form açılışı sıra numarası **yakmaz** |
| **AC-C-4** | Edit'te `CampaignCode` readonly ve sunucuda değiştirilemez |
| **AC-D-1** | Form `type="date"`; submit `PickedDayToUtc`, edit-populate `StoredDayToUtc` |
| **AC-D-2** | Negatif offset'li istemcide seçilen gün = saklanan gün |
| **AC-D-3** | AC-B2-4 (18:00Z aynı gün) **korunur ve geçer** |
| **AC-O-1** | Form sırası Summary → Scope → Cycle → Targeting → Consent |
| **AC-O-2** | `_Form` ↔ `Details` bölüm haritası **paritesi korunur** |

### FU08 + FU09 + FU04 regresyonu

| # | Kriter |
|---|---|
| **AC-P-1** | B2 aynen; iki uç eşit → geçer, bir gün dışarı → 400 |
| **AC-P-2** | bind-active hâlâ **yalnız binding değişince** |
| **AC-P-3** | Kapanan dönem bağını korur |
| **AC-P-4** | D-OPENEND: açık uçlu + bağlı → 400 |
| **AC-P-5** | Scope-uygulanabilirlik aynen; BU kampanya country dönem **görmez** |
| **AC-P-6** | `CyclePeriod`'da kampanya referansı yok; `supportsCampaignBinding` **hâlâ false**; FU06/FU07 dosyaları **değişmemiş** |
| **AC-P-7** | `CampaignTarget` / snapshot / consent **backend'i** — D-TARGETING-MODE-WRITES=(b)'nin eklediği tek mod kapısı dışında **değişmemiş** |

### UI

| # | Kriter |
|---|---|
| **AC-UI-0** | Beş sayfa da `_LayoutTenantShell`; offcanvas/quickview **açılmamış** |
| **AC-UI-1** | Segment picker yalnız proxy'den beslenir; hardcoded liste **yok** |
| **AC-UI-2** | Varsayılan `segmentStatus=active` |
| **AC-UI-3** | Arşivlenmiş/superseded bağlı segment **korunur** + rozetlenir; **sessiz kaldırma yok** |
| **AC-UI-4** | Seçilen her segmentin subject-type'ı görünür |
| **AC-UI-5** | Select2 seçenekler doldurulduktan **sonra** initialize edilir |
| **AC-UI-6** | `_TargetsDataTable` / `_TargetCreateEditOffcanvas` / `_SnapshotPanel` / `_ConsentProvenance` **KORUNMUŞ** ve `manual` modunda çalışır |
| **AC-UI-7** | Targeting sayfası **salt okunur** — hiçbir POST/PUT/DELETE tetiklemez |
| **AC-UI-8** | Targeting sayfası "ne promote" bölümünü **kapalı** olarak yazar; boş tablo/sahte veri göstermez |
| **AC-UI-9** | **Mod toggle çalışır:** `manual` → manuel targeting kartı görünür + segment picker gizli; `segment` → tersi. Gizleme `d-none` iledir (disable değil), Segment `applySegmentTypeVisibility` deseninin aynası |
| **AC-UI-10** | Pasif modun **var olan** verisi targeting sayfasında "dormant" olarak görünür — gizlenip unutturulmaz |
| **AC-UI-11** | Row action ⋮ → Targeting; event delegation |
| **AC-L10N-1** | Yeni anahtarlar **7 dilde**; XML dengeli, parite tam |
| **AC-L10N-2** | Kaldırılan alanların kullanılmayan anahtarları **temizlenir** (parite bozulmadan, grep ile doğrulanarak) |

### Doğrulama

| # | Kriter |
|---|---|
| **AC-V-1** | `verify_datatable_page --area CRM --module Campaigns --reference compact --api-profile proxy` **CRM baseline'ından gerilemez** (FU09: 87/8) |
| **AC-V-2** | `dotnet build` **0 hata** (CrmService + Diten.Web) |
| **AC-V-3** | Kaydedilmiş görünümler **çökmez**; kolon sayısı değişince `applyColOrder` sessizce atlar |
| **AC-V-4** | CAND literal **0** |
| **AC-V-5** | `verify_module_id --check-all` **HARD violations: 0** |
| **AC-V-6** | Test süiti yeşil; FU06/FU07 testleri **değişmez** |

---

## 17. Test Expectations

Yeni dosya: `tests/.../CampaignTargetingModeTests.cs`.

### 17.1 Kapsam matrisi

| Grup | Test |
|---|---|
| **Mod** | Zorunlu · bilinmeyen → 400 · pre-FU10 türetme = `manual` · türetme **yazmaz** · mod değişimi **atomik** |
| **Dormant** | `manual`→`segment`: manuel hedefler **durur** · `segment`→`manual`: `TargetedSegments` **durur** · geri dönüşte veri **yeniden aktif** |
| **Mod kapısı** | D-TARGETING-MODE-WRITES=(b) → `segment` modunda manuel hedef POST'u **400**; (a) seçilirse test tersine yazılır |
| **Segment link** | Yalnız id saklanır · projeksiyon read-time · tekrar → 400 · tavan → 400 · `segment` modunda sıfır → 400 |
| **Validate-on-change** | ACTIVE ekleme OK · draft/archived → 400 · olmayan → 400 · cross-tenant → 400 · **arşivlenmiş bağlı + açıklama düzenleme → OK** · **yeni ekleme → yalnız eklenen doğrulanır** · kaldırma serbest |
| **Karışık tip / boş küme** | account+contact OK · `manual` modda boş küme OK |
| **Sürüm** | Superseded bağlı kalır + rozetlenir; yeni sürüm kampanyayı değiştirmez |
| **Yön** | `ISegmentRepository` referansı **0** · seam'de write metodu yok · `HttpClient` yok |
| **Üretmeme** | Segment modunda `CampaignTarget` write sayacı **0** |
| **Auto-code** | Boş → üretilir · dolu → korunur · çakışma → 5 deneme → 500 · form açılışı numara yakmaz · edit'te değişmez |
| **Date-only** | `PickedDayToUtc`/`StoredDayToUtc` round-trip · negatif offset · **AC-B2-4 korunur** |
| **Deprecate** | 9 alan command/DTO'da yok · entity'de var · yazımda **silinmez** |
| **FU08/FU09 regresyonu** | B2 sınırları · bind-active tetikleyicisi · close-dayanıklılığı · D-OPENEND · scope-uygulanabilirlik · çapraz eksen yok |
| **FU04 regresyonu** | Manuel hedef create/update/archive + snapshot + consent provenance **aynen çalışır** (`manual` modda) |
| **Contract** | Yeni bayrak true · target/snapshot bayrakları **true ve artık dürüst** · vokabüler + limit yayımlanmış · 7–8 yeni kod · 7–8 limitations satırı |

### 17.2 Frontend / manuel

| # | Adım |
|---|---|
| S1 | Fleet FU10 build'iyle yeniden başlatılır (RESX + JS) |
| S2 | İki ACTIVE segment hazırlanır (biri account, biri contact subject-type) |
| S3 | Create: kod boş → kaydedince `CMP-2026-000001` |
| S4 | Form sırası Summary → Scope → Cycle → Targeting → Consent |
| S5 | Scope seçilir → cycle listesi filtrelenir (FU09 regresyonu) |
| S6 | Mod `segment` → segment picker görünür, manuel kart yok; iki segment (karışık tip) seçilip kaydedilir |
| S7 | Mod `manual`'a çevrilir → picker gizlenir, Details'te manuel targeting kartı **görünür**; `TargetedSegments` **silinmemiştir** (Targeting sayfasında dormant görünür) |
| S8 | `manual` modda manuel hedef eklenir + snapshot çalıştırılır → **FU04 davranışı aynen** |
| S9 | Mod `segment`'e geri çevrilir → manuel hedefler **durur** (dormant), picker eski segmentleri **hatırlar** |
| S10 | Segment `/CRM/Segments`'ten **arşivlenir**; kampanya Edit → rozetle **korunur**; açıklama değişip kaydedilir → **200** |
| S11 | Aynı formda üçüncü (aktif) segment eklenir → 200; arşivlenmiş korunur |
| S12 | DataTable ⋮ → **Targeting** → aktif modun hedefleri + pasif modun dormant özeti + "ne promote" **kapalı** |
| S13 | Tarih alanları **gün** seçtirir; seçilen gün = kaydedilen gün |

### 17.3 Bilerek değiştirilecek MEVCUT testler (şeffaflık)

| Test | Değişiklik | Gerekçe |
|---|---|---|
| `CampaignTargetingRuntimeTests` | Fixture komutları kaldırılan 9 alan için sadeleşir; `TargetingMode` eklenir (FU04 senaryoları `manual` moda sabitlenir, davranış iddiaları **değişmez**) | Derleme gereği + mod zorunluluğu |
| `CampaignTargetingRuntimeTests.T35` | Bayrak ad kümesine **9.** bayrak (`SupportsSegmentTargeting`) | Her yeni bayrak **bilinçli beyan** gerektirir (FU08'de sertleştirilen disiplin) |
| `CampaignTargetingRuntimeTests.T36_T37` | Endpoint ad kümesi güncellenir (yeni read endpoint'i eklenirse) | Aynı disiplin |
| `CampaignCycleBindingTests` · `CampaignScopeMirrorTests` | Fixture komut çağrıları sadeleşir + `TargetingMode` eklenir; **davranış iddiaları değişmez** | Derleme gereği |
| FU06/FU07 testleri | **DEĞİŞMEZ** | AC-V-6 |

> **FU08/FU09 dersi:** her iki FU'da da pack'in öngördüğünden **fazla** test değişti (FU09: 2 yerine 4).
> Bu FU 9 alan kaldırıp 2 alan eklediği için fixture'ları **kesinlikle** etkileyecektir; sayı önceden
> verilmiyor, **teslim raporunda tam liste** verilecektir.

---

## 18. Localization

**Eklenen (7 dil):** `TargetingSection` · `TargetingMode` · `TargetingMode_segment` · `TargetingMode_manual` ·
`TargetingModeHelp` · `TargetedSegments` · `TargetedSegmentsHelp` · `SelectSegments` · `SegmentSuperseded` ·
`SegmentArchived` · `SegmentRequired` · `SegmentNotActive` · `SegmentNotFound` · `SegmentDuplicate` ·
`SegmentLimitExceeded` · `SubjectType` · `ManualTargetingHint` · `TargetingTitle` · `TargetingWhatToPromote` ·
`TargetingWhatToPromoteClosed` · `TargetingDormantData` · `NoTargetedSegments` · `CampaignCodeAutoHint`.

**Temizlenen:** kaldırılan alanların artık hiçbir yerde kullanılmayan anahtarları
(`BrandId`, `ProductId`, `TopicId`, `ConceptChainTemplateId`, `EngagementJourneyId`,
`DefaultKnowledgePathId`, `DefaultKnowledgeContentId`, `OwnerUserId`, `ExternalReferences*`).

> **Dikkat:** target/snapshot anahtarları **KORUNUR** (R2: manuel UI yaşıyor). `SubjectId` gibi anahtarlar başka
> bağlamlarda da kullanılıyor olabilir; temizlik **grep ile doğrulanarak** ve **parite bozulmadan** yedi
> dosyada birlikte yapılır (AC-L10N-2).

---

## 19. Ready-for-dev Checklist

| # | Madde | Durum |
|---|---|---|
| 1 | DCP-002 exit 0 + FU gerekçesi + MOD-0155-FU06 teyidi | ✅ §0.1 |
| 2 | Golden reference (Compact, **18** alan, türetme) | ✅ §11.1 |
| 3 | Layout açıkça yazıldı (5 sayfa) | ✅ §9 |
| 4 | Backend dosya konvansiyonu | ✅ §10 |
| 5 | Frontend dosya seti — **hiçbir dosya silinmiyor** | ✅ §5.2, §11.2 |
| 6 | Validation Rules + D-RECHECK 4./5. uygulaması + 16 senaryo | ✅ §12 |
| 7 | Failure Path | ✅ §12.7 |
| 8 | Authorization | ✅ §14 |
| 9 | Gateway kararı | ✅ §15 |
| 10 | Acceptance Criteria | ✅ §16 |
| 11 | Test Expectations + şeffaflık | ✅ §17 |
| 12 | Protected paths | ✅ §2.1 |
| 13 | Migrasyon gerekmediği + veri sonucu ilan edildi | ✅ §4.3, §4.4 |
| 14 | FU08/FU09 kilitleri korunuyor | ✅ §2.2, AC-P-1..7 |
| 15 | **R1'in yetenek gerilemesi İPTAL** — manuel UI mod-kapılı yaşıyor | ✅ §1.4, §11.2 |
| 16 | **D-TARGETING-MODE-WRITES kararı** | ⛔ **BEKLİYOR** — §12.2 |
| 17 | D-SEGMENT-SEAM · D-FILES onayı | ⛔ **BEKLİYOR** — §1.3 |
| 18 | `status: ready-for-dev` + `runtime_code_allowed: true` | ⛔ **BEKLİYOR** |

> **Pack, 16–18 kapanmadan `ready-for-dev` sayılmaz.**

---

## 20. Follow-up Items

| # | İş | Domain | Neden |
|---|---|---|---|
| **F-SEGMENT-RESOLUTION** | `TargetedSegments` → `CampaignTarget` snapshot'ı (üyelik çözümleme + consent) | commercial-suite | **Bu FU'nun varlık nedeni.** FU04 altyapısı R2'de **hem backend hem UI olarak** yerinde |
| **F-WHAT-TO-PROMOTE** | Segment başına içerik/ürün planı (MicroTarget / StrategyTemplate) | commercial-suite | Kaldırılan 8 alanın gerçek yeri; targeting placeholder'ı onunla dolar |
| **F-TARGETING-MODE-HYBRID** | Üçüncü mod (`hybrid`) gerekli mi? Segment'te var, Campaign'de **kasten yok** | commercial-suite | İki mod bugünkü ihtiyacı karşılıyor; üçüncüsü gerçek talep çıkınca |
| **F-DORMANT-CLEANUP** | Pasif mod verisinin **yönetişimli** temizlenmesi (kim, ne zaman, hangi onayla) | commercial-suite | R2 hiçbir şeyi silmiyor; birikmiş dormant veri bir gün ele alınmalı |
| **F-REGISTRY** | Registry'ye MOD-0165-FU06..FU10 satırları | portfolio-delivery | FU06'dan beri açık |
| **F-DEPRECATED-FIELD-REMOVAL** | Deprecate edilen 10 alanın entity'den fiilen kaldırılması + veri kararı | commercial-suite | Halef model yerleştikten **sonra** |
| **F-SEGMENT-READER** | `ICampaignSegmentCatalog`'un MOD-0167 tarafında kanonik `ISegmentReader`'a taşınması | commercial-suite | §2.3 — FU09'un F-SCOPE-SHARED'ı ile aynı sınıf borç |
| **F-SEGMENT-VERSION-MOVE** | Superseded segmenti yeni sürüme **taşıma** aksiyonu | commercial-suite | D-SEGMENT-VERSION durumu gösterir, taşımayı otomatikleştirmez |
| **F-CAMPAIGN-SEGMENT-REPORT** | *"Bu segmenti hangi kampanyalar hedefliyor?"* okuma yüzeyi | commercial-suite | Index (§4.7) hazır olur |
| **F-SCOPE-SHARED** · **F-COUNTRY-SOT** · **F-SCOPE-RBAC** · **F-TARGET-SCOPE** · **F-MDM-PERM** · **F-FILE-DRIFT** · **F-CYCLE-CONTRACT-NOTE** | FU08/FU09'dan devralındı | — | Değişmedi |

> **R1'den iptal edilen follow-up:** ~~F-PROXY-CLEANUP~~ — target/snapshot proxy action'ları artık tüketicisiz
> değildir (`manual` mod onları kullanır).

---

## Ek A — Bu pack'in reddettiği sekiz kolay yol

| # | Kolay yol | Neden reddedildi |
|---|---|---|
| A1 | `ISegmentRepository`'yi doğrudan enjekte et | Yazma metotları taşıyor; sınır yapısal olmaktan çıkar (FU07 emsali) |
| A2 | Segment kodunu/adını kampanyaya **kopyala** | Kopya bayatlar (FU08 D-PROJECTION) |
| A3 | Kaldırılan alanları DTO'da salt-okunur bırak | Kullanıcı kararı *"form/DTO/VM'den kaldırılır"*; sonucu §4.3'te ilan edildi |
| A4 | `VersionLineageId` pinle | Kampanyanın kime yöneldiği kimse dokunmadan **sessizce** değişir |
| A5 | Segmenti **her** yazımda doğrula | Bir segment arşivlendiği gün ona bağlı her kampanya kilitlenir (FU09 D-SCOPE-LEGACY-REF) |
| A6 | Form açılışında kod önizlemesi | Terk edilen her form bir numara yakar |
| A7 | Manuel targeting UI'sını **sil** (R1'in kararı) | **R2'de iptal:** çalışan bir yüzeyi, halefi hazır olmadan kapatmak kullanıcıya bedel yükler. Mod-kapısı hem eskiyi korur hem yeniyi ekler |
| A8 | Mod değişiminde pasif veriyi **temizle** | Geri alınamaz veri kaybı; kullanıcı kararına aykırı. Dormant bırakmak, moda geri dönüldüğünde veriyi **yeniden anlamlı** kılar |

## Ek B — İlan edilmiş boşluklar (sessiz değil)

| # | Boşluk | Nerede ilan edildi |
|---|---|---|
| B1 | Segmentlerden **kitle çözümlenmiyor**; `CampaignTarget` üretilmiyor | §2.4 · limitations #2 · AC-S-9 · F-SEGMENT-RESOLUTION |
| B2 | "Ne promote" kampanyada **yok** | §1 · limitations #6 · F-WHAT-TO-PROMOTE |
| B3 | Deprecate edilen 10 alan API'den **okunamaz** (veri durur) | §4.3 · limitations #6 · F-DEPRECATED-FIELD-REMOVAL |
| B4 | Superseded segment **otomatik taşınmaz** | §12.3 · limitations #3 · F-SEGMENT-VERSION-MOVE |
| B5 | `ICampaignSegmentCatalog` tüketici tarafında | §2.3 · F-SEGMENT-READER |
| B6 | **Pasif modun verisi hiç temizlenmiyor** — zamanla birikir | §12.2 · limitations #1 · F-DORMANT-CLEANUP |
| B7 | `hybrid` (iki modun birlikte) **yok** — Segment'te var | §1.3 · F-TARGETING-MODE-HYBRID |

---

**Otorite sırası:** Blueprint Excel > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` >
`.antigravity/rules/`.
