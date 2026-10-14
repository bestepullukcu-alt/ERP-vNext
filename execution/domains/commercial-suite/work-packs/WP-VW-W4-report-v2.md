# WORK PACKAGE — WP-VW-W4 · Rapor v2: hekim + eczane raporu, doktor profili, potansiyel / fırsat, rakip ürün (MDM + Workflow) — 2026-10-13

> **Kaynaklar:**
> - Rapor mockup'ı [`03-rapor.html`](mockups/visit-workspace/decoded/03-rapor.html) + demo verisi [`00-demo-veri.js`](mockups/visit-workspace/decoded/00-demo-veri.js). Ana dosya: `mockups/visit-workspace/Ziyaret Calisma Alani (standalone).html` (rapor ekranı).
> - **Fark belgesi (asıl şartname):** [DESIGN-GAP-W4-report-2026-10-13](mockups/visit-workspace/DESIGN-GAP-W4-report-2026-10-13.md). Kararlar mockup'ın önüne geçer.
> - Rakip ürün brifi + kararlar: [BRIEF-W4-competitor-products](mockups/visit-workspace/BRIEF-W4-competitor-products.md). Yönetim ekranının mockup'ı ayrıca gelecek.
> - Yol haritası [ROADMAP-visit-workspace](ROADMAP-visit-workspace.md) K-W4…K-W7.
> - W3 yürütme [WP-VW-W3-visit-execution](WP-VW-W3-visit-execution.md) + W3 yazım [WP-VW-W3-authoring](WP-VW-W3-authoring.md): onay çekirdeği deseni (§37 BE-c).
>
> **Taban:** test dalı `test/crm-content-visit-e2e` (CRM 2643/0/5, Web 955/0, mimari denetimsiz 47).

## 0. Kararlar (kullanıcı, 2026-10-12 / 13)
- **K-W4 rapor, ürün başına:**
  1. **Kullanım aşaması** (6): Farkında değil · Farkında · Değerlendiriyor · Deniyor · Düzenli kullanıyor · Savunucu. İçerik yolculuğu aşamasıyla aynı listedir; bir sonraki ziyaretin içeriğini seçer (JourneyProgress ilerler, SB-3c kapanır).
  2. **Hasta potansiyeli, sayı (tahmini):**
     - doktorun aylık **toplam** hastası (doktor düzeyi) + endikasyon × profil başına aylık hasta;
     - endikasyonların toplamı toplamı geçemez;
     - **3 ayda bir** sorulur, arada son değer gösterilir;
     - Az / Orta / Çok eşikleri **ürün başına**, varsayılan < 10 / 10–30 / > 30, pazarlama değiştirir.
  3. **Bizim payımız:** Hiç / Ara sıra / Yarı yarıya / Çoğunlukla / Hep.
  4. **Ziyaret başarısı:** Ulaşıldı / Kısmen / Ulaşılamadı. **Doktor taahhüdü:** Deneyecek / Hastasına yazacak / Daha fazla bilgi istiyor / Taahhüt yok.
  - **Fırsat matrisi:**
    - Çok × Hiç–Ara sıra = Fırsat;
    - Çok × Yarı yarıya = Büyüt;
    - Orta × Hiç–Yarı yarıya = Büyüt;
    - Az × Hiç–Yarı yarıya = Düşük öncelik;
    - her potansiyel × Çoğunlukla–Hep = Koru.
- **K-W5:** benimseme tipolojisi + iletişim stili + KOL düzeyi. Doktor profilinde tutulur (ürüne değil); seçenekler BRD'den; temsilci raporda önerir; değişiklik geçmişi tutulur.
- **K-W6:**
  - Endikasyon / hasta profili = **Kavram Grafiği** (KnowledgeConcepts).
  - Rakip ürün = **MOD-0290 (MDM) içinde ayrı kayıt**, bizim ekibimiz yapar.
  - Temsilci "listede yok" der → **önerilen rakip**; öneren hemen kullanır.
  - Pazarlama / tıbbi kararı hazırlar (tamamla / birleştir / reddet) → **MDM içinde Workflow onayı, Satış Operasyon (POS-SALES-OPS)**.
  - Doğrudan kayıt ve fiyat güncellemesi yalnız yetkiyle (onaysız).
  - Birleştirilen kayıt silinmez; "birleştirildi → hayatta kalan" işaretçisi tutar, CRM okurken yönlenir.
  - Etken madde serbest etiket; geri ödeme durumu BRD seti.
- **K-W7:** temsilci kendi doktorları; yönetici ekibinin doktorları; pazarlama toplulaştırılmış, doktor kimliği olmadan.
- **Fark belgesi kararları:**
  - S1: yıldız → ziyaret başarısı;
  - S2: rakibi kullanan hasta % kalkar, + / − avantaj isteğe bağlı;
  - S3: eczane satışında eğilim zorunlu, kutu / ay isteğe bağlı.
- **Plan kuralları:**
  - Ürün = **Global Ürün**, numune / stok = **GSKU** (marka W10).
  - Pazar payı W7, ön sipariş W6, otomatik e-postalar W5.
- **AUD-001:** yeni CRM yazma komutları denetimsiz sayıyı artırır. Faz 8'de toplu çözülür (kullanıcı kararı); borç satırı EKLENMEZ.

## 1. Tur planı
| tur | prompt | çalışma yeri / dal | bağımlılık |
|---|---|---|---|
| 1 | **W4-BE-c** · CRM: doktor profili (+ geçmiş), ürün potansiyeli, eşikler, fırsat, görünürlük, ürün → endikasyon × profil okuması, yeni BRD setleri (profil) | `C:\tmp\vw-w4-be-c` / `wp/vw-w4-be-c` | — |
| 1 | **W4-BE-b1** · MDM: Workflow bağlantısı (onay çekirdeği, gateway istemcisi, RabbitMQ sonuç tüketicisi, inbox, açık kiracılı CAS deposu) | `C:\tmp\vw-w4-be-b1` / `wp/vw-w4-be-b1` | — |
| 2 | **W4-BE-a** · CRM: rapor v2 (hekim + eczane), ön doldurma, önceki değer, gerekçeli düzeltmede eski / yeni değer, JourneyProgress ilerletme, rapor BRD setleri, MDM rakip istemcisi | `C:\tmp\vw-w4-be-a` / `wp/vw-w4-be-a` | BE-c test dalında |
| 2 | **W4-BE-b2** · MDM: rakip ürün (kayıt, ülke fiyat / geri ödeme + geçmiş, diğer adlar, öneri, birleştirme, Workflow onayı), menü, gateway, yetkiler | `C:\tmp\vw-w4-be-b2` / `wp/vw-w4-be-b2` | BE-b1 test dalında |
| 3 | **W4-WEB-a** · hekim raporu sihirbazı (mockup 03-rapor + fark belgesi) | `C:\tmp\vw-w4-web-a` / `wp/vw-w4-web-a` | BE-a + BE-c + BE-b2 |
| 3 | **W4-WEB-b** · eczane raporu + potansiyel eşik ayar sayfası (standart desen) | `C:\tmp\vw-w4-web-b` / `wp/vw-w4-web-b` | BE-a + BE-c |
| 4 | **W4-WEB-c** · MDM rakip ürün yönetimi (liste, kayıt, ülke tablosu, öneriler, birleştir, onay) | `C:\tmp\vw-w4-web-c` / `wp/vw-w4-web-c` | **mockup** + BE-b2 |
| 4 | **W4-CFG** · Workflow şablonu `MDM-COMP-PROPOSAL` → POS-SALES-OPS (canlı) + yetki betiği | `C:\tmp\vw-w4-cfg` / `wp/vw-w4-cfg` | BE-b2 + fleet restart |

## 2. Sabit sözleşme

### 2.1 Referans veri setleri (BRD, kiracı kapsamı, 7 dil)
Dosya: `services/Diten.Platform/src/Diten.Platform.API/Seed/business-reference-data/crm-visit-report-v2-reference.json`
- `scope_type: tenant`, `scope_key` = 97c5;
- `display_name` İngilizce + `label_tr/fr/es/zh/ar/ru`.

Her set kodu **üç yerde** tüketilebilir listeye eklenir (drift testleri yeşil kalmalı):
- `appsettings.json` → `BusinessReferenceData:ConsumableSets`;
- `appsettings.Development.json`;
- `BusinessReferenceDataConsumableSetsOptions.DefaultConsumableSets`.

| set | değerler (kod → TR) | paket |
|---|---|---|
| `crm-adoption-stage` | unaware Farkında değil · aware Farkında · evaluating Değerlendiriyor · trying Deniyor · regular Düzenli kullanıyor · advocate Savunucu | BE-c |
| `crm-share-bucket` | none Hiç · occasional Ara sıra · half Yarı yarıya · mostly Çoğunlukla · always Hep | BE-c |
| `crm-adoption-typology` | innovator Yenilikçi · early_adopter Erken benimseyen · early_majority Erken çoğunluk · late_majority Geç çoğunluk · laggard Geride kalan | BE-c |
| `crm-communication-style` | analytical Analitik · driver Sonuç odaklı · amiable Uyumlu · expressive Dışavurumcu | BE-c |
| `crm-kol-level` | none Yok · local Yerel · regional Bölgesel · national Ulusal · international Uluslararası | BE-c |
| `crm-visit-success` | reached Ulaşıldı · partial Kısmen · not_reached Ulaşılamadı | BE-a |
| `crm-doctor-commitment` | will_try Deneyecek · will_prescribe Hastasına yazacak · wants_info Daha fazla bilgi istiyor · none Taahhüt yok | BE-a |
| `crm-visit-interest` | interested İlgili · neutral Nötr · not_interested İlgisiz (W3 işaret kodlarıyla aynı) | BE-a |
| `crm-visit-request-type` | literature Literatür / çalışma · sample Numune · patient_material Hasta materyali · training_invite Eğitim / toplantı daveti · msl_meeting MSL görüşmesi · other Diğer | BE-a |
| `crm-competitor-advantage` | efficacy Etkinlik · safety Güvenlik · dosing Doz kolaylığı · price Fiyat · reimbursement Geri ödeme · access Erişim / stok · evidence Kanıt düzeyi · brand Marka bilinirliği | BE-a |
| `crm-pharmacy-met-role` | pharmacist Eczacı · technician Eczane teknisyeni · owner Eczane sahibi · other Diğer | BE-a |
| `crm-pharmacy-stock-level` | none Yok · low Az · ok Yeterli · high Fazla | BE-a |
| `crm-shelf-position` | eye_level Göz hizası · upper Üst raf · lower Alt raf · behind_counter Tezgâh arkası · not_visible Görünmüyor | BE-a |
| `crm-sales-trend` | up Artıyor · flat Aynı · down Azalıyor | BE-a |
| `crm-recommendation-level` | high Yüksek · medium Orta · low Düşük · none Yok | BE-a |
| `mdm-reimbursement-status` | reimbursed Geri ödemede · conditional Geri ödemede · rapor şartlı · not_reimbursed Geri ödeme dışı · unknown Bilinmiyor | BE-b2 |

- Etiketler veri sahibinin; Referans Veri ekranından düzeltilir, betikle yamanmaz.
- `visit-objection-category` (W3) aynen kullanılır.

### 2.2 Doktor profili ve ürün potansiyeli (BE-c, CRM)
**`DoctorProfile`** (yeni toplam; koleksiyon `doctor_profiles`, anahtar TenantId + ContactId):
- `AdoptionTypologyCode?`, `CommunicationStyleCode?`, `KolLevelCode?`;
- `TotalMonthlyPatients?` (0–100 000) + `TotalPatientsAsOf?`;
- `Version`.
- **Geçmiş:** `doctor_profile_history` (alan, eski, yeni, kim, ne zaman, kaynak `report:{visitReportId}` | `manual`), salt ekleme.
- **En uygun gün / saat ve tercih edilen kanal kopyalanmaz.** Mevcut `ContactAvailability` ve `PreferenceRecord` (`preferred-channel`, `preferred-visit-window`) **okunur** ve yanıtta gösterilir.

**`DoctorProductPotential`** (koleksiyon `doctor_product_potentials`, anahtar TenantId + ContactId + GlobalProductId):
- `IndicationRows[]`: `IndicationNodeId`, `ProfileNodeId?`, `MonthlyPatients`;
- `NoPatientsInIndications` bool;
- `AsOf`;
- `ShareBucketCode?` + `ShareAsOf`;
- **hesaplanan** (saklanmaz, okumada):
  - `PotentialClass` (`low` / `medium` / `high`): endikasyon toplamı × ürün eşiği;
  - `OpportunityCode` (`opportunity` / `grow` / `keep` / `low_priority`): §0 matrisi.
- Geçmiş: `doctor_product_potential_history` (satır başına eski / yeni, kaynak).

**Kurallar:**
- Endikasyon toplamı ≤ `TotalMonthlyPatients` (ikisi de varsa); aşarsa 400 `potential_exceeds_total`.
- **3 ay kuralı:** `AsOf` 90 günden eskiyse `isDue = true` (temsilci yeniden sorar); değilse son değer salt okunur döner, temsilci "güncelle" ile yeniden girebilir.

