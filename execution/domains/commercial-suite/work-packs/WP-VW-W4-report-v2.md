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

- Rakip okuması MDM'den (gateway, çağıranın jetonu). MDM ulaşılamazsa `competitorsUnavailable: true` ve rapor yine gönderilebilir; rakip "kullanmıyor" seçilmeden geçilemez **yalnız liste geldiyse**.
- **İzinler:** mevcut `crm.visit-report.read` / `.record` / `.amend`. Yeni anahtar yok.

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
- `Status` (`draft` | `in_review` | `applied` | `returned`).
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
| `GET proposals` (+ benzer kayıt önerisi, ad benzerliği) | manage |
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

## W4-WEB-c — MDM rakip ürün yönetimi (mockup gelince yazılır)
Brif R1–R7 + Workflow ekleri (onaya gönder, onay bekliyor, SoD, onaylayıcı görünümü).

## W4-CFG — Workflow şablonu + yetkiler (canlı yapılandırma, kod YOK)
**Şablon `MDM-COMP-PROPOSAL`:**
- tek adım `sales_ops` "Satış operasyon onayı";
- aday POS-SALES-OPS;
- SLA 4320 / 2880;
- `rejectCommentRequired: true`;
- `requestedObjectType: mdm.competitor-proposal`.

**Grant betiği** `scripts/rbac/grant_w4_report_rbac_97c5.py` (W3-CFG deseni):
- Admin'e yeni CRM + MDM anahtarları;
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
NE: §2.5 v2 alanları (VisitReport genişler, yeni toplam değil; v1 okuma bozulmaz); uçlar v2/prefill, v2/draft, v2/submit, {id}/amend (v2), {id}; ContentEngagementJourney aşamasına AdoptionStageCode (isteğe bağlı) + doğrulama; ilerletme kuralı (L'den sonraki ilk eşlenmiş aşama; eşleme yoksa Advance; plan dışı ilerletmez); IMdmCompetitorClient (gateway, çağıranın jetonu; lookup + proposals; MDM yoksa competitorsUnavailable); 10 BRD seti + ConsumableSets 3 yer; AE / endikasyon dışı zorunlu.
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
NE: §2.6 toplam + kurallar + uçlar (lookup, proposals, decision / submit / withdraw, usage sayısı için CRM'e soru YOK — usage şimdilik 0 / "yakında" alanı, CRM toplu sayımı W7); CompetitorProposalDecision IWorkflowApprovable + OutcomeApplier (complete / merge / reject); merge: MergedIntoId + alias + ülke taşıma; okumada merged → hayatta kalan; includeProposed=mine; MDM manifest COMPETITOR_PRODUCTS + Nav.Page 7 dil; ocelot /api/mdm/competitor-products (+/{everything}) + rota testi; Auth mdm.competitor-products.read|manage|propose (açıklama İNGİLİZCE); mdm-reimbursement-status BRD seti (global değil, tenant) + ConsumableSets.
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
NE: "W4-WEB-b" 1–3. ORTAK ÇEKİRDEK: WEB-a report-core.js'i oluşturuyor (paralel). Sen eczane kurallarını ayrı dosyada tut (report-pharmacy-core.js) ve report-core.js'e yalnız açık bir genişletme noktası üzerinden bağlan; report-core.js'i yeniden yazma. Birleşmede çakışırsa WEB-a imzası korunur.
KORU/YAPMA: sipariş düğmesi pasif ("yakında", W6); kullanıcının giriş yaptığı sekmeye mock / harness enjekte etme.
DOĞRULA: "W4-WEB-b" acceptance + 2 sabotaj (.bak + touch); Web tam takım (-o repo içi); mockup görsel karşılaştırma ayrı sekmede.
Commit: "feat(web): WP-VW-W4-WEB-b — pharmacy report (stock, shelf photo, sales trend, competitors, recommendation, safety) + potential thresholds settings page" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

> **W4-WEB-c ve W4-CFG promptları** rakip ürün mockup'ı gelince ve BE-b2 kabul edilince yazılır.
