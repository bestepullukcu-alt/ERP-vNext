# TASARIM BELGESİ — DESIGN-SB-3 · Çok ürünlü ziyaret içeriği (ürün başına yolculuk aşaması + ziyaret başlat / tamamla)

> **CT (SoR), 2026-10-01.**
> - **Kararlar:** bridge-decision §9 + kullanıcı cevapları (2026-10-01).
> - **Kod analizi:** CT alt ajanı (aşağıdaki §1 kanıtları).
> - **Bağımlılık:** Bilgi Yolu Stüdyosu (KP-1..UI-2) birleşti. Yollar zincirli + MLR'li + yayında.
> - **Modüller:** MOD-0155 (ziyaret planlama / rapor), MOD-0167 (strateji şablonu), MOD-0165 (dönem kapasitesi), MOD-0162 (yolculuk / yol).

## 0. Hedef model (kullanıcı)
```
Zincir Şablonu  → konular sırası
Bilgi Yolu      → zincire içerik + iddia + sayfa (ürün × kitle × ülke/dil), MLR'li, yayında
Etkileşim Yolculuğu → ürün + kitle için yolları ziyaret sırasına dizer (1., 2., 3. ziyaret…)
Strateji Şablonu → hangi doktorlara (segment) hangi ürünler: her ürün satırı = rol (promo/non-promo) + yolculuk
Ziyaret         → en fazla 3 promo + 3 non-promo ürün (dönüşümlü seçim);
                  her ürün için o ürünün yolculuğundaki SIRADAKİ aşama → yolun güncel yayını → içerikler
Ziyaret yap     → ziyaret günü/saatinde "Ziyaret yap" → başlar → tamamla → her ürünün aşaması ilerler
Yolculuk sonu   → başa döner
```

## 1. Bugünkü durum (CT analizi, kanıtlı)
- **Ziyaret düzeyinde ürün listesi yok; yalnız promo / non-promo SAYISI.**
  - `VisitPlanningEngine.cs:253-294` → `VisitContentSequenceResolver`;
  - promo kümesi = play'deki tüm `ProductLines` + SKU (`Resolver.cs:185-204`);
  - non-promo hiçbir yerde tanımlı değil.
- **3 + 3 sınırı yok:** ne kodda ne eski sistemde. Eski sistemde 3 promo süre satırı (RowId 16/17/18) benziyor ama sınır olarak yazılmamış.
- **CycleCapacity:** yalnız `PromoProductTime` / `NonPromoProductTime` (ürün başına dk) + `MaxMinutesPerVisit = 480`.
- **Aşama imleci doktor başına tek** (`VisitPlanningEngine.cs:519-529`):
  - yolculuk / ürün filtresi yok;
  - kaynağı plan, rapor değil.
- **PlannedVisit.Content** ve **VisitReport.ContentActuals** TEKİL (tek yolculuk / aşama). Rapordan ilerleme okuması kapsam dışı bırakılmıştı (F-STAGE-READ).
- **PlannedVisit durumları:** draft / planned / confirmed / cancelled / archived. **"başladı / tamamlandı" yok.**
- **VisitReport:** draft / submitted / amended (`PlannedVisitId` bağlı).
- **StrategyTemplate:**
  - `ContentBindings` (knowledge-path | content-engagement-journey) ürün satırlarından bağımsız;
  - çözücü yalnız ilk yolculuk bağını alıyor;
  - `LineWeightPercentage` / SKU % planlamada okunmuyor.
- **Yolculuk:** `SubjectId` (→ ürün), `AudienceProfileId?`, `LanguageCode?`; aynı ürün + kitle + dil için birden çok yayında yolculuk mümkün (V-J10 yalnız kod + dil).
- **Mobil:** `PlannedVisitDetailDto.Content` (tekil) okunuyor. Liste değişikliği sözleşmeyi etkiler.

