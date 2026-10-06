# WORK PACKAGE — WP-VP-FIX-1 · Ziyaret Planlama: mockup'tan bağımsız düzeltmeler (Web + CRM + Platform menü)

> **CT (SoR), 2026-10-06.**
> - **Kaynak:** [durum analizi](VISIT-PLANNING-current-state-analysis.md) (canlı gezinti) · [yol haritası](ROADMAP-visit-planning.md) Faz 1 · [mockup analizi](mockups/visit-planning/VISIT-PLANNING-mockup-analysis.md).
> - **Kullanıcı (2026-10-06):** "VP-FIX-1 paketle".
> - **Kapsam:** Ziyaret Planlama sayfasının bugünkü hâlinde, **ekran yeniden tasarımı olmadan** düzeltilebilecek 7 madde. Yeni tasarım (sağ panel, Haftalar sekmesi, bölge evreni…) **Faz 4**; hafta durumu / gün dengeleme **Faz 3**. Bunlara girme.
>
> **Çalışma yeri:** worktree `C:\tmp\vp-fix-1`, dal `wp/vp-fix-1`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Bağlam (CT kod okuması, 2026-10-06)
- Sayfa: `/CRM/VisitPlanning` (Web `VisitPlanningController` + `Views/CRM/VisitPlanning/**` + `wwwroot/assets/js/CRM/VisitPlanning/{index,form,details}.js` + `Resources/Views/CRM/VisitPlanning/VisitPlanningIndex.*.resx`).
- Backend: CRM `Features/VisitPlanning/**` (`VisitPlanningEngine`, `PlanningSession*`) + `Features/RouteOptimization/TimeWindowInsertionEngine.cs`.
- Menü: Platform `Features/Crm/SelfRegistration/CrmManifestProvider.cs`. Örnek desen: KP-5a'da eklenen `SAFETY_TEXTS` / `LEGAL_PROFILES` sayfaları + `CrmManifestRegulatoryTextsTests` + menü etiketi yerelleştirmesi.
- Canlı örnek: plan `#0848afed` (onaylı, 122 doktor + 13 eczane + 36 hesap, 176 ziyaret).

## NE
### 1. (D1) Liste "Hedefler" sütunu hep 0
- **Kök neden:** `index.js` `sTargets()` `targetCount` ya da `selectedContacts[]` / `selectedAccountIds[]` arıyor. Liste DTO'su bunları değil `selectedContactCount` döndürüyor → 0.
- **Düzelt:**
  - CRM liste DTO'suna **ek** alan `selectedPharmacyCount` (yalnız ekleme, mevcut alanlar değişmez).
  - Web sütunu "**122 doktor · 13 eczane**" biçiminde gösterir (yerelleştirilmiş). Sıfırsa "0".

### 2. (D2) Onaylı (`committed`) plan düzenlenebilir görünüyor
- **Bugün:**
  - Detayda "Bu haftanın planı olarak kaydet", "Hedefleri kaydet", "Düzenle", "Re-plan", "Route oluştur" açık.
  - Hedef onay kutuları tıklanabiliyor.
  - Sunucu seçim düzenlemesini zaten 409 ile reddediyor (`PlanningSessionCommandHandlers` ~127).
- **Düzelt (Web):**
  - `committed` / `archived` planda yazma eylemleri **gizlenir**, hedef kutuları **salt okunur** olur.
  - Üstte tek satır bilgi: "Bu plan onaylı ve salt okunur."
  - "Düzenle" sayfası da salt okunur açılır ya da detaya döner. Hangisi daha az değişiklikse onu seç; raporla.
  - Rota görüntüleme açık kalır.
- **Sunucu:**
  - `committed` plana yönelik her yazma yolunu listele ve raporla: seçim düzenleme, durum, önizleme, uygula, re-plan.
  - Seçim düzenleme 409 zaten var; aynı plana **ikinci kez uygula** da reddedilmeli. Yoksa ekle (409, makine kodu).
  - **Re-plan'ın sunucu davranışını DEĞİŞTİRME** (Faz 3'te hafta bazında yeniden tasarlanacak); yalnız Web'de gizle.

