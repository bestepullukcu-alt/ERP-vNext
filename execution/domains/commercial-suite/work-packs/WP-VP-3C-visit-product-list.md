# WORK PACKAGE — WP-VP-3C · Ziyaret ürün listesi (K-7): kaynak, temsilci seçimi, oyunsuz içerik, karışık sıra, sınır / taşma

> **CT (SoR), 2026-10-07.**
> - **Tasarım:** [DESIGN-VP-FAZ3](DESIGN-VP-FAZ3-planning-engine.md) §2.5 · [K-7 karar](VISIT-PRODUCTS-without-play-decision.md) (K-7a…g kabul) · [mockup v2 analizi](mockups/visit-planning/VISIT-PLANNING-mockup-v2-analysis.md) S-1…S-4 (kabul).
> - **Kullanıcı:** "Faz 3 … paketle" (2026-10-07).
> - **Kapsam:** CRM (planlanan ziyaret içerik kalemi, oturum seçimi, içerik çözücüsü, motor, oyunsuz yolculuk okuyucusu). Web yalnız zorunlu uyum; ekran Faz 4'te.
> - **Ön koşul:** **WP-VP-3A ve WP-VP-3B birleşmiş olmalı.**
>
> **Çalışma yeri:** worktree `C:\tmp\vp-3c`, dal `wp/vp-3c`, taban `test/crm-content-visit-e2e` (3A + 3B sonrası). Commit bu dala, push YOK.

## Bağlam (CT kod okuması, 2026-10-07; satırlar 3A/3B sonrası kayabilir)
- **`PlannedVisitContentItem`** (`Domain/Entities/PlannedVisit.cs:243–265`):
  - alanlar: `ProductId, ProductCode, Role, JourneyId…StageName, PathId/Code/Version, Steps[], Claims[], Warnings`.
  - **Eksik:** `Source`, sıra, kalem başına süre.
  - `ContentItems` :133: "plan anında DONDURULUR", oyun kimliği taşımaz (ARCH GATE S3-9).
  - `PlannedDurationMinutes` :59.
- **`ContentItems`'ı yazan tek yer motor** (`VisitPlanningEngine.cs:541`, `ToContentItem` :632–653).
  - Planlanan ziyaret oluştur / güncelle komutları ürün listesi almıyor (`CreatePlannedVisitCommand.cs:13–46`, `UpdatePlannedVisitCommand.cs:12–38`). Faz 6 konusu, dokunma.
- **Çözücü** (`Application/Features/VisitContentSequence/VisitContentSequenceResolver.cs`):
  - oyun seçimi :159–191 (yoksa `no-strategy`, 0 kalem, süre 0 :62–67);
  - rol başına en çok `EffectiveMaxPromo / NonPromo` (varsayılan 3) :96–125;
  - kalem kurma :194–269 (yolculuk yok → `product_has_no_journey`, yayımlanmamış → düşer);
  - süre :308–317 → `CycleCapacity.VisitMinutes(p, n)`.
- **Döngü** `VisitContentRotation.cs:21–46`: ağırlıklı açık (gap); maruziyet = `JourneyProgress.ExposureCount` + bekleyen planlı ziyaretler (:355–358).
- **Oyun satırı** (`Domain/Entities/StrategyTemplate.cs:259–304`): `GlobalProductId, Role` (null = promo), `JourneyId, LineWeightPercentage, SortOrder`.
- **MDM'de promo / non-promo alanı yok** (`GlobalProduct.cs:5–14`). Seçici `GET api/global-products/selector` (Id, CanonicalCode, GlobalProductName; en çok 100 / sayfa, `search` var).
- **Portföy:** veri, arayüz ve okuyucu yok. `product-portfolio` kapsamı reddediliyor (`TerritoryBusinessScopeResolver.cs:36–40`).
- **Ürün → yolculuk:**
  - Ürün bağı Subject'in birincil dış referansı (`SourceSystem = global-product`), tanım `ChainContextResolver.PrimaryGlobalProduct` (`Application/Features/Knowledge/Chain/ChainContext.cs:45, 87–100`).
  - `IContentEngagementJourneyReader.ResolvePublishedJourneysAsync` konu / konu başlığı / kitle / dil ile süzüyor, **ürünle değil** (`IContentEngagementJourneyReader.cs:7–12, 47–84`).
  - Tek yolculuk–ürün denetimi oyun satırı doğrulayıcısında (`StrategyTemplateBindingValidator.cs:84–121`).
