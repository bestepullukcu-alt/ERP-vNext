# WORK PACKAGE — WP-VW-W2 · 3. TUR: W2-WEB-c (tasarım uyumu) ∥ W2-BE-d (panel verisi) — 2026-10-10

> **Kaynaklar:**
> - [Tasarım farkları](mockups/visit-workspace/DESIGN-GAP-W2-2026-10-09.md): T1–T10, D1–D6, P1–P3, L1–L9 + kullanıcı kararları.
> - Ana belgeler: [WP-VW-W2](WP-VW-W2-calendar-workspace.md), [2. tur](WP-VW-W2-round2.md).
>
> **Taban:** test dalı (CRM 2553/0/5, Web 861/0, mimari 27).
>
> **Kullanıcı kararları:**
> - Durum süzgeci **çoklu seçim** kalır (görünüm mockup stilinde).
> - Süre tahmini: içerik adımı süresi varsa o, yoksa planlanan ziyaret süresi, ikisi de yoksa gösterilmez.
> - Ön sipariş düğmesi yok (W6).
>
> **Çalışma yerleri:**
> - WEB-c: `C:\tmp\vw-w2-web-c` / `wp/vw-w2-web-c`;
> - BE-d: `C:\tmp\vw-w2-be-d` / `wp/vw-w2-be-d`.
>
> İki paket paraleldir. **Sabit sözleşme adları (BE-d → WEB-c):**
> - `periodName`;
> - `visits[].specialtyCode`, `visits[].specialtyLabel`;
> - `visits[].badges[]`;
> - `visits[].accountAddress`;
> - `visits[].pinMoveReason`.

## W2-BE-d — panel verisi + iki takip (CRM)
1. **Dönem adı (T1):** takvim yanıtına `periodName` (görünen haftaların dönemi; birden fazlaysa ilk hafta).
2. **Uzmanlık (D1, L6):** ziyaret öğesine `specialtyCode` + `specialtyLabel`.
   - Etiket referans verisinden, kiracının / isteğin dilinde (4I dil etiketi kuralı, `label_<dil>`); yoksa `display_name`.
   - Kodda sabit etiket YOK. TR etiketi eksikse veri sahibi işidir; raporla, betikle yama YOK.
3. **Rozetler (D1, L6):** `badges[]`: 3D durum okumasındaki segment / potansiyel rozetleri (mevcut kaynak). Yoksa boş.
4. **Kurum adresi (D2):** `accountAddress` (adres satırı + ilçe / il; kurum kaydından; toplu okuma).
5. **Kayma nedeni:** taslak ziyaret öğesine `pinMoveReason` (önizlemenin `pinOverflow` / taşıma nedeni: `pin_time_conflict`, `pin_time_past_day_end`, `pin_day_full`, `pin_overflow`…). Web tahmin etmeyi bırakacak.
6. **Takip — önbellek damgası:** `StampAsync` kiracının TÜM planlı ziyaretlerini okuyup süzüyor. Temsilciye göre okumaya geçir (depo sorgusu `resourceId` ile); davranış aynı.
- **Acceptance:**
  - 1–5 alanlar testle (uzmanlık etiketi dil seçimi: tr var → tr, yok → `display_name`);
  - 5: saatli sabit çakışmasında `pinMoveReason`;
  - 6: damga temsilci süzgeçli okuma (sahte depo çağrı parametresi);
  - CRM tabanı yeşil, mimari 27.
- **Sabotajlar** (her biri kırmızı kanıtlanır, sonra geri alınır):
  - uzmanlıkta dil seçimi kaldırılsın;
  - `pinMoveReason` boş dönsün.

## W2-WEB-c — tasarım uyumu (Web)
> Mockup'la **görsel olarak** karşılaştırarak yap. Her maddeyi mockup ekranı açıkken uygula. `mockups/visit-workspace` dizini yerel sunucu (`py -m http.server`) ile açılabilir. Oturum açık kullanıcı sekmesine dokunma.

