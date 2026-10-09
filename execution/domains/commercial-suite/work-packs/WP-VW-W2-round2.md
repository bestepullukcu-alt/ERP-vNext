# WORK PACKAGE — WP-VW-W2 · 2. TUR: W2-BE-c ∥ W2-WEB-b (2026-10-09)

> **Ana belge:** [WP-VW-W2-calendar-workspace.md](WP-VW-W2-calendar-workspace.md). KORU/YAPMA, sözleşme ekleri ve tüm §37'ler oradadır.
> - **Taban:** test dalı, W2-BE-a / BE-b / WEB-a ve CT düzeltmeleri dahil (CRM 2543/0/5, Web 854/0, mimari 27).
> - **Kaynak:** WEB-a canlı E4 bulguları + kullanıcı istekleri (tatil adı, gün dengesi).
> - **Çalışma yerleri:**
>   - BE-c: `C:\tmp\vw-w2-be-c` / `wp/vw-w2-be-c`;
>   - WEB-b: `C:\tmp\vw-w2-web-b` / `wp/vw-w2-web-b`.
> - İki paket paraleldir.
> - **Sabit sözleşme adları:** `accountDisplayName`, `weeks[].sessionVersion`, `weeks[].unplaced[]`, `days[].holidayName`.

## W2-BE-c — hız, kurum adı, tatil adı, sığmayanlar listesi, gün dengesi (CRM)
### C1. Hız (öncelikli)
- **Sorun:** `GET visit-workspace/calendar` 1 hafta da 5 hafta da ≈ 7 sn; `reschedule-options` de aynı.
- **Neden:** her okumada temsilcinin oturumları için dönemin TÜM taslak önizlemesi (motor + geo) baştan hesaplanıyor (`IWorkspacePlanPreviewSource`).
- **Çözüm — önizleme önbelleği:**
  - Anahtar: kiracı + oturum kimliği + oturum sürümü + gün (UTC) + yazılmış ziyaretlerin girdi damgası.
  - Oturum ya da yazılmış ziyaret değişince anahtar değişir.
  - Süre ve boyut sınırı var; kiracılar arası paylaşım yok.
  - Ajan önizlemeyi etkileyen girdileri koddan çıkarır ve raporlar.
- **Kanıt:** sahte motor çağrı sayısı. Aynı anahtarla ikinci okuma motoru çağırmaz; oturum ya da yazılmış ziyaret değişince yeniden çağırır.
- **Hedef:** ikinci okuma < 1,5 sn.

### C2. Kurum adı
- Takvim ziyaret öğesine `accountDisplayName` eklenir: yazılmış ve taslak ziyaretlerde, mevcut ad okuyucusuyla tek toplu okuma.
- Web kurum süzgeci bunu kullanacak.

### C3. Tatil adı
- `days[].holidayName` dolu gelir: Working Calendar çözümleyicisinin tatil adı, kiracının dilinde, yoksa İngilizce.
- Kaynak adı taşımıyorsa çözümleyici sözleşmesi genişletilir (yalnız okuma). Kaynağı raporlanır.

### C4. Sığmayanlar listesi + oturum sürümü
- `weeks[].unplaced[]`: `{ targetType, targetId, displayName, accountDisplayName, reason }`. Sayı `unplacedCount` ile aynı.
- `weeks[].sessionVersion`: onay / yeniden açmanın beklediği sürüm.

### C5. Gün dengesi (motor)
- **Sorun:** canlıda taslak haftalarda Pzt 29 ziyaret / 425 dk, Sal 3 / 43 dk, Çar 12, Per 7, Cum 3. Tek kurumdaki çok doktor aynı güne toplanıyor; 4G kümesi bölünmüyor.
- **Kural (CT):**
  - kurum kümesinin toplam süresi gün bütçesinin **%60**'ını aşıyorsa küme en fazla **2 güne** bölünebilir (aynı kurum; geo sıra korunur);
  - günler arası yük farkı (en dolu − en boş) bütçenin %50'sini aşmamaya çalışır (yumuşak hedef);
  - sabitler (gün / saat) ve onaylı haftalar dokunulmaz;
  - kararlı: aynı girdi, aynı sonuç.
- **Kabul:** "1 kurumda 20 doktor + 15 dağınık doktor" senaryosu:
  - hiçbir gün bütçenin %100'ünü aşmaz;
  - en dolu − en boş ≤ bütçenin %50'si;
  - kurum ≤ 2 güne bölünür;
  - 4G / 4L / W2-BE-b testleri yeşil (beklenti değişirse gerekçe).

