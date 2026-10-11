# Faz 3 tasarımı — Ziyaret Planlama motoru: dönem planı, haftalar, kapasite, ürün listesi, durum okumaları

> **CT, 2026-10-07.** Kaynak kararlar: [yol haritası](ROADMAP-visit-planning.md) K-1…K-7, MK-3…MK-9, S-1…S-4 · [durum analizi](VISIT-PLANNING-current-state-analysis.md) B1–B4, C1–C5, D3, D5 · [mockup v2 analizi](mockups/visit-planning/VISIT-PLANNING-mockup-v2-analysis.md) · [K-7 karar](VISIT-PRODUCTS-without-play-decision.md).
> Kod haritası CT okumasıdır (2026-10-07, iki tarama); paketler dosya:satır kanıtını taşır.

## 1. Bugün (kısa)
- **Oturum** (`PlanningSession`): tek durum (`draft → generated → committed → archived`, yalnız ileri). Dönem başına tekillik yok. Silme / arşiv komutu yok (yalnız Update `RequestedStatus`).
- **Uygula** tüm dönemi (en çok 6 hafta) tek seferde `PlannedVisit` olarak yazar ve oturumu `committed` yapar. Yeniden açma yok.
- **Haftalar** `TargetWeekStart`'tan dönem sonuna, `MaxWeeks = 6`. Hedef haftadan önceki haftalar planlanmaz.
- **Sıklık** (`FrequencyExtendPlanner`):
  - `PeriodType`'ı yok sayıyor ("haftada 1" → dönemde 1).
  - Bilinmeyen sıklık → yalnız ilk hafta.
  - Sıklık durumu önizlemeye girmiyor.
- **Gün seçimi** (`TimeWindowInsertionEngine`): günleri sırayla 18:00'e kadar dolduruyor. Günlük sınır ve dengeleme yok, sonuç "hepsi Pazartesi". `CycleCapacity.DailyWorkMinutes` kullanılmıyor.
- **Yarım gün** modellenmemiş: Platform `IsHalfDay` veriyor, CRM denetleyicisi düşürüyor.
- **İzin engelli doktor** yine planlanıyor (bayrak taşınıyor, süzülmüyor).
- **Ürün:** `PlannedVisitContentItem` kaynak (oyun / seçim / son ziyaret / portföy) taşımıyor. Ürün yalnız oyundan geliyor; oyun yoksa ürün yok.
  - MDM'de promo / non-promo alanı yok.
  - Portföy verisi yok; bölge kapsamında `product-portfolio` reddediliyor.
- **Durum okumaları:**
  - Doktor başına hedef / yapılan / kalan / son ziyaret veren okuyucu yok.
  - `VisitReport`'ta `ContactId` yok (planlanan ziyaret üzerinden bağlanır).
  - Detay açılışında hesap başına 2–3 istek (D5).
- **Denetim:** CRM komutlarının hiçbiri "denetlenmiş" sayılmıyor (tüm izler aday). Yeni bir yazma komutu, mimari testteki listesiz komut sayısını (26) artırır.

## 2. Hedef model
### 2.1 Dönem planı (MK-3)
- Bir temsilcinin bir dönem için **tek etkin planı** olur (arşivli hariç). Yeni oluşturmada aynı temsilci + dönem için etkin plan varsa 409 `planning_session_exists` döner ve mevcut plan kimliğini taşır.
- **Göç yok:** bugünkü çoklu / eski planlar okunur ve listede kalır.
- Plan **dönem hedeflerini** taşır (bugünkü seçim). Haftalar sıklığa göre bu hedeflerden üretilir (K-2).
- Haftaları önizleme hesaplar, **saklanmaz**. Saklanan tek şey **onaylı haftalardır**.
- **Hafta durumu:**

| durum | anlamı |
|---|---|
| `past` | hafta sonu bugünden önce (türetilir) |
| `approved` | onaylandı; ziyaretleri `PlannedVisit` olarak yazıldı, dondurulmuş (S-1) |
| `draft` | onaysız, ziyaret üretilebilir; önizleme hesaplar |
| `empty` | bu haftaya ziyaret düşmüyor |

- `PlanningSession.Weeks[]` yalnız onaylı (ve bir kez onaylanıp yeniden açılmış) haftaları tutar:
  - `WeekStart`, `Status` (`approved | reopened`), `ApprovedAt / By`, `PlannedVisitIds`, `ManualVisitOrder`;
  - `History[]`: `{ at, by, action: approve | reopen, reason }` (MK-4).

