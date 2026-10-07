# WORK PACKAGE — WP-VP-4B · Ziyaret Planlama ekranı (1/3): liste + yeni plan paneli + detay üstü + durumlar + sayfa iskeleti

> **CT (SoR), 2026-10-07.**
> - **Kaynak:** [mockup v2](mockups/visit-planning/Ziyaret%20Planlama%20v2%20(standalone).html) (çözülmüş: `mockups/visit-planning/visit-planning-v2.decoded.html`) · [brief](mockups/visit-planning/BRIEF-visit-planning-rep-week.md) §1–3, §6–8 · [mockup analizi v1](mockups/visit-planning/VISIT-PLANNING-mockup-analysis.md) (MK-1…MK-9) · [v2 analizi](mockups/visit-planning/VISIT-PLANNING-mockup-v2-analysis.md) · [DESIGN-VP-FAZ3](DESIGN-VP-FAZ3-planning-engine.md).
> - **Kullanıcı:** "Faz 4: mockup v2'ye göre Web arayüzü … paketlemeye başla" (2026-10-07).
> - **Kapsam:** yalnız Web (Ziyaret Planlama liste, yeni plan, detay üst kısmı, sekme iskeleti, durumlar).
>   - Hedefler sekmesi **4C'de**, Haftalar + doktor paneli **4D'de**.
>   - **Rota sekmesi DOKUNULMAZ** (brief ⛔1).
>
> **Çalışma yeri:** worktree `C:\tmp\vp-4b`, dal `wp/vp-4b`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.
> **Paralel:** WP-VP-4A (backend) ayrı dalda. 4A'nın alanları (`doctorCount`, `draftWeekCount`, `currentWeekStart`, `nextDraftWeekStart`, reopen vekili) birleşmeden önce yoksa **zarif düşüş** (alan yoksa eski görünüm). Birleşmeden sonra E4'te tam görünür.

## Mockup ve tasarım ilkesi
- Mockup **düzen ve akış** referansıdır. Görsel dil **mevcut Web temasının** bileşenleriyle kurulur (kart, düğme, rozet, offcanvas, Select2, DataTable); mockup'taki satır içi stiller kopyalanmaz.
- Tema değişkenleri, açık / koyu tema, 7 dil + Arapça sağdan sola (brief §8).
- Tarih biçimi bugünkü gibi ("5 Oct, 26" tarzı; brief ⛔2).
- `details.js` bugün ~1400 satırlık tek dosya. Bu paket **sayfa iskeleti** kurar:
  - ortak durum / olay modülü, ör. `window.VisitPlanningPage` (oturum, seçili hafta, dönem, önizleme sonucu, `on/emit`);
  - sekmeler ayrı dosyalara bağlanabilir: `targets.js` (4C), `weeks.js` + `doctor-panel.js` (4D), `route` (mevcut kod, yerinde kalır).
  - **Davranış değişmeden** taşı. Rota koduna yalnız iskelete bağlamak için dokun (görünüm aynı).

## NE
### 1. Liste (brief §1, mockup ekran "Liste")
- Sütunlar:
  - **Hedefler** "N doktor · M eczane" (4A `doctorCount` / `pharmacyCount`);
  - **Haftalar** "X onaylı · Y taslak" (4A `approvedWeekCount` / `draftWeekCount`);
  - temsilci ad soyad.
  - Temsilci görünümünde temsilci sütunu gizli (yönetici görünümüne yer).
- **Boş plan rozeti** ("boş taslak") + "Boş taslakları sil" (toplu; 3A arşivi, yalnız boş planlar, onaylı pencere).
- Başlık ve düğmeler temsilci dilinde ve Türkçe ("Benim planlarım", "Yeni plan").
- **MK-5:** "Benim planlarım / Ekip" anahtarı görünür; Ekip **pasif** (ipucu: "Yönetici görünümü yakında").
- Eski `committed` planlar "Tüm dönem onaylı (eski plan)" rozeti ile, salt okunur.

### 2. Yeni plan paneli — sağdan açılan çekmece (MK-1, brief §2)
- Liste ve detaydan "Yeni plan" → offcanvas. Bugünkü `Create` sayfası kalabilir ama kullanılmaz; yönlendirme panele.
- Alanlar:
  - **Ülke** otomatik, salt okunur (birden fazlaysa seçim);
  - **Dönem** otomatik (aktif dönem; yalnız aktif / gelecek);
  - **Açılış haftası** (planı olmayan ilk hafta; **geçmiş haftalar seçilemez** — E7-B5);
  - **Temsilci** = oturumdaki kişi, salt okunur ad soyad.
  - Segment ve strateji alanı **yok** (K-3, K-4).