### Acceptance (W2-BE-c)
- C1 önbellek (motor çağrı sayısı), C2–C4 alanlar, C5 senaryo + eski motor testleri.
- **Sabotajlar** (her biri kırmızı kanıtlanır, sonra geri alınır):
  - önbellek anahtarından oturum sürümü çıkarılsın → C1 kırmızı;
  - %60 bölme kapatılsın → C5 kırmızı.
- **Mimari:** 27 (yeni yazma komutu yok).

## W2-WEB-b — Planla modu + kurum süzgeci + yeni metinler (Web)
### P1. Planla / Yürüt geçişi
- Planla modunda taslak hafta düzenlenebilir.
- Onaylı / geçmiş hafta salt okunur; panelde "N. Hafta onaylı — değiştirmek için Haftayı yeniden aç" uyarısı.
- Yürüt modu WEB-a ekranıdır.

### P2. Hedefler paneli (takvimin yanında; mobilde gizli)
- **Mockup:**
  - kurum seçimi;
  - sayılı hızlı süzgeçler;
  - doktor satırları: işaret, ad, uzmanlık, sıklık, "bu hafta görülmeli", segment;
  - "Tümünü seç";
  - seçim özeti: doktor / eczane / hesap.
- **Yeniden kullanım:** Ziyaret Planlama'nın 4J / 4L / 4M Hedefler mantığı (`details.js`, `targets.js`, `page.js`) **yeniden kullanılır, kopyalanmaz**. Gerekirse ortak modüle ayrılır; Ziyaret Planlama sayfası aynen çalışır (testleri yeşil).
- **İşaretle → plana ekle:** mevcut seçim güncellemesi; motor yerleştirir; takvim yenilenir.

### P3. Sürükle-bırak (DitenCalendar `onExternalDrop` / `eventDrop`)
- **Hedefler satırını güne / saate bırak** → `dayPins[{ date, scope:"visit", startTime }]`:
  - saat 15 dk'ya yuvarlanır;
  - tüm gün alanına bırakılırsa saatsiz.
- **Taslak önizleme kartını başka gün / saate sürükle** → aynı sabit güncellemesi.
- **Onaylı / geçmiş haftada** sürükleme kapalı.
- **Kod → kullanıcı metni:**
  - `pin_time_invalid`;
  - `pin_time_outside_hours`;
  - `pin_time_conflict`;
  - `pin_time_past_day_end` = "Ziyaret mesai bitişini aşıyor, erkene alındı";
  - `pin_overflow`;
  - `pin_day_full`;
  - `week_already_approved`.

### P4. Ürün uygula (toplu)
- Seçili doktorların bu haftaki ziyaretlerine uygulanır: ilk ürün **tanıtım**, diğerleri **hatırlatma**.
- Mevcut ürün seçim yazımı kullanılır. Ürün **adları** gösterilir.

### P5. Kurum süzgeci, sığmayanlar, sürüm
- **Kurum süzgeci:** `accountDisplayName` ile. Alan yoksa süzgeç gizli; doktor adı **asla** kullanılmaz.
- **"N ziyaret sığmadı":** `weeks[].unplaced[]` listesini gösterir (ad, kurum, neden metni).
- **Onay / yeniden açma:** `weeks[].sessionVersion` kullanılır; ayrı oturum okuması kalkar.

### P6. Diller
- Yeni anahtarlar 7 dilde, yankı yok; Arapçada RTL.

### Acceptance (W2-WEB-b)
1. Mod geçişi: Planla'da taslak hafta düzenlenebilir; onaylıda salt okunur uyarısı.
2. Hedefler paneli Ziyaret Planlama ile aynı modülü kullanıyor (kopya yok); Ziyaret Planlama testleri yeşil.
3. Bırakma → doğru `dayPins` isteği: gün / saat, 15 dk yuvarlama, tüm gün = saatsiz. Onaylı haftada istek yok.
4. Ürün uygula → ilk tanıtım, diğerleri hatırlatma isteği.
5. Kurum süzgeci `accountDisplayName` (doktor adı yok); sığmayanlar listesi.
6. Kod → metin eşlemesi (7 kod); 7 dil dolu.
- **Sabotajlar** (her biri kırmızı kanıtlanır, sonra geri alınır):
  - yuvarlama kaldırılsın → 3 kırmızı;
  - onaylıda sürükleme açık kalsın → 3 kırmızı.

