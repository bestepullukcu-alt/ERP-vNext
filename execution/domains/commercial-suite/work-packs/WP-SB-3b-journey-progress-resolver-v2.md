# WORK PACKAGE — WP-SB-3b · Yolculuk ilerlemesi (`JourneyProgress`) + ziyaret içerik çözücüsü v2 (çok ürün, rotasyon, ürün başına aşama)

> **CT (SoR), 2026-10-01.**
> - **Tasarım:** `DESIGN-SB-3-multi-product-visit-content.md` §2 (S3-1 / S3-2 / S3-3 / S3-5 / S3-7 / S3-9), §3.2, §3.3, §3.4 (yalnız `ContentItems[]` kısmı).
> - **Önceki paket:** WP-SB-3a (birleşti, `a318fd82`): ürün satırı `Role` + `JourneyId`, `CycleCapacity.MaxPromoProducts` / `MaxNonPromoProducts`.
> - **Paralel paket:** WP-SB-3-UIa (yalnız Web; çakışma yok).
> - **Kapsam:** yalnız CrmService. Ziyaret başlat / tamamla ve ilerlemenin YAZILMASI SB-3c'de.
>
> **Çalışma yeri:** worktree `C:\tmp\sb-3b`, dal `wp/sb-3b`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Amaç
Planlanan bir ziyaret, tek yolculuk / tek aşama yerine **ürün listesi** taşısın:
- play'in promo satırlarından en fazla `MaxPromoProducts`, non-promo satırlarından en fazla `MaxNonPromoProducts` ürün, **ağırlıklı rotasyonla**;
- her ürün için **kendi yolculuğundaki sıradaki aşama** → aşamanın yolunun **güncel yayını** → yolun adımları → içerik listesi + iddialar;
- ilerleme doktor × ürün × yolculuk başına tutulur (`JourneyProgress`); bu pakette **okunur**, SB-3c'de yazılır.

## Kanıt (CT)
- **Çözücü:** `Application/Features/VisitContentSequence/VisitContentSequenceResolver.cs`.
  - play: `ResolveBindingsAsync` (:143; doğrudan id ya da segment → ilk play);
  - yolculuk: şablon düzeyi **ilk** `content-engagement-journey` bağı (:68-73) → **S3-2 ile emekli**;
  - aşama: `PriorStageIndex + 1`, son aşamadan sonra `EndOfJourney` ile **durur** (:106-114) → **S3-7 ile başa döner**;
  - promo / non-promo: içerik ÖĞESİ sayısı (`SplitContentAsync` :185) → **ürün sayısı** olur;
  - süre: `ActivityTimeBudgetCalculator.VisitDuration(capacity, promo, nonPromo)` (:243).
  - Tüketiciler: `VisitPlanningEngine` (:41, :255-296), `PreviewVisitContentHandler`, `VisitContentController`.
- **Planlama motoru:** `Application/Features/VisitPlanning/VisitPlanningEngine.cs`.
  - aşama imleci `ResolvePriorStageIndexAsync` (:519-529): doktorun son planındaki `Content.StageIndex` (ürün / yolculuk ayrımı yok) → **`JourneyProgress` ile değişir**;
  - `BuildAtomAsync` (:444-510): `Content` tekil, `_journeyProbe.ResolveAsync` ile doldurulur;
  - önizleme `DoctorContentPreview` (:291-294, :655-690).
- **PlannedVisit:** `Domain/Entities/PlannedVisit.cs`; `PlannedVisitContentRef` (:211, tekil). Mobil `PlannedVisitDetailDto.Content` okur.
- **Yolculuk aşaması:** `ContentEngagementJourneyStage` (`Domain/Entities/ContentEngagementJourney.cs:103`): `RecommendedKnowledgePathId`, `PathCode`, `PathVersionPinPolicy` (`pinned` varsayılan | `latest-published`), `StageOrder`, `StageStatus`. Okuyucu `IContentEngagementJourneyReader.GetOrderedStagesAsync`.
- **Bilgi Yolu:** `KnowledgePath` (`PathCode`, `PathVersion`, `PathStatus`, `CountryCode`, `LanguageCode`, `Steps[]` {`StepOrder`, `StepCode`, `StepTitle`, `StepType`, `ContentId`, `ContentCode`, `EstimatedDurationMinutes`, `StepStatus`}, `Claims[]`).
  - "Güncel yayın" ve "kullanımda" tanımı KP-3'te: `Features/Knowledge/Path/Release/**` (önceki sürüm etkin dışı olur). **Kullan, kopyalama.**