- Aynı temsilci + dönem için plan varsa (409 `planning_session_exists`): "Bu dönem için planınız var" + "Plana git" (yanıttaki kimlik).

### 3. Detay üst kısmı (brief §3)
- **Özet kartı** Türkçe etiketlerle (Dönem, Hafta, Temsilci, Durum).
- **Hafta seçici** (dönemin haftaları, durum rozetiyle: geçmiş / onaylı / taslak / boş); varsayılan `currentWeekStart` (4A), yoksa ilk taslak.
- **Durum bazlı eylemler** (seçili haftaya göre; yalnız uygun olan görünür):
  - **taslak:** "Hedefleri kaydet", "Rota oluştur", **"Haftayı onayla"** (3A onay akışı + E2E-FIX-1'deki onay / toast / yeniden yükleme deseni);
  - **onaylı:** salt okunur bant; **"Haftayı yeniden aç"** (gerekçe penceresi ≥ 10 karakter → reopen vekili; MK-4 metni "gerekçe haftanın geçmişine kaydedilir") ve "Sonraki haftayı aç" (`nextDraftWeekStart`);
  - **geçmiş:** salt okunur, eylem yok;
  - **eski `committed` plan:** salt okunur, bilgi bandı "Bu plan tüm dönem için onaylanmış (eski plan)".
- **Kapasite kartları** (C5, 3B):
  - "Bu haftanın kapasitesi" ↔ "Bu hafta planlanan" (dakika → saat; `weekCapacity`);
  - "Dönem kapasitesi" ↔ "Dönemde planlanan" (`periodCapacity`, ilerleme çubuğu);
  - eski `SupplyDemandSummary` kartı kalkar.
- Sekmeler: **Hedefler | Haftalar | Rota**. Haftalar sekmesi bu pakette **yer tutucu** ("Yakında" değil; 4D gelene kadar sekme gizli ya da pasif — hangisi temizse, raporla).

### 4. Durumlar (brief §7) — her ekranda
- Boş (plan yok → "Bu dönem için plan oluştur"), yükleniyor, hata.
- Aktif dönem yok; dönem kapasitesi yok (`budgetSource = default_hours` bilgisi); çalışma takvimi okunamadı (`calendarStatus` uyarısı).
- Bölge atanmamış (K-5: sarı bant, tüm hesaplar).
- **Yetkisiz:** sayfa iskeleti çizilmez, yönlendirme yapılmaz (UAS-001; mevcut desen).

### 5. Küçük takipler (birikmiş)
- Onay pencerelerinde gereksiz alt metin ("Devam etmek istediğinize emin misiniz?") Ziyaret Planlama'da kalkar (FIX-2'deki `subtext: ''` deseni).
- Bütün yeni / değişen metinler 7 dil (TR diakritik); "Cycle period" gibi İngilizce kalıntılar Türkçe.

## KORU / YAPMA
- **Rota sekmesi** görünüm ve davranışı değişmez (yalnız iskelete bağlanır).
- Backend'e dokunma (4A'nın işi). Yeni uç yok; eksik alan → zarif düşüş + rapor.
- Hedefler (4C) ve Haftalar (4D) içerikleri bu pakette yok.
- Seed / grant / göç YOK. Yeni metinler `SharedResource`'a değil görünüm resx'ine (mevcut desen).

## Acceptance
### E2 (taban ölç, yalnız farkı raporla)
Web (738/0), CRM dokunulmaz (2410/0/5), mimari (38/1; 27 sabit). Build 0 hata. JS `node --check`.

**Yeni testler (Web; kaynak / denetleyici / L10n testleri, mevcut desen):**
1. Liste: hedef ve hafta sütunları DTO alanlarından; boş plan rozeti; Ekip anahtarı pasif.
2. Yeni plan paneli: segment / strateji alanı yok; geçmiş hafta seçeneği yok; 409'da "Plana git".
3. Detay eylemleri hafta durumuna göre: taslak / onaylı / geçmiş / eski plan (kaynak testi ya da JS mantık birimi).
4. Yeniden aç penceresi: gerekçe < 10 → gönderilmez; vekile doğru yol.
5. Kapasite kartları `weekCapacity` / `periodCapacity` kullanır; eski arz / talep kartı yok.
6. Yeni anahtarlar 7 dilde, TR değeri anahtar yankısı değil.
7. Rota kodu davranışı değişmedi (mevcut Rota testleri yeşil; Rota dosyasında yalnız iskelet bağlantısı).

**Sabotaj (kırmızı kanıtla, geri al):**
1. Onaylı haftada "Hedefleri kaydet"i göster → test 3 kırmızı.
2. Yeni plan panelinde geçmiş haftayı listele → test 2 kırmızı.