- **E7-B1:** adımlar yolun iki dalını düzleştiriyor (E2E: Ana akış 3 + Kısa akış 2). Adımların kurulduğu yer `BuildItemAsync` (+ yol okuyucusu).

## NE
### 1. Veri modeli (ek, geriye uyumlu)
- `PlannedVisitContentItem` + `Source` (`play | rep-pick | last-visit | portfolio`; eski kayıtlarda null → okumada `play`) + `Order` (int).
  - Ürünü olup yolculuğu olmayan kalem: `JourneyId` boş + `Warnings: [no_approved_content]` ya da `[ambiguous_journey]`.
- Planlama seçimi: `SelectedContacts[].Products[]`: `{ ProductId, ProductCode, Role (promo | non-promo) }`.
  - Yazma yolu **mevcut oturum güncellemesi** (D9 `MergeSelection`: doktorun `Products` null = koru, [] = temizle). **Yeni komut YOK** (S-4).
  - Doğrulama:
    - ürün MDM'de var mı (`selector` ya da tekil okuma; MDM kapalıysa fail-closed 503 `product_lookup_unavailable`);
    - rol sözlüğü;
    - doktor başına en çok 20 ürün.
- Class-map: yeni tipler kayıtlı (GUID tuzağı), eski belgeler okunur.

### 2. Liste kurma kuralı (tek yer, çözücüde)
Doktor × ziyaret için sıra:
1. **Oyun** ürünleri (varsa; bugünkü seçim + ağırlıklı döngü). `source = play`, rol oyundan, **kilitli** (S-3).
2. **Temsilcinin seçimi** (`rep-pick`): oyun ürünlerine ek olanlar ya da oyun yoksa tek kaynak. Rol seçimden; seçimde rol yoksa **promo** (K-7d).
3. **Son ziyaret** (`last-visit`): **SB-3c'ye kadar boş.** Arayüz noktasını bırak, kaynak boş döner.
4. **Portföy** (`portfolio`): veri yok → otomatik doldurma yok. Önizlemede `portfolioStatus = undefined`.

- Aynı ürün iki kaynaktan gelirse önceki kaynak kazanır.
- **Sınır** (`EffectiveMaxPromo / NonPromo`): aşan kalemler o ziyarete alınmaz.
  - Önizlemede ziyaret başına `overflowProducts[]`: `{ productId, productCode, role, reason: max_promo | max_non_promo }`.
  - Sıradaki ziyarette öne alınır (K-7e, S-2).
- **Karışık sıra (K-7e):** `rep-pick` seti aynı kalır; doktorun dönem içindeki n'inci ziyaretinde sıra (n−1) kayar (A, B, C → B, C, A). Oyun kalemleri bugünkü ağırlıklı döngüyle. `Order` alanı son sırayı taşır.
- **Süre:** listedeki promo / non-promo sayısından (`VisitMinutes`); ürünsüz ziyaret yalnız rapor süresi + `no_products` uyarısı (K-7a).

### 3. Oyunsuz ürünün içeriği (K-7f)
- Yeni okuyucu: `ResolvePublishedJourneysForProductAsync(productId, language?, audienceProfileId?)`.
  - Ürünün birincil dış referansı olan Subject'leri bul (`PrimaryGlobalProduct` tek tanım) → bu konulardaki yayımlanmış, yürürlükteki yolculuklar.
  - Mevcut okuyucuya ekle; konu okuması toplu.
- Sonuç:
  - **tek** yolculuk → aşama ilerlemesi oyunlu kalemle aynı kural (JourneyProgress + bekleyen);
  - yoksa → `no_approved_content`;
  - birden fazla → `ambiguous_journey` (ürün içeriksiz gider).
