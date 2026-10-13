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
Repository: C:\tmp\vw-w4-web-c (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w4-web-c · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W4-report-v2.md — önce oku: §0 (K-W6), §2.6, §2.7, §2.5 (competitor-usage), "W4-WEB-c"; BE-b2 / BE-a / BE-c §37 notları. Mockup: execution/domains/commercial-suite/work-packs/mockups/visit-workspace/W4 Rakip Urunler (standalone).html (çözülmüş decoded-w4-rakip/Rakip.dc.html; 15 ekran seçicide). Fark belgesi: mockups/visit-workspace/DESIGN-GAP-W4-competitor-2026-10-13.md (§1 aynen, §2 G1–G6).
DESEN: MDM Ürünler sayfaları (frontend/Diten.Web/Views/MasterData/Products/*, Controllers/MasterData/ProductsController.cs — liste + ayrı sayfalar + bölüm kartları); Workflow karar akışı + SoD + onaylayıcı görünümü = W3 itiraz kütüphanesi (Views/CRM/ObjectionResponses, wwwroot/assets/js/CRM/ObjectionResponses/*); L10n köprüsü parantez koruması (RazorInlineScriptSyntaxTests yeşil kalmalı).
NE: "W4-WEB-c" 1–5; vekiller MDM api/mdm/competitor-products/* + CRM api/crm/doctor-insights/products/{id}/indication-profiles + api/crm/visit-report/v2/competitor-usage; MDM menü sayfası (BE-b2 manifest) + Nav.Page 7 dil.
KORU/YAPMA: telefon düzeni gerekmez (masaüstü + tablet); pazar payı / satış yok (W7 yer tutucu); kullanıcının giriş yaptığı sekmeye mock / harness enjekte etme — ayrı sekme.
DOĞRULA: "W4-WEB-c" acceptance + 2 sabotaj (.bak + touch); Web tam takım (-o repo içi); mockup ile görsel karşılaştırma ayrı sekmede (15 ekran + G1 Workflow durumları), farkları rapora.
Commit: "feat(web): WP-VW-W4-WEB-c — MDM competitor products (list + proposals, form + country price/reimbursement history, detail, proposal decisions via workflow, direct merge, read-only / unauthorized / conflict, AR)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.1 Agent Prompt — W4-CFG (paste-ready · 4. TUR, BE-b2 test dalında + fleet yeniden başlatıldıktan SONRA)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W4 · W4-CFG — Workflow şablonu MDM-COMP-PROPOSAL (POS-SALES-OPS) canlı oluştur + yayınla; grant betiği (dry-run varsayılan) — tenant 97c5, uygulama KODU YOK
Repository: C:\tmp\vw-w4-cfg (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w4-cfg · commit (rapor + betik) bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W4-report-v2.md — "W4-CFG" + §2.6 + §2.7 + BE-b2 §37 notu. DESEN: WP-VW-W3-authoring.md "W3-CFG" + §37 W3-CFG (aynı akış: POST api/v1/workflow/definitions → publish; kök options.rejectCommentRequired; idempotent; parmak izi) + scripts/rbac/grant_w3_authoring_rbac_97c5.py (dry-run, subtype-4 GUID, tarih biçimi yerel belgeden, marker, rol / izin OLUŞTURMAZ). Ön koşul: kullanıcı fleet'i yeniden başlatmış ve yerleşik tarayıcıda giriş yapmış olmalı.
VERİ YAZMA YETKİSİ (yalnız): (1) MDM-COMP-PROPOSAL şablonunun oluşturulması + yayınlanması; (2) betiğin YAZILMASI (çalıştırmayı kullanıcı yapar).
NE: şablon tek aşama / tek adım sales_ops "Satış operasyon onayı", aday POS-SALES-OPS (c1acdf12-40d2-4b2c-a2a4-afe1bdd7681d — canlıdan doğrula), SLA 4320 / 2880, rejectCommentRequired: true, requestedObjectType mdm.competitor-proposal; betik scripts/rbac/grant_w4_report_rbac_97c5.py: Admin'e W4 CRM + MDM anahtarları (crm.doctor-insights.* — read-all ExplicitGrantOnly olduğu için yalnız bu betikle Admin'e gider, unutma; crm.visit-report.read-aggregate, mdm.competitor-products.read|manage|propose) + MDM'nin Workflow çağrısı için platform.workflow.instances.start / view, tasks.approve / reject / cancel (eksikse); temsilci / pazarlama rolleri varsa "W4-CFG" listesi, yoksa DUR + raporla. Auth kataloğunda anahtarların varlığını önce doğrula.
KORU/YAPMA: kod değişikliği YOK; diğer şablonlara DOKUNMA (parmak izi önce / sonra aynı); örnek başlatma YOK.
DOĞRULA: şablon Published + tek adım + doğru aday + options.rejectCommentRequired; diğer şablon parmak izleri aynı; betik dry-run çıktısı rapora.
Commit: "chore(mdm): WP-VW-W4-CFG — workflow template MDM-COMP-PROPOSAL (live) + RBAC grant script (dry-run default)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22), kullanıcıya --apply + yeniden giriş talimatı. K13.
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
