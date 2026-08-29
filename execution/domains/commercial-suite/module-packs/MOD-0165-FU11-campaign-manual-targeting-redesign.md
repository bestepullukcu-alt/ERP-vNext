---
id: MOD-0165-FU11
name: Campaign Manual-Targeting Redesign
parent: MOD-0165
parent_name: Campaign Management
siblings: MOD-0165-FU04 (campaign + target runtime) · MOD-0165-FU05 (admin UI) · MOD-0165-FU08 (cycle binding) · MOD-0165-FU09 (scope mirror) · MOD-0165-FU10 (targeting mode + segments — bu FU onun MANUEL modunu iyileştirir)
domain: commercial-suite
service: Diten.CrmService
frontend: frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: review
runtime_code_allowed: true
flip_approved_by: "control-tower (2026-08-28) — draft verified: DCP-002 exit 0, supportsCampaignBinding stays false, golden_reference compact (correct). Author caught 4 real conflicts + 2 traps; all recommendations ENDORSED: D-PRIORITY-MAP=(a) 1→high/2→medium/≥3→low (field is 'smaller wins', my 1→low was wrong; no consumer sorts by it); D-SELECTION-REASON=server-generates 'Manually selected by {actor} on {date}' (removing zeroes FU04's invariant); D-PRIORITY-FIELD=new PriorityLevel enum + deprecate Priority(int) (same-BSON int→enum crashes deserialize — runtime error, forced); D-SNAPSHOT-HIDE=accept (button hidden all modes now, endpoint + supportsStaticTargetSnapshot:true stay, F-SEGMENT-RESOLUTION gives it a home); snapshot-untouched lock NARROWED to behavior-untouched (Priority type + 3 read lines may change); D-STATUS-LIST=remove 'excluded' (needs ExclusionReason which is removed → unfixable 400; excluded is a consent result not an author choice); D-TARGET-TYPES; D-DATATABLE-SLIM=hide-not-delete (26→8 visible, 18 in row-detail, keep 6 consent-provenance cols); D-FILES. Picker reuses Segments api/accounts+api/contacts passthroughs (nothing new). CONCURRENCY: run the build AFTER the other active Campaign session settles (build-race hit twice)."
runtime_code_scope: "YETKİ YOK (draft). Onaylanırsa kapsam: CampaignTarget'a PriorityLevel enum alanı + vokabüler + class-map + Priority(int) deprecate işareti, manuel hedef handler default'ları (TargetSource / ReasonCodes / SelectionReason / EffectiveFrom), komut+request opsiyonelleştirme, contract vokabüler + limitations, Campaigns proxy'sine account/contact passthrough, targets DataTable slim yeniden düzeni, target canvas yeniden düzeni (picker + Priority dropdown + kaldırmalar), snapshot butonunun gizlenmesi, 7 dil RESX, boundary testleri. YASAK: CampaignTarget aggregate'inin başka alanının değişmesi, snapshot/consent MANTIĞININ değişmesi, segment targeting (FU10) yüzeyine dokunma, Account/Contact/Segment aggregate'lerine yazma, CyclePeriod her şeyi, supportsCampaignBinding'in çevrilmesi, veri migrasyonu/backfill, Mongo hand-edit, ocelot.json yazımı, registry yazımı, RBAC seed/grant."
owner: module-pack-author
branch: feature/crm/mod-0165-fu11-campaign-manual-targeting-redesign
started: 2026-08-28
target: TBD (kullanıcı onayı sonrası)
form_field_count: 18
form_field_count_note: "Modülün Compact FORMU (Create/Edit) FU10'dan devralınır ve DEĞİŞMEZ — 18 alan. Bu FU yalnız Details üzerindeki İKİNCİL targets tablosunu ve onun offcanvas'ını yeniden düzenler (§11.1)."
predecessor: MOD-0165-FU10 (SHIPPED — 36 test, verifier 87/8)
dependencies:
  - MOD-0165-FU10 (ZORUNLU ÖNCÜL — mod toggle + manuel UI'nın korunması; KORUNUR)
  - MOD-0165-FU04 (CampaignTarget + snapshot + consent provenance — genişletilen/ayarlanan)
  - MOD-0149 Account (picker kaynağı — SALT OKUNUR, `crm.account.read`)
  - MOD-0150 Contact (picker kaynağı — SALT OKUNUR, `crm.contact.read`)
  - MOD-0167-FU02 Segment (Segments'in account/contact picker deseninin KAYNAĞI — DOKUNULMAZ)
  - MOD-0164 (consent provenance — DOKUNULMAZ)
  - MOD-0018 (RBAC — yalnız tüketim; yeni anahtar YOK)
  - DEV-0000 (Golden Reference Slim — ikincil tablo için stil kaynağı) · DEV-0001 (Compact — modülün kendisi)
---

# MOD-0165-FU11 — Campaign Manual-Targeting Redesign

> **TESLİM EDİLDİ (2026-08-28) — `status: review`.** Aşağıdaki §0 teslim kaydı geçerlidir.
>
> **Ne yapar:** FU10'un **koruduğu** manuel targeting yüzeyini kullanılabilir hâle getirir. Bugün bir hedef eklemek
> için kullanıcıdan **GUID yazması** ve altı ayrı provenance alanını doldurması isteniyor; bu FU onu tek bir
> **account/contact seçicisine** ve bir **öncelik açılır listesine** indirger. Kaldırılan alanları sunucu **bildiği
> gerçeklerden** doldurur.
>
> **Ne yapmaz:** segment targeting'e (FU10), cycle binding'e (FU08), scope'a (FU09) dokunmaz. `CampaignTarget`
> aggregate'i bir alan dışında değişmez; snapshot ve consent **mantığı** değişmez.

---

## 0. Delivery Record (2026-08-28)

> **RUNTIME AUTHORIZATION.** Control-tower pack'i `ready-for-dev` + `runtime_code_allowed: true` yaptı ve **beş açık
> kararın hepsini yazarın önerisi yönünde** kapattı: D-PRIORITY-MAP=(a) · D-SELECTION-REASON=sunucu üretir ·
> D-PRIORITY-FIELD=yeni alan + deprecate · D-SNAPSHOT-HIDE=kabul · snapshot kilidi **davranış-dokunulmaz**a daraltıldı ·
> D-STATUS-LIST=`excluded` çıkar. Uygulama pack'e harfiyen uyularak yapıldı; aşağıdaki sapmalar dışında hiçbir karar
> değişmedi.

**Teslim edilen yüzeyler.**
Backend: `Campaign.cs` (+`CampaignTarget.PriorityLevel` +`DerivedPriorityLevel()` +`CampaignTargetPriorityLevels`
+`CampaignTargetStatuses.Authorable`/`IsAuthorable()` +3 reason code, `Priority`(int) **deprecate işaretli**) ·
`CampaignValidation.cs` (+`ValidatePriorityLevel` +`ValidateAuthorableTargetStatus`; `ValidatePriority` **korundu**) ·
`CampaignTargetCommandHandlers.cs` (+`ResolveManualDefaults` +`BuildSelectionReason`, `Validate` imzası +2 parametre) ·
komut/request/DTO/mapper (+`PriorityLevel`, üç alan opsiyonelleşti) · `CampaignTargetSnapshotHandler.cs`
(**4 satır**, hiçbiri davranış satırı değil) · `CampaignContract.cs` (+2 vokabüler, +6 limitations) ·
`Persistence/DependencyInjection.cs` (**açıklama yorumu**; §S2) · API controller mapping.
Frontend: proxy (+`api/accounts` +`api/contacts` salt-okunur passthrough) · `_TargetCreateEditOffcanvas.cshtml`
**yeniden yazıldı** (13 alan → 5) · `_TargetsDataTable.cshtml` (Priority başlığı → PriorityLevel) ·
`details.js` (slim kolon düzeni + Select2 ajax picker + sadeleşmiş payload + snapshot satırı bant) ·
`Details.cshtml` (snapshot butonu kaldırıldı, yerine **neden** yazan yorum) · `_IndexL10n.cshtml` ·
7 dil RESX (**+10 anahtar, −2 anahtar**, 182→190 ×7, parite doğrulandı).
Tests: `CampaignManualTargetingTests.cs` (**YENİ**, 29 senaryo / 40 test).

**KORUNDU:** `_SnapshotPanel.cshtml` · `_ConsentProvenance.cshtml` · FU10 segment targeting yüzeyi ve mod toggle'ı ·
`CyclePeriod`/`ConsentPreference`/`Segmentation` dizinlerinin **hiçbir dosyası** (mtime kontrolü: 0 dosya).
`CyclePeriodFeatureFlags.SupportsCampaignBinding: false` **değişmedi**.

**Pack'ten sapmalar:**

| # | Sapma | Gerekçe |
|---|---|---|
| **S1** | Canvas'ta **`Notes` alanı** korundu/eklendi (pack §11.2 "kalır/eklenir" diyordu, kesinleşti: **var**) | Beş alanın beşincisi. Kaldırılan sekiz alanın hiçbiri yazarın *söylemek istediği* şey değildi; `Notes` tek serbest metin olarak kaldı, aksi hâlde ekranda yazarın kendi cümlesini kuracağı hiçbir yer kalmıyordu |
| **S2** | `DependencyInjection.cs`'e **class-map kaydı EKLENMEDİ** — yerine bir açıklama yorumu kondu | `PriorityLevel` bir `string?`; auto-map onu zaten doğru yazıp okuyor. Kayıt eklemek işlevsiz olurdu. Yorum, kodun **neden çalıştığını** anlatıyor: alan yeniden adlandırıldığı için eski `Int32` elemanına hiç dokunulmuyor |
| **S3** | `UpdateCampaignTargetHandler`'da `target.Priority = request.Priority ?? target.Priority` | Pack bunu ayrıca kilitlememişti ama düz atama **veri kaybı** olurdu: FU11 ekranı int göndermiyor, dolayısıyla ilk düzenlemede her FU11-öncesi satırın tamsayısı silinirdi — "alan KORUNUR, migrasyon YOK" kararının tam tersi |
| **S4** | `_SnapshotPanel.cshtml` partial'ı **render edilmeye devam ediyor** (tetikleyicisi yok) | Kilit "buton gizlenir" diyordu. Partial'ı da kaldırmak F-SEGMENT-RESOLUTION'ın geri getirmesi gereken yüzeyi silmek olurdu; erişilemeyen bir offcanvas zararsız, silinmiş bir dosya değil |

**§17.3'ün istediği TAM liste — mevcut testlerdeki değişiklikler:**

| Test | Değişiklik | Davranış iddiası değişti mi? |
|---|---|---|
| `CampaignTargetingRuntimeTests.TargetCmd` helper | +`priorityLevel` parametresi; çağrı **konumsaldan adlıya** çevrildi | **Hayır** — komut 3 parametre opsiyonelleşince konumsal çağrı argümanları yanlış parametrelere hizalıyordu |
| `T11_Create_Manual_Target_Valid_Returns_201` | Boş gerekçe artık 400 değil **201**; assertion **invaryantı** ölçüyor: dönen satırın `SelectionReason`'ı **boş değil** ve `campaign_target_selection_reason_generated` taşıyor | **Evet, bilinçli** — invaryant duruyor, *zorlandığı yer* değişti (D-SELECTION-REASON). Assertion eskisinden **daha güçlü**: eskiden sadece reddi ölçüyordu, şimdi sonucun doğruluğunu ölçüyor |
| `T14_Unknown_TargetSource_And_Missing_Exclusion_Reason_Return_400` | Son satır `201` → **`400`**: `excluded` artık gerekçeyle **de** yazılamaz | **Evet, bilinçli** — D-STATUS-LIST. FU04 kuralı ("sessizce düşürme yasak") zayıflamadı, **bir adım öne** alındı; snapshot yolu `excluded` yazmaya devam ediyor (yeni T22 bunu ölçüyor) |
| FU06 / FU07 / FU08 / FU09 / FU10 testleri | **DEĞİŞMEDİ** | — |

**Doğrulama (ham çıktılar):**

| Kontrol | Sonuç |
|---|---|
| `verify_datatable_page --area CRM --module Campaigns --reference compact --api-profile proxy` | **87 PASS / 8 FAIL** — 95 kontrolün ad+sonuç diff'i CRM kardeşi Segments ile **yalnız modül adının geçtiği 2 satırda** farklı; **yeni FAIL yok** |
| `verify_module_id --check-all` | **HARD violations: 0** |
| `verify_module_id --check-id MOD-0165-FU11` | **exit 0** |
| `dotnet build` (Api + Diten.Web) | **0 hata** (önceden var olan uyarılar korundu) |
| Test süiti (**3 koşu**) | **1390 başarılı / 0 başarısız / 5 skip** — üç koşuda da aynı |
| FU11 testleri | **40/40** |
| `node --check` (details.js · form.js · index.js) | **temiz** |
| RESX parite | 190 ×7, `en`'e göre anahtar kümesi diff'i **6 dilde de 0** |
| CAND literal | **0** |
| Snapshot davranış satırları | **0 değişiklik** — eklenen 4 satırın hepsi `PriorityLevel`; `existing.Priority = item.Priority ?? existing.Priority` yerinde |
| DOKUNULMAZ dizinler (CyclePeriod / ConsentPreference / Segmentation) | FU11 penceresinde **0 dosya** değişti |

**Eşzamanlılık notu:** başka bir oturum aynı anda Campaign dosyalarına dokundu (19:09–19:19) ve ardından
CycleCapacity'ye geçti. FU11'in dayandığı olgular (B1–B7) **uygulamadan önce yeniden doğrulandı** ve hiçbiri
etkilenmemişti. O oturum `api/next-code` endpoint'ini de **"MOD-0165 FU11"** diye etiketledi — bu FU'nun kimliğiyle
**etiket çakışması**; kod yorumunda kaldı, registry'ye girmedi, düzeltilmesi o oturumun işi.

**Açık kalan:** F-SEGMENT-RESOLUTION (snapshot butonunun gerçek yeri) · F-PRIORITY-INT-REMOVAL ·
F-TARGET-TYPE-PICKERS · F-PRIORITY-ORDERING · F-REGISTRY + FU08/FU09/FU10'dan devralınanlar (§20).
**Authenticated smoke (§17.2 S1–S10) kullanıcı tarafından** çalıştırılır; fleet'in FU11 build'i ile yeniden
başlatılmasını gerektirir.

---

## 0.0 Kimlik Geçidi ve Ön Bulgular

### 0.1 DCP-002 — PASS (2026-08-28)

```text
$ py .antigravity/scripts/verify_module_id.py . --check-id MOD-0165-FU11 --name "Campaign Manual Targeting Redesign" --parent MOD-0165
OK  MOD-0165-FU11: proven against Blueprint/registry.
REAL_EXIT=0
```

`grep -rn "MOD-0165-FU11" execution/` → **0 sonuç**; FU11 boştu. Parent `MOD-0165` altında FU01–FU10 kullanımda,
ilk çakışmayan id **FU11**.

> **Geçidin kapsamı (FU08'de kanıtlandı):** geçit **kimliği** doğrular, FU açıklayıcı **adını doğrulamaz**.

**Registry satırı bu pack tarafından EKLENMEZ** — §20 / F-REGISTRY.

### 0.2 Kod okumasından çıkan bulgular — dördü kilitli kararlarla ÇELİŞİYOR

| # | Bulgu | Sonuç |
|---|---|---|
| **B1** | `CampaignTarget.Priority` (int?) **snapshot yolunda da kullanılıyor**: `CampaignSnapshotTargetItem.Priority` (`CampaignDtos.cs:190`) + `CampaignTargetSnapshotHandler.cs:182, 213, 310` | *"snapshot backend DOKUNULMAZ"* kilidi **daraltılmalı**: bir aggregate alanının tipi, o alanı yazan HER yolu değiştirmeden değiştirilemez → §12.2 |
| **B2** | `Priority` alanının kendi belgesi: *"Optional deterministic ordering weight for consumers. **Smaller wins.**"* Kullanıcının önerdiği eşleme (1→low, 2→medium, 3→high) bu anlamı **TERS ÇEVİRİR** | `Priority=1` taşıyan mevcut satırlar (bugünün "en önemli"si) ekranda **"low"** görünürdü → **D-PRIORITY-MAP karar bekliyor** (§12.3) |
| **B3** | **`int?` → enum aynı BSON alan adında yapılırsa mevcut satırlar DESERIALIZE OLMAZ** — Mongo `Int32`'yi `string`'e çeviremez ve okuma patlar. Bu, bir tasarım tercihi değil, çalışma zamanı hatasıdır | Alan **yeniden adlandırılmalı** (`PriorityLevel`), `Priority` deprecate edilmeli → §12.4. Kullanıcının *"class-map"* ipucunun ima ettiği özel serializer alternatifi §12.4'te reddedildi |
| **B4** | `SelectionReason` **zorunlu konumsal parametre** ve `ValidateSelectionReason` boşu reddediyor: *"a campaign target may never be selected without a stated reason"*. FU04 bunu açık bir invaryant olarak ilan etmişti | *"selectionReason KALDIR + null-kabul"* kilidi o invaryantı **sıfırlar**. Alternatif: sunucu **üretir** → **D-SELECTION-REASON karar bekliyor** (§12.5) |
| **B5** | `ValidateExclusion`: `TargetStatus=excluded` iken `ExclusionReason` **zorunlu** | `exclusionReason` canvas'tan kaldırılırsa yazar `excluded` statüsünü seçip **düzeltemeyeceği bir 400** alır → §12.6 |
| **B6** | **FU10 R2 zaten** manuel targeting kartını (snapshot butonu dâhil) yalnız `manual` modda render ediyor | *"snapshot butonu MANUEL modda gizlenir"* ⇒ buton **hiçbir modda görünmez**. Bu, FU10 R1'de iptal edilen *"yetenek gerilemesi"*nin aynısıdır → §12.7'de açıkça ilan edilir |
| **B7** | Segments modülünde **tam olarak istenen desen zaten var**: `SegmentsController` `api/accounts` + `api/contacts` passthrough'ları (`crm.account.read` / `crm.contact.read`) ve `form.js`'te `SelectAccount`/`SelectContact` etiketli Select2 subject picker | Aynalanacak; **yeni backend endpoint'i ve yeni Gateway route'u gerekmez** (§15) |
| **B8** | **Hiçbir tüketici `CampaignTarget.Priority`'ye göre sıralamıyor** — repodaki tüm `OrderBy(Priority)` kullanımları başka aggregate'lere ait (ConsentPreference, Territory, VFP, ConceptGraph) | Anlam değişikliği (sıra ağırlığı → 3 bant) **hiçbir davranışı bozmaz**; risk yalnız mevcut satırların **gösterim dürüstlüğü**dür |
| **B9** | Targets tablosu bugün **26 kolon** taşıyor; 6'sı consent provenance (FU04'ün denetim yüzeyi) | "Slim" kolonları **silmek** değil, **gizlemek** olmalı → §11.3 |

---

## 1. Module Summary

FU11, **yalnız manuel modun yazma yüzeyini** değiştirir. Bugün ile yarın:

| | Bugün (FU10 sonrası) | FU11 sonrası |
|---|---|---|
| Hedef seçimi | `targetId` alanına **GUID yazılır** + `targetDisplayName` elle girilir | **Tek picker**: account veya contact seç; id + görünen ad picker'dan gelir |
| Öncelik | Serbest tamsayı (*"smaller wins"*) | **low / medium / high** açılır liste |
| Canvas'ta doldurulan alan | 13 | **5** (tip · hedef · statü · öncelik · not) |
| Kaldırılanların değeri | Yazar yazar | **Sunucu bildiği gerçeklerden üretir** |
| Targets tablosu | 26 kolon, yatay kaydırma | **8 görünür kolon**, kalanı satır-içi detayda (silinmez) |

### 1.1 Ne DEĞİLDİR

| Kavram | Sahibi | Bu FU ile ilişkisi |
|---|---|---|
| **Manuel `CampaignTarget` yazma yüzeyi** (bu FU) | MOD-0165-FU04 | **BU FU** — sadeleştirilir |
| `CampaignTarget` aggregate'i | MOD-0165-FU04 | **KORUNUR** — tek yeni alan (`PriorityLevel`), tek deprecate (`Priority`) |
| Snapshot + consent provenance | MOD-0165-FU04 · MOD-0164 | **MANTIĞI DEĞİŞMEZ.** Yalnız `Priority` tipinin akışı zorunlu olarak dokunur (§12.2) |
| Segment targeting + mod toggle | MOD-0165-FU10 | **DOKUNULMAZ** — bu FU yalnız `manual` modun içini düzenler |
| `Account` / `Contact` | MOD-0149 / MOD-0150 | **SALT OKUNUR** picker kaynağı; hiçbir master yazılmaz veya kopyalanmaz |
| Segmentten kitle çözümleme | — | **KAPSAM DIŞI** — F-SEGMENT-RESOLUTION |

> **Tek cümlelik sınır:** *Bu FU, bir hedefin **nasıl seçildiğini** değiştirir; **ne olduğunu** değiştirmez.*

### 1.2 D-Karar özeti

| # | Karar | Durum |
|---|---|---|
| **D-PICKER** | Account/Contact tek-seçim picker; id + display picker'dan | **KİLİTLİ** |
| **D-PICKER-ENDPOINT** | Campaigns proxy'sine `api/accounts` + `api/contacts` (Segments deseni birebir) | **KİLİTLİ** |
| **D-PRIORITY-ENUM** | Öncelik `low \| medium \| high`, in-domain vokabüler | **KİLİTLİ** |
| **D-REMOVED-FIELDS** | sourceRef/selectionReason/reasonCodes/effectiveFrom-To/exclusionReason canvas'tan çıkar | **KİLİTLİ** |
| **D-SOURCE-AUTO** | `TargetSource = manual` otomatik | **KİLİTLİ** |
| **D-DATATABLE-SLIM** | Targets tablosu slim | **KİLİTLİ** · uygulaması §11.3 |
| **D-SNAPSHOT-HIDE** | Snapshot butonu manuel modda gizlenir | **KİLİTLİ** · **sonucu §12.7'de ilan edilir** |
| **D-PRIORITY-FIELD** | Yeni alan `PriorityLevel`; `Priority` (int) deprecate — **yeniden adlandırma zorunlu** | **ÖNERİ** ⚠️ §12.4 |
| **D-PRIORITY-MAP** | Mevcut int satırların bant eşlemesi | **KARAR BEKLİYOR** ⚠️ §12.3 |
| **D-SELECTION-REASON** | Sunucu üretir mi, boş mu kabul edilir? | **KARAR BEKLİYOR** ⚠️ §12.5 |
| **D-STATUS-LIST** | Canvas statü listesinden `excluded` çıkar | **ÖNERİ** ⚠️ §12.6 |
| **D-TARGET-TYPES** | Manuel canvas yalnız `account` + `contact` yazar | **ÖNERİ (ilan)** §12.8 |
| **D-GOLDEN** | Frontmatter `compact` **KALIR**; slim yalnız ikincil tablonun stili | **ÖNERİ** §11.1 |
| **D-FILES** | Gruplanmış düzen korunur | **ÖNERİ** |

---

## 2. Ownership and Boundaries

**In-scope:** `CampaignTarget.PriorityLevel` + `CampaignTargetPriorityLevels` vokabüleri + `Priority` deprecate
işareti + class-map · manuel hedef handler default'ları · komut/request opsiyonelleştirme · contract vokabüler +
limitations · Campaigns proxy'sine account/contact passthrough · targets DataTable slim düzeni · target canvas
yeniden düzeni · snapshot butonunun gizlenmesi · 7 dil RESX · boundary testleri.

**Out-of-scope (YASAK):**

| Yasak | Neden |
|---|---|
| `CampaignTarget`'ın **başka** bir alanının değişmesi | Yalnız öncelik alanı |
| Snapshot / consent **MANTIĞININ** değişmesi | §12.2 — yalnız `Priority` tipinin akışı zorunlu |
| FU10 segment targeting yüzeyi + mod toggle | Bu FU manuel modun **içini** düzenler |
| `Account` / `Contact` / `Segment` aggregate'lerine **yazma** | Picker salt okunur |
| FU08 / FU09 / FU10 kilitlerinin gevşetilmesi | §2.2 |
| Veri migrasyonu / backfill / Mongo hand-edit | §12.4 — okuma-anı türetme |
| `ocelot.json` · registry · RBAC seed/grant | Pack yetkisi dışı |

### 2.1 Protected paths

```text
services/.../Features/Campaign/Snapshot/**
    └── TEK İSTİSNA: CampaignSnapshotTargetItem.Priority tipi ve onu okuyan üç satır (§12.2).
        Snapshot'ın additive/idempotent/never-half-applied davranışı DEĞİŞMEZ.
services/.../Features/ConsentPreference/**                       [DOKUNULMAZ]
services/.../Features/CyclePeriod/**                             [DOKUNULMAZ]
services/.../Features/Segmentation/**                            [DOKUNULMAZ]
services/.../Domain/Entities/{Account,Contact,Segment}*.cs        [OKUNUR, YAZILMAZ]
services/.../Api/Controllers/CRM/{AccountController,ContactController}.cs  [OKUNUR — picker kaynağı]
frontend/.../Controllers/CRM/SegmentsController.cs                [OKUNUR — passthrough deseninin kaynağı]
frontend/.../Views/CRM/Segments/** · wwwroot/assets/js/CRM/Segments/**    [OKUNUR — picker deseni]
frontend/.../Views/CRM/Campaigns/_Form.cshtml · Details.cshtml (bölüm haritası)  [KORUNUR — §11.1]
gateway/**/ocelot.json · execution/registries/module-id-registry.md        [DOKUNULMAZ]
```

### 2.2 FU08 + FU09 + FU10 kilitleri — hepsi korunur

| Kilit | FU11'deki durumu |
|---|---|
| CyclePeriodId pin · B2 · bind-active · D-OPENEND · D-RECHECK (FU08) | **Aynen** |
| Ayrımlı scope · scope-filtreli picker · D-SCOPE-* (FU09) | **Aynen** |
| `TargetingMode` toggle · dormant veri · mod kapısı (FU10) | **Aynen** — bu FU yalnız `manual` dalın içini düzenler |
| `CyclePeriod.supportsCampaignBinding: false` | **Aynen false** |
| `_Form` ↔ `Details` bölüm haritası paritesi | **Aynen** — bu FU o beş bölüme dokunmaz |

### 2.3 Legacy CrmV2

Bu FU legacy'den hiçbir kavram getirmez. Öncelik bandı legacy'nin `PlannedPromoWeek` benzeri sayısal alanlarının
karşılığı **değildir**; kaldırılan alanların yerine legacy kavramı konmaz.

---

## 3. Owned Objects

| Nesne | Tür | Sahiplik |
|---|---|---|
| `CampaignTarget.PriorityLevel` | Alan (`string?`) | **YENİ — bu FU** |
| `CampaignTargetPriorityLevels` | Vokabüler sabit sınıfı | **YENİ — bu FU** |
| `CampaignTarget.Priority` (int?) | Alan | **MEVCUT — DEPRECATE** (§12.4) |
| Manuel hedef default'ları (`CampaignTargetWrite`) | Application mantığı | **YENİ — bu FU** |
| Campaigns proxy `api/accounts` + `api/contacts` | Frontend passthrough | **YENİ — bu FU** (Segments deseni) |
| `_TargetsDataTable` · `_TargetCreateEditOffcanvas` | View | **MEVCUT — yeniden düzenlenir** |
| `CampaignTarget` (aggregate) · snapshot · consent | Aggregate + akış | **MOD-0165-FU04 — korunur** |
| `Account` / `Contact` | Aggregate | **MOD-0149 / MOD-0150 — salt okunur** |

---

## 4. Entity Fields

### 4.1 Eklenen

| Alan | Tip | Zorunlu | Kısıt | Açıklama |
|---|---|---|---|---|
| `PriorityLevel` | `string?` | Hayır | `CampaignTargetPriorityLevels` — `low` \| `medium` \| `high` | Hedefin öncelik **bandı**. Boş = öncelik belirtilmemiş (uydurulmaz) |

### 4.2 Deprecate edilen

| Alan | Durum |
|---|---|
| `Priority` (`int?`) | **Deprecate-nullable.** Entity'de kalır, yeni yazımda **doldurulmaz**, okuma anında `PriorityLevel` yoksa banda türetilir (§12.3). **Migrasyon YOK** |

### 4.3 Neden bant, neden şimdi

`Priority`'nin bugünkü sözleşmesi *"deterministic ordering weight … smaller wins"*. Ama **hiçbir tüketici ona göre
sıralamıyor** (B8): repodaki her `OrderBy(Priority)` başka bir aggregate'e ait. Yani alan, vaat ettiği işi hiç
yapmadan yazardan serbest bir tamsayı istiyor — ve *"3 mü 5 mi yazmalıyım?"* sorusunun doğru cevabı yok.

Üç bant, yazarın gerçekten verebileceği kararı sorar. Sıralama ihtiyacı doğarsa banda göre sıralamak
(`high < medium < low`) hâlâ mümkündür ve **deterministiktir**; kaybedilen tek şey, kimsenin kullanmadığı
ince taneli ağırlıktır.

---

## 5. Repo Scope

### 5.1 Backend

```text
Domain/Entities/Campaign.cs                                   [DEĞİŞİR] +PriorityLevel +CampaignTargetPriorityLevels
                                                                        +DerivedPriorityLevel(), Priority deprecate,
                                                                        +2 reason code
Application/Features/Campaign/
├── CampaignValidation.cs                                     [DEĞİŞİR] +ValidatePriorityLevel; ValidatePriority KALIR
│                                                                        (snapshot hâlâ int alabilir)
├── Commands/CampaignCommands.cs                              [DEĞİŞİR] target komutlarında SelectionReason /
│                                                                        EffectiveFrom / TargetSource OPSİYONEL,
│                                                                        +PriorityLevel
├── Handlers/CampaignTargetCommandHandlers.cs                 [DEĞİŞİR] manuel default'lar (§12.5/§12.6)
├── CampaignDtos.cs / CampaignMapper.cs                       [DEĞİŞİR] +PriorityLevel (+snapshot item: §12.2)
├── Snapshot/CampaignTargetSnapshotHandler.cs                 [DEĞİŞİR] YALNIZ Priority tipinin akışı (§12.2)
└── Contract/CampaignContract.cs                              [DEĞİŞİR] +vokabüler, +limitations, +reason code
Persistence/DependencyInjection.cs                            [DEĞİŞİR] class-map (PriorityLevel string)
Api/Models/CRM/CampaignRequests.cs · Controllers/CRM/CampaignsController.cs  [DEĞİŞİR]
tests/.../CampaignManualTargetingTests.cs                     [YENİ]
tests/.../CampaignTargetingRuntimeTests.cs                    [DEĞİŞİR] §17.3
```

### 5.2 Frontend

```text
Controllers/CRM/CampaignsController.cs        [DEĞİŞİR] +api/accounts +api/contacts passthrough
Views/CRM/Campaigns/_TargetsDataTable.cshtml  [DEĞİŞİR] slim kolon düzeni (§11.3)
Views/CRM/Campaigns/_TargetCreateEditOffcanvas.cshtml  [DEĞİŞİR] picker + Priority dropdown + kaldırmalar
Views/CRM/Campaigns/Details.cshtml            [DEĞİŞİR] YALNIZ snapshot butonunun kaldırılması (§12.7)
Views/CRM/Campaigns/_IndexL10n.cshtml         [DEĞİŞİR] +anahtarlar
wwwroot/assets/js/CRM/Campaigns/details.js    [DEĞİŞİR] kolon düzeni + picker + payload sadeleşmesi
Resources/Views/CRM/Campaigns/CampaignIndex.*.resx  [DEĞİŞİR] 7 dil
```

---

## 6. Protected Paths

§2.1'de tam liste verilmiştir.

---

## 7. Dependencies

| Bağımlılık | Rol | Durum | Not |
|---|---|---|---|
| **MOD-0165-FU10** | genişletilen | SHIPPED | Mod toggle + manuel UI'nın korunması **aynen** |
| **MOD-0165-FU04** | ayarlanan | SHIPPED | Aggregate +1 alan; snapshot mantığı değişmez |
| **MOD-0149 / MOD-0150** | **okunan** | SHIPPED | `crm.account.read` / `crm.contact.read`; route'lar mevcut |
| **MOD-0167-FU02** | **desen kaynağı** | SHIPPED | Segments'in picker + passthrough deseni birebir aynalanır |
| **MOD-0164** | komşu | SHIPPED | **Değişmez** |
| **Gateway** | — | route **mevcut** | Yeni ocelot route'u gerekmez (§15) |
| **DEV-0000 / DEV-0001** | şablon | mevcut | Modül **Compact**; ikincil tablo slim stili (§11.1) |

---

## 8. Runtime Constraints

### 8.1 Picker salt okunurdur

Picker `Account` / `Contact` master'ını **okur**, yazmaz. Seçilen id **olduğu gibi** `TargetId`'ye geçer; görünen
ad `TargetDisplayName`'e **etiket olarak** yazılır ve FU04'ün kuralı aynen geçerlidir: *"a snapshot LABEL for
display/audit only… consumers must resolve the name from the owning master, never from here."*

### 8.2 Erişilemezlik davranışı

| Durum | Cevap |
|---|---|
| Account/Contact listesi okunamıyor | Picker **boş** gelir → hedef eklenemez (fail-closed). Hardcoded liste **yasak** |
| Bilinmeyen `PriorityLevel` | **400** `campaign_target_priority_level_unknown` — sessizce bir banda düşürülmez |
| `TargetId` boş GUID | **400** (FU04 kuralı, değişmez) |
| Mongo erişilemez | **500** (mevcut davranış) — sahte 503 katmanı eklenmez |

### 8.3 Yetki

Picker'ın iki kaynağı **kendi** guard'larını uygular (`crm.account.read` / `crm.contact.read`). Yetkisi olmayan
aktör boş liste görür ve hedef ekleyemez — **fail-closed**, ve bu doğrudur: hedefleyemeyeceğin bir kaydı
hedefleyememelisin.

---

## 9. Layout & Shell Contract

| Öğe | Değer |
|---|---|
| `shell` | `tenant` |
| Razor layout | **`Layout = "_LayoutTenantShell";`** — FU10'un beş sayfası **değişmez** |
| View klasörü | `frontend/Diten.Web/Views/CRM/Campaigns/` |
| Golden reference | **Compact** (modül) · ikincil tablo **slim stili** (§11.1) |
| Nav | Yeni girdi **YOK** |

Bu FU **hiçbir yeni sayfa açmaz.**

---

## 10. Backend File Convention

**D-FILES:** `Features/Campaign/` gruplanmış düzeni **korunur** (FU08–FU10'dan miras; F-FILE-DRIFT açık kalır).
Yeni dosya yoktur; tüm değişiklikler mevcut dosyalara girer.

---

## 11. Frontend File Contract

### 11.1 ⚠️ Golden karar — frontmatter `compact` KALIR

Görev girdisi *"Golden Reference slim"* diyor. Bu, **modülün** golden referansı olarak alınırsa yanlış olur:

- Modülün **kendi formu** (Create/Edit/Details) FU10'da **18 kullanıcı alanı** taşıyor → `> 8` → **Compact**.
- Slim yapılacak şey, `Details` sayfasının **içindeki İKİNCİL** targets tablosudur — modülün Index'i değil.
- Orchestrator doğrulamayı `--reference compact` ile çalıştırıyor; frontmatter `slim`'e çevrilirse **yanlış
  doğrulama** koşulur ve modülün Compact formu haksız yere FAIL verir.

**Karar:** `golden_reference: compact` **kalır**; slim burada bir **stil hedefidir**, bir referans beyanı değil.
`form_field_count: 18` da değişmez ve frontmatter'da bunu söyleyen bir not taşır.

### 11.2 Canvas yeniden düzeni

| Alan | Bugün | FU11 |
|---|---|---|
| Target Type | select (7 değer) | select (**2 değer**: account · contact — §12.8) |
| Hedef | `targetId` (GUID metin) + `targetDisplayName` (metin) | **tek picker** (Select2, tipe göre account/contact) |
| Target Status | select | select (**`excluded` çıkar** — §12.6) |
| Priority | number | **select** (low/medium/high) |
| Target Source | select | **kaldırılır** → sunucu `manual` yazar |
| Source Reference Type/Id | 2 input | **kaldırılır** → `null` |
| Selection Reason | textarea (zorunlu) | **kaldırılır** → §12.5 |
| Reason Codes | input (zorunlu) | **kaldırılır** → `manual_target_selected` |
| Effective From / To | 2 datetime | **kaldırılır** → `from = now`, `to = null` |
| Exclusion Reason | input | **kaldırılır** → `null` (§12.6) |
| Notes | (yok) | **kalır/eklenir** — yazarın söylemek isteyeceği tek serbest alan |
| External References (JSON) | textarea | **kaldırılır** — manuel hedefte entegrasyon kimliği anlamsız |

**Sonuç: 13 alan → 5.** Picker Segments'in `SelectAccount`/`SelectContact` deseninin aynısıdır: seçenekler
fetch ile doldurulduktan **sonra** Select2 initialize edilir, `change` yeniden yayımlanır.

### 11.3 D-DATATABLE-SLIM — kolonlar silinmez, **gizlenir**

Tablo bugün **26 kolon** taşıyor (B9); 6'sı consent provenance, yani FU04'ün *"why is this target in or out?"*
denetim yüzeyi. Bunları **silmek** o denetimi kaybettirir.

**Görünür (8):** Target Type · Hedef (ad + id) · Target Status · **Priority** · Target Source ·
Snapshot Batch · Effective From · Actions.

**Satır-içi detayda (responsive child row, `none` sınıfı):** Source Reference Type/Id · Selection Reason ·
Reason Codes · Exclusion Reason · 6 consent provenance kolonu · Effective To · Archived · Updated At ·
CampaignTargetId.

> Kullanıcının *"provenance kolonları display-only KALIR"* talimatı böylece **fazlasıyla** karşılanır: hiçbir
> kolon kaybolmaz, yalnız varsayılan görünüm okunabilir hâle gelir. `scrollX` kapatılabilir.

### 11.4 Verifier etkisi

Targets tablosu modülün **Index'i değil**; `verify_datatable_page` yalnız `Index` / `_Filter` / `_DataTable` /
`_IndexL10n` / `Create` / `Edit` / `Details` / `_Form` yüzeylerine bakar. Bu FU onların hiçbirinin **yapısını**
değiştirmez (Details'te yalnız bir buton kalkar), dolayısıyla **87/8 baseline'ı korunmalıdır** (AC-V-1).

---

## 12. Validation Rules

### 12.1 Alan düzeyi

| Kural | Hata kodu | HTTP |
|---|---|---|
| `PriorityLevel` verilmişse bilinen bant olmalı | `campaign_target_priority_level_unknown` | 400 |
| `TargetType` manuel canvas'tan yalnız `account` \| `contact` | (mevcut `ValidateTargetType`) | 400 |
| `TargetId` boş GUID olamaz | (mevcut) | 400 |
| Picker'dan gelmeyen `TargetDisplayName` | serbest — etiket, doğrulanmaz | — |

### 12.2 ⚠️ *"Snapshot dokunulmaz"* kilidinin zorunlu daraltılması

`Priority` alanını **snapshot da yazıyor** (B1). Bir aggregate alanının tipini, o alanı yazan her yolu
değiştirmeden değiştirmek mümkün değildir. Dolayısıyla kilit şöyle daraltılır ve pack'te böyle uygulanır:

> **Snapshot'ın DAVRANIŞI dokunulmaz** — additive kalır, idempotent kalır, asla yarım uygulanmaz, consent
> provenance'ı aynen yazar. **Değişen tek şey**, `CampaignSnapshotTargetItem`'ın öncelik alanının tipi ve onu
> okuyan üç satırdır.

**Ayrıca:** `CampaignValidation.ValidatePriority(int?)` **silinmez** — snapshot çağıranları hâlâ tamsayı
gönderiyorsa (dış entegrasyon) reddedilmemelidir. Snapshot yolu için öneri: `Priority` (int) **ve**
`PriorityLevel` (bant) ikisini de kabul et, ikisi de verilirse bandı yaz, yalnız int verilirse §12.3'ün
eşlemesiyle banda çevir. Böylece snapshot çağıranı **hiç değişmeden** çalışmaya devam eder.

### 12.3 ⚠️ D-PRIORITY-MAP — **karar bekliyor**

Kullanıcı eşlemeyi *"1→low / 2→medium / 3→high ya da default"* olarak önerdi. Alanın kendi belgesi ise
**"Smaller wins"** diyor — yani `1` bugünün **en yüksek** önceliğidir (B2).

| # | Eşleme | Değerlendirme |
|---|---|---|
| **(a)** | **1 → high · 2 → medium · ≥3 → low · null → null** | ✅ **ÖNERİLEN.** Alanın **var olan anlamını korur**. Bedeli: 3'ün üstündeki keyfi değerler (7, 42) `low`'a toplanır — sıralama vaadi zaten kimse tarafından kullanılmadığı için (B8) bu bir kayıp değil, bir yuvarlamadır |
| (b) | 1 → low · 2 → medium · 3 → high (görev girdisindeki) | ❌ **Anlamı ters çevirir.** `Priority=1` taşıyan her mevcut satır — bugünün en önemlileri — ekranda **"low"** görünür. Kimse bir şey yapmadan veri yalan söylemeye başlar |
| (c) | Mevcut tüm int satırlar → `null` ("sayıydı, bant değildi") | ⚠️ En "uydurmayan" seçenek ama **bilgiyi atar**: bir öncelik verilmiş olduğu gerçeği ekrandan silinir |

**Öneri: (a).** Türetme **okuma anındadır ve hiçbir şey yazmaz** (FU09/FU10 doktrini); backfill script'i **yoktur**.
Karşı-karar verilirse §17'deki eşleme testleri ve §13.2'nin limitations satırı yeniden yazılmalıdır.

### 12.4 ⚠️ D-PRIORITY-FIELD — alan yeniden adlandırılmalı (çalışma zamanı zorunluluğu)

`int?` → enum dönüşümü **aynı BSON alan adında yapılamaz**: mevcut belgelerde `Priority` bir `Int32`'dir ve onu
`string`'e deserialize etmek **okuma anında patlar** (B3). Bu bir tercih değil, bir hata koşuludur.

| # | Seçenek | Değerlendirme |
|---|---|---|
| **(a)** | **Yeni alan `PriorityLevel` (string), `Priority` (int) deprecate; okuma anında türet** | ✅ **ÖNERİLEN.** FU10'un 10 alanı deprecate ettiği desenin aynısı: migrasyon yok, serializer hilesi yok, çökme yok, eski değer **durur** |
| (b) | Aynı adı koru + int-veya-string okuyan özel BSON serializer | ❌ Her okumada tip dallanması; iki biçimin **kalıcı** birlikteliği; hata anında teşhisi zor. Görev girdisindeki *"class-map"* ipucu bunu ima ediyor olabilir ama bedeli daha yüksek |
| (c) | Veriyi migrate et | ❌ Bu FU'da migrasyon **yasak**; ayrıca geri alınamaz |

### 12.5 ⚠️ D-SELECTION-REASON — **karar bekliyor**

FU04, `SelectionReason`'ı **zorunlu** yaptı ve gerekçesini kodda bıraktı: *"a campaign target may never be
selected without a stated reason"* (B4). Görev girdisi alanı canvas'tan kaldırıp *"default/null-kabul"* diyor.

| # | Seçenek | Değerlendirme |
|---|---|---|
| **(a)** | **Sunucu ÜRETİR**: *"Manually selected by {actor} on {date}"* (aktör yoksa *"Manually selected on {date}"*) | ✅ **ÖNERİLEN.** Yazardan yazma yükü kalkar, **invaryant korunur**: her hedef hâlâ neden var olduğunu söyler ve söylediği şey **doğrudur** — uydurma değil, bildiğimiz iki gerçek |
| (b) | `null` kabul et, invaryantı kaldır | ❌ FU04'ün açık kuralını sıfırlar; denetimde her manuel satırın gerekçesi **boş** görünür. *"Silent target selection is forbidden"* yalnız yazı olarak kalır |
| (c) | Canvas'ta tut | ❌ Kullanıcı kararına aykırı; asıl sadeleştirme hedefini bozar |

**Öneri: (a).** `ValidateSelectionReason` **değişmez**; handler alanı doldurduğu için hiçbir zaman boş gelmez.

### 12.6 ⚠️ D-STATUS-LIST — `excluded` canvas'tan çıkmalı

`ValidateExclusion`, `TargetStatus=excluded` iken `ExclusionReason` **zorunlu** kılar (B5). `exclusionReason`
canvas'tan kaldırılırsa ve statü listesinde `excluded` kalırsa, yazar **düzeltemeyeceği bir 400** ile karşılaşır.

**Öneri:** manuel canvas statü listesi `draft · active · inactive · completed` ile sınırlanır. `excluded`
**yazarın seçeceği bir şey değildir** — snapshot'ın consent değerlendirmesinin **sonucudur** (FU04: blocked/unknown
⇒ `excluded` + reason). Bu, listeyi kısaltmaktan çok, alanın gerçek sahibini kabul etmektir.
`archived` de listede yoktur: arşivleme kendi aksiyonudur.

### 12.7 ⚠️ D-SNAPSHOT-HIDE — ilan edilen sonuç

FU10 R2 manuel targeting kartını **yalnız `manual` modda** render ediyor; snapshot butonu o kartın içinde (B6).
Butonu `manual` modda da gizlemek şu anlama gelir:

> **Snapshot butonu hiçbir modda görünmez — snapshot'ı tetikleyecek UI kalmaz.**

Bu, FU10 **R1'de reddedilen** *"yetenek gerilemesi"*nin aynısıdır ve bu kez **bilinçli olarak** kabul edilmektedir.
Gerekçe tutarlıdır: snapshot bir **çözümleme** işidir ve çözümlenecek şey (segment üyeliği) henüz açık değildir;
manuel modda bir "static target snapshot" çalıştırmak, yazarın elle girdiği satırların üstüne kendi kopyasını
yazmak demektir — anlamsızdır.

**Bu yüzden:**
- Endpoint **kalır** ve contract bayrağı `supportsStaticTargetSnapshot` **`true` kalır** (API gerçekten destekliyor);
- limitations'a *"bu sürümde ekranı yoktur"* satırı **eklenir** (§13.2);
- **F-SEGMENT-RESOLUTION** butonun gerçek yerini (segment modu) açacak follow-up olarak kaydedilir.

Karşı-karar verilirse buton `manual` modda kalır ve bu §, AC-UI-6 ile birlikte geri alınır.

### 12.8 D-TARGET-TYPES — manuel canvas iki tiple sınırlanır (ilan)

`CampaignTargetTypes` yedi değer taşır. Manuel canvas bunlardan **ikisini** yazabilir hâle gelir:
`account` ve `contact` — picker'ı olan tek ikisi. Diğer beşi (`account-contact-link`, `segment`,
`territory-node`, `concept-node`, `audience-profile`) **manuel olarak yazılamaz** olur.

Bu bir daraltmadır ve **ilan edilir**: `segment` hedefleme artık FU10'un işidir; kalan üçü hiçbir zaman bir
picker'a sahip olmadı ve GUID yazdırarak yazılmaları zaten kullanılabilir bir yol değildi. **API bunları
kabul etmeye devam eder** — kısıt canvas'ındır, contract'ın değil.

### 12.9 Manuel yazımda sunucunun doldurdukları

| Alan | Değer | Not |
|---|---|---|
| `TargetSource` | `manual` | Otomatik; canvas'ta yok |
| `ReasonCodes` | `["manual_target_selected"]` | `NormalizeReasonCodes` yaşam döngüsü kodunu zaten ekliyor |
| `SelectionReason` | üretilir (§12.5/(a)) | İnvaryant korunur |
| `EffectiveFrom` | `now` | |
| `EffectiveTo` · `SourceReferenceType/Id` · `ExclusionReason` · `ExternalReferences` | `null` / boş | |
| `TargetStatus` | canvas'tan (`draft` varsayılan) | |
| `PriorityLevel` | canvas'tan (opsiyonel) | |

### 12.10 Failure Path to Verify

| Yol | Beklenen |
|---|---|
| Duplicate | Aynı (campaign, type, id) için ikinci ACTIVE hedef → **409** (FU04 kuralı, değişmez) |
| Missing | Picker seçilmeden kaydet → **400** (`TargetId` boş) |
| Unauthorized | `crm.account.read` yoksa picker boş → hedef eklenemez (fail-closed) |
| Mode gate | `segment` modunda manuel hedef yazımı → **400** (FU10 kuralı, değişmez) |
| Archived campaign | Hedef mutasyonu → **409** (FU04 kuralı, değişmez) |
| Bilinmeyen bant | → **400**, sessizce düşürülmez |
| Half-applied | **İmkânsız** — doğrulama yazımdan önce |

---

## 13. Contract Surface

### 13.1 Bayraklar ve vokabüler

**Bayrak değişmez.** Dokuz bayrağın hiçbiri eklenmez veya çevrilmez: bu FU yeni bir yetenek açmıyor, mevcut bir
yeteneğin **yazma yüzeyini** düzenliyor. `supportsStaticTargetSnapshot` **`true` kalır** (§12.7).

**Vokabülere eklenir:** `targetPriorityLevels: ["low", "medium", "high"]` — UI kendi listesini uydurmasın.

### 13.2 `limitations` — eklenen satırlar

1. *"FU11: a manual target is authored through an account/contact picker — the id comes from the picker and the
   display name is a LABEL only; consumers still resolve names from the owning master"*
2. *"FU11: manual authoring fills TargetSource=manual, reasonCodes=[manual_target_selected], effectiveFrom=now and a
   generated selectionReason. The 'a target always states why it exists' rule is unchanged — the author no longer
   types the reason, the server states a true one"*
3. *"FU11: target priority is a BAND (low/medium/high) on `priorityLevel`. The former integer `priority` is deprecated
   and kept: existing rows are read as bands (1→high, 2→medium, ≥3→low) at READ time and nothing is migrated"*
4. *"FU11: the manual canvas authors only `account` and `contact` targets — the two with a picker. The API still
   accepts every target type; the restriction is the screen's, not the contract's"*
5. *"FU11: the static target snapshot has no screen in this release. The endpoint and its flag stay true because the
   API genuinely supports it; a snapshot belongs to segment resolution, which is a separate follow-up"*
6. *"FU11: `excluded` is not an authorable target status — it is the OUTCOME of a consent evaluation (blocked/unknown),
   and it always carries the reason the evaluator wrote"*

### 13.3 CyclePeriod / Segment contract'larına DOKUNULMAZ

`supportsCampaignBinding: false` kalır; Segment contract'ı hiç değişmez.

---

## 14. Authorization Convention

| Konu | Karar |
|---|---|
| Yeni permission anahtarı | **YOK** |
| Hedef yazma | Mevcut `crm.campaign.target.*` |
| Account picker | `crm.account.read` (Segments passthrough'unun aynısı) |
| Contact picker | `crm.contact.read` |
| Yetki yoksa | Picker **boş** → hedef eklenemez (fail-closed) |
| RBAC seed / grant | **YASAK** |

---

## 15. Gateway / API Routing Decision

| Soru | Cevap |
|---|---|
| Yeni Ocelot route'u | **HAYIR** — `/api/crm/accounts` ve `/api/crm/contacts` route'ları mevcut |
| Yeni backend endpoint | **HAYIR** — `AccountController [HttpGet]` ve `ContactController [HttpGet]`/`search` var |
| Frontend | **2 ekleme:** Campaigns proxy'sine `api/accounts` + `api/contacts` (SegmentsController'ın aynısı) |
| `integration-agent` görevi | **HAYIR** |

---

## 16. Acceptance Criteria

### Öncelik bandı

| # | Kriter |
|---|---|
| **AC-P-1** | `PriorityLevel` yalnız `low`/`medium`/`high` kabul eder; bilinmeyen değer → 400, sessizce düşürülmez |
| **AC-P-2** | Bant contract vokabülerinde **yayımlanır**; UI hardcoded liste taşımaz |
| **AC-P-3** | `Priority` (int) alanı entity'de **durur**, deprecate işaretlidir ve yeni yazımda **doldurulmaz** |
| **AC-P-4** | Mevcut int satırlar **okunabilir** (deserialization hatası yok) ve §12.3'ün eşlemesiyle banda türetilir |
| **AC-P-5** | Türetme **hiçbir şey yazmaz**; backfill script'i **yoktur** |
| **AC-P-6** | Snapshot yolu tamsayı `Priority` göndermeye **devam edebilir** ve reddedilmez (§12.2) |

### Manuel yazma yüzeyi

| # | Kriter |
|---|---|
| **AC-M-1** | Canvas 5 alan gösterir; kaldırılan 8 alan **yoktur** |
| **AC-M-2** | Kaydedilen hedefte `TargetSource == "manual"` (canvas'ta hiç görünmedi) |
| **AC-M-3** | `ReasonCodes` en az `manual_target_selected` taşır |
| **AC-M-4** | `SelectionReason` **boş değildir** ve üretilen metin aktör + tarih içerir (D-SELECTION-REASON=(a)) |
| **AC-M-5** | `EffectiveFrom == now`, `EffectiveTo == null`, `SourceReferenceType/Id == null`, `ExclusionReason == null` |
| **AC-M-6** | Canvas statü listesinde `excluded` ve `archived` **yoktur** |
| **AC-M-7** | Komut/request'te `SelectionReason` / `EffectiveFrom` / `TargetSource` **opsiyoneldir**; verilirse aynen kullanılır (dış çağıran kırılmaz) |
| **AC-M-8** | FU04'ün duplicate (409), archived-campaign (409) ve FU10'un mod kapısı (400) kuralları **aynen** çalışır |

### Picker

| # | Kriter |
|---|---|
| **AC-K-1** | Target Type = account → account listesi; = contact → contact listesi |
| **AC-K-2** | Seçenekler **yalnız** proxy'den gelir; hardcoded liste **yok** |
| **AC-K-3** | Seçilen id `TargetId`'ye **olduğu gibi** geçer; ad `TargetDisplayName`'e **etiket** olarak yazılır |
| **AC-K-4** | Picker `Account`/`Contact` master'ına **yazmaz** (write sayacı 0) |
| **AC-K-5** | Yetki yoksa liste boş → hedef eklenemez; sahte/yerel liste **gösterilmez** |
| **AC-K-6** | Select2 seçenekler doldurulduktan **sonra** initialize edilir |
| **AC-K-7** | Düzenlemede mevcut hedef picker'da **korunur** (arşivlenmiş/silinmiş olsa bile) — sessiz değişim yok |

### DataTable

| # | Kriter |
|---|---|
| **AC-T-1** | **8 kolon görünür**; kalan 18'i satır-içi detayda **erişilebilir** |
| **AC-T-2** | Hiçbir kolon **silinmemiştir** — 6 consent provenance kolonu dâhil |
| **AC-T-3** | Provenance kolonları (source / sourceRef / snapshotBatch) **display-only** kalır |
| **AC-T-4** | Priority kolonu **bant** gösterir; eski int satırlar türetilmiş bandı gösterir |

### Snapshot ve regresyon

| # | Kriter |
|---|---|
| **AC-UI-6** | Snapshot butonu **hiçbir modda render edilmez**; endpoint ve bayrak **korunur** ve limitations bunu **açıkça** söyler |
| **AC-R-1** | FU10 mod toggle, dormant veri ve mod kapısı **aynen** çalışır |
| **AC-R-2** | FU08/FU09 kilitleri **aynen** (B2, bind-active, scope-uygulanabilirlik) |
| **AC-R-3** | Snapshot **davranışı** değişmemiştir: additive · idempotent · asla yarım · consent provenance aynen |
| **AC-R-4** | `_Form` ↔ `Details` bölüm haritası **paritesi korunur** |

### Doğrulama

| # | Kriter |
|---|---|
| **AC-V-1** | `verify_datatable_page --area CRM --module Campaigns --reference **compact** --api-profile proxy` **87/8 baseline'ından gerilemez** |
| **AC-V-2** | `dotnet build` **0 hata**; `node --check` temiz |
| **AC-V-3** | Test süiti yeşil; FU06–FU10 **davranış** iddiaları gevşetilmez |
| **AC-V-4** | CAND literal **0** |
| **AC-V-5** | `verify_module_id --check-all` **HARD violations: 0** |
| **AC-V-6** | 7 dil RESX parite tam; değerler gerçekten çevrilmiş |

---

## 17. Test Expectations

Yeni dosya: `tests/.../CampaignManualTargetingTests.cs`.

### 17.1 Kapsam matrisi

| Grup | Test |
|---|---|
| **Bant** | 3 değer kabul · bilinmeyen → 400 · null serbest · contract'ta yayımlı |
| **Türetme** | 1→high · 2→medium · 3→low · 42→low · null→null · türetme **yazmaz** · int satır **okunabilir** |
| **Snapshot uyumu** | Snapshot int `Priority` göndermeye devam eder → kabul + banda çevrilir · snapshot **davranışı** değişmez (additive/idempotent/409-abort) |
| **Default'lar** | TargetSource=manual · reasonCodes · üretilen selectionReason **boş değil** · effectiveFrom=now · diğerleri null |
| **Opsiyonelleşme** | Dış çağıran açık `selectionReason`/`effectiveFrom`/`targetSource` gönderirse **aynen** kullanılır |
| **Statü** | `excluded` manuel yazımda reddedilir/sunulmaz · snapshot yolu `excluded` yazmaya **devam eder** |
| **Picker** | id olduğu gibi geçer · ad etiket · Account/Contact repo write sayacı **0** · yetkisiz → boş liste |
| **Regresyon** | duplicate 409 · archived campaign 409 · FU10 mod kapısı 400 · consent provenance yalnız snapshot'tan |
| **Contract** | Yeni bayrak **yok** · vokabüler + 6 limitations satırı |

### 17.2 Frontend / manuel

| # | Adım |
|---|---|
| S1 | Fleet FU11 build'iyle yeniden başlatılır |
| S2 | `manual` modda bir kampanya → Details → Create Target |
| S3 | Type=account → picker hesapları listeler; biri seçilir; Priority=high; kaydedilir |
| S4 | Tabloda satır görünür: hedef adı, `manual` kaynağı, `high` bandı |
| S5 | Satır detayı açılır → selection reason **üretilmiş metni** gösterir, reason codes `manual_target_selected` |
| S6 | Type=contact → picker kişileri listeler |
| S7 | Aynı hedef ikinci kez eklenir → **409** |
| S8 | Snapshot butonu **hiçbir yerde yok** |
| S9 | Eski (FU11 öncesi) int öncelikli bir hedef satırı bant olarak görünür ve **açılabilir** |
| S10 | `segment` moduna geçilir → manuel kart gizlenir (FU10 regresyonu) |

### 17.3 Bilerek değiştirilecek MEVCUT testler (şeffaflık)

| Test | Değişiklik | Gerekçe |
|---|---|---|
| `CampaignTargetingRuntimeTests` `TargetCmd` helper | `selectionReason`/`effectiveFrom`/`targetSource` opsiyonelleştiği için imza sadeleşir; `Priority` → `PriorityLevel` | Derleme + tip değişimi |
| Priority doğrulama testleri | int yerine bant assert'i; **int yolu snapshot için korunur** ve ayrı test alır | §12.2 |
| `T35` bayrak kümesi | **DEĞİŞMEZ** — yeni bayrak yok | — |
| FU06–FU10 davranış testleri | **DEĞİŞMEZ** | AC-V-3 |

> **FU08–FU10 dersi:** üç FU'da da pack'in öngördüğünden **fazla** test değişti. Bu FU bir alan tipini
> değiştirdiği için fixture'ları kesinlikle etkileyecektir; sayı önceden verilmiyor, **teslim raporunda tam liste**
> verilecektir.

---

## 18. Localization

**Eklenen (7 dil):** `PriorityLevel` · `PriorityLevel_low` · `PriorityLevel_medium` · `PriorityLevel_high` ·
`PriorityLevelUnknown` · `TargetPicker` · `TargetPickerHelp` · `ManualTargetDefaultsHint`
(*"Kaynak, gerekçe ve tarihler otomatik doldurulur"*) · `GeneratedSelectionReason` (üretilen metnin şablonu).

**Yeniden kullanılan (FU10'da eklenmişti):** `SelectAccount` · `SelectContact` — Segments'te de aynı işi görüyor.

**Temizlenen:** canvas'tan kalkan alanların yalnız orada kullanılan anahtarları — **grep ile doğrulanarak** ve
**parite bozulmadan**. `SourceSystem`/`ExternalId`/`ExternalCode`/`ExternalName`/`ExternalReferencesJson` gibi
anahtarlar başka yüzeylerde de kullanılıyor olabilir; kaldırma yalnız Campaigns yüzeyinde kullanılmayanlara
uygulanır (FU10'da bu tarama kapsam hatasıyla yanlış sonuç vermişti — kapsam **Campaigns view + js** ile
sınırlanmalıdır).

---

## 19. Ready-for-dev Checklist

| # | Madde | Durum |
|---|---|---|
| 1 | DCP-002 exit 0 + FU gerekçesi | ✅ §0.1 |
| 2 | Golden karar (frontmatter **compact** kalır, slim = ikincil tablo stili) | ✅ §11.1 |
| 3 | Layout | ✅ §9 |
| 4 | Backend dosya konvansiyonu | ✅ §10 |
| 5 | Frontend dosya seti | ✅ §5.2, §11 |
| 6 | Validation Rules | ✅ §12 |
| 7 | Failure Path | ✅ §12.10 |
| 8 | Authorization | ✅ §14 |
| 9 | Gateway kararı | ✅ §15 |
| 10 | Acceptance Criteria | ✅ §16 |
| 11 | Test Expectations + şeffaflık | ✅ §17 |
| 12 | Protected paths | ✅ §2.1 |
| 13 | Migrasyon gerekmediği kanıtlandı | ✅ §12.4 |
| 14 | FU08/FU09/FU10 kilitleri korunuyor | ✅ §2.2, AC-R-1..4 |
| 15 | **D-PRIORITY-MAP kararı** (1→high mi 1→low mu?) | ⛔ **BEKLİYOR** — §12.3 |
| 16 | **D-SELECTION-REASON kararı** (üret mi, boş mu?) | ⛔ **BEKLİYOR** — §12.5 |
| 17 | **D-PRIORITY-FIELD onayı** (yeniden adlandırma — çalışma zamanı zorunluluğu) | ⛔ **BEKLİYOR** — §12.4 |
| 18 | **D-SNAPSHOT-HIDE sonucunun kabulü** (buton hiçbir modda görünmez) | ⛔ **BEKLİYOR** — §12.7 |
| 19 | **"Snapshot dokunulmaz" kilidinin daraltılması** onayı | ⛔ **BEKLİYOR** — §12.2 |
| 20 | D-STATUS-LIST · D-TARGET-TYPES · D-DATATABLE-SLIM · D-FILES onayı | ⛔ **BEKLİYOR** — §1.2 |
| 21 | `status: ready-for-dev` + `runtime_code_allowed: true` | ⛔ **BEKLİYOR** |

> **Pack, 15–21 kapanmadan `ready-for-dev` sayılmaz.**

---

## 20. Follow-up Items

| # | İş | Domain | Neden |
|---|---|---|---|
| **F-SEGMENT-RESOLUTION** | `TargetedSegments` → `CampaignTarget` snapshot'ı | commercial-suite | Snapshot butonunun **gerçek yeri**; §12.7 |
| **F-PRIORITY-INT-REMOVAL** | Deprecate `Priority` (int) alanının fiilen kaldırılması + veri kararı | commercial-suite | §12.4 — bant yerleştikten sonra |
| **F-TARGET-TYPE-PICKERS** | Kalan 5 hedef tipi için picker (gerekirse) | commercial-suite | §12.8 — bugün kasten yok |
| **F-PRIORITY-ORDERING** | Banda göre deterministik sıralama tüketicisi | commercial-suite | Bugün hiçbir tüketici sıralamıyor (B8) |
| **F-REGISTRY** | Registry'ye MOD-0165-FU06..FU11 satırları | portfolio-delivery | FU06'dan beri açık |
| **F-WHAT-TO-PROMOTE** · **F-DORMANT-CLEANUP** · **F-TARGETING-MODE-HYBRID** · **F-SEGMENT-READER** · **F-FILE-DRIFT** · **F-SCOPE-SHARED** · **F-COUNTRY-SOT** · **F-MDM-PERM** | FU08–FU10'dan devralındı | — | Değişmedi |

---

## Ek A — Bu pack'in reddettiği altı kolay yol

| # | Kolay yol | Neden reddedildi |
|---|---|---|
| A1 | `Priority`'yi aynı adla enum'a çevir | Mevcut `Int32` belgeleri **deserialize olmaz** — çalışma zamanı hatası (§12.4) |
| A2 | `SelectionReason`'ı `null` kabul et | FU04'ün açık invaryantını sıfırlar; her manuel satırın gerekçesi boş kalır (§12.5) |
| A3 | `1→low` eşlemesini olduğu gibi al | Alanın *"smaller wins"* anlamını ters çevirir; en önemli satırlar "low" görünür (§12.3) |
| A4 | Consent provenance kolonlarını **sil** ("slim olsun") | FU04'ün *"neden içeride/dışarıda?"* denetim yüzeyini kaybettirir; gizlemek yeterli (§11.3) |
| A5 | `excluded`'ı statü listesinde bırak | Yazar düzeltemeyeceği bir 400'e girer (§12.6) |
| A6 | Frontmatter'ı `slim` yap | Modülün formu 18 alan → Compact; doğrulama yanlış referansla koşulur (§11.1) |

## Ek B — İlan edilmiş boşluklar (sessiz değil)

| # | Boşluk | Nerede ilan edildi |
|---|---|---|
| B1 | Snapshot'ın **ekranı yok** (endpoint + bayrak duruyor) | §12.7 · limitations #5 · AC-UI-6 · F-SEGMENT-RESOLUTION |
| B2 | Manuel canvas yalnız **account/contact** yazar | §12.8 · limitations #4 · F-TARGET-TYPE-PICKERS |
| B3 | `Priority` (int) **okunur ama yazılmaz**; veri durur | §12.4 · limitations #3 · F-PRIORITY-INT-REMOVAL |
| B4 | 3'ün üstündeki eski öncelikler `low`'a **yuvarlanır** | §12.3 · limitations #3 |
| B5 | Bant hiçbir tüketici tarafından **sıralanmıyor** | §4.3 (B8) · F-PRIORITY-ORDERING |
| B6 | Snapshot'ın `Priority` **tipi** zorunlu olarak dokunuluyor | §12.2 · AC-P-6 · AC-R-3 |

---

**Otorite sırası:** Blueprint Excel > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` >
`.antigravity/rules/`.