**`ProductPotentialThresholds`** (koleksiyon `product_potential_thresholds`):
- anahtar TenantId + GlobalProductId (`null` = kiracı varsayılanı);
- `LowBelow` (varsayılan 10), `HighAbove` (varsayılan 30); kural `0 < LowBelow ≤ HighAbove`;
- belge yoksa varsayılan; geçmiş satırı.

**Uçlar** (`api/crm/doctor-insights`):

| uç | açıklama | izin |
|---|---|---|
| `GET doctors/{contactId}/profile` | profil + geçmiş özeti + uygunluk / kanal (okunur) + `displayNames` | `crm.doctor-insights.read` (görünürlük §2.4) |
| `PUT doctors/{contactId}/profile` | elle düzenleme (`If-Match` sürüm) | `crm.doctor-insights.manage` |
| `GET doctors/{contactId}/potentials?productIds=` | ürün başına satırlar + `isDue` + sınıf + fırsat + eşik | read |
| `GET products/{productId}/indication-profiles` | Kavram Grafiği'nden: ürün düğümü (`ExternalRefType=global-product`) –addresses→ endikasyon –leads-to→ profil; düz liste `{indicationNodeId, indicationName, profileNodeId?, profileName?}` | read |
| `GET thresholds` · `PUT thresholds/{productId\|default}` · `GET thresholds/history` | eşik ayarı | read / `crm.doctor-insights.thresholds.manage` |
| `GET opportunities?scope=mine\|team&productId=` | fırsat listesi (planlamada öne çıkarma için) | read |
| `GET aggregates?productId=` | pazarlama: sınıf × pay × fırsat sayıları, **doktor kimliği yok** | `crm.doctor-insights.read-aggregate` |

**Kavram türü eşlemesi:** tür kodları kiracı verisidir (seed `CT-IND`, `CT-PROF`, `CT-PROD`; belgelerdeki "CT-002 / CT-003" yanlış ad).
- Ayar: `Crm:DoctorInsights:ConceptTypes:Indication = CT-IND`, `:Profile = CT-PROF`.
- Ürün düğümü `ExternalRefType = global-product` ile bulunur.
- Eşleme yoksa liste boş + `reason: no_concept_graph` (rapor "endikasyon listesi tanımlı değil" gösterir, toplam hasta yine sorulur).

**Kaydedici seam** (BE-a kullanır): `IDoctorInsightsRecorder.RecordFromReportAsync(tenant, contactId, reportId, profileSuggestion, totalPatients?, perProduct[] {productId, rows?, noPatients?, shareBucket?})`.
- Doğrular (toplam kuralı), yazar, geçmişe `report:{id}` kaynağıyla ekler.
- BE-c uygular ve test eder; BE-a çağırır.

### 2.3 Fırsat matrisi (tek kaynak, CRM)
`OpportunityRules.Classify(potentialClass, shareBucket)`:

| | none / occasional | half | mostly / always |
|---|---|---|---|
| high | opportunity | grow | keep |
| medium | grow | grow | keep |
| low | low_priority | low_priority | keep |

- Sınıf yoksa (sayı yok) ya da pay yoksa → `null` (etiket gösterilmez).
- Web aynı tabloyu `report-core.js`'te **yalnız önizleme** için yineler; asıl değer sunucudan gelir.
- Testler sunucu tablosunu ölçer.

### 2.4 Görünürlük (K-W7)
- **Kendi doktorları:** `RepTerritoryCoverage` → `TerritorySubtree` → `AccountTerritoryAssignment` → kurumun doktorları (mevcut `GetMyAccountsQuery` zinciri).
- **Ekip:** kullanıcının `territory-subtree` kapsamlı aktif ataması olan düğümün alt ağacı. CRM'de yönetici kuralı bugün yok; yeni `TeamScopeResolver` bu kuralı kurar, seam `VisitEvidenceRepRoster` ile tutarlı.
- **`crm.doctor-insights.read-all`:** tüm kiracı (yönetim, test).
- **Pazarlama:** yalnız `aggregates` (≥ 5 doktor altı hücre gizlenir: `"<5"`).
- **Görünür olmayan doktor:** 404 (sızdırmaz).