### 3. (C2) Hafta sonuna ziyaret düşüyor
- **Kanıt:** `VP-0848afed-0176` → 2026-10-25 (Pazar), `source = route-plan`.
- **Kök neden:**
  - `TimeWindowInsertionEngine.EnumerateDates` dönemin **her gününü** (Cmt / Paz dahil) aday gün yapıyor;
  - `VisitPlanningEngine.BuildWeeks` 7 günlük pencere kuruyor.
- **Düzelt:**
  - Planlayıcı yalnız **çalışma günlerine** ziyaret koyar (madde 4'teki takvimden).
  - Takvim okunamazsa **Cumartesi ve Pazar hariç** tutulur ve sonuç "takvim okunamadı" işareti taşır (madde 4).

### 4. (C3) Çalışma takvimi okunamıyor → tatiller planlamaya girmiyor
- **Web — kök neden:** `details.js` `loadWorkingCalendar` → Web proxy `api/working-calendar` → Platform `/api/platform/working-calendars?country=TR&year=…` (yönetici ucu) → **400** "'X-Tenant-Id' is not allowed on admin endpoints".
- **Sunucu — kök neden:** CRM planlayıcı takvime hiç bakmıyor.
- **Düzelt (CRM):**
  - Planlayıcı haftanın günleri için takvimi, kiracıdan açılabilen **mevcut seam** üzerinden okur.
  - Seam: `/api/platform/working-calendars/overrides/resolve`, `platform.working-calendar.override.read` — bkz. `WorkingCalendarWorkingDayCounter`, aynı istemci / ayar.
  - İşlem `is-working-day` (gün başına, çalıştırma içinde önbellekli). Daha az istekli mevcut bir işlem varsa onu kullan.
  - Tatil ve çalışılmayan gün aday gün olmaz.
  - Önizleme / plan yanıtına **ek** alanlar: `calendarStatus` (`resolved` / `unresolved` + neden) ve haftanın `nonWorkingDates[]`.
- **Düzelt (Web):**
  - Rota gün sekmeleri ve tatil gösterimi **sunucunun döndürdüğü** `nonWorkingDates` / `calendarStatus` ile çalışır.
  - Web'in `api/working-calendar` proxy'si ve istemcideki takvim yüklemesi kaldırılır.
  - `unresolved` ise sayfa üstünde uyarı: "Çalışma takvimi okunamadı; tatiller dikkate alınmadı."
  - Rota sekmesinin **tasarımı değişmez**.
- **Yarım gün DEĞİL** (Faz 3, MK-9). Platform'a yeni işlem EKLEME. Gerekirse DUR + raporla.

### 5. (A1) Strateji şablonu seçicisini kaldır (K-3)
- **Kaldırılacaklar:** `_Form.cshtml` alanı + `form.js` yükleme / gönderme. Kullanılmayan Web proxy `api/strategy-templates` de kaldırılır.
- Seçici zaten hep boştu: API `templateId / templateName` dönüyor, `form.js` `strategyTemplateId / name` okuyor. Yani davranış değişmez.
- `details.js` `strategyTemplateId: sessionData.strategyTemplateId` geri gönderimini **koru** (sunucu sözleşmesi aynı kalır; B-3 Faz 2'de).
- **Segment alanına DOKUNMA:** K-4 gereği kalkacak, ama sıklık bugün segmentten çözüldüğü için B-3 ile birlikte gider.

### 6. (D7) Menü: Ziyaret Planlama + Ziyaret Yürütme
- `CrmManifestProvider`'a iki sayfa:
  - `VISIT_PLANNING` "Visit Planning" → `/CRM/VisitPlanning`, okuma `crm.visit-plan.read`, eylemler `crm.visit-plan.generate`, `crm.visit-plan.apply`. Sıra 64, Planlanan Ziyaretler'den (65) önce.
  - `VISIT_EXECUTION` "Visit Execution" → `/CRM/VisitExecution`, okuma `crm.visit-report.read`, eylemler `crm.visit-report.record`, `crm.visit-report.amend`. Sıra 66.
- `crm.visit-report.read` 97c5'te verilmemişse, Web denetleyicisindeki belgelenmiş yedek (`crm.territory.read`) aynı `VisitFrequencyPolicyReadFallback` deseniyle kullanılır. Hangisini seçtiğini kanıtla birlikte raporla.
- Menü etiketleri **7 dil**, TR: "Ziyaret Planlama", "Ziyaret Yürütme". Mevcut menü yerelleştirme mekanizması ve "değer ≠ anahtar" guard'ı ile.
- **Yetki seed'i / grant YAZMA.**

### 7. (D6 / A5) Bugünkü İngilizce / ham metinler → 7 dil
Ziyaret Planlama'nın bugünkü ekranlarındaki (liste, oluştur / düzenle formu, detay özet + eylemler + kartlar, Hedefler, Rota) görünür metinlerin tamamı yerelleştirme anahtarından gelir: 7 dil, TR diakritikli.
- Bilinen örnekler: "Turn a rep's selection…", "New session", "Cycle period", "Route oluştur", "Re-plan", "Accounts (clinics / hospitals)", "Doctors", "Specialty", "Out-of-territory accounts are warned, never hidden.", "Segment narrows… / backend tek segment" notları.
- Tek tek bul. Tablo başlıkları büyük harf dönüşümünde "PERİOD" gibi bozuk Türkçe İ üretmemeli.
- **Kurum türü** (hospital / clinic / pharmacy → HOSPİTAL) ve **uzmanlık** (Urology): ham kod yerine **referans seti etiketi** gösterilir. Okuma, ortak `CrmReferenceSetReader` ile, arayüz dilinde etiket. Setin TR etiketi yoksa kod kalır, raporla (veri işi, DUR değil).
- **Tarih biçimi DEĞİŞMEZ** ("5 Oct, 26" tarzı, kullanıcı kararı).
- Rota sekmesinin düzeni değişmez; yalnız metinleri.

## KORU / YAPMA
- **Ekran tasarımı değişmez** (Faz 4). Rota sekmesinin tasarımı hiç değişmez.
- **Yeni yazma komutu ekleme** (AUD-001 testi Faz 8'e kadar zaten kırmızı; yeni komut eklersen DUR + raporla). Mevcut komutlara ek alan / ek kontrol serbest.
- Segment alanı, temsilci seçimi, hafta durumu, gün dengeleme, sıklık varsayılanı, bölge evreni: **YAPMA** (Faz 2–3).
- Re-plan'ın sunucu davranışı değişmez.
- RBAC seed / grant / canlı veri yazma YOK. Platform'a yeni takvim işlemi YOK.
- CRM sınıf haritası kuralları: yeni alan BSON'a yazılıyorsa class-map'e kaydet; GUID string.
- `esc()` / `textContent`. Kiracı sınırı her okumada.

## Acceptance
### E2
- **Taban ölç, yalnız farkı raporla:** Web (taban 652/0), CRM Application (2248/0/5; bilinen PII flake), Platform odaklı manifest testleri, mimari testler (taban 38/1; AUD-001 kırmızısı bilinen, **sayı artmamalı**). Build 0 hata.
- **Yeni testler (üretim koduyla ölçülür):**
  1. Liste DTO `selectedPharmacyCount` + Web sütun biçimi (doktor · eczane).
  2. Onaylı planda Web eylem bayrakları kapalı; ikinci uygula 409.
  3. Planlayıcı: dönem bir hafta sonu içerdiğinde Cmt / Paz'a ziyaret yok.
  4. Planlayıcı: takvim bir hafta içi tatil döndürdüğünde o güne ziyaret yok; takvim okunamazsa Cmt / Paz hariç + `calendarStatus = unresolved`.
  5. Manifest: iki yeni sayfa, doğru yol / izin / sıra; menü etiketi 7 dilde, değer ≠ anahtar.
  6. Ziyaret Planlama yerelleştirme anahtarları 7 dilde eşit ve boş değil; JS'nin okuduğu her anahtar ailede var.
  7. Strateji seçicisi formda yok; `api/strategy-templates` proxy'si yok.
- **Sabotaj (kırmızı kanıtla, geri al):**
  1. `sTargets` eski hâline → test 1 kırmızı.
  2. `EnumerateDates` hafta sonu süzgecini kaldır → test 3 kırmızı.
  3. Tatil kontrolünü atla → test 4 kırmızı.
  4. Manifest'ten `VISIT_PLANNING` sil → test 5 kırmızı.
  5. Bir TR anahtarını sil → test 6 kırmızı.

### E4 (CT, fleet; kullanıcı giriş yapar; ayrı sekme)
- Liste "122 doktor · 13 eczane".
- `#0848afed` salt okunur.
- Menüde iki yeni öğe.
- Ekranda İngilizce metin kalmadı (tarih biçimi hariç).
- Yeni bir taslakta önizleme: 29 Ekim haftası seçilince Perşembe ve hafta sonu boş; konsolda 400 yok.
- Önizleme **kaydetmeden**; kullanıcı onayı olmadan uygulama yok.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md] @[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VP-FIX-1 · Ziyaret Planlama: mockup'tan bağımsız düzeltmeler (Web + CRM + Platform menü)
Repository: C:\tmp\vp-fix-1 (worktree) · Branch: wp/vp-fix-1 · commit bu dala, push YOK

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-VP-FIX-1-visit-planning-quick-fixes.md — önce tamamını oku. Ayrıca: …/VISIT-PLANNING-current-state-analysis.md · …/ROADMAP-visit-planning.md · services/Diten.CrmService/src/**/Features/{VisitPlanning,RouteOptimization}/** · services/Diten.CrmService/src/**/WorkingCalendarWorkingDayCounter.cs · services/Diten.Platform/src/**/Features/Crm/SelfRegistration/CrmManifestProvider.cs (+ CrmManifestRegulatoryTextsTests deseni) · frontend/Diten.Web/{Controllers/CRM/VisitPlanningController.cs, Views/CRM/VisitPlanning/**, wwwroot/assets/js/CRM/VisitPlanning/**, Resources/Views/CRM/VisitPlanning/**, Services/CrmReferenceSetReader.cs} · .antigravity/rules/audit-trail-standard.md · memory: crm-classmap-rejects-unknown-elements, l10n-bridge-pascalcase-loader, working-calendar-country-source, workingcalendar-rbac-grant.

NE (7 madde, ayrıntı WP'de):
(1) D1 liste Hedefler hep 0 → CRM liste DTO'suna ek selectedPharmacyCount; Web "N doktor · M eczane".
(2) D2 committed planda Web yazma eylemleri gizli + hedefler salt okunur + bilgi satırı; sunucuda committed'a giden yazma yollarını listele, ikinci uygula 409 (yoksa ekle); re-plan sunucu davranışı DEĞİŞMEZ.
(3) C2 planlayıcı yalnız çalışma günlerine ziyaret koyar (EnumerateDates/BuildWeeks hafta sonu dahil ediyordu; kanıt VP-0848afed-0176 Pazar).
(4) C3 CRM planlayıcı takvimi mevcut overrides/resolve seam'iyle okur (is-working-day, çalıştırma içi önbellek); tatil aday gün olmaz; yanıta ek calendarStatus + nonWorkingDates; okunamazsa Cmt/Paz hariç + unresolved. Web api/working-calendar proxy'si + istemci takvim yüklemesi kalkar, sunucu alanları kullanılır, unresolved uyarısı. Platform'a yeni işlem YOK (gerekirse DUR). Yarım gün YOK.
(5) A1 strateji şablonu seçicisi + api/strategy-templates proxy'si kalkar; details.js geri gönderimi korunur; segment alanına DOKUNMA.
(6) D7 CrmManifestProvider: VISIT_PLANNING (/CRM/VisitPlanning, crm.visit-plan.read, sıra 64) + VISIT_EXECUTION (/CRM/VisitExecution, crm.visit-report.read ya da belgelenmiş yedek crm.territory.read — kanıtla seç, sıra 66); menü etiketleri 7 dil (TR "Ziyaret Planlama", "Ziyaret Yürütme"). Seed/grant YOK.
(7) D6/A5 Ziyaret Planlama'nın bugünkü tüm görünür metinleri 7 dil (TR diakritik; büyük harf İ bozulması yok); kurum türü + uzmanlık ham kod yerine referans seti etiketi (CrmReferenceSetReader); TR etiketi yoksa raporla. TARİH BİÇİMİ DEĞİŞMEZ. Rota tasarımı değişmez.
KORU/YAPMA: ekran tasarımı değişmez (Faz 4); YENİ YAZMA KOMUTU YOK (AUD-001 kırmızısı artmamalı; gerekirse DUR); segment/temsilci/hafta durumu/gün dengeleme/sıklık varsayılanı/bölge evreni YOK; RBAC seed/grant/canlı veri yazma YOK; class-map + GUID string; esc()/textContent; kiracı sınırı.
DOĞRULA (E2): tabanı ölç, yalnız farkı raporla — dotnet test frontend/Diten.Web.Tests (taban 652/0) · services/Diten.CrmService/tests/Diten.CrmService.Application.Tests (2248/0/5, PII flake bilinen) · Platform manifest testleri · tests/architecture/TenantArchitecture.ArchitectureTests (38/1, AUD-001 kırmızısı bilinen, SAYI ARTMAMALI); build 0 hata. Web, fleet açıkken bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad> kullan (derinlik 3). Yeni testler WP Acceptance 1–7. Sabotaj 1–5 (kırmızı kanıtla, geri al).
Commit: "fix(crm,web,platform): WP-VP-FIX-1 — visit planning quick fixes (target count, committed lock, working days + calendar, strategy picker removed, menu, l10n)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde başına ne yapıldı + kanıt (dosya:satır, test adı), committed yazma yolları listesi, menü izni seçimi, TR etiketi eksik referans setleri. §22 TÜRKÇE. K13.
```

---

## §37 CT kabul — E2 ACCEPTED (2026-10-06)
**Commit:** `b0fe13aa` (ajan `de9e04f1`, `test/crm-content-visit-e2e` üzerine rebase + ff). Push: test dalı.

**CT K13 doğrulaması (kendi koşum, worktree):**
| Paket | Taban | Sonuç |
|---|---|---|
| Web.Tests | 652/0 | **666/0** (+14) |
| CRM Application.Tests | 2249/0/5 | **2254/0/5** (+5) |
| Platform (Crm/Manifest/Nav) | 191/3 | **194/3** (+3; 3 kırmızı = yerel mongod yok, ortam, tabanda da var) |
| Mimari | 38/1 | **38/1** — AUD-001 denetimsiz komut **26, değişmedi** (yeni yazma komutu yok) |

**Kod okuması (CT):**
- C2 / C3: `PlanningWorkingCalendar` gün başına `is-working-day`, mevcut seam (`WorkingCalendarWorkingDayCounter` artık `IWorkingDayChecker` da). İlk hatada Cmt / Paz yedeği; karışık sonuç yok. `OptimizationPeriod.NonWorkingDates` (ek, null = eski davranış). Önizleme yanıtına `calendarStatus` + `nonWorkingDates` (ek). Platform yanıt modeli `IsWorkingDay` alanını taşıyor (WorkingCalendarModels.cs:130) ✓.
- D2: Web Details / Edit durumu sunucuda okur. Okunamazsa eski davranış, CRM 409 yine korur. İkinci uygula → 409 `planning_session_already_committed`.
- D7: manifest iki sayfa; `crm.visit-report.read` 97c5 Admin'de verilmiş (ajan, salt okuma kanıtı).
- D6: `api/reference-labels` → `CrmReferenceSetReader`.

**CT sabotajı (ajanınkinden ayrı):**
1. Yedekte Pazar'ı çıkar → `An_unreadable_calendar_falls_back_to_saturday_and_sunday_and_says_unresolved` **kırmızı**.
2. Apply'daki `IsCommitted` korumasını kapat → `A_second_apply_of_a_committed_plan_is_refused_with_409_and_a_machine_code` **kırmızı**.

İkisi de geri alındı, ağaç temiz.

**Ajan raporundaki notlar → CT kararı:**
- **Re-plan da tatil / hafta sonuna ziyaret koymuyor** (ortak üretim akışı) → **istenen davranış**, kabul.
- **Düzenle formu saklı `strategyTemplateId`'yi artık koruyor** (önceden boş gidiyordu) → **kabul**. Mobil yanıtlarıyla aynı kural: okunan değer geri gönderilir; B-3'te sunucu türetir.
- **Takvim istek sayısı** en fazla 42, sıralı → şimdilik kabul. Hız sorunu görülürse B-5'te toplu işlem.
- **DataTable doğrulayıcısı 9 sapma** (tabanda da var) → ayrı paket yok. Liste **VP-UI-1**'de (Faz 4) yeniden yapılıyor; sapmalar orada kapanır.
- **Referans setlerinde TR etiketi yok:** `account-type` (9 değer), `medical-specialty` (22 değer) → veri işi, yol haritasında **0.5**.

**E4 (CT, bekliyor; fleet yeniden başlatılmalı — CRM + Platform manifest + Web değişti):**
- Liste "122 doktor · 13 eczane".
- `#0848afed` salt okunur.
- Menüde Ziyaret Planlama + Ziyaret Yürütme.
- Metinler Türkçe.
- 29 Ekim haftasında Perşembe + hafta sonu boş (kaydetmeden önizleme).
- Konsolda 400 yok.
### §37 ek — E4 ACCEPTED (CT canlı, 2026-10-06; fleet yeniden başlatıldı, kullanıcı girişi, ayrı sekme, kayıt yok)
| Kontrol | Sonuç |
|---|---|
| Menü | ✓ "Ziyaret Planlama" (`/CRM/VisitPlanning`) · "Planlanan Ziyaretler" · "Ziyaret Yürütme" (`/CRM/VisitExecution`) sırasıyla |
| Liste Hedefler | ✓ `#0848afed` "122 doktor · 13 eczane"; `#ad5fd16c` "11 doktor · 0 eczane"; boş taslaklar "0" |
| Onaylı plan | ✓ "Bu plan onaylı ve salt okunur."; Düzenle / kaydet / hedefleri kaydet / route / re-plan düğmeleri yok; hedef kutuları kapalı (açık kalan 3 kutu yalnız rota görünüm anahtarları: harita, ziyaret no, yol süreleri) |
| Takvim | ✓ önizleme `calendarStatus = resolved`; `nonWorkingDates` hafta sonları + **2026-10-29**. Ziyaret günleri: 5 / 6 / 7 / 8 Eki + **26 Eki Pzt (34)**. Önceki **25 Eki Pazar ziyareti yok**; 29 Ekim boş |
| 400 / 404 | ✓ temiz yüklemede başarısız istek yok; `working-calendar` isteği yok (konsoldaki eski hatalar önceki sayfalardan) |
| Metinler | ✓ Türkçe ("Dönem", "Kurumlar (klinik / hastane)", "Bölge dışı hesaplar gizlenmez; uyarıyla gösterilir.", "Doktorlar", "Uzmanlık"); form: strateji şablonu yok |
| Referans etiketleri | ⚠ Bilinen (0.5): "Hospital", "Urology"… set TR etiketi yok |

**Küçük gözlemler (Faz 4 / B paketlerine):**
- Liste başlığı hâlâ "Taslak planlarım" (onaylı planı da içeriyor) → VP-UI-1 "Benim planlarım".
- Ziyaret Yürütme sayfa başlığı "Ziyaret Raporu", menü "Ziyaret Yürütme" → tek ad (Faz 6'da).
- Segment notu "seçebileceğiniz doktorları daraltır" gerçeği yansıtmıyor (plan anında düşürüyor) → B-3'te alan zaten kalkıyor.
- Gün dağılımı hâlâ dengesiz (57 / 51 / 27 / 7 / 0) ve süre 3 dk → B-5 / B-7 (beklenen).