**Sayfa başı ve takvim kartı:**
- **T1:** dönem çipi (`periodName`).
- **T2 / L1:** Planla / Yürüt bölmeli düğmesi başlığın altında, solda. Yanında "+ Plan dışı ziyaret" dolu birincil düğme, **iki modda da**.
- **T3 / L2:**
  - hafta şeridi KALDIRILIR;
  - tek takvim kartı: ‹ › Bugün + tarih aralığı başlığı; süzgeçler (kurum, durum **çoklu**, ürün) ve Gün / Hafta / Ay bölmeli düğmesi kartın başlığında;
  - FullCalendar araç çubuğu gizlenir (`headerToolbar:false`, kendi başlığımız `calendar.prev/next/today/changeView`'ı çağırır);
  - "Bugün" tek.
- **T4 / L3:** hafta başlığı takvim kartının içinde:
  - "42. Hafta · 12–16 Eki" (büyük H);
  - durum çipi ("Taslak · otomatik" / "Onaylı");
  - "Bu hafta 38,3 sa kapasite · 13,8 sa planlı" + ince çubuk;
  - "⚠ N ziyaret sığmadı" sarı çip (tıklanınca liste);
  - "Haftayı onayla" lacivert / "🔒 Haftayı yeniden aç" çerçeveli.

**Izgara ve kartlar:**
- **T5:** gün başlığı: ad + "Bugün" rozeti + gün doluluk mini çubuğu + "4 ziyaret · boş 4,9 sa" (2 satırı geçmez).
- **T6:** `allDaySlot:false`; dilim yüksekliği mockup'a yakın (saat satırı uzun, etiket :30); bugün sütunu açık mavi; şimdi çizgisi.
- **T7 / L4:** kart:
  - durum rengine göre pastel zemin + sol kenar (taslak kesikli);
  - sağ üstte durum simgesi;
  - saat, **ad kalın**, kurum soluk (`accountDisplayName`), ürün çipleri (tanıtım dolu, hatırlatma çerçeveli);
  - alt satırda durum metni ("Rapor için 31 sa kaldı", "→ 13 Eki", "Değiştir (54 dk)");
  - metin ellipsis ile kesilir, üst üste binmez; dar sütunda kompakt kart (saat + ad).
- **T8:** durum süzgeci çoklu, "Tüm durumlar" etiketli.
- **T9:** Ay görünümü (gün hücresinde ziyaret sayısı + durum noktaları; tıklanınca o güne Gün görünümü).
- **T10 / L9:** altta açıklama satırı (durum renkleri, sabit, plan dışı, ürün tanıtım / hatırlatma).

**Ayrıntı paneli:**
- **D1:** durum çipi, büyük ad, uzmanlık (`specialtyLabel`) + rozetler (`badges`).
- **D2:** kurum bloğu: harita yer tutucu kutu, kurum adı, `accountAddress`, "Sal 6 Eki · 14:00–14:25 · 25 dk".
- **D3:** durum uyarı metni mockup'taki gibi.
- **D4:**
  - NE SUNACAĞIM: numaralı ürün kartları (ürün adı + rol çipi + "N içerik adımı" + varsa adım süresi);
  - sağ üstte "Sıklık: dönemde N · yapılan/gereken";
  - altta "1 tanıtım + 1 hatırlatma + rapor ≈ X dk" (kullanıcı kuralı).
- **D6:** eylemler panelin altında, tam genişlik.
- **D5:** W4'e kalır; bölüm son ziyaret tarihi + sonucu gösterir.

**E2 (iptal / yapılamadı / ertele):**
- **P1:**
  - tek pencere, üç sekme (İptal et · Yapılamadı · Ertele);
  - alt başlık "Ad · 8 Eki Perşembe 11:30";
  - sekmeye göre kısa açıklama;
  - geçerli olmayan sekme pasif (ör. geçmiş günde "İptal et").
- **P2:** erteleme günleri 4 sütunlu kart ızgarası (gün, mini çubuk, "4 ziyaret · boş 4,9 sa").

**Planla modu paneli:**
- **L5:** "HEDEFLER" başlığı + simge, "42. Hafta · sürükleyip güne bırakın"; kurum seçimi "(N)" doktor sayısıyla.
- **L6:** doktor kartı:
  - ⋮⋮ tutamaç; ad kalın;
  - renkli uzmanlık çipi (`specialtyLabel`);
  - planlı ürün çipleri;
  - "dönemde N · yapılan/kalan · **Sal 10:15**" (bu haftaki yeri taslak ziyaretlerden);
  - segment çipi.
- **L7:** "Tümünü seç" + "Ürün uygula (N)" tek satır; hızlı süzgeçler küçük, tek satır.
- **L8:** seçim özeti kartı: büyük sayılar (doktor / eczane / hesap) + ürün dağılımı.
- **Kayma nedeni:** `pinMoveReason` gelirse onu göster (tahmin kodu kalır, yalnız alan yoksa).

**Diller ve RTL:** yeni anahtarlar 7 dilde, yankısız; Arapçada RTL (bölmeli düğmeler, ızgara, panel).

### Acceptance (W2-WEB-c)
1. Saf düzen kuralları Node'da:
   - durum → zemin / simge / alt satır metni;
   - kompakt kart eşiği;
   - süre tahmini kuralı (adım > planlanan > yok);
   - Ay hücresi özeti;
   - E2 sekme uygunluğu (geçmişte iptal pasif).
2. Duman testi (sahte DOM):
   - şerit yok;
   - takvim kartı başlığında süzgeçler + Gün / Hafta / Ay;
   - `allDaySlot:false`;
   - `headerToolbar:false`;
   - E2 tek pencere 3 sekme;
   - ertele 4 sütun ızgara;
   - Planla'da "Plan dışı ziyaret" görünür;
   - `createError` null.
3. Panel: uzmanlık etiketi `specialtyLabel` (kod gösterilmez), rozetler, kurum bloğu, numaralı ürün kartları.
4. 7 dil dolu, yankısız.
5. Ziyaret Planlama testleri yeşil (ortak modül değişirse).
- **Sabotajlar** (her biri kırmızı kanıtlanır, sonra geri alınır):
  - şerit geri gelsin → 2 kırmızı;
  - süre kuralında adım süresi yok sayılsın → 1 kırmızı.

---

## §36.1 Agent Prompt — W2-BE-d (paste-ready)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W2 · W2-BE-d — panel verisi (dönem adı, uzmanlık etiketi, rozetler, kurum adresi, kayma nedeni) + önbellek damgası temsilciye göre okuma
Repository: C:\tmp\vw-w2-be-d (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w2-be-d · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W2-round3.md — önce oku (W2-BE-d 1–6 + Acceptance). Bağlam: WP-VW-W2-round2.md §37 (CachedWorkspacePlanPreviewSource, StampAsync takip notu), mockups/visit-workspace/DESIGN-GAP-W2-2026-10-09.md (T1, D1, D2, L6), WP-VP-4I §37 (dil etiketi kuralı label_<dil>), 3D ContactPeriodStatusReader (badges). Kod: services/Diten.CrmService/src/**/Features/{VisitWorkspace,VisitPlanning,PlannedVisit}/** · Infrastructure (referans verisi okuyucu) · Persistence (planlı ziyaret deposu).
NE: (1) takvim yanıtına periodName. (2) ziyaret öğesine specialtyCode + specialtyLabel (referans verisi, kiracı/istek dili, yoksa display_name; kodda sabit etiket YOK; TR etiketi eksikse raporla, betikle yama YOK). (3) badges[] (3D segment/potansiyel rozetleri). (4) accountAddress (adres satırı + ilçe/il, toplu okuma). (5) taslak öğeye pinMoveReason (önizleme pinOverflow/taşıma nedeni). (6) StampAsync temsilciye göre depo okuması (davranış aynı). Alan adları SABİT (WEB-c paralel): periodName, specialtyCode, specialtyLabel, badges, accountAddress, pinMoveReason.
KORU/YAPMA: ARCH GATE; TenantId; yeni yazma komutu yok (mimari 27); göç/seed/grant yok; canlı veriye yazma yok; W2-BE-c önbellek davranışı aynı.
DOĞRULA (E2): CRM 2553/0/5 tabanı (PiiMasking_… bilinen kararsız) · mimari 27. Testler belge Acceptance; sabotaj 2 (uzmanlıkta dil seçimi kaldır → kırmızı; pinMoveReason boş → kırmızı; geri al — git checkout -- YOK, touch). dotnet test -o çıktısı REPO İÇİNDE.
Commit: "feat(crm): WP-VW-W2-BE-d — workspace panel data: period name, specialty label, badges, account address, pin move reason; stamp by resource" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, uzmanlık etiketi kaynağı ve TR etiket durumu (kaç kod TR etiketsiz), mobil için yeni alanlar, elle denenecekler. §22 TÜRKÇE. K13.
```

## §36.1 Agent Prompt — W2-WEB-c (paste-ready)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W2 · W2-WEB-c — Ziyaret Çalışma Alanı tasarım uyumu (mockup ile görsel eşleşme): sayfa başı, takvim kartı, ızgara, kartlar, Ay görünümü, açıklama satırı, ayrıntı paneli, E2 tek pencere, Planla paneli
Repository: C:\tmp\vw-w2-web-c (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w2-web-c · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W2-round3.md — önce oku (W2-WEB-c + Acceptance + kullanıcı kararları). FARK LİSTESİ: execution/domains/commercial-suite/work-packs/mockups/visit-workspace/DESIGN-GAP-W2-2026-10-09.md (T1–T10, D1–D6, P1–P3, L1–L9). MOCKUP: aynı dizindeki "Ziyaret Calisma Alani (standalone).html" (Ekran seçici: E1 Takvim — Yürüt / Planla, E1 Ayrıntı — *, E2) + decoded/01-takvim.html, 00-tema.css — her maddeyi mockup ekranı açıkken GÖRSEL karşılaştırarak yap (dizini py -m http.server ile ayrı bir sekmede aç; oturum açık kullanıcı sekmesine dokunma). Backend: W2-BE-d PARALEL — sabit alanlar periodName, visits[].specialtyCode/specialtyLabel, visits[].badges[], visits[].accountAddress, visits[].pinMoveReason (yoksa: çip/rozet/adres gizli, kayma nedeni tahmini kalır). Kod: frontend/Diten.Web/wwwroot/assets/js/CRM/VisitWorkspace/** (visit-workspace.js, workspace-core.js, visit-workspace.css) · Views/CRM/VisitWorkspace/** · Resources/Views/CRM/VisitWorkspace/** · VisitPlanning/targets-core.js (ortak; değişirse Ziyaret Planlama testleri yeşil) · shared/diten-calendar.js (yalnız seçenek; davranış değiştirme).
NE: fark listesindeki maddeler: T1 dönem çipi; T2/L1 Planla/Yürüt başlık altında solda + "+ Plan dışı ziyaret" iki modda; T3/L2 hafta şeridi KALDIR, tek takvim kartı başlığı (‹ › Bugün + tarih, süzgeçler kurum/durum ÇOKLU/ürün, Gün/Hafta/Ay), FullCalendar headerToolbar:false; T4/L3 hafta başlığı kart içinde ("42. Hafta", durum çipi, "Bu hafta X sa kapasite · Y sa planlı" + çubuk, sarı "⚠ N ziyaret sığmadı" çipi, onayla lacivert / 🔒 yeniden aç); T5 gün başlığı (Bugün rozeti + mini çubuk + "N ziyaret · boş X sa", ≤2 satır); T6 allDaySlot:false, uzun saat satırı, :30 etiket, bugün sütunu açık mavi, şimdi çizgisi; T7/L4 kart (pastel zemin + sol kenar, taslak kesikli, sağ üst simge, saat + ad kalın + kurum soluk + ürün çipleri tanıtım dolu/hatırlatma çerçeveli + alt satır durum metni, ellipsis, dar sütunda kompakt); T8 çoklu "Tüm durumlar"; T9 Ay görünümü; T10/L9 açıklama satırı; D1 durum çipi + ad + specialtyLabel + badges; D2 kurum bloğu (harita yer tutucu, ad, accountAddress, tarih · saat · süre); D3 uyarı metni; D4 numaralı ürün kartları + "Sıklık: dönemde N · a/b" + "… ≈ X dk" (süre: adım süresi > planlanan süre > gösterme); D6 eylemler altta; P1 E2 tek pencere 3 sekme + alt başlık tarih/saat + sekme açıklaması + geçersiz sekme pasif; P2 erteleme 4 sütun gün kartı ızgarası; L5 HEDEFLER başlığı + kurum (N); L6 doktor kartı (tutamaç, ad kalın, renkli uzmanlık çipi, planlı ürün çipleri, "dönemde N · a/b · Sal 10:15", segment çipi); L7 Tümünü seç + Ürün uygula tek satır, hızlı süzgeçler küçük tek satır; L8 seçim özeti kartı (büyük sayılar + ürün dağılımı); pinMoveReason varsa onu göster. 7 DİL yankısız, Arapça RTL.
KORU/YAPMA: ARCH GATE; davranış (veri, istekler, iki adımlı Kaydet, sürükle-bırak kuralları, yetkiler) DEĞİŞMEZ — bu paket görünüm ve düzen; CRM koduna dokunma; yeni JS kütüphanesi yok; DitenCalendar.create sırası (arama tabloları create'ten ÖNCE); oturum açık kullanıcı sekmesine mock/harness enjekte etme, kalıcı yazma yok.
DOĞRULA (E2): Web 861/0 tabanı · Ziyaret Planlama testleri yeşil · CRM dokunulmaz · mimari 27. Testler belge Acceptance 1–5 (saf düzen kuralları Node'da; duman testi sahte DOM: şerit yok, kart başlığında süzgeçler + Gün/Hafta/Ay, allDaySlot:false, headerToolbar:false, E2 tek pencere 3 sekme, ertele 4 sütun, Planla'da plan dışı düğmesi, createError null); sabotaj 2 (şerit geri → kırmızı; süre kuralında adım süresi yok sayılsın → kırmızı; geri al — git checkout -- YOK). dotnet test -o çıktısı REPO İÇİNDE.
Commit: "feat(web): WP-VW-W2-WEB-c — visit workspace design fit (calendar card, cards, month view, legend, detail panel, one E2 dialog, plan panel)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde madde (T/D/P/L kodlarıyla) yapıldı / bilinçli sapma, mockup'la görsel karşılaştırma notu, BE-d'ye bağlı kısımlar, elle denenecekler. §22 TÜRKÇE. K13.
```

---

## §37 CT kabul — W2-BE-d E2 ACCEPTED (2026-10-10)
**Commit:** `726f403c2` (ajan `d4fd8e58c`, cherry-pick, çakışma yok). Push: test dalı.

**CT K13:**
- CRM 2553 → **2558/0/5**. PiiMasking bilinen kararsız test bir koşuda düştü, tekrarında yeşil.
- Mimari **27**.

**Kod okuması:**
- `WorkspaceReferenceLabels` mevcut `LabelOf` kuralını kullanıyor (`label_<dil>` → `display_name`); etiketi bulunamayan kod gösterilmiyor.
- Rozetler 3D ile aynı kaynak (yöntem paylaşıldı, kopya yok).
- `pinMoveReason` önizleme `PinOverflow` listesinden geliyor.
- Damga `ListByResource` ile okunuyor.

**CT sabotajı:** etiketi bulunmayan kod ham hâliyle dönsün → 2 kırmızı (`D2`, `D3_D4`). Geri alındı, touch yapıldı.

**Veri bulguları (kod değil, veri sahibi işi; betikle yama YOK):**
1. `medical-specialty` setindeki 22 aktif kodun hepsinde `label_tr` yok; uzmanlıklar İngilizce görünecek. Çözüm: Referans Verileri ekranından `label_tr` girilmesi.
2. Kurumların `CityRef` değeri `TR-01-ADANA` biçiminde (demo yükleme, bölge kodu); `city` setinin kodları ise `adana` biçiminde. 81 / 81 eşleşmiyor; adreste il çıkmıyor.
3. `DistrictRef` boş; `district` setinde yalnız 9 Edirne ilçesi var.

İl / ilçe / uzmanlık setlerinde TR etiket yok.

**Bekleyen:** W2-WEB-c.

## §37 CT kabul — W2-WEB-c E2 ACCEPTED (2026-10-10)
**Commit:** `1a129eef5` (ajan `87f42d85f`, cherry-pick, çakışma yok). Push: test dalı.

**CT K13:**
- Web 861 → **865/0**. Ziyaret Planlama yeşil (`targets-core.js` / `diten-calendar.js` değişmedi).
- CRM dokunulmadı; mimari 27.

**Ajanın görsel karşılaştırması:** ayrı sekmede statik sahne (worktree'nin gerçek CSS/JS + sahte veri) ↔ mockup, 1440 genişlik. Testlerin göremediği 5 CSS hatası bulup düzeltti:
- `#vw-calendar` zaten `.fc` olduğundan iç kurallar tutmuyordu;
- Sneat -24px kenar boşluğu;
- mesai penceresi olmayan günlerin gri gölgesi;
- `dc-event-body` daralması;
- renk değişkenleri panel / E2 dışında kalıyordu.

**Bilinçli sapmalar (CT kabul):**
- saatsiz ziyaret 08:30 diliminde işaretli;
- "Değiştir (N dk)" yok (DTO'da rapor zamanı yok);
- süre tahmini şimdilik planlanan süre (W3 `steps[].durationMinutes` gelince kendiliğinden geçer);
- sığmayanlar modal;
- Planla satırında "Bu hafta görülmeli" rozeti yok (hızlı süzgeçte var).

**CT sabotajı:** E2 sekme uygunluğu kapatıldı (hepsi etkin) → 2 kırmızı (`A1`, `A2` design fit). Geri alındı, touch yapıldı; tam tur 865/0.

**Sıradaki:** fleet yeniden başlatılınca canlı E4 + mockup ile görsel karşılaştırma (Yürüt, Planla, Ayrıntı, E2, Ay, RTL, telefon).