- Tanıtım ürününde onaylı içerik yoksa planlanır, uyarı taşır (K-7f; engel yok).

### 4. E7-B1 — yalnız ana dal (CT varsayılanı F3-4)
- Kalem adımları yolun **ana dalından** alınır. Ana dal = yolun varsayılan / ilk dalı; model alanını bul, tanımını raporla. Diğer dallar ziyarete düzleştirilmez.
- E2E verisinde TUTUKON kaleminde 5 yerine 3 adım beklenir.
- E7-B2 (F3-5): adım dakikası süreye girmez.

### 5. S-1 — onaylı haftalar dondurulmuş
- Seçim değişince yalnız taslak / öngörülen haftaların ziyaretleri yeniden hesaplanır. Onaylı haftaların (3A) `ContentItems`'ı değişmez. Testle kanıtla.

### 6. Önizleme ve okuma alanları (ek)
- Slot: `contentItems[].source`, `order`, `overflowProducts[]`, `productWarnings[]`.
- Doktor içerik özeti: `products[]` (sonraki ziyaretin ürünleri + kaynak), `durationMinutes`.
- Dönem özeti: `productDistribution[{ productId, productCode, doctorCount }]`, `doctorsWithoutProducts`.
- Hafta özeti: `productVisitCounts[{ productId, productCode, visits, promoVisits }]` (mockup "ürün başına haftalık ziyaret").
- `portfolioStatus` (`defined | undefined`).
- Planlanan ziyaret liste / ayrıntı DTO'larında `contentItems[].source` (mobil ek alan).

### 7. Web zorunlu uyum
- Bugünkü Hedefler "Hedefleri kaydet" yolu doktor `Products`'ını göndermez (null = koru); seçim silinmez.
- Ürün seçici Faz 4'te.

## KORU / YAPMA
- **Yeni yazma komutu YOK.** Seçim mevcut oturum güncellemesiyle; listesiz sayı değişmez.
- Planlanan ziyaret oluştur / güncelle komutlarına ürün listesi **eklenmez** (tek ziyaret düzeltmesi Faz 6). `ContentItems` yine yalnız motorca yazılır.
- Oyun / kampanya kimliği içerik kalemine **girmez** (ARCH GATE S3-9). Temsilci oyunu görmez (K-3); `source = play` yalnız "önerilen" anlamında.
- 3A (hafta / onay / sıklık) ve 3B (gün / kapasite / taşma) kuralları değişmez.
- MDM'ye ve Platform'a dokunma; MDM'de rol alanı açma. Portföy verisi / arayüzü YOK.
- `JourneyProgress` yazma YOK (SB-3c).
- Seed / grant / göç / indeks YOK. Mobil yalnız ek alan.

## Acceptance
### E2 (taban ölç, yalnız farkı raporla)
CRM / Web / mimari: 3B kabulündeki değerler. Build 0 hata.

**Yeni testler (üretim koduyla):**
1. Oyunlu doktor: oyun kalemleri `source = play`, rol oyundan; temsilci ek ürünü `rep-pick` olarak eklenir; aynı ürün iki kaynakta → `play` kazanır.
2. Oyunsuz doktor + seçim [A promo, B non-promo] → kalemler `rep-pick`, süre `VisitMinutes(1, 1)`; seçimsiz → 0 kalem, `no_products` uyarısı, süre yalnız rapor.
3. Karışık sıra: aynı doktorun 1., 2., 3. ziyaretinde A, B, C → B, C, A → C, A, B.
4. Sınır: 4 promo seçim, en çok 3 → 3 kalem + `overflowProducts` 1; sonraki ziyarette taşan öne alınır.
5. Oyunsuz ürün içeriği: tek yayımlanmış yolculuk → yolculuk / aşama dolu; hiç yok → `no_approved_content`; iki yolculuk → `ambiguous_journey`.
6. E7-B1: iki dallı yolda yalnız ana dal adımları.
7. S-1: onaylı haftanın `ContentItems`'ı seçim değişikliğinden sonra aynı; taslak hafta yeni listeyi alır.
8. Seçim yazma: `Products` null → korunur, [] → temizlenir; geçersiz ürün → 400; MDM kapalı → 503 `product_lookup_unavailable`.
9. Eski `ContentItems` belgesi (Source yok) okunur → `play`.
10. Önizleme özetleri: `productDistribution`, `doctorsWithoutProducts`, `productVisitCounts` doğru.

