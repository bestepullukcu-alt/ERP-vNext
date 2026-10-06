# WORK PACKAGE — WP-CYC-UI-2 · Dönem Kapasitesi ekranları (liste, canlı hesaplı düzenleme, ayrıntı: hesap şelalesi, ay ay grafik, arz / talep) — Web

> **CT (SoR), 2026-10-05.**
> - **Mockup:** `mockups/cycle-planning/cycle-planning-prototype.html` (çözülmüş: `cycle-planning-screens.decoded.html`, ekranlar 04–06; hesap S:679–691). **Analiz + kararlar:** `mockups/cycle-planning/CYCLE-PLANNING-mockup-analysis.md` §2 (K-1, K-4, K-5, K-7, K-8, E8, E9), §5.
> - **CRM sözleşmesi:** WP-CAP-MODEL (`9deaca04`, §37) — tipik ziyaret alanları, `visitModel`, yazar FTE, mikro-hedefleme kırpma, hesap DTO toplamları; WP-CYC-UI-1 §Sözleşme — `GET /api/crm/cycle-periods/{id}/usage` (paralel yazılıyor; testlerde sahte gateway).
> - **Kapsam:** yalnız `frontend/Diten.Web` (Kapasite). CRM DOKUNMA.
>
> **Çalışma yeri:** worktree `C:\tmp\cyc-ui-2`, dal `wp/cyc-ui-2`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Kanıt (CT)
- **Bugünkü Web:** `Controllers/CRM/CycleCapacitiesController.cs` (`/CRM/CycleCapacities`: Index, Create, Edit, Details), `Views/CRM/CycleCapacities/{Index, _DataTable, _Filter, _Form, Create, Edit, Details, _IndexL10n}.cshtml`, `wwwroot/assets/js/CRM/CycleCapacities/{index.js, form.js, index.l10n.js}`, resx `CycleCapacitiesIndex.*` (SB-3-UIa'da `max*Products` alanları eklendi).
- **CRM (CAP-MODEL):**
  - kapasite yazma: `typicalPromoCount`, `typicalNonPromoCount`, `reportMinutesPerVisit` (üçü birlikte ya da hiçbiri; 400 `typical_visit_empty` / `typical_count_exceeds_max` / `typical_visit_incomplete`), aylık `fte` (gönderilirse `authored`, 0 dahil), `maxPromoProducts / maxNonPromoProducts` (1–10);
  - okuma: `visitModel` (`typical` / `legacy`), `typicalVisitMinutes`, `dailyFixedMinutes`, `fteSource`, `fteIsEditable`;
  - hesap / önizleme: ay satırı (`calendarDays, workingDays, nonWorkingDays, meeting/training/vacationDays, deductedDays, fieldDays, availableMinutes, dailyFixedMinutes, microTargetingMinutes, remainingMinutes, typicalVisitMinutes, fte, visits`), kök `visitModel, typicalVisitMinutes, dailyFixedMinutes, totals{…}`, `resolution`, `isEstimate`, `reasonCodes`; **takvim çözülemezse `totalVisitNumber` ve `totals` null, ay tablosu boş** (K-4).
  - limitler `CycleCapacityLimits` (günlük 1–1440, ziyaret ≤ 480, tampon ≤ 240) — UI sınırları buradan (sözleşme / contract ucu varsa oradan) (E9).
- **Takvim ülkesi (K-8):** dönem ülke kapsamlıysa türetilir ve kilitli; diğerlerinde seçilir (tüzel kişinin ülkesi öneri).

## NE
### 1. Liste
Sütunlar: dönem (kod + tarih), takvim ülkesi, **tahmini ziyaret (temsilci başına)**, tipik ziyaret süresi (+ "eski model" rozeti `visitModel = legacy`), ortalama FTE, ürün sınırları, durum (düzenlenebilir / dönem kapalı / hesaplanamadı), son güncelleme. Filtreler: yıl, dönem, ülke; arşiv anahtarı; **Arşivle** eylemi (mevcut uç). Her dönem için tek kapasite.

### 2. Oluştur / düzenle — iki sütun, canlı hesap (mockup 05)
- Sol girdiler:
  1. **Dönem** (oluştururken seçilir, sonra kilitli; kapalı ve kapasitesi olan dönemler seçilemez).
  2. **Takvim** (K-8 kuralı; çözülemezse uyarı).
  3. **Günlük zaman:** iş günü dk, yol dk, sınav dk (gün başına) — toplamı iş gününden az.
  4. **Ziyaret süresi:** promo dk / ürün, non-promo dk / ürün, **rapor dk / ziyaret**, **tipik ziyaret** (promo sayısı, non-promo sayısı; sınırları aşamaz) → "tipik ziyaret süresi" ve formül ipucu (`2×12 + 1×5 + 5 = 34 dk`); ziyaretler arası tampon (bilgi: kapasiteyi değiştirmez).
  5. **Ürün sınırı** (1–10).
  6. **Aylık tablo:** toplantı / eğitim / izin günü, mikro-hedefleme gün + dk, **FTE (girilebilir, 0–1, adım 0,05; kaynağı rozet: varsayılan / girildi)**; hesaplanan sütunlar salt okunur; kırpılmış ay işareti; "tüm aylara uygula".
- Sağ özet: **önizleme ucuyla canlı** (gecikmeli istek; istemcide formül kopyalanmaz) — saha günü, ziyarete kalan dakika, tipik ziyaret, ortalama FTE, **tahmini ziyaret**; uyarılar (düşülen > iş günü → ay 0) ve engeller (günlük sabit ≥ iş günü, süre 0, tipik sayı > sınır) kodlardan yerelleştirilmiş.
- **Eski model kaydı** (`legacy`): bandı "Bu kapasite eski süre modeliyle hesaplanıyor; tipik ziyareti doldurup kaydedince yeni modele geçer" + mevcut sayının değişebileceği notu; tipik alanlar boş başlar, üçü doldurulmadan kaydedilirse eski model korunur (CAP-MODEL davranışı).
- Takvim çözülemezse: girdiler düzenlenebilir, sağ özette **sayı yok** + neden (`calendar_unresolved` / `calendar_forbidden`) (K-4).
- Kapalı dönem: tüm form salt okunur + neden.
- Durumlar: yükleniyor / hata (mockup'ta eksikti — ekle).

### 3. Ayrıntı (mockup 06)
- **Hesap şelalesi** (toplamlardan): iş günü → düşülen → saha günü → kullanılabilir dk → günlük sabit → mikro-hedefleme → ziyarete kalan → ÷ tipik ziyaret × ort. FTE → **ziyaret**.
- **Ay ay grafik:** ziyaret + saha günü (eksen etiketleri tema renkleriyle, 7 dil).
- **Takvim durumu:** çözüldü / tahmin / yetki yok — ayrı anlatım.
- **Arz / talep (K-7, temsilci başına):** her ay kapasite ziyareti ↔ `usage.demandByMonth` planlanan ziyaret; aşım uyarısı; bağlı planlama oturumları listesi. Ekip / temsilci sayısı **yok** (sonraki iş — ekranda not).

### 4. Genel
7 dil (en, tr, fr, es, zh, ar, ru; TR diakritik; Arapça sağdan sola), tema değişkenleri (sabit hex yok), klavye (Esc, odak, sekme sırası), yerel sayı biçimi + dk ↔ saat ipucu. Liste altın şablondan sapmasız.

## KORU / YAPMA
- **CRM DOKUNMA.** Hesap istemcide yeniden yazılmaz — önizleme ucu tek kaynak.
- Dönem ekranlarına DOKUNMA (CYC-UI-1).
- `esc()` / `textContent`; proxy 204 tuzağı.
- **DUR:** önizleme ucu yeni alanları kabul etmiyorsa ya da hesap DTO'sunda şelale kalemi eksikse → raporla (CRM ek iş).

## Acceptance
- **E2:** Web 0 kırmızı (taban 473), CRM 0 kırmızı (dokunulmadı; taban 2223/0/5), build 0 hata.
  - **Yeni testler:** proxy gövdesinde tipik alanlar üçü birlikte / hiçbiri; FTE gönderimi (dokunulmamış ay gönderilmez → kayıttaki korunur); limitler sözleşmeden; önizleme isteği gecikmeli ve tek; legacy bandı; takvim çözülemezse sayı yok; kapalı dönemde salt okunur; şelale kalemleri toplamlardan; arz / talep usage'dan (sahte gateway); yetkisizde iskelet yok; 7 dil anahtar eşitliği.
  - **Sabotaj:** (1) yalnız iki tipik alan gönder → "üçü birlikte" testi kırmızı; (2) takvim çözülemezken hafta içi tahmini göster → K-4 testi kırmızı.
- **E4 (CT):** canlıda yeni kapasite + canlı hesap; eski kapasite açılınca aynı sayı + legacy bandı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-CYC-UI-2 · Dönem Kapasitesi ekranları (liste, canlı hesaplı düzenleme, ayrıntı: hesap şelalesi, ay ay grafik, arz/talep) — Web
Repository: C:\tmp\cyc-ui-2 (worktree) · Branch: wp/cyc-ui-2 · commit bu dala, push YOK · yalnız frontend/Diten.Web (Kapasite)

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-CYC-UI-2-cycle-capacity-screens.md — önce tamamını oku. Mockup: …/mockups/cycle-planning/cycle-planning-screens.decoded.html (ekran 04–06, hesap S:679-691) + prototip HTML; kararlar …/mockups/cycle-planning/CYCLE-PLANNING-mockup-analysis.md §2 + §5. CRM sözleşmesi: …/WP-CAP-MODEL-visit-duration-capacity.md (§NE + §37) ve …/WP-CYC-UI-1-cycle-periods-screens.md §Sözleşme (usage ucu — paralel yazılıyor, sahte gateway). Ayrıca: frontend/Diten.Web/{Controllers/CRM/CycleCapacitiesController.cs, Views/CRM/CycleCapacities/**, wwwroot/assets/js/CRM/CycleCapacities/**, Models/CRM/CycleCapacity*.cs} · services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/CycleCapacity.cs (limitler) · Application/Features/CycleCapacity/** (DTO adları) · memory l10n-bridge-pascalcase-loader, proxy-forward-204-content-length-crash, dt-inline-filter-host-class, updatevisualstate-global-selectors.

NE: (1) Liste: dönem, takvim ülkesi, tahmini ziyaret (temsilci başına), tipik ziyaret süresi + legacy rozeti, ort. FTE, ürün sınırları, durum, güncelleme; filtreler; arşiv anahtarı + Arşivle. (2) Oluştur/düzenle iki sütun: dönem (kilitli; kapalı/kapasiteli seçilemez), takvim (ülke kapsamında kilitli, diğerlerinde seçim), günlük zaman (iş günü/yol/sınav), ziyaret süresi (promo/non-promo dk/ürün, rapor dk/ziyaret, tipik sayılar ≤ sınır, formül ipucu, tampon bilgisi), ürün sınırı 1-10, aylık tablo (toplantı/eğitim/izin, mikro gün+dk, FTE 0-1 adım 0,05 + kaynak rozeti, kırpılmış ay, tüm aylara uygula); sağ özet ÖNİZLEME UCUNDAN canlı (gecikmeli, istemcide formül yok), uyarı/engeller kodlardan yerelleştirilmiş; legacy bandı; takvim çözülemezse sayı yok + neden; kapalı dönem salt okunur; yükleniyor/hata. (3) Ayrıntı: şelale (totals'tan), ay ay grafik, takvim durumu, arz/talep (usage.demandByMonth, temsilci başına, aşım uyarısı, oturumlar; ekip sayısı yok notu). (4) 7 dil + RTL + tema değişkenleri + klavye + yerel sayı; liste altın şablon.
KORU/YAPMA: CRM DOKUNMA; hesap istemcide yazılmaz; dönem ekranlarına DOKUNMA; esc()/textContent; 204 tuzağı.
DOĞRULA (E2): dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 473); CRM testleri 0 kırmızı (dokunulmadı); build 0 hata. Yeni testler WP Acceptance. Sabotaj: (1) iki tipik alan gönder → kırmızı; (2) takvim çözülemezken tahmin göster → kırmızı; geri al. Commit ("feat(web): WP-CYC-UI-2 — cycle capacity screens (live preview form, waterfall, monthly chart, supply vs demand)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: önizleme ucu yeni alanları kabul etmiyorsa ya da şelale kalemi eksikse → DUR + raporla.
```

---

## §37 — CT bağımsız doğrulama (2026-10-05) — **ACCEPTED (E2)**
- **Commit:** ajan `8ad19675` (taban `0bffdeb9`) → `test/crm-content-visit-e2e` fast-forward. 22 dosya (+3825 / −797). Yalnız Web.
- **K13 okuma:** sağ özet yalnız önizleme ucundan (istemcide formül yok; gecikmeli tek istek, eskisi iptal); önizleme proxy'si gövdeyi kendisi kurar (tipik üçlü hep / hiç); yalnız dokunulan ayın FTE'si gönderilir (kültürden bağımsız metin); sınırlar sözleşme ucundan, yoksa CRM sabitleri; K-4 çift katman (CRM `totals = null` + Web `IsResolved`); arz / talep `usage.demandByMonth` (okunamazsa kart söyler); 125 anahtar × 7 dil; yetkisize kabuk içinde açıklama.
- **Ajan kararları (kabul):** yeni kapasitede tipik ziyaret ekranda zorunlu (CRM boş üçlüyü eski modelle kabul ediyor); ürün sınırı 1–10 CRM sabitinden.
- **CT testleri:** Web **505/0** (+32); CYC-UI-1 ile birleşik CRM 2231/0/5, Web 536/0.
- **CT sabotajı:** (1) `IsResolved` her zaman true → **kırmızı olmadı** (CRM toplamları boş gönderdiği için çift katman; davranış güvende, not); (2) dokunulmamış ayın FTE'sini de gönder → 1 kırmızı. Geri alındı. Ajan: iki tipik alan (5), takvim çözülemezken tahmin (1).
- **Açık:** liste altın şablon 12 sapma (önceden var) — kullanıcı kararı; ajan önerisi 1–3 düzelt (veri modu, `_TableSkeleton`, hızlı bakış), 4–12 bilinen sapma.
- **WP-CYC-UI-FIX-1 güncellemesi (2026-10-05):** liste altın şablon sapmaları **1–3 düzeltildi** — veri modu `data-dt-data-mode="client"` (tüm küme bir kez yüklenir; sayfalama / sıralama / filtre tarayıcıda), ortak `<partial name="_TableSkeleton" />`, `#offcanvasDetailsPreview` hızlı bakış (satır verisinden, ek istek yok, düz metin). **Bilinen sapmalar** (doğrulayıcı `--api-profile proxy` ile kalan 8 madde; `CycleCapacitiesController` belge yorumunda da kayıtlı):
  - ortak `personalization-client.js` kiracı başlığı kontrolü — modül dışı ortak dosya, ayrı iş;
  - doğrulayıcının varsayılan profilde doğrudan gateway (`window.API`) beklentisi — sayfa bilinçli aynı köken proxy kullanır (proxy profiliyle bu madde raporlanmaz);
  - tümünü seç sütunu, toplu işlem yapılandırması, toplu seçim bağlama, `/bulk` ucu, toplu silme tetikleyicisi, `reloadWithToast`, seçimi temizle — modülde silme yok (sonlandırma Kapat / Arşivle), toplu yüzey yok.
- **E4:** CT, fleet sonrası (yeni kapasite canlı hesap; eski kapasite aynı sayı + legacy bandı).

### §37 ek — E4 (CT, canlı, 2026-10-06) — **ACCEPTED (E4) + bulgular**
- ✓ Liste: `tr-2026-q4`, TR, 6.543 ziyaret / temsilci, "4 dk eski model" rozeti, 1,00 FTE, 3 / 3, düzenlenebilir; hızlı bakış mevcut.
- ✓ Ayrıntı: eski model bandı, takvim durumu, şelale (65 iş günü − 8 → 57 saha günü → 27.360 dk − 1.140 − 45 → 26.175 dk ÷ 4 dk → 6.543), ay ay grafik, arz / talep (176 / 6.543, %3), oturum listesi. **CRM hesap ucu da 6.543** (aynı sayı).
- ✓ Düzenleme canlı önizleme (KAYDEDİLMEDİ): tipik 2 promo + 1 non-promo + 3 dk rapor → "2×3 + 1×1 + 3 = 10 dk", günlük sabit yol + sınav (17 dk) → 26.346 dk ÷ 10 → **2.635**; uyarı yok. Mongo: kayıt değişmedi (`UpdatedAt` null, tipik alan yok).
- **Bulgular (→ WP-CYC-UI-FIX-2):**
  4. Oturum tablosunda durumlar ham kod ("draft", "committed").
  5. Eski model kayıtta şelale etiketi "Günlük sabit işler (yol, sınav)" — eski modelde rapor da günlük düşülüyor (etiket "yol, rapor, sınav" olmalı).
  6. Başlıkta tarih biçimi "1.10.2026", diğer yerlerde "01 Eki 2026" (tutarsız).
- Not: test verisi gerçekçi değil (promo 3 dk, non-promo 1 dk) — ekran hatası değil.