### 2.2 Haftayı onayla / yeniden aç
- **Onayla** = mevcut `apply` ucu **`weekStart` ile**. Yalnız o haftanın ziyaretleri yazılır, hafta `approved` olur.
  - Geçmiş hafta onaylanamaz.
  - Bir sonraki hafta kendiliğinden taslak görünür (K-2; hesaplanır, yazılmaz).
- `weekStart` verilmeyen eski `apply` davranışı Faz 4 Web'i yeni akışa geçirene kadar aynen kalır (kullanımdan kalkacak diye işaretlenir).
- **Yeniden aç** = **yeni komut** `ReopenPlanningWeek` (gerekçe zorunlu, ≥ 10 karakter):
  - haftanın **sonuç / raporu olmayan** ziyaretleri `cancelled` olur (neden `week_reopened`);
  - sonucu / raporu olanlar kalır ve yeniden üretimde sabit sayılır;
  - gerekçe haftanın geçmişine yazılır.
  - Merkezi denetime bağlanması **Faz 8**'de (AUD-CRM-1).
  - ⚠ Mimari testteki listesiz komut sayısı **26 → 27**. Bu Faz 8'de kapanır (yol haritası Faz 8 maddesi).
- **Boş taslakları sil (D3):** mevcut Update `RequestedStatus = archived`. Yalnız hedefi ve onaylı haftası olmayan planda izin verilir. Yeni komut yok.

### 2.3 Sıklık ve dağılım (B1, B2, MK-7)
- Dönemde gereken ziyaret = politika sayısı × dönemdeki `PeriodType` birimi:
  - hafta → dönemdeki çalışma haftası sayısı;
  - ay → dönemdeki ay sayısı;
  - dönem → 1.
- **Bilinmeyen sıklık** = dönemde 1 + `frequencyStatus = unknown` rozeti.
- **Eczane** = hesap politikası, yoksa dönemde 1.
- **Kalan** = gereken − (yapılan + onaylı haftalardaki planlı). Kalan, **kalan haftalara eşit aralıkla** dağıtılır (ör. 13 haftada 2 → yaklaşık 6–7 hafta arayla). `MaxWeeks = 6` kalkar; ufuk dönem sonudur.

### 2.4 Gün ve kapasite (B-5, B-7, MK-6, MK-8, MK-9, C1–C5)
- **Günlük bütçe** (dakika) = `DailyWorkMinutes − DailyFixedMinutes`; yarım günde yarısı; tatilde 0.
- **Haftalık kapasite** = günlük bütçelerin toplamı. Önizleme hafta başına şunları verir:
  - `{ weekStart, workingDays, halfDays, holidays, capacityMinutes, plannedMinutes, visitCount }`;
  - ayrıca dönem toplamı (C5: arz ve talep aynı birimle).
- **Dengeleme:**
  - ziyaretler kurum (hesap) grupları halinde haftanın çalışma günlerine yük dengeli dağıtılır (büyükten küçüğe, en boş güne);
  - aynı kurumun doktorları aynı gün kalır;
  - gün içi sıra rota iyileştiricisinden.
- **Taşma (MK-6):** haftaya sığmayan ziyaret sonraki haftaya kayar. Önizlemede `shifted[]` listelenir: `{ target, fromWeek, toWeek, reason: capacity_full | holiday }`. Dönem sonunu aşan `unscheduled: period_exhausted` olur.
- **Süre:** ziyaretin ürün listesinden, tipik modelle (C4).
- **İzin engelli doktor** planlanmaz: `unscheduled: consent_blocked`.

### 2.5 Ziyaret ürün listesi (K-7, S-1…S-4)
- `PlannedVisitContentItem` ek alanlar alır (geriye uyumlu):
  - `source`: `play | rep-pick | last-visit | portfolio`;
  - `order`.
  - Ürün yalnızsa (yolculuk yok) `journeyId` boş kalır ve `warnings` alanında `no_approved_content` bulunur.
- **Temsilcinin seçimi** planda doktor başına saklanır: `SelectedContacts[].Products[{ productId, role }]`.
  - Yazma yolu mevcut Update (D9 `MergeSelection`: null = koru) — **yeni komut yok** (S-4).
  - Toplu uygulama istemcide birleşir, aynı uçla yazılır.
- **Liste nasıl dolar:**
  1. Oyun ürünleri. Rol oyundan gelir ve kilitlidir (S-3); planlamada çıkarılamaz.
  2. Temsilcinin eklediği ürünler. Rol temsilciden gelir; varsayılan promo, çünkü MDM'de alan yok (K-7d).
  3. Son ziyarette sunulanlar: **SB-3c'ye kadar boş** (ertelendi).
  4. Portföy: veri yok. "Portföy tanımlı değil" durumunda **otomatik doldurma yapılmaz**; seçici tüm ürünleri sunar.