### 2.5 Rapor v2 (BE-a, CRM `VisitReport` genişler — yeni toplam değil)
- `VisitReport.SchemaVersion` (1 = eski, 2 = yeni). Eski raporlar v1 okunur, v2 alanları boş.
- **v2 alanları:**
  - `General`: `VisitKindCode` (in_person | remote), `CoVisitorUserIds` (W3'ten), `MetPerson` (hekimde contact; eczanede `MetRoleCode`), `GeneralNote` (≤ 4000);
  - `ProductSections[]` (hekim);
  - `Pharmacy` (eczane);
  - `Safety`: `AdverseEvent` bool + `AdverseEventNote?` · `OffLabelRequest` bool (hekimde zorunlu, eczanede yalnız AE);
  - `FollowUp`: `Required`, `SuggestedDate?`, `Note`.
- **`ProductSections[]`** (hekim raporu, ürün başına):
  - `ProductId` (Global Ürün), `Unplanned` bool;
  - `PresentedSteps[]` (W3 `SlideTimings`'ten, salt okunur özet), `MatchedPlan` (sunucu);
  - `Minutes` (1–240, `ProductTimings`'ten ön dolar, düzeltilebilir);
  - `VisitSuccessCode`*, `CommitmentCode`*, `InterestCode`* (W3 işaretinden ön dolar);
  - `AdoptionStageCode`*;
  - `Potential` (`IndicationRows[]` | `NoPatientsInIndications`, yalnız `isDue` ya da temsilci güncellediyse zorunlu), `ShareBucketCode`*;
  - `Competitors[]` {`CompetitorProductId`, `NameSnapshot`, `Proposed` bool, `AdvantageCodes[]`, `DisadvantageCodes[]`} | `NoCompetitor`*;
  - `Objections[]` {`CategoryCode`, `ObjectionResponseId?` (W3 kütüphanesi), `Note`, `Resolved` (yes | partly | no)} | `NoObjection`*;
  - `Requests[]` {`TypeCode`, `DueDate?`, `Note`} | `NoRequest`*;
  - `Sample` {`GskuId`, `Quantity` 1–100, `Lot?`} | `NoSample`*, imza bilgisi W3'ten;
  - `NextMessage`* (≤ 500) + `NextMessageSuggestion` (sunucu: kullanım aşamasının sonraki adımı + taahhüt kuralı, ör. will_try → "Nasıl gitti? Deneme hastalarını sor").
  - `*` = zorunlu ya da "yok" seçeneği.
- **`Pharmacy`** (eczane raporu):
  - `MetRoleCode`*;
  - `Stock[]` {`GskuId`, `LevelCode`, `NearExpiry`, `ReturnIssue`};
  - `Shelf` {`PositionCode`, `MaterialStand`, `PhotoEvidenceId?` (MOD-0262, W3 kanıt deseni)};
  - `Sales[]` {`GskuId`, `TrendCode`*, `BoxesPerMonth?`};
  - `Competitors[]` (aynı yapı);
  - `Recommendation` {`LevelCode`, `LinkedContactIds[]`};
  - `Training` {`Given` bool, `Requests[]`};
  - `OrderPlaceholder` yok (W6).
- **Doktor düzeyi** (yalnız hekim raporu, gönderimde `IDoctorInsightsRecorder`'a gider):
  - `ProfileSuggestion` {`AdoptionTypologyCode?`, `CommunicationStyleCode?`, `KolLevelCode?`};
  - `TotalMonthlyPatients?` (`isDue` ise zorunlu).
- **Kurallar:**
  - Taslak serbest kaydedilir; **gönderimde** zorunluluk denetimi → 400 `report_incomplete` + `missing[]` (`{section, productId?, field}`).
  - Gönderimde: `JourneyProgress` ilerler (aşağıda) + kaydedici çağrılır + rakip önerileri MDM'ye iletilir.
  - **60 dk kuralı değişmez.** **Gerekçeli düzeltme artık eski ve yeni değeri saklar:** `Amendments[].Changes[] {path, old, new}` (mevcut yalnız alan adı yazan kusur kapanır).
- **Kullanım aşaması ↔ yolculuk:**
  - `ContentEngagementJourney` aşamasına isteğe bağlı `AdoptionStageCode` eklenir (BE-a alan + doğrulama; Web yolculuk düzenleyicisi seçici ekler — WEB-a kapsamında küçük ek).
  - Gönderimde, temsilci L aşamasını seçtiyse JourneyProgress, **L'den sonraki ilk eşlenmiş aşamaya** ilerler.
  - Eşleme yoksa sunulan aşamadan bir sonrakine (`Advance`).
  - Plan dışı ürün ilerletmez.
- **Uçlar** (`api/crm/visit-report`, mevcut denetleyici):

| uç | açıklama |
|---|---|
| `GET v2/prefill?plannedVisitId=` | yürütmeden ön doldurma + önceki değerler (son rapor + profil + potansiyel `isDue`) + endikasyon satırları + rakip seçenekleri (MDM) + seçenek setleri + hekim / eczane türü |
| `PUT v2/draft` | taslak (`If-Match`) |
| `POST v2/submit` | gönderim (eksik → 400 `missing[]`) |
| `POST {id}/amend` | v2 gövdesi kabul eder; eski / yeni değer |
| `GET {id}` | v1 / v2 okuma |
| `GET v2/competitor-usage?competitorIds=&months=3` | rakip başına toplu sayı: doktor, rapor, ürünümüze ve ülkeye göre; **doktor kimliği yok**, hücre < 5 → `"<5"`; birleştirilmiş kayıtlar hayatta kalana sayılır (MDM `mergedIntoId` okunur) — izin `crm.visit-report.read-aggregate` (yeni anahtar) |

- Rakip okuması MDM'den (gateway, çağıranın jetonu). MDM ulaşılamazsa `competitorsUnavailable: true` ve rapor yine gönderilebilir; rakip "kullanmıyor" seçilmeden geçilemez **yalnız liste geldiyse**.
- **İzinler:** mevcut `crm.visit-report.read` / `.record` / `.amend` + yeni `crm.visit-report.read-aggregate` (rakip kullanım sayıları; mockup fark belgesi G2).

### 2.6 MDM rakip ürün (BE-b2) ve Workflow (BE-b1)
**`CompetitorProduct`** (MDM, koleksiyon `mdm_competitor_products`):
- `Name`, `Aliases[]`, `Company`, `ActiveIngredients[]` (serbest etiket), `FormCode?` (MDM form sözlüğü), `Strength?`, `AtcCode?`;
- `CompetesWithGlobalProductIds[]` (≥ 1), `IndicationNodeIds[]` (CRM kavram düğümleri; yalnız kimlik saklanır);
- `Status` (`proposed` | `active` | `inactive` | `merged` | `rejected`), `MergedIntoId?`;
- `CountryPrices[]` {`MarketCode` (BRD `market`), `BoxPrice`, `CurrencyCode`, `ReimbursementStatusCode` (`mdm-reimbursement-status`), `ValidFrom`, `Source?`} + fiyat geçmişi `CountryPriceHistory[]`;
- `ProposedBy?`, `ProposedAt?`, `ProposedFromReportId?`;
- `Version`; değişiklik geçmişi.
- **Kural:** aynı kiracıda (`Company` + `Name`) normalize tekil.

**Öneri kararı** (`CompetitorProposalDecision`, onaylanabilir kayıt `IWorkflowApprovable`):
- `ProposalId`, `Action` (`complete` | `merge` | `reject`);
- `Payload` (complete → doldurulan alanlar; merge → `TargetId` + taşınacak ülke satırları; reject → `Reason`);
- `Status` (`draft` | `in_review` | `applied` | `returned`). Öneri kartı durumu: karar yok → "Karar bekliyor"; diğerleri bu alandan (fark belgesi G1).
- Onaylanınca uygulanır:
  - **complete** → `active`;
  - **merge** → öneri `merged` + `MergedIntoId`; ad hedefin `Aliases`'ına; ülke satırları taşınır;
  - **reject** → `rejected`.
- **SoD:** hazırlayan / son düzenleyen / gönderen onaylayamaz (W3 çekirdeği).

**Uçlar** (`api/mdm/competitor-products`):

| uç | izin |
|---|---|
| `GET` (süzgeçler: ürün, firma, etken madde, ülke, geri ödeme, durum, q; `includeProposed=mine` temsilci için) | `mdm.competitor-products.read` |
| `GET {id}` (merged ise `mergedIntoId` + hayatta kalanın özeti) | read |
| `GET lookup?productId=&indicationNodeId=&market=&q=` (rapor için kısa liste: aktif + çağıranın kendi önerileri; ülke kartı) | read |
| `POST` / `PUT {id}` / `POST {id}/deactivate` | `mdm.competitor-products.manage` |
| `POST proposals` (temsilci: ad + firma + productId + reportId?) → `proposed` | `mdm.competitor-products.propose` |
| `GET proposals` (+ benzer kayıtlar, **benzerlik 0–100**: küçük harf + Türkçe katlama + noktalama temizliği + trigram, diğer adlar dahil) | manage |
| `GET merge-candidates?sourceId=&q=` (hedef arama, benzerlik sıralı) | manage |
| `POST {id}/merge` (**iki aktif kayıt**, doğrudan, onaysız; `targetId`, taşınacak ülke satırları, `confirm=true`) | manage |
| `POST proposals/{id}/decision` (taslak) · `/submit` · `/withdraw` · `/decision/approve-reject` (Workflow görevinden) | manage |
| `GET {id}/usage` (CRM'den toplu kullanım sayısı; doktor kimliği yok) | read |

**Workflow:**
- Şablon `MDM-COMP-PROPOSAL`, `requestedObjectType = mdm.competitor-proposal`.
- Ayar `Mdm:CompetitorProposals:Workflow:TemplateCode`.
- Onaylayıcı POS-SALES-OPS (CFG).

**BE-b1 MDM altyapısı (CRM'den kopya, ortak kütüphane yok):**
- `WorkflowApprovalCore` (Kind / Store / Rules / Flow / OutcomeApplier / Reconciler, okumada uzlaştırma 120 sn) + `IWorkflowApprovable`.
- `GatewayWorkflowClient`: `Gateway:BaseUrl`, çağıranın Authorization + X-Tenant-Id; `/api/v1/workflow/instances|tasks|history`.
- **MassTransit RabbitMQ 8.3.6** tüketici:
  - `EventTransportMessage`, ad alanı `Diten.Platform.Application.Contracts.Eventing` (URN eşleşsin);
  - süzgeç `platform.workflow.instance.completed.v1`;
  - inbox `mdm_event_inbox`.
- **Güven:** olay yalnız kiracı + nesne + örnek açık turla eşleşirse uygulanır.
- **Açık kiracılı depo + CAS:** MDM `RepositoryBase` kiracıyı HTTP'den alır ve `UpdateAsync` sürüm denetimi yapmaz. Onaylanabilir kayıtlar için açık kiracılı yükle / sürüm filtreli değiştir eklenir.
- **Kanıt:** b1'de test-only sahte onaylanabilir kayıtla (b2 gerçek toplamı ekler).
- Yapılandırma `Eventing:Transport=RabbitMQ` iken etkin; yoksa tüketici kayıtlı değil, uygulama açılır.

### 2.7 Menü, rotalar, izinler
- **CRM manifest:** `CRM_DOCTOR_INSIGHT_THRESHOLDS` sayfası (`/CRM/PotentialThresholds`, CRM Ayarlar / Ziyaret; izin `crm.doctor-insights.thresholds.manage`). Rapor sayfası mevcut yürütme / çalışma alanı akışından açılır (WEB-a yer).
- **MDM manifest** (`ProductItemSkuMasterManifestProvider`): `COMPETITOR_PRODUCTS` (`/MasterDataManagement/CompetitorProducts`, `mdm.competitor-products.read`).
- **Gateway (ocelot):**
  - `/api/crm/doctor-insights` + `/{everything}`;
  - `/api/mdm/competitor-products` + `/{everything}` → 5059;
  - rapor v2 mevcut `/api/crm/visit-report/{everything}` altında.
  - Rota testleri.
- **Auth katalog** (`DataSeeder`): `crm.doctor-insights.read | manage | read-all | read-aggregate | thresholds.manage`, `mdm.competitor-products.read | manage | propose`. **Açıklamalar yalnız İngilizce.**
- **Nav L10n:** yeni sayfalar için `Nav.Page.*` 7 dil (`NavManifestL10nGuardTests`).

---

## W4-BE-c — doktor profili, potansiyel, eşik, fırsat, görünürlük (CRM)
1. §2.1 profil setleri (5 set) + tüketilebilir listeler (3 yer).
2. `DoctorProfile` + geçmiş, `DoctorProductPotential` + geçmiş, `ProductPotentialThresholds` + geçmiş; class map'ler (**GUID tuzağı:** yeni toplam `RegisterClassMaps`'e).
3. `OpportunityRules` (§2.3), `PotentialRules` (toplam kuralı, 90 gün `isDue`).
4. `ProductIndicationProfileReader` (Kavram Grafiği, ayarlı tür kodları; eşleme yoksa `no_concept_graph`).
5. `TeamScopeResolver` + görünürlük (§2.4), 404 sızdırmaz; aggregate < 5 gizleme.
6. `IDoctorInsightsRecorder` (BE-a için) + uçlar §2.2 + manifest sayfası + gateway + Auth anahtarları + Nav L10n.

**Acceptance:**
- Matris 15 hücre.
- Toplam aşımı 400.
- 90 gün sınırı (89 / 90 / 91).
- Eşik varsayılan / ürün / geçersiz (`LowBelow > HighAbove` → 400).
- Kavram eşlemesi var / yok.
- Görünürlük: kendi / ekip / read-all / yabancı → 404; aggregate < 5.
- Kaydedici geçmiş satırı + kaynak.
- Kiracı izolasyonu.

**Sabotajlar:**
- Matris "Çok × Yarı" = opportunity → kırmızı.
- Toplam kuralı kalksın → kırmızı.
- Ekip kapsamı tüm kiracı → kırmızı.

## W4-BE-b1 — MDM Workflow bağlantısı
§2.6 "BE-b1 MDM altyapısı" maddeleri.

**Acceptance:**
- Gönder → başlat çağrısı (idempotency `{type}:{id}:r{round}`).
- Onay olayı → uygula.
- Yabancı kiracı / yanlış örnek / kapalı tur → uygulanmaz.
- Tekrar olay → inbox ile tek uygulama.
- SoD (yazar / editör / gönderen onayı → taslak + `…_sod`).
- Uzlaştırma (120 sn sonra okumada geçmişten).
- CAS çakışması 409.
- RabbitMQ kapalıyken uygulama açılır.
- MDM test projesi + mimari yeşil.

**Sabotajlar:**
- Güven denetimi kalksın → kırmızı.
- `IsOwn` hep false → kırmızı.

## W4-BE-a — rapor v2 (CRM)
§2.1 rapor setleri (10 set) + §2.5 tümü + yolculuk aşamasına `AdoptionStageCode` + `IMdmCompetitorClient` (gateway; §2.6 `lookup` / `proposals` sözleşmesine göre; testte sahte).

**Acceptance:**
- **Ön doldurma:** W3 işaret / süre / numune / imza → alanlar.
- Önceki değer.
- `isDue` doğru.
- Gönderim zorunlulukları (her biri için `missing`).
- "Yok" seçenekleri.
- Eczane ayrımı (`TargetType = pharmacy`).
- AE zorunlu.
- JourneyProgress eşlemeli / eşlemesiz / plan dışı.
- Kaydedici çağrısı.
- Rakip önerisi MDM'ye.
- MDM yok → `competitorsUnavailable`.
- 60 dk.
- Düzeltmede eski / yeni değer.
- v1 rapor okunur.

**Sabotajlar:**
- AE zorunluluğu kalksın → kırmızı.
- Düzeltme eski değeri yazmasın → kırmızı.
- Plan dışı ürün ilerletsin → kırmızı.

## W4-BE-b2 — MDM rakip ürün
§2.6 tümü (b1 çekirdeğini kullanır) + `mdm-reimbursement-status` seti + manifest + gateway + Auth.

**Acceptance:**
- Tekillik (normalize).
- Ülke tekrar 400.
- Fiyat değişikliği geçmişe.
- Öneri yalnız önerene görünür (`includeProposed=mine`).
- **Karar akışları:**
  - complete → active;
  - merge → `MergedIntoId` + alias + ülke taşıma;
  - reject;
  - SoD.
- Merged okuma → hayatta kalan.
- Pasif seçilemez.
- Doğrudan düzenleme onaysız.
- Kiracı izolasyonu.

**Sabotajlar:**
- Merge alias eklemesin → kırmızı.
- Öneri herkese görünsün → kırmızı.

## W4-WEB-a — hekim raporu sihirbazı
**Mockup ile görsel karşılaştır:** 03-rapor (masaüstü + tablet + telefon), fark belgesi §1 değişiklikleri uygulanmış hâli.
1. **Sayfa:**
   - W3 yürütme bitince / takvimden "Raporu yaz" → rapor sihirbazı (yer: `VisitWorkspace` içinden açılan sayfa; mevcut `VisitExecution` rapor formu v2 için kullanılmaz, v1 okuma kalır).
   - **Adımlar:** Genel → ürün başına (A Sunum ve ilgi · B Pazar ve rakip · C Talepler ve sonraki adım) → Takip ve güvenlik → Doktor profili (kart) → Özet ve gönder. Sol adım listesi + eksik sayısı.
2. **Fark belgesi §1:**
   - yıldız yok → ziyaret başarısı + taahhüt;
   - pay kovaları;
   - endikasyon satırları + toplam hasta (doktor düzeyi) + "Aynısı" + 3 ay;
   - sınıf + fırsat önizlemesi (sunucu değeri esas);
   - rakip çipleri + kart (ülke) + "Listede yok, rakip ekle" (öneri; "onay bekliyor" etiketi) + isteğe bağlı + / − avantaj;
   - itirazlar (W3 kütüphane yanıtı satırda);
   - talepler, numune (GSKU, adet, lot, imza bilgisi);
   - sonraki mesaj önerisi;
   - AE / endikasyon dışı zorunlu;
   - profil önerisi (tipoloji, iletişim stili, KOL) + geçmiş bağlantısı.
3. **Özet ve gönder:**
   - eksik listesi → adıma git;
   - gönder;
   - 60 dk serbest düzeltme, sonra gerekçeli düzeltme (değişen alanlar, eski → yeni).
4. **Yolculuk düzenleyicisi** (Content Studio / ContentEngagementJourneys): aşamaya "Kullanım aşaması" seçici (BRD).
5. Taslak otomatik kaydı (çakışmada 409 → yeniden oku).
6. Seçenekler BRD'den, 7 dil, Arapça RTL, koyu tema, UAS-001.

**Acceptance:**
- **Saf kurallar Node'da:**
  - eksik hesabı;
  - toplam kuralı;
  - fırsat önizlemesi = sunucu tablosu;
  - sonraki mesaj önerisi gösterimi;
  - 60 dk sayacı.
- **Duman testi:**
  - ön doldurma;
  - "yok" seçenekleri;
  - rakip önerisi;
  - gönder → eksik 400 gösterimi;
  - düzeltme gerekçesi;
  - `createError` null.
- 7 dil.

**Sabotajlar:**
- AE sorusu atlanabilsin → kırmızı.
- Toplam kuralı kalksın → kırmızı.

## W4-WEB-b — eczane raporu + potansiyel eşik ayar sayfası
1. **Eczane raporu** (mockup 03-rapor eczane adımları):
   - Genel → Stok ve raf (fotoğraf: W3 kanıt deseni) → Sipariş ve satış (sipariş düğmesi pasif "yakında", eğilim zorunlu, kutu / ay isteğe bağlı) → Rakip ve tavsiye → Güvenlik (AE) → Özet.
   - WEB-a ile ortak çekirdek (`report-core.js`).
   - **Paralel çalışma:** WEB-a çekirdeği oluşturur; WEB-b çekirdeğe **yalnız eczane işlevleri ekler**, çakışmada WEB-a'nın imzası korunur.
2. **`/CRM/PotentialThresholds`** (standart CRM ayar deseni, VisitEvidenceSettings gibi):
   - kiracı varsayılanı + ürün satırları (Global Ürün seçici);
   - Az < X · Orta · Çok > Y;
   - kaydet onayı;
   - geçmiş;
   - menü (§2.7).
3. 7 dil, RTL, koyu tema, UAS-001.

**Acceptance:**
- Eczane eksik hesabı.
- Eğilim zorunlu.
- Fotoğraf yetkisi.
- Eşik doğrulaması (`LowBelow ≤ HighAbove`).
- Duman testi (eczane gönder; eşik kaydet + geçmiş).
- 7 dil.

**Sabotajlar:**
- Eğilim zorunluluğu kalksın → kırmızı.
- Eşik doğrulaması kalksın → kırmızı.

## W4-WEB-c — MDM rakip ürün yönetimi
**Mockup:** `mockups/visit-workspace/W4 Rakip Urunler (standalone).html` (çözülmüş `decoded-w4-rakip/Rakip.dc.html`).
**Fark belgesi:** [DESIGN-GAP-W4-competitor-2026-10-13](mockups/visit-workspace/DESIGN-GAP-W4-competitor-2026-10-13.md) — §1 aynen, §2 G1–G6 uygulanır.

1. **`/MasterDataManagement/CompetitorProducts`** (MDM Ürünler deseni: liste + ayrı yeni / düzenle / ayrıntı sayfaları, bölüm kartları):
   - **R1:** iki sekme;
   - **R2:** ülke tablosu + fiyat geçmişi + temsilci kartı önizlemesi;
   - **R3:** değişiklik geçmişi, raporlarda kullanım (CRM toplu ucu, G2), birleştirme izi, "Pazar verisi · yakında";
   - **R7:** salt okuma, UAS-001, 409 çakışma "Yenile".
2. **Öneri kararları Workflow ile (G1):**
   - **R4 / R5 / R6:** taslak karar → "Onaya gönder";
   - kart durum çipleri;
   - "Geri çek";
   - SoD kutusu;
   - onaylayıcı görünümü (Görev Merkezi derin bağlantısı): "Onayla" / "Taslağa geri gönder" (not zorunlu).
   - Desen: W3 itiraz kütüphanesi (`objection-details.js`, `objection-common.js`).
3. **R3 "Başka kayıtla birleştir"** (iki aktif kayıt): doğrudan, 3 adım, son düğme "Birleştir".
4. **Endikasyon seçici** CRM'den (G3); **form** MDM sözlüğünden (G4); **benzerlik yüzdesi** sunucudan (G5).
5. Web vekilleri (MDM + CRM `doctor-insights` / `visit-report` toplu); 7 dil, Arapça RTL, koyu tema, menü.

**Acceptance:**
- **Saf kurallar Node'da:**
  - eylem uygunluğu (durum × yetki × SoD);
  - ülke tekrar engeli;
  - fiyat dönemi (yeni satır değil);
  - birleştirme adımı ilerleme koşulları (hedef seçili, onay kutusu);
  - "<5" gösterimi.
- **Duman testi:**
  - liste;
  - öneri → onayla-tamamla taslak → onaya gönder;
  - birleştir 3 adım → onaya gönder;
  - reddet gerekçesiz engel;
  - doğrudan birleştir;
  - 409 Yenile;
  - `createError` null.
- 7 dil.

**Sabotajlar:**
- Birleştirmede onay kutusu atlanabilsin → kırmızı.
- Hazırlayan kendi kararını onaylayabilsin → kırmızı.

## W4-CFG — Workflow şablonu + yetkiler (canlı yapılandırma, kod YOK)
**Şablon `MDM-COMP-PROPOSAL`:**
- tek adım `sales_ops` "Satış operasyon onayı";
- aday POS-SALES-OPS;
- SLA 4320 / 2880;
- `rejectCommentRequired: true`;
- `requestedObjectType: mdm.competitor-proposal`.

**Grant betiği** `scripts/rbac/grant_w4_report_rbac_97c5.py` (W3-CFG deseni):
- Admin'e yeni CRM + MDM anahtarları; **`crm.doctor-insights.read-all` açık-atama listesinde** (karar 2026-10-13: otomatik yol vermez, Admin'ler yine görsün) → betik Admin'e **açıkça** verir;
- temsilci rolüne `mdm.competitor-products.propose` + `read`, `crm.doctor-insights.read`;
- pazarlama rolüne `read-aggregate` + `thresholds.manage` + `mdm.competitor-products.manage` (rol yoksa DUR + raporla).

---

## §36.1 Agent Prompt — W4-BE-c (paste-ready · 1. TUR)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W4 · W4-BE-c — CRM doktor profili (+ geçmiş), ürün potansiyeli (+ 3 ay / toplam kuralı), ürün başına potansiyel eşikleri, fırsat matrisi, görünürlük (kendi / ekip / read-all / pazarlama toplu), ürün → endikasyon × profil okuması (Kavram Grafiği), IDoctorInsightsRecorder, 5 BRD seti
Repository: C:\tmp\vw-w4-be-c (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w4-be-c · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W4-report-v2.md — önce oku: §0 kararlar, §2.1 (profil setleri: crm-adoption-stage, crm-share-bucket, crm-adoption-typology, crm-communication-style, crm-kol-level), §2.2, §2.3, §2.4, §2.7, "W4-BE-c". Fark belgesi mockups/visit-workspace/DESIGN-GAP-W4-report-2026-10-13.md §1.3, §1.6, §3.
DESEN: ayar = VisitEvidencePolicy (Features/VisitEvidence, VisitEvidencePolicyReads.cs); ürün doğrulama = IStrategyTemplateProductReferenceValidator (ObjectionResponseHandlers.cs 60-78); BRD okuma = GatewayReferenceDataValidator; kapsam = RepTerritoryCoverage / TerritorySubtree / GetMyAccountsQuery, VisitEvidenceRepRoster; Kavram Grafiği = KnowledgeConcept* (ConceptNode ExternalRefType=global-product, ConceptRelationship addresses / leads-to). BRD seed: services/Diten.Platform/src/Diten.Platform.API/Seed/business-reference-data/*.json (tenant, scope_key 97c5, 7 dil) + ConsumableSets 3 yer (appsettings.json, appsettings.Development.json, BusinessReferenceDataConsumableSetsOptions.DefaultConsumableSets) — drift testleri yeşil.
NE: §2.2 toplamlar + geçmişler + class map (yeni toplam RegisterClassMaps'e — GUID tuzağı); §2.3 OpportunityRules tek kaynak; PotentialRules (toplam ≤, 90 gün isDue); ProductIndicationProfileReader (tür kodları ayardan: Crm:DoctorInsights:ConceptTypes:Indication=CT-IND, :Profile=CT-PROF; eşleme yoksa no_concept_graph); TeamScopeResolver + görünürlük (yabancı 404, aggregate <5 gizli); IDoctorInsightsRecorder (BE-a çağıracak); uçlar api/crm/doctor-insights/* (§2.2 tablo); CRM manifest sayfası CRM_DOCTOR_INSIGHT_THRESHOLDS (/CRM/PotentialThresholds) + Nav.Page 7 dil; ocelot /api/crm/doctor-insights (+/{everything}) + rota testi; Auth DataSeeder anahtarları crm.doctor-insights.read|manage|read-all|read-aggregate|thresholds.manage (açıklama İNGİLİZCE).
KORU/YAPMA: Contact toplamına alan EKLEME (DoctorProfile ayrı); ContactAvailability / PreferenceRecord kopyalanmaz, okunur; AUD-001 için borç satırı EKLEME (Faz 8); Web sayfası YOK (WEB-b); rapor kodu YOK (BE-a).
DOĞRULA: "W4-BE-c" acceptance + 3 sabotaj (.bak + touch ile geri al, git checkout YOK); CRM + Platform (BRD drift) + Gateway + Web (NavL10n) + mimari (-o ile repo içi çıktı); denetimsiz sayı önce / sonra raporda.
Commit: "feat(crm): WP-VW-W4-BE-c — doctor profile + history, product potential (3-monthly, total rule), per-product thresholds, opportunity matrix, visibility (own/team/aggregate), product indication-profile read, insights recorder, 5 reference sets" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: uçlar, test sayıları önce / sonra, sabotaj kanıtı, sapmalar. §22 TÜRKÇE. K13.
```

## §36.1 Agent Prompt — W4-BE-b1 (paste-ready · 1. TUR, BE-c ile paralel)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W4 · W4-BE-b1 — MDM Workflow bağlantısı: onay çekirdeği (CRM WorkflowApprovalCore kopyası), gateway Workflow istemcisi, MassTransit RabbitMQ sonuç tüketicisi + inbox, açık kiracılı CAS deposu; test-only sahte onaylanabilir kayıtla kanıt
Repository: C:\tmp\vw-w4-be-b1 (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w4-be-b1 · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W4-report-v2.md — önce oku: §0, §2.6 ("BE-b1 MDM altyapısı" + Workflow), "W4-BE-b1".
KAYNAK (kopyalanacak, ortak kütüphane YOK): services/Diten.CrmService/src/Diten.CrmService.Application/Features/Approvals/WorkflowApprovalCore.cs, Domain/Entities/WorkflowApprovable.cs, Infrastructure/Workflow/GatewayClaimWorkflowClient.cs, Infrastructure/Eventing/ClaimWorkflowOutcomeConsumer.cs + EventTransportMessage.cs (ad alanı Diten.Platform.Application.Contracts.Eventing KORUNUR — MassTransit URN), Infrastructure/DependencyInjection.cs 162-188 + AddClaimWorkflowEventing (222+), CrmEventingOptions, crm_event_inbox. Platform: WorkflowInstanceCompletedV1, olay adı platform.workflow.instance.completed.v1; başlatma için kayıt gerekmez (yalnız yayınlanmış şablon).
MDM HEDEF: services/Diten.MdmService (Api / Application / Domain / Infrastructure / Persistence). Bugün MassTransit / Eventing / Gateway ayarı / inbox YOK; RepositoryBase kiracıyı HTTP'den alır, UpdateAsync sürüm denetlemez → onaylanabilir kayıtlar için açık kiracılı yükle + sürüm filtreli değiştir ekle (mevcut Brand / Product davranışını DEĞİŞTİRME).
NE: MDM'ye Features/Approvals (Kind, Store<T>, Rules.IsOwn, Flow submit / withdraw / decide, OutcomeApplier.Handles(objectType "mdm.*"), Reconciler okumada 120 sn), IWorkflowApprovable, GatewayWorkflowClient (Gateway:BaseUrl, çağıranın Authorization + X-Tenant-Id), MdmWorkflowOutcomeConsumer (MassTransit.RabbitMQ 8.3.6, Eventing:Transport=RabbitMQ iken), mdm_event_inbox, güven denetimi (kiracı + nesne + örnek açık turla eşleşmeli), appsettings Gateway / Eventing (Development değerleri CRM ile aynı yerel RabbitMQ), Mdm:CompetitorProposals:Workflow:TemplateCode=MDM-COMP-PROPOSAL ayarı (b2 kullanacak). Kanıt: test-only sahte onaylanabilir kayıt (üretim toplamı YOK — b2'de gelir).
KORU/YAPMA: CRM kodunu DEĞİŞTİRME (yalnız kopya); MDM'de yeni uç / menü / kalıcı toplam YOK; AUD-001 borç satırı EKLEME; fleet'i durdurma.
DOĞRULA: "W4-BE-b1" acceptance + 2 sabotaj (.bak + touch); MDM test projesi + mimari + CRM (değişmedi) (-o repo içi). MDM servisinin RabbitMQ olmadan açıldığını testle göster.
Commit: "feat(mdm): WP-VW-W4-BE-b1 — workflow approval core, gateway workflow client, RabbitMQ outcome consumer + inbox, explicit-tenant CAS store" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: dosyalar, test sayıları, sabotaj, sapmalar. §22 TÜRKÇE. K13.
```

## §36.1 Agent Prompt — W4-BE-a (paste-ready · 2. TUR, BE-c test dalına alındıktan SONRA)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W4 · W4-BE-a — CRM rapor v2 (hekim + eczane): VisitReport SchemaVersion 2, ön doldurma (W3 yürütme), önceki değer + isDue, gönderim zorunlulukları + missing[], gerekçeli düzeltmede eski / yeni değer, JourneyProgress ilerletme (kullanım aşaması eşlemesi), IDoctorInsightsRecorder çağrısı, MDM rakip istemcisi + öneri iletimi, 10 BRD seti
Repository: C:\tmp\vw-w4-be-a (worktree, test/crm-content-visit-e2e güncel başı — BE-c içerir) · Branch: wp/vw-w4-be-a · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W4-report-v2.md — önce oku: §0, §2.1 (rapor setleri), §2.5 tümü, §2.6 (lookup / proposals sözleşmesi), "W4-BE-a". Fark belgesi DESIGN-GAP-W4-report-2026-10-13.md §1–§2 (asıl şartname). BE-c §37 notu (IDoctorInsightsRecorder imzası).
MEVCUT: Domain/Entities/VisitReport.cs (ContentActuals, Samples, Feedback, Amendments — düzeltme bugün yalnız alan adı yazıyor), Features/VisitReport (SubmitVisitReportHandler 60 dk / 48 sa, AmendVisitReportHandler), VisitReportController (api/crm/visit-report), VisitExecution (Marks, ProductTimings, SlideTimings, Signature, PersonalNotes — kişisel notlar rapora GİTMEZ), VisitExecutionReportBinding, JourneyProgress.Advance (çağıran yok — SB-3c), ContentEngagementJourney.Stages, PlannedVisit.TargetType=pharmacy, MOD-0262 kanıt deseni (W3 fotoğraf).
NE: §2.5 v2 alanları (VisitReport genişler, yeni toplam değil; v1 okuma bozulmaz); uçlar v2/prefill, v2/draft, v2/submit, {id}/amend (v2), {id}; ContentEngagementJourney aşamasına AdoptionStageCode (isteğe bağlı) + doğrulama; ilerletme kuralı (L'den sonraki ilk eşlenmiş aşama; eşleme yoksa Advance; plan dışı ilerletmez); IMdmCompetitorClient (gateway, çağıranın jetonu; lookup + proposals; MDM yoksa competitorsUnavailable); 10 BRD seti + ConsumableSets 3 yer; AE / endikasyon dışı zorunlu; v2/competitor-usage toplu ucu (doktor kimliği yok, <5 gizli, merged → hayatta kalan) + yeni izin crm.visit-report.read-aggregate (Auth DataSeeder, açıklama İNGİLİZCE) — mockup fark belgesi DESIGN-GAP-W4-competitor-2026-10-13.md G2.
KORU/YAPMA: v1 rapor ve W1–W3 akışları (outcome / missed / rescheduled) bozulmaz; Contact'a alan EKLEME; AUD-001 borç satırı EKLEME; Web YOK.
DOĞRULA: "W4-BE-a" acceptance + 3 sabotaj (.bak + touch); CRM + Platform (BRD drift) + mimari (-o repo içi); denetimsiz sayı önce / sonra.
Commit: "feat(crm): WP-VW-W4-BE-a — visit report v2 (doctor + pharmacy), prefill from execution, previous values, submit completeness, amend old/new values, journey advance by adoption stage, competitor client + proposals, 10 reference sets" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.1 Agent Prompt — W4-BE-b2 (paste-ready · 2. TUR, BE-b1 test dalına alındıktan SONRA; BE-a ile paralel)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W4 · W4-BE-b2 — MDM rakip ürün: CompetitorProduct (diğer adlar, etken madde etiketleri, ülke fiyat / geri ödeme + fiyat geçmişi, ürünlerimiz + endikasyon bağı), temsilci önerisi, öneri kararı (tamamla / birleştir / reddet) Workflow onayıyla (b1 çekirdeği), birleştirme işaretçisi, menü, gateway, yetkiler, mdm-reimbursement-status seti
Repository: C:\tmp\vw-w4-be-b2 (worktree, test/crm-content-visit-e2e güncel başı — b1 içerir) · Branch: wp/vw-w4-be-b2 · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W4-report-v2.md — önce oku: §0 (K-W6), §2.6 tümü, §2.7, "W4-BE-b2". Brif + kararlar: mockups/visit-workspace/BRIEF-W4-competitor-products.md. b1 §37 notu.
DESEN: MDM Brand (Domain/Entities/Brand.cs, Features/Brand, BrandRepository, BrandProductClassMaps, BrandsController api/mdm/brands, izinler mdm.brands.*), menü ProductItemSkuMasterManifestProvider, Auth DataSeeder BrandProductPermissionKeys (97c5 grant), ocelot /api/mdm/brands çifti → 5059, BRD market seti (ülke), MDM form sözlüğü (BrandProductVocabulary DosageForm).
NE: §2.6 toplam + kurallar + uçlar (lookup, proposals, decision / submit / withdraw, usage sayısı için CRM'e soru YOK — usage şimdilik 0 / "yakında" alanı, CRM toplu sayımı W7); CompetitorProposalDecision IWorkflowApprovable + OutcomeApplier (complete / merge / reject); merge: MergedIntoId + alias + ülke taşıma; benzerlik 0–100 (proposals + merge-candidates, Türkçe katlama + trigram, diğer adlar dahil); iki AKTİF kaydın doğrudan birleşmesi POST {id}/merge (onaysız, confirm=true); mockup fark belgesi DESIGN-GAP-W4-competitor-2026-10-13.md G1 / G4 / G5; okumada merged → hayatta kalan; includeProposed=mine; MDM manifest COMPETITOR_PRODUCTS + Nav.Page 7 dil; ocelot /api/mdm/competitor-products (+/{everything}) + rota testi; Auth mdm.competitor-products.read|manage|propose (açıklama İNGİLİZCE); mdm-reimbursement-status BRD seti (global değil, tenant) + ConsumableSets.
KORU/YAPMA: Brand / Product / GSKU davranışı değişmez; silme YOK (pasif / merged); AUD-001 borç satırı EKLEME; Web sayfası YOK (WEB-c mockup sonrası).
DOĞRULA: "W4-BE-b2" acceptance + 2 sabotaj (.bak + touch); MDM + Platform (BRD drift) + Gateway + Web (NavL10n) + mimari (-o repo içi).
Commit: "feat(mdm): WP-VW-W4-BE-b2 — competitor products (aliases, ingredients, country price + reimbursement history), rep proposals, complete/merge/reject via workflow, merged pointer, menu, gateway, permissions" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.1 Agent Prompt — W4-WEB-a (paste-ready · 3. TUR, BE-a + BE-c + BE-b2 test dalında)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W4 · W4-WEB-a — hekim raporu sihirbazı v2 (mockup 03-rapor + fark belgesi kararları) + yolculuk aşamasına "Kullanım aşaması" seçici
Repository: C:\tmp\vw-w4-web-a (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w4-web-a · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W4-report-v2.md — önce oku: §0, §2.2–2.5 (uçlar), "W4-WEB-a"; BE-a / BE-c / BE-b2 §37 notları. Mockup: mockups/visit-workspace/decoded/03-rapor.html (+ 00-demo-veri.js, 00-tema.css; ana dosya "Ziyaret Calisma Alani (standalone).html" rapor ekranı). Fark belgesi DESIGN-GAP-W4-report-2026-10-13.md §1 (✏️ / ➕ / ➖ uygulanır; kararlar mockup'ın önüne geçer).
NE: "W4-WEB-a" 1–6; ortak çekirdek wwwroot/assets/js/CRM/VisitReport/report-core.js (saf kurallar: eksik hesabı, toplam kuralı, fırsat önizlemesi, 60 dk sayacı — WEB-b eczane işlevlerini buna EKLEYECEK, imzaları sabit tut); Web vekilleri (api/crm/visit-report/v2/*, api/crm/doctor-insights/*, api/mdm/competitor-products/lookup + proposals); W3 VisitWorkspace / VisitPresentation akışından "Raporu yaz" girişi; ContentEngagementJourneys aşama düzenleyicisine Kullanım aşaması seçici; 7 dil (yankısız), Arapça RTL, koyu tema, UAS-001.
KORU/YAPMA: v1 rapor ekranı (VisitExecution) okuma için kalır; kullanıcının giriş yaptığı sekmeye mock / harness enjekte etme — ayrı sekme; eczane raporu YOK (WEB-b).
DOĞRULA: "W4-WEB-a" acceptance + 2 sabotaj (.bak + touch); Web tam takım (-o repo içi); mockup ile görsel karşılaştırma ayrı sekmede (masaüstü + tablet + telefon), farkları rapora.
Commit: "feat(web): WP-VW-W4-WEB-a — doctor report wizard v2 (adoption stage, potential + total rule, share buckets, opportunity, competitors + proposals, success + commitment, safety, profile suggestion, submit / amend) + journey stage adoption picker" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.1 Agent Prompt — W4-WEB-b (paste-ready · 3. TUR, WEB-a ile paralel)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W4 · W4-WEB-b — eczane raporu (mockup 03-rapor eczane adımları) + /CRM/PotentialThresholds eşik ayar sayfası (standart CRM ayar deseni)
Repository: C:\tmp\vw-w4-web-b (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w4-web-b · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W4-report-v2.md — önce oku: §0, §2.1, §2.2 (eşik uçları), §2.5 (Pharmacy), §2.7, "W4-WEB-b". Fark belgesi §2. Desen: Views/CRM/VisitEvidenceSettings + visit-evidence-settings.js (ayar + kaydet onayı + geçmiş), W3 fotoğraf kanıtı (VisitPresentation / VisitWorkspace start).
NE: "W4-WEB-b" 1–3 + RAF FOTOĞRAFI UCU (CRM, küçük, BE-a §37 açık 2): POST api/crm/visit-report/v2/{plannedVisitId}/shelf-photo (multipart, yalnız eczane planı, W3 başlatma fotoğrafı deseni: MOD-0262 CrmVisitEvidence kapsamı, aynı izin / kapsam / boyut + tür sınırları, yanıt { photoEvidenceId }) + Web vekili [DisableFormValueModelBinding] + gövde tamponlama (MultipartProxyBodyWebTests deseni); taslak Pharmacy.Shelf.PhotoEvidenceId bu kimliği taşır. ORTAK ÇEKİRDEK: WEB-a report-core.js'i oluşturuyor (paralel). Sen eczane kurallarını ayrı dosyada tut (report-pharmacy-core.js) ve report-core.js'e yalnız açık bir genişletme noktası üzerinden bağlan; report-core.js'i yeniden yazma. Birleşmede çakışırsa WEB-a imzası korunur.
KORU/YAPMA: sipariş düğmesi pasif ("yakında", W6); kullanıcının giriş yaptığı sekmeye mock / harness enjekte etme.
DOĞRULA: "W4-WEB-b" acceptance + 2 sabotaj (.bak + touch); Web tam takım (-o repo içi); mockup görsel karşılaştırma ayrı sekmede.
Commit: "feat(web): WP-VW-W4-WEB-b — pharmacy report (stock, shelf photo, sales trend, competitors, recommendation, safety) + potential thresholds settings page" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.1 Agent Prompt — W4-WEB-c (paste-ready · 4. TUR, BE-b2 + BE-a + BE-c test dalında)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W4 · W4-WEB-c — MDM rakip ürün yönetimi (liste + öneriler, yeni / düzenle + ülke fiyat / geri ödeme + geçmiş, ayrıntı, öneri kararları Workflow ile, doğrudan birleştirme, salt okuma / yetkisiz / çakışma, AR)
Repository: C:\tmp\vw-w4-web-c (worktree, test/crm-content-visit-e2e güncel başı = main 19497c934, PR #141 sonrası) · Branch: wp/vw-w4-web-c · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W4-report-v2.md — önce oku: §0 (K-W6), §2.6, §2.7, §2.5 (competitor-usage), "W4-WEB-c"; BE-b2 / BE-a / BE-c §37 notları + WEB-a / WEB-b §37 (I2: eczane önerisi planın ilk ürünüyle gelir).
ÖN KOŞUL (yerel 97c5): Workflow şablonu MDM-COMP-PROPOSAL yerelde CT tarafından KURULDU (4ca5b242, v1 Published, aday POS-SALES-OPS) — "Onaya gönder" duman testinde kullanılabilir; şablona DOKUNMA. Şablonsuz kiracı için MDM 409 workflow_template_missing → arayüz anlaşılır mesaj göstermeli (Node testiyle). Admin'de mdm.competitor-products.* yoksa W4-CFG yetki betiği gerekir (paralel paket) → eksikse dur, raporla. Canlı (be823006) şablon + yetkiler zaten kuruldu — canlıya DOKUNMA. Mockup: execution/domains/commercial-suite/work-packs/mockups/visit-workspace/W4 Rakip Urunler (standalone).html (çözülmüş decoded-w4-rakip/Rakip.dc.html; 15 ekran seçicide). Fark belgesi: mockups/visit-workspace/DESIGN-GAP-W4-competitor-2026-10-13.md (§1 aynen, §2 G1–G6).
DESEN: MDM Ürünler sayfaları (frontend/Diten.Web/Views/MasterData/Products/*, Controllers/MasterData/ProductsController.cs — liste + ayrı sayfalar + bölüm kartları); Workflow karar akışı + SoD + onaylayıcı görünümü = W3 itiraz kütüphanesi (Views/CRM/ObjectionResponses, wwwroot/assets/js/CRM/ObjectionResponses/*); L10n köprüsü parantez koruması (RazorInlineScriptSyntaxTests yeşil kalmalı).
NE: "W4-WEB-c" 1–5; vekiller MDM api/mdm/competitor-products/* + CRM api/crm/doctor-insights/products/{id}/indication-profiles + api/crm/visit-report/v2/competitor-usage; MDM menü sayfası (BE-b2 manifest) + Nav.Page 7 dil.
KORU/YAPMA: telefon düzeni gerekmez (masaüstü + tablet); pazar payı / satış yok (W7 yer tutucu); kullanıcının giriş yaptığı sekmeye mock / harness enjekte etme — ayrı sekme.
DOĞRULA: "W4-WEB-c" acceptance + 2 sabotaj (.bak + touch); Web tam takım (-o repo içi); mockup ile görsel karşılaştırma ayrı sekmede (15 ekran + G1 Workflow durumları), farkları rapora.
Commit: "feat(web): WP-VW-W4-WEB-c — MDM competitor products (list + proposals, form + country price/reimbursement history, detail, proposal decisions via workflow, direct merge, read-only / unauthorized / conflict, AR)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.1 Agent Prompt — W4-CFG (paste-ready · 4. TUR, BE-b2 test dalında + fleet yeniden başlatıldıktan SONRA)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W4 · W4-CFG — W4 yetki betiği (dry-run varsayılan) — tenant 97c5, uygulama KODU YOK; Workflow şablonu MDM-COMP-PROPOSAL CT tarafından zaten kuruldu (yalnız doğrula)
Repository: C:\tmp\vw-w4-cfg (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w4-cfg · commit (rapor + betik) bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W4-report-v2.md — "W4-CFG" + §2.6 + §2.7 + BE-b2 §37 notu. DESEN: scripts/rbac/grant_w3_authoring_rbac_97c5.py (dry-run, subtype-4 GUID, tarih biçimi yerel belgeden, marker, rol / izin OLUŞTURMAZ, idempotent) + WP-VW-W3-authoring.md §37 W3-CFG (rapor biçimi).
ŞABLON DURUMU (yerel 97c5, 2026-10-13 CT): MDM-COMP-PROPOSAL 4ca5b242-2cf2-44ca-be80-00e0835315c9, v1 65679ab1 Published, tek adım sales_ops, aday position:c1acdf12-40d2-4b2c-a2a4-afe1bdd7681d (POS-SALES-OPS), SLA 4320 / 2880, options.rejectCommentRequired true, requestedObjectType mdm.competitor-proposal. YENİDEN OLUŞTURMA / YAYIMLAMA YOK — yalnız salt okuma doğrula (Mongo workflow_templates / workflow_template_versions).
VERİ YAZMA YETKİSİ (yalnız): betiğin YAZILMASI (çalıştırmayı kullanıcı yapar). Şablon, rol, izin, kayıt yazımı YOK.
NE: betik scripts/rbac/grant_w4_report_rbac_97c5.py: Admin'e W4 CRM + MDM anahtarları (crm.doctor-insights.* — read-all ExplicitGrantOnly olduğu için yalnız bu betikle Admin'e gider, unutma; crm.visit-report.read-aggregate, mdm.competitor-products.read|manage|propose) + MDM'nin Workflow çağrısı için platform.workflow.instances.start / view, tasks.approve / reject / cancel (eksikse); temsilci / pazarlama rolleri varsa "W4-CFG" listesi (temsilciye ayrıca mdm.gskus.read — eczane stok / satış satırları ve numune GSKU listesi için, WEB-b §37), yoksa DUR + raporla. Auth kataloğunda anahtarların varlığını önce doğrula.
KORU/YAPMA: kod değişikliği YOK; diğer şablonlara DOKUNMA (parmak izi önce / sonra aynı); örnek başlatma YOK. YALNIZ yerel 97c5: canlı (be823006, ditenteknoloji.com) 2026-10-13'te CT + kullanıcı tarafından kuruldu (scripts/golive/workflow-templates-live.js, grant-roles-live.js; ülke pozisyonları *RO-SALES-OPS / *RO-MED-DIR) — canlıya DOKUNMA. Temsilci rolü canlıda CrmUser; yerelde temsilci rolü yoksa DUR + raporla.
DOĞRULA: şablon (salt okuma) Published + tek adım + doğru aday + options.rejectCommentRequired; betik dry-run çıktısı rapora (rol başına eklenecek / zaten var).
Commit: "chore(mdm): WP-VW-W4-CFG — W4 RBAC grant script (dry-run default); MDM-COMP-PROPOSAL template verified" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22), kullanıcıya --apply + yeniden giriş talimatı. K13.
```

---

## §37 CT kabul — W4-BE-c + W4-BE-b1 E2 ACCEPTED (2026-10-13)

**Kaynak:** kullanıcının ajanları.
- BE-c `02544ce27` (`wp/vw-w4-be-c`) → test dalında `2a07d0864`.
- BE-b1 `9ff6e07a0` (`wp/vw-w4-be-b1`) → test dalında `5f4f79f78`. Çakışma yok: b1 yalnız MDM'ye dokunuyor.

**CT ölçümü (ikisi birlikte, test dalı başında):**

| takım | sonuç |
|---|---|
| CRM | **2681/0/5** (2643 + 38) |
| MDM | **484/3**; 3 kırmızı (`ProductItemSkuMasterManifestProviderTests`) tabanda da var |
| Web | **955/0** (Nav L10n yeşil) |
| Gateway | **122/1** (yalnız önceden var olan 5063–5065) |
| Auth | **1120/0** |
| Platform (BRD + CRM manifest süzgeci) | 241/14: 9 Mongo (yerel `mongod` yok, ortam) + 5 `VerifiedMarket` / `VerifiedGsku` uygunluk testi. **CT, bu 5 testi taban commit'te ayrı çalışma klasöründe koştu: aynı 5 kırmızı.** Yeni seed dosyasından kaynaklanmıyor. |
| Mimari | denetimsiz **47 → 49** (`UpdateDoctorProfileCommand`, `UpdatePotentialThresholdCommand`; borç satırı yok, Faz 8) |

**CT kod okuması:**
- **BE-c:**
  - `OpportunityRules.Classify` §2.3 tablosuyla birebir.
  - Görünürlük her adımda `TenantId` süzüyor. Ataması olmayan, `read-all`'ı da olmayan kullanıcı → `Nobody` (sızdırmaz).
  - Ekip = `territory-subtree` atamasının alt ağacı.
- **BE-b1:**
  - Tüketici yalnız `platform.workflow.instance.completed.v1` + `mdm.*` alır. Zarf / yük kiracısı eşleşmesi zorunlu; inbox tekrarları eler.
  - CAS deposu açık kiracı + sürüm filtreli.
  - Not: olay uygulanmadan önce inbox'a yazılıyor; uygulama çökerse olay kaybolur, okumadaki 120 sn uzlaştırma telafi eder (CRM ile aynı desen).

**CT sabotajları** (`.bak` + touch ile geri alındı; ayrı `-o` çıktısı):
- BE-c `IsDue` sınırı `>` → `>=` → "90 günlük değer güncel" testi kırmızı.
- BE-b1 tüketicide inbox tekrar denetimi devre dışı → "tekrar olay tek uygulanır" testi kırmızı.

**Kabul edilen sapmalar:**
- **BE-c:**
  - setler yeni `crm-visit-report-v2-reference.json` dosyasında (BE-a buraya ekler);
  - `no_indications` ayrı neden;
  - anahtarlar hem DataSeeder hem manifestte.
- **BE-b1:**
  - tek `IMdmWorkflowClient`;
  - tür başına kayıt deseni (b2 rakip öneri kararını kaydeder);
  - 409 `concurrency_conflict` kodu;
  - açık turda yeniden gönderim 409;
  - boş `Gateway:BaseUrl` → yerel varsayılan.

**Açık:**
- **`crm.doctor-insights.read-all` açık-atama listesinde değil** (`ExplicitGrantOnlyPermissions`). Modül yetki senkronizasyonu Admin'lere otomatik verebilir → Admin tüm doktorları görür. Öneri: `crm.planned-visit.read-all` gibi listeye eklensin (kullanıcı kararı bekleniyor).
- **BE-b2'ye not:** b1'in Görev Merkezi derin bağlantısı geçici (`/MDM/CompetitorProducts/Proposals/{id}`). Asıl sayfa `/MasterDataManagement/CompetitorProducts` (§2.7). b2 bağlantıyı bu yola göre kesinleştirir (ör. `/MasterDataManagement/CompetitorProducts/Proposals/{id}`), WEB-c aynı yolu kullanır.

**Sıradaki:** 2. tur — W4-BE-a ∥ W4-BE-b2.

## §37 CT kabul — W4-BE-a + W4-BE-b2 E2 ACCEPTED (koşullu, 2026-10-13)

**Kaynak:**
- BE-a `256053368` → test dalında `5279e19a2`.
- BE-b2 `4725ebcde` → test dalında `df3ab1d6e`.
- Birleştirmede çakışma yalnız BRD tüketilebilir listelerinde çıktı (`appsettings.json`, `appsettings.Development.json`, `BusinessReferenceDataConsumableSetsOptions`); iki tarafın setleri birleştirilerek CT çözdü.

**CT ölçümü (ikisi birlikte):**

| takım | sonuç |
|---|---|
| CRM | **2703/0/5** |
| MDM | **504/3** (bilinen manifest kırmızıları) |
| Web | **955/0** |
| Auth | **1122/0** |
| Gateway | **145/1** (bilinen 5063–5065) |
| Platform (BRD + CRM + Lookups süzgeci) | **335/14** (aynı 14 bilinen: 9 Mongo + 5 Verified uygunluk) |
| Mimari | denetimsiz **52** (49 + 3 BE-a rapor komutu; BE-b2 MDM komutları denetimli, artış yok) |

**CT kod okuması:**
- **BE-b2, b1 çekirdeğine `ApplyEffectsAsync` kancası ekledi.** SoD + ek ret kontrollerinden sonra, kaydın kendisi değiştirilmeden önce çalışır; ret dönerse hiçbir şey değişmez.
- Görev Merkezi bağlantısı `/MasterDataManagement/CompetitorProducts/Proposals/{id}` (§2.7 ile uyumlu, WEB-c aynı yolu kullanır).
- BE-a ↔ BE-b2 sözleşmesi: lookup kartı alanı `country`; öneri `POST {name, company, productId, reportId}` → `{id, status, existing}`.

**CT sabotajları:**
- **BE-b2 ✅:** lookup'ta pasif kayıt da gelsin → 2 kırmızı (pasif lookup'ta yok + yalnız önerene görünür). Geri alındı.
- **BE-a ⚠ yakalanmadı:** `shareBucketCode` zorunluluğu kaldırıldı → **18/18 yeşil**. Test yorumu "her ürün alanı adlandırılır" diyor ama yalnız `visitSuccessCode`, `sample`, `adverseEventNote` sınanıyor. `commitmentCode`, `interestCode`, `adoptionStageCode`, `shareBucketCode`, `competitors | noCompetitor`, `objections | noObjection`, `requests | noRequest`, `nextMessage` için koruma yok. Kod doğru; eksik olan test.
- **Kapanış:** tam gövdeden her zorunlu alanı tek tek boşaltan veri güdümlü test (ürün + eczane) — kullanıcı kararıyla CT ya da WEB-a öncesi küçük iş.

**Kabul edilen sapmalar:**
- **BE-a:**
  - MDM yanıtı esnek okunuyor;
  - öneri metni anahtar olarak geliyor (Web çevirir);
  - önceki değer yalnız aynı temsilcinin raporundan;
  - eczane rakip sorusu planın ilk ürünüyle;
  - düzeltme içgörüleri yeniden yazmıyor;
  - rakip kullanım sayımı bellekte (büyük hacimde iyileştirme gerekebilir).
- **BE-b2:**
  - tekillik kodda (`$ne` kısmi indeks yasak);
  - öneride firma isteğe bağlı;
  - 5 form MDM sözlüğüne eklendi;
  - menü sayfası WEB-c'den önce görünür;
  - kullanım sayısı MDM'de 0, Web CRM ucundan birleştirir.

**Açık:**
- (1) ✅ **KAPANDI (CT, kullanıcı onayı):** `VwW4ReportV2CompletenessTests`. Tam hekim / eczane raporu geçerli; 21 hekim + 6 eczane cevabı tek tek boşaltılınca yalnız o alan eksik çıkıyor; "yok" seçenekleri listelerin yerini tutuyor; kurallara karşılığı olmayan yeni bir zorunluluk eklenirse test kırmızı. Sabotaj (pay + eczane rakip zorunluluğu kalktı) → 2 kırmızı. CRM 2733/0/5.
- (2) **Eczane raf fotoğrafı için yükleme ucu yok** (yalnız `PhotoEvidenceId` alanı). WEB-b'ye küçük bir CRM ucu eklenir: W3 başlatma fotoğrafı deseni, MOD-0262 `CrmVisitEvidence`, `[DisableFormValueModelBinding]` vekil.

**Sıradaki:** 3. tur — W4-WEB-a ∥ W4-WEB-b.

## §37 CT kabul — W4-WEB-a + W4-WEB-b E2 ACCEPTED (koşullu, 2026-10-13)

**Kaynak:** kullanıcının ajanları, çakışmasız birleşti.
- WEB-a `3ec56bf64` → test dalında `/CRM/VisitReport` hekim sihirbazı + yolculuk aşamasına "Kullanım aşaması" seçici.
- WEB-b `07a7ff6cf` → test dalında `/CRM/PharmacyReport/{id}` eczane raporu + `/CRM/PotentialThresholds` + CRM raf fotoğrafı ucu.

**CT ölçümü (birlikte):**

| takım | sonuç |
|---|---|
| CRM | **2738/0/5** |
| Web | **1001/0** (955 + 24 + 22) |
| Gateway | **147/1** (bilinen 5063–5065) |
| Mimari | denetimsiz **53** (+ `UploadShelfPhotoCommand`) |

**CT sabotajları** (betik kaynaktan okunur, `--no-build`; `.bak` + touch ile geri alındı):
- WEB-a `report-core.js` fırsat önizlemesinde Orta × Hiç–Ara sıra → `low_priority` → `A2` kırmızı.
- WEB-b `report-pharmacy-core.js` stok düzeyi zorunluluğu kalktı → `B9` kırmızı.

**Paralel yazımın sonucu:** WEB-b, WEB-a'nın `report-core.js`'ini göremediği için eczaneyi **ayrı sayfa + ayrı çekirdek** olarak yazdı. WEB-a'nın eczane kancaları (`pharmacySteps` / `pharmacyHtml`) boş kaldı. Kabul edildi: iki sayfa bağımsız ve testli. **Ama birleşme boşlukları var:**
- **I1 · Eczane girişi:** çalışma alanı / sunum "Raporu gönder" → `/CRM/VisitReport` açıyor. Eczane ziyaretinde yalnız "eczane raporu" uyarısı çıkıyor (`visit-report.js:68`), `/CRM/PharmacyReport/{id}`'ye **yönlendirmiyor**.
  - Çözüm: `reportKind === 'pharmacy'` → `location.replace('/CRM/PharmacyReport/' + id)` + test.
- **I2 · Eczane rakip önerisi MDM'ye hiç ulaşmıyor** (BE-a hatası, WEB-b bildirdi, CT doğruladı).
  - `ForwardProposalsAsync` eczane rakiplerini `Guid.Empty` ürünle iletiyor (`VisitReportV2Handlers.cs:333`); MDM `ProposeAsync` ürün kimliği boşsa 400 dönüyor → öneri sonsuza dek `competitor_proposal_pending`.
  - Çözüm: eczanede planın ilk ürünü (lookup ile aynı kural); plan ürünsüzse öneri yerel kalır + uyarı. Test.

**Kabul edilen sapmalar:**
- **WEB-a:**
  - adımlar mockup'taki gibi üstte yatay;
  - ortak ziyaretçi salt okunur;
  - avantaj çipleri rakip kartında;
  - endikasyon "doğruluk" etiketi yok (sunucuda alan yok);
  - numune sunum işaretinden ön dolmuyor;
  - 15 form `Form_*` Web anahtarı (MDM form BRD'si yok).
- **WEB-b:**
  - GSKU satırları `mdm.gskus.read` ile okunuyor; temsilcide yoksa bilgi mesajı (**W4-CFG'de temsilci rolüne `mdm.gskus.read` önerilir**);
  - stok adet / fiyat gözlemi / materyal çipleri / 48 sa sayacı yok (sunucuda alan yok).

**I1 + I2 KAPANDI (CT, kullanıcı onayı, 2026-10-13):**
- **I1:** sihirbaz eczane ziyaretini `/CRM/PharmacyReport/{id}`'ye devrediyor. `report-core.js` `pharmacyReportUrl`, view `data-pharmacy-url`; yalnız ön doldurma başarılıysa (UAS-001 korunur, A1 daraltıldı: tek `location.replace` = eczane devri). Duman testi `pharmacy`: yalnız prefill isteği + yönlendirme, çizim yok. Sabotaj → A8b kırmızı.
- **I2:** `ForwardProposalsAsync` eczane önerisini planın ilk ürünüyle iletiyor (`PharmacyProposalProduct`, lookup ile aynı kural); ürünsüz planda MDM'ye gönderilmez, `competitor_proposal_pending` uyarısı. Sabotaj (ürün yine boş) → kırmızı.
- Web **1002/0**, CRM **2739/0/5**.

**Sıradaki:**
- 4. tur: W4-WEB-c (rakip yönetimi) + W4-CFG (şablon + yetkiler; read-all açık atama, temsilciye gskus.read).
- Ardından W4 canlı E4.

## §36.2 Uygulama raporu — W4-CFG (2026-10-13, yerel 97c5)

**Kapsam:** yalnız yerel 97c5. Canlıya (be823006) dokunulmadı. Şablon, rol, izin ve kayıt yazılmadı; kod değişikliği yok.

### 1. Şablon `MDM-COMP-PROPOSAL` — salt okuma doğrulandı
Kaynak: Mongo `diten_personalization_dev.workflow_templates` / `workflow_template_versions`.

| Alan | Değer | Beklenen |
|---|---|---|
| Template | `4ca5b242-2cf2-44ca-be80-00e0835315c9`, `Status 2` (Published) | ✅ |
| Aktif sürüm | `65679ab1-c781-485b-9623-3588f1f4553a`, v1, `Status 1` (Published), `IsImmutable` | ✅ |
| `requestedObjectType` | `mdm.competitor-proposal` | ✅ |
| Aşama / adım | tek aşama `sales_ops` "Satış Operasyon" / tek adım `sales_ops` "Satış operasyon onayı" (`approval`) | ✅ |
| Aday | `position:c1acdf12-40d2-4b2c-a2a4-afe1bdd7681d` = `POS-SALES-OPS` "Satış Operasyon" (aktif); sahibi `d27fa4a6…` (sema, Admin) | ✅ |
| SLA | `dueInMinutes 4320`, `escalateAfterMinutes 2880` → aynı pozisyon | ✅ |
| Seçenek | `options.rejectCommentRequired: true` | ✅ |

**Parmak izi** (tüm şablon + sürüm dokümanlarının SHA-256'sı):
- önce: şablon 26 / `f164e53a4eda13c2`, sürüm 26 / `4fe07db0c5a24163`;
- sonra: **aynı**.

### 2. Yetki betiği — yazıldı, RBAC'a yazılmadı
**Auth kataloğu:** 15 anahtarın hepsi mevcut.
- `crm.doctor-insights.{read,manage,read-all,read-aggregate,thresholds.manage}`
- `crm.visit-report.read-aggregate`
- `mdm.competitor-products.{read,manage,propose}`
- `mdm.gskus.read`
- `platform.workflow.instances.{start,view}`, `platform.workflow.tasks.{approve,reject,cancel}`

**Betik:** `scripts/rbac/grant_w4_report_rbac_97c5.py` (W3-CFG deseni).
- dry-run varsayılan; subtype-4 GUID; tarih alanları yerel 97c5 kaydından aynen kopyalanır;
- idempotent; marker `manual-grant-w4-report-rbac`;
- rol ve izin oluşturmaz; katalog eksikse durur;
- `crm.doctor-insights.read-all` (ExplicitGrantOnly) Admin'e **açıkça** verilir;
- isteğe bağlı rol bayrakları:
  - `--rep-role`: `mdm.competitor-products.read / propose`, `crm.doctor-insights.read`, `mdm.gskus.read`;
  - `--marketing-role`: `crm.doctor-insights.read-aggregate / thresholds.manage`, `crm.visit-report.read-aggregate`, `mdm.competitor-products.read / manage`.
- Komut satırında verilen rol yoksa betik **ABORT** (çıkış 2) ile durur.

**Dry-run (Admin):**
```
skip (already granted): Admin <- mdm.gskus.read
skip (already granted): Admin <- platform.workflow.instances.start
skip (already granted): Admin <- platform.workflow.instances.view
skip (already granted): Admin <- platform.workflow.tasks.approve
skip (already granted): Admin <- platform.workflow.tasks.reject
skip (already granted): Admin <- platform.workflow.tasks.cancel
role 'Admin' (6a315467-7d80-4ad8-bd76-78f8f762fe8a): 9 grant(s) to insert: ['crm.doctor-insights.read', 'crm.doctor-insights.manage', 'crm.doctor-insights.read-all', 'crm.doctor-insights.read-aggregate', 'crm.doctor-insights.thresholds.manage', 'crm.visit-report.read-aggregate', 'mdm.competitor-products.read', 'mdm.competitor-products.manage', 'mdm.competitor-products.propose']
NOTE: no --rep-role / --marketing-role given — only the admin role is planned (local 97c5 has no rep / marketing role; decide them before granting).
DRY-RUN — nothing written. Re-run with --apply.
```
`--rep-role CrmUser` denemesi: `ABORT: role 'CrmUser' not found in tenant 97c5 … Existing: ['Admin', 'DocumentMasterRegisterLinker', 'GQD', 'QADocumentation', 'Viewer']` (çıkış 2). Marker'lı satır sayısı: 0.

### 3. DUR — temsilci / pazarlama rolü yerelde yok
Yerel 97c5 rolleri: `Admin`, `DocumentMasterRegisterLinker`, `GQD`, `QADocumentation`, `Viewer`.
- Temsilci rolü yok (canlıda `CrmUser`).
- Pazarlama rolü yok.

Bu yüzden yalnız Admin planlandı. Temsilci ve pazarlama rolleri için karar ürün sahibinde:
- rol oluşturma bu paketin yetkisi dışında;
- roller açıldıktan sonra `--rep-role` / `--marketing-role` ile aynı betik çalıştırılır.

### 4. Kullanıcı adımı
1. `py -3 scripts/rbac/grant_w4_report_rbac_97c5.py --apply` (Admin'e 9 anahtar).
2. Admin rolündeki kullanıcılar **yeniden giriş** yapar (izinler JWT'ye basılır).
3. Geri alma: `db.rolePermissions.deleteMany({CreatedBy: "manual-grant-w4-report-rbac"})`.

---

## §37 CT kabul — W4-WEB-c + W4-CFG E2 ACCEPTED (2026-10-14)
Test dalına alındı: WEB-c `c22313360` (ajan `be07bddb2`), CFG `50eff4764` (ajan `89973f741`). Cherry-pick çakışmasız.

**WEB-c — CT kontrolü:**
- Controller: 18 vekil, her biri yalnız kendi ucunun anahtarıyla (A9 teorisi her yolu + anahtarı sabitliyor); gövdede `tenantId` → 400; BRD okuması iki setle sınırlı (`mdm-reimbursement-status`, `market`).
- UAS-001: 5 sayfanın hepsi okuma anahtarı yoksa yalnız erişim uyarısı çiziyor (iskelet yok, yönlendirme yok).
- L10n köprüsü `JsonSerializer.Serialize` (HTML kaçışlı); `RazorInlineScriptSyntaxTests` yeşil.
- SoD: `decisionActions` onay / geri gönder çiftini hazırlayan / son düzenleyen / gönderene hiç çizmiyor (eski bir `canDecide` gelse bile); sunucu zaten zorluyor.
- Web tam takım (worktree, `-o ./.ct-out/web`): 1658/3. İkisi `ShipmentDetailsDateNodeTests` (tabanda da kırmızı); üçüncüsü `WorkCenterMenuWithoutPermissionsTests` CT'nin `-o` klasöründen kaynaklı göreli yol hatası (`C:\Diten.Web\Views\…` bulunamadı), değişiklikle ilgisiz. Ajanın 1659/2 sonucuyla tutarlı. WEB-c testleri 28/28.
- **CT sabotajı (ajanın dokunmadığı kural):** `isOwn`'dan "son düzenleyen" (`updatedBy`) çıkarıldı → `A2_actions_sod_…` kırmızı. `.bak` ile geri alındı + touch → 28/28.

**CFG — CT kontrolü:**
- Betik W3 kalıbıyla aynı (subtype-4, tarih tipleri yerel kayıttan, idempotent, marker `manual-grant-w4-report-rbac`, rol / izin oluşturmaz, `--db diten_auth_v3` W3 ile aynı varsayılan).
- CT dry-run: Admin'e 9 eklenecek, 6 zaten var — rapor ile birebir. Yazma yok.
- Şablon `MDM-COMP-PROPOSAL` (CT 2026-10-13 kurdu) ajan tarafından salt okumayla doğrulandı; 26 şablon / 26 sürüm parmak izi önce = sonra.

**Açık / kullanıcıya:**
- `py -3 scripts/rbac/grant_w4_report_rbac_97c5.py --apply` + Admin yeniden giriş (yerel). Canlı (be823006) zaten yapıldı (scripts/golive).
- Yerelde temsilci / pazarlama rolü yok → rol kararı ürün sahibinde.
- Form sözlüğü `mdm.brands.read`, kişi adları `platform.meetings.create` ister; yoksa formda uyarı / kısaltılmış kimlik.
- Duman testi "Onaya gönder" yerelde koşulmadı (ajan şablonun kurulmasını beklemiş; şablon artık var) → W4 canlı E4'te denenecek.
- Görsel farklar (kalan): G1 öğeleri mockup'ta yok (karar gereği); birleştirme adayında etken madde / form yok (MDM adayı göndermiyor); avatar tek ton.

**Sıradaki:** W4 canlı E4 (mockup görsel karşılaştırması dahil: rapor sihirbazı, eczane, eşik sayfası, rakip ürünler) → W3E / W5…

---

## W4 yerel E4 bulguları (CT, 2026-10-14)
Yerel 97c5, kullanıcı onaylı yazmalar. Yapılan:
- hekim sihirbazı mockup karşılaştırması;
- eşik ekle (KONSET 15/40 → rapor önizlemesi kullandı) → geri al (10/30);
- rakip kaydı `E4-TEST Konsetra` (fiyatsız, aşağıdaki ALTYAPI yüzünden);
- Bülent Yardımcı raporu `d68d299f` gönderildi (profil, potansiyel, geçmiş, MDM önerisi `E4-TEST Konsepro`);
- öneri → birleştir kararı → onaya gönder → sema onayladı → MDM `merged` + diğer ad. SoD ve "not zorunlu" doğru.

| # | Bulgu | Yer |
|---|---|---|
| F-1 | Rakip formunda `change` → tam `render()`; tıklanan / Tab ile geçilen sonraki alan DOM'dan silinir, odak `BODY`'ye düşer, yazılanlar kaybolur | `competitor-pages.js` `change` dinleyicisi (`data-form`, `data-row`) |
| F-2 | "Rakip olduğu ürünlerimiz" yalnız ilk 100 ürün (`api/products?pageSize=100`); 177'den 77'si seçilemez, arama yok | `competitor-pages.js` ürün yükleme + çip listesi |
| F-3 | BRD seçenekleri `v.valueCode` / `v.displayName` okuyor; Web vekilinin gerçek yanıtı `code` / `label` → geri ödeme + pazar seçenek değerleri boş, kayıtta boş kod. Duman harness'ı yanlış alanla sahte veri kullandığı için testler yeşildi | `competitor-pages.js:110,117,129` + `competitor-products-smoke.js:70-71` |
| F-5 | Ürünün onaylı endikasyon × profil satırı YOKKEN potansiyel sorusu yine zorunlu (satır ya da "hasta görmüyor"); temsilci yanlış beyana zorlanır, `NoPatientsInIndications=true` potansiyele işlenir (sınıf / fırsat bozulur) | CRM `VisitReportV2Rules.cs:216` + Web `report-core.js:250` |
| F-7 | Karar kartında "Hazırlayan" saati hazırlama değil uygulama saati (10:53 yerine 11:04) | `competitor-pages.js` karar kartı |
| F-4 (not) | Kişi adları `platform.meetings.create` ister; yoksa "kullanıcı c5769c62" | yetki / FU |
| F-6 (not) | Ürüne özel eşik satırı silinemez (kiracı varsayılanı değişince ürün eski değerde kalır) | FU |
| ALTYAPI | Doğrulanmış referans (GSKU UOM + pazar) yerelde yok: GSKU create-options 503, ülke fiyatlı rakip kaydı 503 `reference_data_unavailable` | W4-INFRA |

## §36.1 Agent Prompt — W4-FIX-a (paste-ready · rakip ürün sayfası F-1 / F-2 / F-3 / F-7)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W4 · W4-FIX-a — rakip ürün sayfası: alan değişiminde odak kaybı (F-1), ürün seçici 100 sınırı (F-2), BRD seçenek alanları code/label (F-3), "Hazırlayan" saati (F-7)
Repository: C:\tmp\vw-w4-fix-a (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w4-fix-a · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W4-report-v2.md — önce oku: "W4 yerel E4 bulguları (CT, 2026-10-14)" tablosu + "W4-WEB-c" + §37 WEB-c. Dosyalar: frontend/Diten.Web/wwwroot/assets/js/MasterData/CompetitorProducts/{competitor-pages.js,competitor-core.js}, frontend/Diten.Web.Tests/Js/competitor-products-smoke.js, frontend/Diten.Web.Tests/Controllers/CompetitorProductsWebTests.cs.
NE:
(F-1) Metin / sayı alanlarının `change` olayı tam yeniden çizim YAPMASIN: değer state'e yazılsın, yalnız etkilenen parçalar (önizleme kartı, hata satırı, fiyat dönemi notu) güncellensin; ya da yeniden çizimde odaklanan alan + imleç konumu korunsun. Kabul: ad → firma → güç alanlarına tıkla-yaz ve Tab ile geç → hiçbir değer kaybolmaz, odak gidilen alanda kalır (ülke satırı fiyat / para birimi / tarih alanları dahil). Seçim kutuları (select) çizimi tetikleyebilir ama odağı korur.
(F-2) "Rakip olduğu ürünlerimiz": 100 ile kesilmesin — arama kutulu seçici (yazdıkça `api/products?q=` ya da tüm sayfalar), seçilenler üstte çip; önerinin tamamlanmasında da aynı seçici. Kabul: 177 ürünün hepsi (ör. TUTUKON) bulunup seçilebilir.
(F-3) BRD seçeneklerinde gerçek Web yanıt sözleşmesi: `code` + `label` + `attributes.label_<dil>` (bkz. `Controllers/MasterData/CompetitorProductsController.ReferenceValues` → `CrmReferenceSetReader`; aynı biçim `CRM/VisitReport/report-core.js` optionLabel). Hem geri ödeme hem pazar listesi; İngilizce etiket `label`. Duman harness'ı ve C# testleri GERÇEK biçimle (sahte veri `valueCode` / `displayName` OLMASIN) — eski alanla yazılmış harness'ın yanlış yeşil verdiğini sabotajla göster.
(F-7) Karar kartı "Hazırlayan" = kararın oluşturulma zamanı + oluşturan (createdAt / createdBy); uygulama zamanı ayrı satırda ("Uygulandı · kim, ne zaman").
KORU/YAPMA: MDM / CRM sunucu koduna dokunma; kullanıcının giriş yaptığı sekmeye mock / harness enjekte etme (ayrı sekme); var olan SoD / birleştirme onay kutusu / 409 Yenile davranışı değişmez (testler yeşil kalmalı).
DOĞRULA: yeni testler (F-1 odak + değer korunur, F-2 >100 ürün, F-3 gerçek yanıt biçimiyle seçenek değeri dolu, F-7) + 2 sabotaj (.bak + touch): (1) F-3 eşleme yine `valueCode` → kırmızı; (2) F-1 `change` yine tam render + odak koruması kapalı → kırmızı. Web tam takım (-o repo içi), taban kırmızılarını ayrı worktree'de karşılaştır. Yerel tarayıcıda ayrı sekmede elle doğrulama: yeni rakip formu tıkla-yaz-Tab, TUTUKON seçimi, geri ödeme kodu dolu (ülke satırlı kayıt W4-INFRA olmadan 503 verebilir — bu beklenir, raporla).
Commit: "fix(web): WP-VW-W4-FIX-a — competitor form keeps focus on field change, product picker beyond 100 with search, BRD options read code/label, decision card shows preparer time" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.1 Agent Prompt — W4-FIX-b (paste-ready · rapor potansiyel kuralı F-5; FIX-a ile paralel)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W4 · W4-FIX-b — hekim raporu: ürünün onaylı endikasyon × profil satırı yoksa endikasyon potansiyeli sorulmaz / zorunlu değildir / kaydedilmez (F-5); "hasta görmüyor" yanlış beyanı engellenir
Repository: C:\tmp\vw-w4-fix-b (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w4-fix-b · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W4-report-v2.md — önce oku: "W4 yerel E4 bulguları (CT, 2026-10-14)" F-5 + §2.2 + §2.5 + §37 BE-a / BE-c (tamlık testi VwW4ReportV2CompletenessTests). Kod: services/Diten.CrmService/src/Diten.CrmService.Application/Features/VisitReport/V2/{VisitReportV2Rules.cs,VisitReportV2Handlers.cs} (potentialAsked, ProductIndicationProfileReader, DoctorProductInsightInput) + Features/DoctorInsights/DoctorInsightsRecorder.cs + frontend/Diten.Web/wwwroot/assets/js/CRM/VisitReport/{report-core.js,visit-report.js}.
NE:
(1) Sunucu: bir ürün bölümü için "potansiyel soruluyor" = (isDue ya da temsilci güncelledi) VE ürünün onaylı endikasyon × profil satırı VAR. Satır yoksa: tamlık kuralı potansiyel istemez (`missing[]`'de `potential` yok); gövdede `noPatientsInIndications=true` ya da satır gelse bile potansiyele YAZILMAZ (yok sayılır + `warnings` içinde `potential_ignored_no_indications:<productId>`); doktorun aylık toplamı (doktor düzeyi) kuralı değişmez; pay kovası (shareBucket) kuralı değişmez. Ön doldurma yanıtında ürün başına `potentialAsked` (bool) açıkça dönsün ki istemciler kendi hesabını yapmasın.
(2) Web: report-core tamlık kuralı ve sihirbaz aynı bayrağı kullanır; satır yoksa "Bu endikasyonlarda hasta görmüyor" düğmesi ve zorunluluk yıldızı GÖSTERİLMEZ, yalnız bilgi notu ("endikasyon listesi tanımlı değil; doktorun toplamı profil adımında") kalır; potansiyel önizlemesi "—".
(3) Mobil sözleşme notu: execution/domains/commercial-suite/work-packs/mobile/ altına kısa not (prefill `productSections[].potentialAsked`, yeni uyarı kodu, eski davranışla fark) — CT mobil sayfasına taşıyacak.
KORU/YAPMA: satırı olan ürünlerde davranış aynen (90 gün kuralı, endikasyon toplamı ≤ doktor toplamı, sınıf / fırsat matrisi); var olan raporların / potansiyel kayıtlarının verisine dokunma (geçmişe dönük düzeltme YOK, yalnız raporla); W4 tamlık testi kapsama denetimi yeşil kalmalı.
DOĞRULA: CRM testleri (satırsız ürün: missing'de potential yok + noPatients yazılmaz + uyarı; satırlı ürün: eski kural) + Web Node testleri (bayrağa göre çizim + tamlık) + 2 sabotaj (.bak + touch): (1) sunucu kuralında satır şartı kaldırılsın → kırmızı; (2) recorder satırsız üründe noPatients yazsın → kırmızı. CRM + Web tam takım (-o repo içi), taban karşılaştırması.
Commit: "fix(crm,web): WP-VW-W4-FIX-b — potential is not asked (nor recorded) for a product without approved indication rows; prefill exposes potentialAsked" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22), mobil not yolu rapora. K13.
```

## §36.1 Agent Prompt — W4-INFRA (paste-ready · doğrulanmış referans verisi: GSKU UOM + pazar; FIX'lerden bağımsız)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W4 · W4-INFRA — doğrulanmış referans verisi (verified GSKU UOM + verified market) yerel 97c5'te çalışır hâle gelsin; canlı için operatör adımları belgelensin. GSKU taslağı oluşturma ve ülke fiyatlı rakip kaydı 503 vermesin.
Repository: C:\tmp\vw-w4-infra (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w4-infra · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W4-report-v2.md — "W4 yerel E4 bulguları (CT, 2026-10-14)" ALTYAPI satırı. CT ölçümü (2026-10-14):
- MDM `GET api/gskus/create-options` → 503 "Doğrulanmış referans verileri kullanılamıyor"; MDM `POST api/mdm/competitor-products` ülke satırıyla → 503 `reference_data_unavailable` "The market catalog could not be read".
- Platform: `BusinessReferenceData:Provider:ReferenceTenantId` hiçbir appsettings'te yok (aynı neden `/health` 503; bkz. docs/guides/operations/go-live-2026-09-meetings-task-engine.md §7/11, backlog BL-404). `BusinessReferenceData:VerifiedGskuOperationalProvisioning` ve `:VerifiedMarketOperationalProvisioning` ayarsız (runner'lar Program.cs'te kayıtlı; uygunluk sınıfları Development…Eligibility).
- Kilitli kataloglar (`Seed/business-reference-data/mod-0290-gsku-reference.json`, `mod-0290-market-reference.json`) Windows checkout'ta CRLF (core.autocrlf=true, depoda .gitattributes YOK) → SHA-256 kilitli parmak iziyle tutmuyor; LF'ye çevrilince tam tutuyor (gsku e95ef856…, market b94c4528…).
- MDM `VerifiedGskuResolver` bölümü (PlatformBaseAddress, Timeout, CredentialIdentifier, CredentialSecret — GSKU ve market istemcisi AYNI bölüm) ve Platform `VerifiedGskuResolverCredential:Mdm` (Identifier, ActiveSecret, ConsumerService, AllowedAudience…) ayarsız.
NE:
(1) .gitattributes: iki kilitli katalog için `eol=lf` (yalnız bu iki dosya ya da kilitli katalog listesi; diğer seed'leri değiştirme) + renormalize; parmak izini working-tree baytları üzerinden doğrulayan guard testi (dosya CRLF'ye çevrilince kırmızı).
(2) Development ayarı (appsettings.Development.json, Platform + MDM): Provider:ReferenceTenantId = 97c5; verified GSKU + market provisioning Enabled (EnumerationEnabled dahil), CatalogPath / ExpectedCatalogVersion / ExpectedCatalogFingerprint kilitli sabitlerle aynı, ConsumerTenantId 97c5, ActorId / IdempotencyNamespace; MDM VerifiedGskuResolver PlatformBaseAddress (yerel Platform 5057) + Timeout. Kimlik bilgisi: GİZLİ değer depoya YAZILMAZ — dotnet user-secrets (Platform + MDM UserSecretsId) ile ayarlayan betik `scripts/dev/set-verified-reference-dev-credential.ps1` (rastgele dev sırrı üretir, iki tarafa aynı değeri yazar; kullanıcı çalıştırır). Ayar yazmadan önce mevcut runner / eligibility / authenticator'ın beklediği tam anahtarları koddan doğrula.
(3) Canlı için operatör belgesi: docs/guides/operations/ altında (yer .antigravity/rules/docs-organization.md'ye göre) — hangi anahtarlar, hangi değerler (canlı referans kiracı = be823006), kimlik bilgisinin nasıl üretilip iki servise verileceği, provisioning'in Production'da çalışıp çalışmadığı (Development…Eligibility Production yolunu engelliyorsa AÇIKÇA yaz ve DUR — üretim yolu tasarımı ayrı karar). Canlıya DOKUNMA.
(4) Doğrulama: yerelde (kullanıcı betiği çalıştırıp fleet'i yeniden başlattıktan sonra) GSKU create-options 200 + UOM listesi; rakip kaydı ülke satırıyla doğrulama yolu 503 vermez; Platform `/health/ready` durumu. Veri YAZMA: yalnız katalog provisioning'in kendi yazdığı referans satırları; GSKU / rakip kaydı OLUŞTURMA (CT E4'te yapacak).
KORU/YAPMA: kilitli sabitleri (sürüm, parmak izi) değiştirme; kodda doğrulama gevşetme YOK; gizli değer commit YOK; user-secrets OKUMA YOK (betik yazar, kullanıcı çalıştırır); diğer servislerin ayarlarına dokunma.
DOĞRULA: CRLF guard sabotajı (.bak + touch: dosyayı CRLF yap → kırmızı); Platform + MDM ilgili test takımları (-o repo içi); yerel ölçüm çıktıları rapora.
Commit: "chore(platform,mdm): WP-VW-W4-INFRA — locked reference catalogs pinned to LF, dev verified GSKU/market provisioning config, dev credential script, live operator guide" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22): kullanıcıya betik + fleet restart talimatı, canlı için açık kararlar. K13.
```