### E4 (CT, fleet, ayrı sekme; 4A birleştikten sonra)
- Liste sütunları, boş rozet, Ekip pasif.
- Yeni plan paneli + 409 "Plana git".
- `23b1706a`'da 42. hafta onaylı → salt okunur + "Haftayı yeniden aç" (gerekçeli; kullanıcı onaylı).
- Kapasite kartları; Rota aynı.
- Arapça sağdan sola.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VP-4B · Ziyaret Planlama ekranı (1/3): liste + yeni plan paneli + detay üstü + durumlar + sayfa iskeleti
Repository: C:\tmp\vp-4b (worktree) · Branch: wp/vp-4b · commit bu dala, push YOK

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-VP-4B-list-new-plan-panel-detail-header.md — önce tamamını oku. Mockup: execution/domains/commercial-suite/work-packs/mockups/visit-planning/ (Ziyaret Planlama v2 (standalone).html + visit-planning-v2.decoded.html + BRIEF-visit-planning-rep-week.md + BRIEF-ADDENDUM-K7-visit-products.md + VISIT-PLANNING-mockup-analysis.md + VISIT-PLANNING-mockup-v2-analysis.md). Tasarım: …/DESIGN-VP-FAZ3-planning-engine.md. Ayrıca: frontend/Diten.Web/Views/CRM/VisitPlanning/** · frontend/Diten.Web/wwwroot/assets/js/CRM/VisitPlanning/** · frontend/Diten.Web/Controllers/CRM/VisitPlanningController.cs · Resources/Views/CRM/VisitPlanning/*.resx · WP-VP-4A belgesi (gelecek alanlar).
NE:
(0) Sayfa iskeleti: ortak durum/olay modülü (window.VisitPlanningPage: oturum, seçili hafta, dönem, önizleme, on/emit); sekmeler ayrı dosyalara bağlanabilir (targets.js 4C, weeks.js+doctor-panel.js 4D); davranış değişmeden taşı; Rota yalnız iskelete bağlanır, görünüm aynı.
(1) Liste: Hedefler "N doktor · M eczane", Haftalar "X onaylı · Y taslak" (4A alanları; yoksa zarif düşüş), temsilci ad soyad (temsilci görünümünde gizli), boş taslak rozeti + "Boş taslakları sil" (yalnız boş, onaylı pencere, arşiv), Türkçe temsilci dili, MK-5 Benim planlarım/Ekip anahtarı (Ekip pasif), eski committed rozeti.
(2) Yeni plan sağ çekmece (MK-1): ülke/dönem otomatik, açılış haftası (geçmiş seçilemez, E7-B5), temsilci salt okunur; segment/strateji YOK; 409 planning_session_exists → "Plana git".
(3) Detay üstü: Türkçe özet; hafta seçici (durum rozetli; varsayılan currentWeekStart); durum bazlı eylemler (taslak: Hedefleri kaydet / Rota oluştur / Haftayı onayla; onaylı: salt okunur + Haftayı yeniden aç (gerekçe ≥10, reopen vekili) + Sonraki haftayı aç; geçmiş: yok; eski plan: bilgi bandı); kapasite kartları weekCapacity/periodCapacity (saat), eski arz/talep kartı kalkar; sekmeler Hedefler|Haftalar|Rota (Haftalar 4D'ye kadar gizli/pasif, raporla).
(4) Durumlar brief §7 (boş, yükleniyor, hata, aktif dönem yok, kapasite yok, takvim okunamadı, bölge atanmamış, yetkisiz = iskelet yok, yönlendirme yok).
(5) Onay alt metni tekrarı kalkar; yeni metinler 7 dil (TR diakritik), İngilizce kalıntılar Türkçe.
KORU/YAPMA: Rota görünüm/davranışı DEĞİŞMEZ; backend'e dokunma (4A); Hedefler (4C) ve Haftalar (4D) içerikleri YOK; mockup stilleri kopyalanmaz, mevcut tema bileşenleri; tarih biçimi aynı; seed/grant/göç YOK; metinler görünüm resx'inde.
DOĞRULA (E2): tabanı ölç, yalnız farkı raporla — Web (738/0) · CRM (2410/0/5, dokunulmaz) · mimari (38/1, 27); build 0 hata; JS node --check; fleet açıkken Web bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad>. Yeni testler WP Acceptance 1–7. Sabotaj 1–2 (kırmızı kanıtla, geri al).
Commit: "feat(web): WP-VP-4B — visit planning list, new-plan drawer, week-aware detail header with approve/reopen, capacity cards, states, page skeleton" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde başına ne yapıldı + kanıt (dosya:satır, test adı), iskelet modül yapısı (4C/4D nereye bağlanacak), 4A alanları yokken düşüş davranışı, ekran görüntüsü yoksa hangi durumların elle denenmesi gerektiği. §22 TÜRKÇE. K13.
```

---

## §37 CT kabul — E2 ACCEPTED (2026-10-08)
**Commit:** `28a57eae7` (ajan `a47bbc5d5`, 4A üzerine rebase, çakışmasız, ff). Push: test dalı.

**CT K13 (4A dahil):** Web 742 → **750/0** (+8) · CRM 2419/0/5 (dokunulmadı) · mimari **27** · tüm Ziyaret Planlama JS `node --check` temiz.

**Kod okuması:**
- İskelet `page.js` (`window.VisitPlanningPage`: durum + olaylar + eylem kuralı `actionsFor` tek yerde: draft / empty / approved / past / legacy).
- `header.js`: özet, hafta seçici, eylemler, yeniden aç penceresi ≥ 10, kapasite kartları, bantlar.
- `new-plan.js` çekmece (geçmiş hafta listelenmez, 409 → "Plana git").
- `details.js` yalnız yayın / abone noktalarıyla bağlandı; Rota ve Hedefler kodu yerinde (mevcut testler içeriğini sabitliyor).

**CT sabotajı:** eski plana eylem verdirildi (`actionsFor` legacy dalı kaldırıldı) → 1 kırmızı (`The_header_offers_only_the_selected_weeks_actions`). Geri alındı.

**Ajan kararları (kabul):**
- Haftalar sekmesi gizli yer tutucu (`#vp-tab-weeks-item` / `#vp-tab-weeks` `d-none`; 4D açar).
- `Create` sayfası yedek olarak duruyor.
- Listedeki eski "Uygula" eylemi detaya yönlendiriyor.

**4C'ye devir:** Hedefler sekmesindeki "Hedefleri kaydet" onaylı haftada açık (üst kısım gizliyor; sekme içi 4C'nin işi).

**E4 (CT, bekliyor; 4C / 4D ile birlikte ya da ayrı):** ajan raporundaki 8 maddelik liste (liste sütunları, çekmece, 42. hafta yeniden aç, taslak hafta eylemleri, geçmiş / eski plan, kapasite kartları, Rota aynı, Arapça).

### §37 ek — E4 ACCEPTED (2026-10-08, CT, fleet, Beste; yeniden açma daha önce kullanıcı onaylı)
- **Liste:** başlık "Benim planlarım"; Hedefler / Haftalar sütunları; eski planlarda "Tüm dönem onaylı (eski plan)" rozeti; Ekip pasif ✓.
- **Yeni plan çekmecesi:**
  - "Bu dönem için planınız var" + "Plana git" önceden; "Planı oluştur" kapalı ✓;
  - geçmiş hafta listede yok; temsilci salt okunur ✓.
- **`23b1706a` 42. hafta (onaylı):**
  - yalnız "Haftayı yeniden aç" + "Sonraki haftayı aç" ✓;
  - pencere: gerekçe 10 karakterden kısayken düğme kapalı ✓;
  - gerekçeyle yeniden açıldı → toast "Hafta yeniden açıldı; gerekçe kaydedildi.", hafta `draft/reopened`, geçmişte approve + reopen (gerekçeli) ✓;
  - ziyaret `VP-23b1706a-0001` → `cancelled` ✓.
- **Taslak hafta (41):** Haftayı onayla / Hedefleri kaydet / Rota oluştur ✓.
- **Eski plan:** tek bilgi bandı, eylem yok ✓.
- **Kapasite kartları** saat (38,3 sa / 0,1 sa · 498,3 sa / 1,2 sa) ✓.
- **Rota:** gün sekmeleri + harita yükleniyor ✓.
- Sayfa istekleri hep 200.
- **Arapça RTL denenmedi:** dil değişimi kullanıcı ayarı yazar.

**Küçük bulgular (→ WP-VP-4D "4B E4 takipleri"):**
- E4-4B-1: çekmecedeki önceden "Plana git" eski plana (`a238bdc5`) gidiyor; sunucunun 409'u `23b1706a`'yı gösteriyor → aynı kural.
- E4-4B-2: detay özetinde temsilci e-postayla görünüyor (brief: ad soyad).
- E4-4B-3: onaylı haftada kilit bandı iki kez (üst + Rota).
- E4-4B-4: çekmecede ülke, temsilcinin ülkesi yerine kiracının 6 ülkesiyle seçilebilir.
- E4-4B-5: hiç boş taslak yokken "Boş taslakları sil" görünüyor.
- E4-4B-6: hafta geçmişindeki "kim" alanı kimlik (GUID) → ad gösterilmeli (4D'nin hafta geçmişi listesi).