- **Ürün:** satır `GlobalProductId` + `GlobalProductCodeDisplay`; yolculuğun ürünü `ChainContextResolver.PrimaryGlobalProduct` (KP-1 / SB-3a ortak).
- **Kapasite:** `CycleCapacity.EffectiveMaxPromoProducts()` / `EffectiveMaxNonPromoProducts()` (SB-3a).
- **Canlı veri:** STR-TUTUKON-URO aktif, satırlarında yolculuk yok (SB-3a öncesi) → bugün de `NoJourney`. Hiçbir doktorun tamamlanmış ziyareti yok → tüm ilerlemeler 0'dan.

## NE
1. **`JourneyProgress` aggregate (yeni):**
   - **Anahtar:** (TenantId, ContactId, ProductId, JourneyId); benzersiz index.
   - **Alanlar:** `CurrentStageIndex` (sıradaki, 0'dan), `LastCompletedStageIndex?`, `LastCompletedAt?`, `LastVisitReportId?`, `Cycle` (başa dönüş sayısı), `ExposureCount` (toplam gösterim), `Version` (iyimser kilit), denetim alanları.
   - **Domain metodu `Advance(stageCount, visitReportId, at)`:** `LastCompleted = Current`; `Current + 1`; son aşamadan sonra **0'a dön + `Cycle + 1`** (S3-7); `ExposureCount + 1`. SB-3c kullanır; bu pakette yalnız birim testleri.
   - `stageCount` küçüldüyse (`Current ≥ stageCount`) okuma tarafında 0 kabul edilir + gerekçe `stage_index_reset`.
   - Repository: get-by-key, list-by-contact, upsert (sürüm kontrollü). **Class-map kaydı zorunlu** (string-Guid; bkz. memory `crm-new-aggregate-classmap-guid`, `crm-classmap-rejects-unknown-elements`).
   - Bu pakette **yazan uç yok** (yalnız okuma + test).
2. **Çözücü v2** (`VisitContentSequenceResolver`; aynı sınıf, geriye uyumlu sonuç):
   - **Play** bugünkü gibi çözülür (doğrudan id / segment + üyelik).
   - **Aday ürünler:** play'in `ProductLines` — `EffectiveRole()` ile promo / non-promo ayrılır. **Şablon düzeyi `ContentBindings` KULLANILMAZ** (S3-2).
   - **Ağırlıklı rotasyon (S3-5)**, rol başına ayrı:
     - ağırlık = `LineWeightPercentage` (boşsa tüm satırlar eşit);
     - toplam = roldeki satırların `ExposureCount` toplamı;
     - açık = toplam × ağırlık / Σağırlık − ürünün `ExposureCount`'u;
     - **açığı en büyük** olanlar seçilir; eşitlikte yüksek ağırlık, sonra `SortOrder`. Deterministik.
   - **Ürün başına öğe:**
     - satırın `JourneyId`'si yoksa → ürün düşer, gerekçe `product_has_no_journey`;
     - yolculuk yayında / etkin değilse → `journey_unpublished`;
     - aşama = `JourneyProgress.CurrentStageIndex` (yoksa 0), sıralı etkin aşamalar üzerinden;
     - aşamanın yolu: `latest-published` ise aynı `PathCode` + aynı ülke / dil kimliğinin güncel yayını; `pinned` ise `RecommendedKnowledgePathId` (yayında + kullanımda değilse düşer, `stage_path_unpublished`; sessiz sürüm kayması YOK);
     - adımlar: etkin adımlar `StepOrder` sırasıyla → `{stepId, contentId, contentCode, title, type, minutes}`;
     - iddialar: yolun `Claims[]` (id + kod);
     - **düşen ürünün yerine sıradaki aday girer** (rol sınırı dolana ya da aday bitene kadar).
   - **Kitle uyuşmazlığı (§5.3, CT varsayılanı — kullanıcı onayı bekleniyor):** yolculuğun `AudienceProfileId`'si doktorun uzmanlığını kapsamıyorsa ürün **düşmez**, öğeye uyarı `journey_audience_mismatch`. Kapsama belirlenemiyorsa uyarı yok. Mevcut kitle eşleştirme mantığı varsa onu kullan; yoksa yalnız uzmanlık boyutu. Tek noktada (sabit / seçenek) tut ki "düşsün" kararı tek satırla değişebilsin.
   - **Süre:** `VisitDuration(capacity, promoÜrünSayısı, nonPromoÜrünSayısı)` (içerik öğesi sayısı değil).
   - **Sonuç:** yeni `Items[]` = `{productId, productCode, role, journeyId, journeyCode, stageId, stageIndex, stageCode, stageName, pathId, pathCode, pathVersion, steps[], claims[], warnings[]}` + toplamlar + gerekçe kodları.
     - **Geriye uyum:** bugünkü üst düzey alanlar (`JourneyId`, `StageId`, `StageIndex`, `StageCode`, `StageName`, `PromoItemCount`, `NonPromoItemCount`, süre) **ilk promo öğeden** (yoksa ilk öğeden) doldurulur; `PromoItemCount` / `NonPromoItemCount` artık ürün sayısıdır (XML notu).
     - Hiç öğe yoksa bugünkü `NotResolved` durumları + gerekçeler.
   - **ARCH GATE (S3-9):** öğelerde `strategyTemplateId` / kampanya YOK. (Bugünkü üst düzey `StrategyTemplateId` alanına dokunma; rep-facing düzeltmesi ayrı iş.)
3. **Planlama motoru:**
   - `ResolvePriorStageIndexAsync` yerine **`JourneyProgress`** okunur (doktor × ürün × yolculuk).
   - **Bekleyen ziyaret projeksiyonu (CT kararı):** ilerleme yalnız tamamlanınca artar (SB-3c). Aynı doktorun, bu ziyaretten **önceki** iptal edilmemiş ve tamamlanmamış planlarında aynı ürün varsa, aşama ve gösterim o sayı kadar ileri **projekte** edilir (aynı oluşturma çalışmasındaki önceki ziyaretler dahil). Böylece arka arkaya iki plan aynı aşamayı göstermez. SB-3c ziyaret başlatılırken gerçek ilerlemeyle tazeler.
   - `PlannedVisit.ContentItems[]` (yeni gömülü liste; class-map'e) plan anında **dondurulur**. Tekil `Content` ilk promo öğeden bugünkü probe ile dolmaya devam eder (mobil).
   - Önizleme (`DoctorContentPreview`) `Items[]` taşır.
   - `PlannedVisitDetailDto` / liste DTO'ları `contentItems` döner (ek alan; mevcut alanlar aynı).
4. **Yolculuk aşaması varsayılanı (S3-1):** yeni aşamada `PathVersionPinPolicy` gönderilmezse **`latest-published`**. Mevcut aşamalar değişmez (göç yok).
5. **Gerekçe kodları** (`VisitContentSequenceReasonCodes`'a): `product_has_no_journey`, `journey_unpublished`, `stage_path_unpublished`, `stage_index_reset`, `journey_audience_mismatch` (uyarı), mevcut `strategy_not_found` / `capacity_not_found` korunur.

## KORU / YAPMA
- **Ziyaret başlat / tamamla, `JourneyProgress` yazan uç, VisitReport `ContentActuals[]` YOK** (SB-3c).
- **Web / Platform / Auth DOKUNMA.** Mobil sözleşme yalnız EK alanla genişler; mevcut alan adı / şekli değişmez.
- Şablon yazma kuralları (SB-3a) DEĞİŞMEZ. Aktif / donmuş şablon değişmez.
- Rota optimizasyonu, slot yerleşimi, onay, frekans / izin / uygunluk probe'ları DEĞİŞMEZ.
- Ham repository yazması yok. Yeni aggregate ve gömülü tipler class-map'e (string-Guid). `DateTimeOffset` alanlarında sıralama tuzağına dikkat (memory `mongo-datetimeoffset-parallel-arrays-sort`).
- **DUR:**
  - KP-3'te "güncel yayın / kullanımda" için tek bir tanım yoksa ya da birden çok çelişen tanım varsa → raporla;
  - planlama motoru bir doktor için aynı oluşturmada birden çok ziyaret üretmiyorsa projeksiyon kuralını basitleştir ve raporla (uydurma);
  - mobil DTO'da mevcut bir alanın şeklini değiştirmek gerekiyorsa → DUR.

## Acceptance
- **E2:**
  - CRM testleri 0 kırmızı (taban **2123/0/5**; bilinen sıra flake'i hariç). **Web testleri 0 kırmızı (taban 396)** — CRM sabit / ad taşırsa Web kırılabilir. Build 0 hata.
  - **Yeni testler:**
    - `JourneyProgress.Advance`: ilerleme, son aşamadan sonra 0 + `Cycle + 1`, `ExposureCount`; class-map round-trip; benzersiz anahtar; tenant izolasyonu;
    - rotasyon: 5 promo aday / sınır 3 → açığa göre seçim; ağırlık etkisi; eşitlikte sıra; ilk ziyarette ağırlık + `SortOrder`; non-promo ayrı sayılır; `MaxPromo/NonPromo` kapasiteden;
    - ürün başına aşama: iki ürün farklı aşamada;
    - `latest-published`: yeni yayın sonrası güncel sürüm; `pinned` + etkin dışı yol → ürün düşer + sıradaki aday girer;
    - `product_has_no_journey` (SB-3a öncesi satır), `journey_unpublished`, `stage_index_reset`;
    - kitle uyuşmazlığı → uyarı, düşme yok;
    - süre = ürün sayısı × ürün süresi + rapor süresi;
    - geriye uyum: üst düzey alanlar ilk promo öğeden; tekil `Content` dolu; mobil DTO alanları aynı;
    - şablon düzeyi yolculuk bağı artık okunmuyor (S3-2);
    - bekleyen ziyaret projeksiyonu: arka arkaya iki planda aşama 0 → 1;
    - `ContentItems[]` plan anında dondurulur (yol sonra yeniden yayınlansa da plan değişmez);
    - öğelerde play / kampanya kimliği yok.
  - **Sabotaj:** (1) rotasyonda açık hesabı kaldırılınca (sabit sıra) rotasyon testi kırmızı; (2) başa dönüş kaldırılınca `Advance` testi kırmızı.
- **E4:** SB-3-UI (önizleme + ziyaret ekranı) sonrası; canlı ALMIBA play'i SB-3-UIa ile oluşturulunca planlama önizlemesinde ürün başına içerik.

## Riskler / notlar
- **Yolculuğun yeni sürümü:** satır belirli bir `JourneyId`'yi tutar; yolculuk yeni sürümle yayınlanırsa eski sürüm etkin dışı kalır → ürün `journey_unpublished` ile düşer ve şablon okumasında uyarı çıkar (SB-3a). "Satır yolculuğun güncel sürümünü izlesin" ayrı karar (CT notu, kullanıcıya sorulacak).
- Mevcut D-END-OF-JOURNEY "durur" testleri S3-7 ile değişir: testi silmek yerine yeni kurala göre güncelle ve §37 raporunda listele.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SB-3b · Yolculuk ilerlemesi (JourneyProgress) + ziyaret içerik çözücüsü v2 (çok ürün, rotasyon, ürün başına aşama)
Repository: C:\tmp\sb-3b (worktree) · Branch: wp/sb-3b · commit bu dala, push YOK · yalnız CrmService

Amaç: DESIGN-SB-3 §3.2/§3.3: planlanan ziyaret ürün listesi taşısın — play'in promo satırlarından ≤MaxPromoProducts, non-promo satırlarından ≤MaxNonPromoProducts ürün (ağırlıklı rotasyon); her ürün için kendi yolculuğundaki sıradaki aşama → aşamanın yolunun güncel yayını → adımlar/içerikler + iddialar. İlerleme doktor×ürün×yolculuk (JourneyProgress) — bu pakette OKUNUR; yazma (başlat/tamamla) SB-3c.

Önce oku: execution/domains/commercial-suite/work-packs/WP-SB-3b-journey-progress-resolver-v2.md · …/DESIGN-SB-3-multi-product-visit-content.md · …/WP-SB-3a-template-product-roles-journeys.md (§37) · services/Diten.CrmService/src/Diten.CrmService.Application/Features/VisitContentSequence/** · Features/VisitPlanning/VisitPlanningEngine.cs · Domain/Entities/{PlannedVisit, ContentEngagementJourney, KnowledgePath, StrategyTemplate, CycleCapacity}.cs · Features/Knowledge/Path/Release/** (güncel yayın/kullanımda — kullan, kopyalama) · Features/Knowledge/Chain/ChainContext.cs · Features/Knowledge/ContentEngagementJourney/IContentEngagementJourneyReader.cs · Persistence/DependencyInjection.cs · memory crm-new-aggregate-classmap-guid, crm-classmap-rejects-unknown-elements, mongo-datetimeoffset-parallel-arrays-sort, rep-facing-visit-play-campaign-invisibility.

NE:
 1) JourneyProgress aggregate: anahtar (Tenant, ContactId, ProductId, JourneyId) benzersiz; CurrentStageIndex, LastCompletedStageIndex?, LastCompletedAt?, LastVisitReportId?, Cycle, ExposureCount, Version. Domain Advance(stageCount, reportId, at): Current+1, son aşamadan sonra 0 + Cycle+1, ExposureCount+1 (yalnız birim test; yazan uç YOK). Current≥stageCount → okumada 0 + stage_index_reset. Repository + class-map (string-Guid).
 2) Çözücü v2: play bugünkü gibi; adaylar ProductLines (EffectiveRole ile promo/non-promo); şablon düzeyi ContentBindings KULLANILMAZ. Rol başına ağırlıklı rotasyon: açık = Σexposure×w/Σw − exposure; en büyük açık; eşitlikte yüksek ağırlık, sonra SortOrder; ağırlık yoksa eşit. Ürün başına: satır JourneyId yok → düşer product_has_no_journey; yolculuk yayında değil → journey_unpublished; aşama = JourneyProgress.Current (yoksa 0); yol: latest-published → aynı PathCode + aynı ülke/dil güncel yayını; pinned → RecommendedKnowledgePathId yayında+kullanımda değilse düşer stage_path_unpublished; düşenin yerine sıradaki aday. Adımlar etkin + StepOrder → {stepId, contentId, contentCode, title, type, minutes}; iddialar yolun Claims[]. Kitle uyuşmazlığı → UYARI journey_audience_mismatch (düşmez; tek noktada değiştirilebilir). Süre = VisitDuration(capacity, promoÜrün, nonPromoÜrün). Sonuç Items[] {productId, productCode, role, journeyId, journeyCode, stageId, stageIndex, stageCode, stageName, pathId, pathCode, pathVersion, steps[], claims[], warnings[]}; üst düzey eski alanlar ilk promo öğeden (geriye uyum). Öğelerde play/kampanya kimliği YOK.
 3) Planlama motoru: ResolvePriorStageIndexAsync yerine JourneyProgress; bekleyen (iptal/tamamlanmamış, daha önceki tarihli, aynı çalışma dahil) planlarda aynı ürün varsa aşama+gösterim o kadar ileri projekte; PlannedVisit.ContentItems[] (class-map) plan anında dondurulur; tekil Content ilk promo öğeden bugünkü probe ile; DoctorContentPreview Items[]; PlannedVisit detay/liste DTO'ları contentItems ekler (mevcut alanlar aynı).
 4) Yeni yolculuk aşamasında PathVersionPinPolicy gönderilmezse latest-published (mevcut aşamalar değişmez).
 5) Gerekçe kodları: product_has_no_journey, journey_unpublished, stage_path_unpublished, stage_index_reset, journey_audience_mismatch.
KORU/YAPMA: başlat/tamamla, JourneyProgress yazan uç, VisitReport.ContentActuals YOK (SB-3c); Web/Platform/Auth DOKUNMA; mobil DTO yalnız EK alan; SB-3a şablon kuralları, rota/slot/onay/frekans/izin/uygunluk DEĞİŞMEZ; ham repository yazması yok; yeni tipler class-map'e.
DOĞRULA (E2): cd C:\tmp\sb-3b; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo → 0 kırmızı (taban 2123/0/5; bilinen sıra flake'i hariç); dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 396); build 0 hata. Yeni testler WP Acceptance listesindeki her madde. D-END-OF-JOURNEY "durur" testlerini S3-7'ye göre güncelle, raporda listele. Sabotaj: (1) rotasyon açık hesabı → sabit sıra: rotasyon testi kırmızı; (2) Advance başa dönüşü kaldır: test kırmızı. Commit ("feat(crm): WP-SB-3b — journey progress + visit content resolver v2 (multi-product, weighted rotation, per-product stage)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: KP-3'te güncel yayın/kullanımda tanımı tek değilse; mobil DTO'da mevcut alan şekli değişmek zorundaysa → DUR + raporla. Motor bir doktora aynı çalışmada tek ziyaret üretiyorsa projeksiyonu sade tut ve raporla.
```

---

## §37 — CT bağımsız doğrulama (2026-10-01) — **ACCEPTED (E2)**
- **Commit:** ajan `253dc4e5` (taban `377cea16`) → `test/crm-content-visit-e2e` fast-forward. 23 dosya (+1994 / −535). Yalnız CrmService.
- **DUR yok:**
  - "güncel yayın" tek tanım: KP-3 kural dosyasına `IsCurrentRelease` / `CurrentReleaseOf` olarak eklendi (yayında + arşivsiz; PathCode + ülke + dil kimliği);
  - mobil DTO'da yalnız sona `ContentItems` eklendi (testle sabit);
  - motor aynı doktora aynı çalışmada birden çok ziyaret üretiyor (sıklık) → projeksiyon tam uygulandı.
- **Diff (K13 okuma):**
  - `JourneyProgress`: tenant'lı filtreler, benzersiz kısmi index (`IsDeleted == false`, `$ne` yok), sürüm kontrollü upsert, class-map string-Guid. `Advance` başa dönüş + `Cycle`; yazan uç yok.
  - Rotasyon saf ve deterministik (`VisitContentRotation`).
  - `VisitContentSourceReader` tüm okumaları tenant ile sınırlıyor. Kitle: yalnız `specialty` ekseni, tek anahtar `VisitContentAudiencePolicy.DropOnMismatch = false`.
  - `PlannedVisit.ContentItems[]` + 3 gömülü tip class-map'te; plan anında dondurulur.
  - Yeni aşama varsayılanı `latest-published` (yalnız yeni aşama).
- **CT testleri:** CRM **2153/0/5** (+30), Web **396/0**.
- **CT sabotajı:** aynı çalışmadaki önceki ziyaretler projeksiyondan çıkarıldı → VisitPlanning / VisitContent testlerinde 1 kırmızı. Kod geri alındı. Ajan: rotasyon açığı (3) + başa dönüş (1).
- **Ajan yorumları (kabul):**
  - bir roldeki satırlardan biri ağırlıksızsa o rolün tümü eşit ağırlık;
  - pinned + etkin dışı yol → ürün düşer (`stage_path_unpublished`), KP-3'ün `previous_path_in_use` uyarısı bunu haber veriyor;
  - projeksiyonla yeniden çözülen ziyaretin slot süresi ilk çözümden kalır;
  - SB-3b öncesi planlar (`ContentItems` yok) projeksiyonda sayılmaz.
- **CT notları (takip):**
  - **Yeniden planlama (replan):** yalnız slotları taşıyor, `ContentItems` aynı kalıyor; önizlemedeki projeksiyon değiştirilecek mevcut planları da "önceki" sayabilir. Ziyaret başlatılırken (SB-3c) içerik gerçek ilerlemeyle tazelenecek; SB-3c'de ele al.
  - Çözücü her doktor / yeniden çözümde tenant'ın tüm yollarını okuyor (`ListPathsAsync`); yol sayısı az, şimdilik kabul. Büyürse istek başı önbellek.
- **E4:** SB-3-UIb sonrası (önizlemede ürün başına içerik).