## 2. Kararlar
| # | Karar | Kaynak |
|---|---|---|
| S3-1 | Aşamalar varsayılan **"yolun en son yayındaki sürümünü izle"**; sabitleme istisna | §9.1 |
| S3-2 | **Doğrudan yol bağı yok**; içerik ziyarete yolculuk üzerinden gelir | §9.2 |
| S3-3 | Ziyarette **en fazla 3 promo + 3 non-promo ürün** | §9.3 |
| S3-4 | **Ürünler strateji şablonundan:** her ürün satırı = **rol** (promo / non-promo) + **yolculuk** (o ürünün yayındaki yolculuğu, açık seçim) | kullanıcı |
| S3-5 | 3'ten fazla aday varsa **dönüşümlü (ağırlıklı rotasyon)** | kullanıcı |
| S3-6 | **Aşama ilerlemesi ziyaret gerçekleşince:** ziyaret günü / saatinde "Ziyaret yap" ile başlatılır → tamamlanınca o ziyaretteki her ürünün yolculuğu bir sonraki aşamaya geçer | kullanıcı |
| S3-7 | **Yolculuk bitince başa döner** (1. aşama, tur sayacı +1) | kullanıcı |
| S3-8 | **CT:** 3 + 3 sınırı **Dönem Kapasitesi**'nde `MaxPromoProducts` / `MaxNonPromoProducts` (varsayılan 3 / 3), şablonda değil | CT |
| S3-9 | **ARCH GATE korunur:** temsilci play / kampanya görmez; ziyarette yalnız ürünler + içerik | memory `rep-facing-visit-play-campaign-invisibility` |

## 3. Model

### 3.1 Strateji şablonu — ürün satırı
- `ProductLine` **genişler:**
  - `Role` (`promo` | `non-promo`, zorunlu);
  - `JourneyId` (zorunlu, yayında yolculuk; konusu satırın ürünü, ülke / dil play kapsamıyla uyumlu);
  - mevcut `LineWeightPercentage` + SKU dağılımı kalır.
- **Doğrulama:**
  - aynı ürün iki kez yok (mevcut);
  - satır ürünü = yolculuk konusunun ürünü (409 `journey_product_mismatch`);
  - yolculuk yayında (409 `journey_not_published`).
- **Şablon düzeyi `ContentBindings`:**
  - ziyarette **kullanılmaz** (S3-2);
  - `knowledge-path` türü yeni bağ olarak **kabul edilmez**;
  - mevcut bağlar okunur, ekranda "eski bağ" notu.
  - Canlıda yolculuğa bağlı play yok; göç yok.

### 3.2 Yolculuk ilerlemesi — `JourneyProgress` (yeni aggregate)
- **Anahtar:** (Tenant, ContactId, ProductId, JourneyId).
- **Alanlar:** `CurrentStageIndex` (sıradaki), `LastCompletedStageIndex?`, `LastCompletedAt?`, `LastVisitReportId?`, `Cycle` (başa dönüş sayısı), `ExposureCount` (rotasyon için toplam gösterim).
- **Tamamlanan ziyarette** her ürün için:
  - `LastCompleted = CurrentStage`;
  - `CurrentStage + 1`;
  - son aşamadan sonra **0'a dön, `Cycle + 1`** (S3-7);
  - `ExposureCount + 1`.
- Yolculuk değişirse (satıra başka yolculuk seçildi) yeni anahtar → 0'dan başlar; eskisi tarih olarak kalır.

### 3.3 Ziyaret içerik çözücüsü (`VisitContentSequenceResolver` v2)
- **Girdi:** doktor, segment / play (sunucu türetir), dönem, tarih.
- **Adımlar:**
  1. play'in promo satırlarından **en fazla `MaxPromoProducts`**, non-promo satırlarından **en fazla `MaxNonPromoProducts`** ürün seçilir: **ağırlıklı rotasyon** (S3-5).
     - Her ürün için "hak edilen gösterim" = toplam gösterim × satır ağırlığı.
     - **Açığı en büyük olanlar** seçilir (deterministik; eşitlikte satır sırası). `JourneyProgress.ExposureCount` kullanılır.
  2. **Her seçilen ürün için:**
     - yolculuk → `CurrentStageIndex` aşaması;
     - aşamanın yolunun **en son yayındaki** sürümü (S3-1; sabitlenmişse o sürüm);
     - yol adımları **sırasıyla** → içerik listesi (id, başlık, tip, süre) + adımdaki iddialar.
  3. **Süre:** promo ürün sayısı × `PromoProductTime` + non-promo × `NonPromoProductTime` + rapor süresi (mevcut formül, ürün sayısıyla).
- **Çıktı:** `items[] {productId, productName, role, journeyId, stageIndex, stageName, pathId, pathCode, pathVersion, steps[] {contentId, title, type, minutes, claims[]}}` + toplamlar + gerekçe kodları.
  - **Gerekçe kodları:** `no_strategy`, `product_has_no_journey`, `journey_unpublished`, `stage_path_unpublished`, `capacity_not_found`.