---

## §36.1 Agent Prompt — W2-BE-c (paste-ready)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W2 · W2-BE-c — takvim hızı (önizleme önbelleği), kurum adı, tatil adı, sığmayanlar listesi + oturum sürümü, gün dengesi (motor)
Repository: C:\tmp\vw-w2-be-c (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w2-be-c · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W2-round2.md — önce oku (W2-BE-c: C1–C5 + Acceptance). Ana belge: WP-VW-W2-calendar-workspace.md (KORU/YAPMA, sözleşme ekleri, W2-BE-a/BE-b §37 ve canlı E4 ekleri). Kod: services/Diten.CrmService/src/**/Features/{VisitWorkspace,VisitPlanning}/** (IWorkspacePlanPreviewSource, GetWorkspaceCalendarHandler, GetRescheduleOptionsHandler, VisitWorkspaceDays, VisitPlanningEngine, DayBalancer/AssignAroundPins, planlama takvimi/Working Calendar çözümleyicisi), Infrastructure (Working Calendar istemcisi).
NE: (C1) taslak önizleme önbelleği: anahtar = kiracı + oturum + oturum sürümü + gün(UTC) + yazılmış ziyaret girdi damgası; TTL + boyut sınırı; kiracılar arası paylaşım yok; aynı anahtarla ikinci okuma motoru ÇAĞIRMAZ, oturum/ziyaret değişince çağırır; calendar + reschedule-options ikisi de kullanır; önizlemeyi etkileyen girdileri koddan çıkar ve raporla. (C2) ziyaret öğesine accountDisplayName (yazılmış + taslak, toplu ad okuması). (C3) days[].holidayName dolu (kiracı dili, yoksa en; çözümleyici adı taşımıyorsa yalnız okuma genişlet, kaynağı raporla). (C4) weeks[].unplaced[] {targetType,targetId,displayName,accountDisplayName,reason} (sayı = unplacedCount) + weeks[].sessionVersion. (C5) gün dengesi: kurum kümesi gün bütçesinin %60'ını aşarsa en fazla 2 güne bölünebilir (geo sıra korunur), günler arası fark ≤ bütçenin %50'si yumuşak hedef, sabitler + onaylı haftalar dokunulmaz, kararlı. Alan adları SABİT (WEB-b paralel): accountDisplayName, sessionVersion, unplaced, holidayName.
KORU/YAPMA: ARCH GATE; TenantId (önbellek dahil); 4G/4L/W2-BE-b davranışı korunur (beklenti değişirse gerekçe); yeni yazma komutu yok (mimari 27); göç/seed/grant yok; canlı veriye yazma yok.
DOĞRULA (E2): CRM 2543/0/5 tabanı (PiiMasking_… bilinen kararsız) · mimari 27. Testler: C1 motor çağrı sayısı, C2–C4 alanlar, C5 "1 kurumda 20 + 15 dağınık doktor" senaryosu (hiçbir gün >%100, en dolu−en boş ≤ %50, kurum ≤ 2 gün) + eski motor testleri; sabotaj 2 (önbellek anahtarından sürüm çıkar → C1 kırmızı; %60 bölme kapalı → C5 kırmızı; geri al — git checkout -- YOK, touch). dotnet test -o çıktısı REPO İÇİNDE.
Commit: "feat(crm): WP-VW-W2-BE-c — workspace preview cache, account and holiday names, unplaced list, day balance" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, önbellek anahtarı ve geçersizleşme kuralı, tatil adı kaynağı, gün dengesi önce/sonra (senaryo sayıları), mobil için yeni alanlar, elle denenecekler (canlıda ikinci okuma süresi). §22 TÜRKÇE. K13.
```

## §36.1 Agent Prompt — W2-WEB-b (paste-ready)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W2 · W2-WEB-b — Ziyaret Çalışma Alanı Planla modu: Hedefler paneli, sürükle-bırak (gün + saat), ürün uygula, kurum süzgeci, sığmayanlar listesi
Repository: C:\tmp\vw-w2-web-b (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w2-web-b · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W2-round2.md — önce oku (W2-WEB-b: P1–P6 + Acceptance). Ana belge: WP-VW-W2-calendar-workspace.md (KORU/YAPMA, W2-WEB-a §37 + canlı E4 eki: state.byId düzeltmesi — DitenCalendar.create içinde ilk olaylar çizilir). MOCKUP: execution/domains/commercial-suite/work-packs/mockups/visit-workspace/ (decoded/01-takvim.html: Planla modu, "Hedefler {hafta} · sürükleyip güne bırakın", Tümünü seç, "Ürün uygula (N)", seçim özeti, onaylıda salt okunur uyarısı). Backend: dayPins[].startTime (W2-BE-b, kodda), pin kodları; W2-BE-c PARALEL — sabit alan adları: accountDisplayName, weeks[].sessionVersion, weeks[].unplaced[], days[].holidayName (yoksa: süzgeç gizli, liste yok, ayrı oturum okuması kalır). Kod: frontend/Diten.Web/wwwroot/assets/js/CRM/{VisitWorkspace,VisitPlanning}/** · Views/CRM/{VisitWorkspace,VisitPlanning}/** · Resources/Views/CRM/VisitWorkspace/** · Controllers/CRM/VisitWorkspaceController.cs · shared/diten-calendar.js (onExternalDrop, eventDrop, setEditable).
NE: (P1) Planla/Yürüt geçişi; Planla'da taslak hafta düzenlenebilir, onaylı/geçmiş salt okunur + uyarı. (P2) Hedefler paneli takvimin yanında (mobilde gizli): kurum seçimi, hızlı süzgeçler sayılı, doktor satırları (işaret, ad, uzmanlık, sıklık, bu hafta görülmeli, segment), Tümünü seç, seçim özeti — Ziyaret Planlama'nın 4J/4L/4M Hedefler mantığı YENİDEN KULLANILIR, KOPYALANMAZ (gerekirse ortak modüle ayır; Ziyaret Planlama sayfası aynen çalışır); işaretle → mevcut seçim güncellemesi → takvim yenilenir. (P3) sürükle-bırak: satırı güne/saate bırak → dayPins {date, scope:"visit", startTime 15 dk yuvarlama; tüm gün alanı = saatsiz}; taslak kartı başka gün/saate sürükle → aynı; onaylı/geçmişte sürükleme kapalı; hata/taşıma kodları kullanıcı metni (pin_time_invalid, pin_time_outside_hours, pin_time_conflict, pin_time_past_day_end = "Ziyaret mesai bitişini aşıyor, erkene alındı", pin_overflow, pin_day_full, week_already_approved). (P4) Ürün uygula: seçili doktorların bu haftaki ziyaretlerine, ilk ürün tanıtım diğerleri hatırlatma, mevcut ürün seçim yazımı, ürün ADLARI. (P5) kurum süzgeci accountDisplayName (doktor adı ASLA), "N ziyaret sığmadı" penceresi weeks[].unplaced[] listesi, onay/yeniden açmada weeks[].sessionVersion. (P6) 7 DİL (en,tr,fr,es,zh,ar,ru) yankısız, Arapça RTL. Vekile yeni uç gerekirse CRM ucunun anahtarlarının HEPSİ.
KORU/YAPMA: ARCH GATE (play/kampanya görünmez); Ziyaret Planlama / Ziyaret Yürütme / Planlanan Ziyaretler davranışı değişmez (ortak modüle ayırma serbest, testleri yeşil); CRM koduna dokunma (eksik varsa rapora yaz); yeni JS kütüphanesi yok; oturum açık kullanıcı sekmesine mock/harness enjekte etme, kalıcı yazma yok; DitenCalendar.create sırası: veri/arama tabloları create'ten ÖNCE hazır.
DOĞRULA (E2): Web 854/0 tabanı · CRM dokunulmaz · mimari 27. Testler belge Acceptance 1–6 — saf kurallar Node'da + visit-workspace-smoke.js sahte DOM (sahte takvim create içinde ilk olayları çizer, createError null kalmalı); sabotaj 2 (yuvarlama kaldır → kırmızı; onaylıda sürükleme açık → kırmızı; geri al — git checkout -- YOK). dotnet test -o çıktısı REPO İÇİNDE.
Commit: "feat(web): WP-VW-W2-WEB-b — visit workspace plan mode: targets panel, drag to day / time, apply products, account filter, unplaced list" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, Hedefler modülünün nasıl paylaşıldığı, mockup'tan bilinçli sapmalar, BE-c'ye bağımlı kısımlar, elle denenecekler. §22 TÜRKÇE. K13.
```