**Sabotaj (kırmızı kanıtla, geri al):**
1. Karışık sıra kaymasını kaldır → test 3 kırmızı.
2. Onaylı hafta dondurmayı atla → test 7 kırmızı.

### E4 (CT, fleet; kullanıcı onaylı test kaydında)
- Oyunsuz bir doktora seçim (ör. TUTUKON + başka ürün) → önizlemede `rep-pick` kalemler, süre değişimi, 2. ziyarette kaymış sıra.
- TUTUKON kaleminde 3 adım (E7-B1).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (3A + 3B kabulünden SONRA)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VP-3C · Ziyaret ürün listesi (K-7): kaynak, temsilci seçimi, oyunsuz içerik, karışık sıra, sınır/taşma
Repository: C:\tmp\vp-3c (worktree) · Branch: wp/vp-3c · commit bu dala, push YOK

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-VP-3C-visit-product-list.md — önce tamamını oku (Bağlam dosya:satır CT okumasıdır, 3A/3B sonrası kayabilir; doğrula). Tasarım: …/DESIGN-VP-FAZ3-planning-engine.md (§2.5, §4) · …/VISIT-PRODUCTS-without-play-decision.md · …/mockups/visit-planning/VISIT-PLANNING-mockup-v2-analysis.md (S-1..S-4). 3A ve 3B §37'lerini oku. Ayrıca: services/Diten.CrmService/src/**/Features/{VisitContentSequence,VisitPlanning,Knowledge/ContentEngagementJourney,Knowledge/Chain,StrategyTemplate/Binding}/** · **/Domain/Entities/{PlannedVisit,PlanningSession,StrategyTemplate,CycleCapacity,KnowledgePath}.cs · MDM GlobalProduct selector (YALNIZ OKU).
NE:
(1) Model: PlannedVisitContentItem + Source (play|rep-pick|last-visit|portfolio; eski null → play) + Order; yolculuksuz kalem JourneyId boş + Warnings no_approved_content|ambiguous_journey. Oturum seçimi SelectedContacts[].Products[{ProductId,ProductCode,Role}] mevcut güncellemeyle (MergeSelection null=koru, []=temizle), doğrulama (MDM ürün var mı, fail-closed 503 product_lookup_unavailable; rol; ≤20); class-map.
(2) Liste kuralı tek yerde (çözücü): oyun (kilitli rol) → rep-pick (rol yoksa promo) → last-visit (SB-3c'ye kadar boş) → portfolio (veri yok, otomatik yok, portfolioStatus=undefined); aynı ürün → önceki kaynak; sınır aşanı overflowProducts + sonraki ziyarette öne; karışık sıra rep-pick'te n'inci ziyarette (n−1) kayma, oyun kalemleri ağırlıklı döngü; süre listeden, ürünsüz → no_products.
(3) Yeni okuyucu ResolvePublishedJourneysForProductAsync (PrimaryGlobalProduct tek tanım, toplu konu okuma): tek → aşama ilerlemesi oyunluyla aynı, yok → no_approved_content, çok → ambiguous_journey.
(4) E7-B1: yalnız ana dalın adımları (ana dal tanımını raporla); E7-B2: adım dakikası süreye girmez.
(5) S-1: onaylı haftaların ContentItems'ı değişmez.
(6) Önizleme/okuma ek alanları: slot source/order/overflowProducts/productWarnings; doktor products[]+durationMinutes; dönem productDistribution + doctorsWithoutProducts; hafta productVisitCounts; portfolioStatus; planlanan ziyaret DTO contentItems[].source.
(7) Web zorunlu uyum: Hedefleri kaydet Products göndermez (null=koru).
KORU/YAPMA: YENİ YAZMA KOMUTU YOK; planlanan ziyaret oluştur/güncelleye ürün listesi EKLENMEZ (Faz 6); oyun/kampanya kimliği kaleme girmez (S3-9), temsilci oyunu görmez; 3A/3B kuralları değişmez; MDM/Platform'a dokunma, rol alanı açma, portföy YOK; JourneyProgress yazma YOK; seed/grant/göç/indeks YOK; mobil yalnız ek alan.
DOĞRULA (E2): tabanı 3B sonrası ölç, yalnız farkı raporla — CRM Application · Web · mimari (listesiz sayı DEĞİŞMEZ); build 0 hata; fleet açıkken Web bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad>. Yeni testler WP Acceptance 1–10. Sabotaj 1–2 (kırmızı kanıtla, geri al).
Commit: "feat(crm): WP-VP-3C — visit product list with source (play/rep-pick), rep selection on the plan, play-less content by product, rotating order, overflow" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde başına ne yapıldı + kanıt (dosya:satır, test adı), ana dal tanımı, ürün→yolculuk okuyucusu maliyeti, mobil için yeni alanlar, S-1 kanıtı. §22 TÜRKÇE. K13.
```

---

## §37 CT kabul — E2 ACCEPTED (2026-10-07)
**Commit:** `e5d3a6b7a` (ff; taban `286cdba3c`, 3A + 3D + 3B dahil). Push: test dalı.

**CT K13:**
| Paket | Taban | Sonuç |
|---|---|---|
| CRM Application | 2398/0/5 | **2410/0/5** (+12; 6 koşudan ikisinde bilinen kararsızlar — PII, ContactWorkbook — ilgisiz) |
| Web | 737/0 | **738/0** (+1) |
| Mimari | 38/1 (27) | **38/1 (27 sabit)** — yeni yazma komutu yok |

**Kod okuması:**
- Liste kuralı tek yerde (`VisitContentSequenceResolver`): oyun (rol kilitli) → rep-pick (rol yoksa promo); aynı ürün oyunda kalır.
- Karışık sıra `RotatedPicks` (n − 1 kayma, taşan öne).
- Ürün → yolculuk okuyucusu `PrimaryGlobalProduct` üzerinden, ürün sayısından bağımsız 3 okuma.
- Ana dal `VisitContentMainBranch`: zincir şablonu `Branches[SortOrder]` ile yolun doldurduğu ilk dal, yoksa ilk adımın dalı.
- Seçim yazma mevcut oturum güncellemesiyle (`PlanningSessionProductPick`; null = koru, [] = temizle, ≤ 20, rol, MDM fail-closed 503); oyun şablonlarının MDM ürün doğrulayıcısı yeniden kullanıldı.

**CT sabotajı (ajanınkinden ayrı):** "aynı ürün oyunda kalır" süzgeci kaldırıldı + ana dal süzgeci kapatıldı → **2 kırmızı** (`Play_items_keep_their_role…`, `Only_the_main_branch_steps_are_told…`). Geri alındı.

**Bilinen / takip (Faz 4 / kullanıcı bilgisi):**
- **Oyun önce gelir:** oyun ürünleri rol sınırını tek başına doldurursa temsilcinin eklediği ürün her ziyarette taşar (`overflowProducts`). K-7b "sınır içinde ekleyebilir" ile tutarlı. Faz 4 ekranı bu durumu uyarıyla göstermeli ("sınır dolu — eklenen ürün sığmıyor").
- Ürünsüz ziyaret artık yalnız rapor süresi + `no_products` (K-7a); iki eski test buna göre güncellendi.
- Ziyaret Yürütme takvimindeki `plannedContent` özetinde `source` yok (paket istemedi) → Faz 4 / Faz 6'da gerekirse ek alan.
- **E4:** TUTUKON zincir şablonunda ana dalın `SortOrder` ile ilk sırada olduğu doğrulanmalı (değilse kısa akış gelir).
- Mobil alanları Faz 5 sözleşme notunda toplanacak.

**E4 (CT, bekliyor; Faz 3 tek tur):** oyunsuz doktora seçim (kullanıcı onaylı test kaydı) → `rep-pick` kalemler, süre değişimi, 2. ziyarette kaymış sıra · TUTUKON kaleminde 3 adım.