- **Ürün düşmesi:** bir ürünün yolu yayında değilse o ürün **düşer**, gerekçe yazılır, sıradaki aday girer.

### 3.4 Planlanan ziyaret ve rapor sözleşmesi
- **`PlannedVisit.ContentItems[]`** (yukarıdaki item yapısının anlık görüntüsü; plan anında dondurulur).
  - **Geriye uyum:** tekil `Content` ilk promo öğeden doldurulmaya devam eder (mobil eski sözleşme). Mobil ekibe yeni alan bildirilir.
- **Ziyaret yürütme durumu (S3-6)** — `PlannedVisit` yeni durumlar: `in-progress`, `completed`.
  - **"Ziyaret yap" (başlat):** yalnız ziyaret **günü** ve planlanan saat penceresi içinde (önce / sonra tolerans ayarı, varsayılan ±2 saat); yalnız atanan temsilci.
    - Plan `in-progress` olur.
    - **Taslak VisitReport** açılır; `ContentActuals[]` plandaki öğelerle ön doldurulur.
  - **Ziyarette:** temsilci ürün başına "anlatıldı" işaretini değiştirebilir (varsayılan hepsi anlatıldı). Örnekler vb. mevcut rapor alanları.
  - **"Tamamla":** rapor **submit** + plan `completed`.
    - **Yalnız "anlatıldı" işaretli ürünlerin** `JourneyProgress`'i ilerler.
    - Anlatılmayan ürün bir sonraki ziyarette aynı aşamayla gelir.
- **`VisitReport.ContentActuals[]`:** `{productId, journeyId, stageIndex, pathId, pathVersion, presented: bool, presentedStepIds?}`. Tekil alan eski raporlar için okunur kalır.

### 3.5 Sınır ayarı
- `CycleCapacity` (dönem kapasitesi): `MaxPromoProducts` (varsayılan 3), `MaxNonPromoProducts` (varsayılan 3).
- Eski kayıtta yoksa 3 / 3 varsayılır.
- Süre formülü ürün sayısıyla tutarlı.

## 4. Paketler
| Paket | Kapsam | Katman | Bağımlı |
|---|---|---|---|
| **SB-3a** | Strateji şablonu ürün satırı rol + yolculuk; doğrulama; şablon düzeyi yol bağı yeni kabul edilmez; dönem kapasitesi max promo / non-promo (3 / 3) | CRM | — |
| **SB-3b** | `JourneyProgress` aggregate; çözücü v2 (rotasyon, ürün başına aşama, son yayını izle, yol adımları → içerik listesi, süre); planlama motoru `ContentItems[]` yazar; `Content` geriye uyum | CRM | SB-3a |
| **SB-3c** | Ziyaret yürütme: Başlat (gün / saat penceresi, temsilci) → `in-progress` + taslak rapor; Tamamla → rapor submit + `completed` + "anlatıldı" ürünlerin ilerlemesi (başa dönüş); `ContentActuals[]` | CRM | SB-3b |
| **SB-3-UI** | Strateji şablonu ürün satırında rol + yolculuk seçici; dönem kapasitesinde 3 / 3; planlama önizlemesinde ürün başına içerik; planlanan ziyarette "Ziyaret yap" / "Tamamla" + ürün "anlatıldı" işaretleri; ziyaret raporunda ürün listesi | Web | SB-3a..c |
| **SB-3-MOB** | Mobil sözleşme notu: yeni `ContentItems[]`, başlat / tamamla uçları (Android ekibine) | doküman | SB-3c |

## 5. Açık sorular (paketleme sırasında)
1. **Başlatma penceresi:** ±2 saat tolerans uygun mu? Gün dışı başlatma (geç ziyaret) yönetici onayıyla mı?
2. **Ürün "anlatıldı" işareti:** varsayılan "hepsi anlatıldı" mı, yoksa temsilci tek tek işaretlesin mi?
3. **Kitle eşleşmesi:** yolculuğun kitlesi (ör. Nefroloji / Doktor) ile doktorun uzmanlığı tutmuyorsa ürün düşsün mü, uyarı mı?
4. **Non-promo ürünlerin kaynağı:** şablonun non-promo satırları yeterli mi, yoksa tüm portföyden mi gelmeli?