- **Sınır:** kapasitedeki `MaxPromo / MaxNonPromo`. Aşan ürün o ziyarette `overflowProducts` olur ve sıradaki ziyarete kalır (S-2, K-7e).
- **Karışık sıra (K-7e):** temsilci seçiminde set aynı kalır, doktorun dönemdeki n'inci ziyaretinde sıra n−1 kayar. Oyun ürünlerinde bugünkü ağırlıklı döngü kalır.
- **Oyunsuz ürünün içeriği:** ürünün konusunda (Subject birincil dış referansı = ürün) dil / kitleye uyan **tek** yayımlanmış yolculuk varsa o kullanılır. Yoksa ya da birden fazlaysa ürün içeriksiz gider ve `no_approved_content` / `ambiguous_journey` uyarısı alır (K-7f).
- **S-1:** onaylı haftalardaki ziyaretler ürün değişikliğinden etkilenmez; yalnız taslak / öngörülen haftalar yeniden hesaplanır.
- **E7-B1 (CT varsayılanı):** yolun yalnız **ana dalının** adımları listelenir (ilk dal / varsayılan yol). Diğer dallar ziyarete düzleştirilmez.
- **E7-B2 (CT varsayılanı):** süre ürün sayısından kalır; adım dakikası yalnız bilgi olarak gösterilir.

### 2.6 Durum okumaları (B-6, B-9, D5)
- **Doktor başına dönem durumu:**
  - gereken, yapılan, onaylı-planlı, kalan;
  - son ziyaret tarihi;
  - bu hafta görülmeli mi (dağılıma göre vadesi gelmiş);
  - sıklık durumu;
  - segment rozeti (B-9; bilgi amaçlı, K-4);
  - izin / pasif.
- "Yapılan" = sonucu `completed` olan planlı ziyaret (rapor üzerinden; `VisitReport` planlanan ziyarete bağlı).
- **Uçlar (yalnız okuma):**
  - `GET api/crm/visit-plan/my-accounts/{accountId}/doctors?planningSessionId=` → bir kurumun doktorları + durum sütunları. Hedefler ekranının doktor tablosu; `quick=due | never | all`.
  - `GET api/crm/visit-plan/sessions/{id}/targets` → planın seçili kurum / eczane / doktorlarının adları, türleri, adresleri ve durumları tek istekte (D5).
  - `GET api/crm/accounts/related?accountIds=` → toplu ilişkili eczane (D5).
- Önizleme her ziyaret / doktor için `frequencyStatus` + `requiredVisitCount` taşır.

## 3. Paketler ve sıra
| Paket | Kapsam | Bağımlılık |
|---|---|---|
| **WP-VP-3A** | Dönem planı + hafta durumu + onayla / yeniden aç + tek plan kuralı + boş taslak arşivi + sıklık (PeriodType, bilinmeyen, eczane) + dönem ufku | — (ilk) |
| **WP-VP-3D** | Durum okumaları: doktor durumu, hedef özeti, toplu okumalar, önizlemede sıklık alanları, segment rozeti | 3A ile **paralel** olabilir (ayrı dosyalar; birleştirmede küçük çakışma `VisitPlanningModels.cs`) |
| **WP-VP-3B** | Gün dengeleme + günlük / haftalık kapasite + yarım gün + taşma + izin engelli | 3A'dan sonra (motor) |
| **WP-VP-3C** | Ziyaret ürün listesi (K-7) + oyunsuz içerik + karışık sıra + sınır / taşma + E7-B1 | 3A ve 3B'den sonra (motor + oturum) |

Her paket: Web yalnız **zorunlu uyum** (mevcut ekran kırılmasın). Yeni ekran Faz 4'te. Mobil yalnız **ek** alan. Testler üretim koduyla, sabotaj kanıtlı.

## 4. Açık / CT varsayılanı (kullanıcı itiraz etmezse)
| # | Konu | CT varsayılanı |
|---|---|---|
| F3-1 | Yeniden açma yeni komut | Kabul; listesiz komut 26 → 27, Faz 8'de bağlanır |
| F3-2 | Eski (tüm dönem `committed`) planlar | Olduğu gibi kalır; hafta modeli yeni planlar için; eski planda yeniden açma yok |
| F3-3 | Yeniden açmada yapılmış ziyaretler | Sonucu / raporu olanlar kalır; diğerleri `cancelled (week_reopened)` |
| F3-4 | E7-B1 dal seçimi | Yalnız ana dal |
| F3-5 | E7-B2 adım süresi | Süreye girmez (yalnız bilgi) |
| F3-6 | Portföy | Veri yok → otomatik doldurma yok, seçicide tüm ürünler; MDM portföy eşlemesi ileride |
